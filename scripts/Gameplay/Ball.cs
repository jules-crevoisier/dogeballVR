using System.Collections.Generic;
using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Gameplay;

/// <summary>
/// Balle de dodgeball. Côté hôte (autorité) : physique et touches. Côté clients : affichage.
/// Charge de passe (0 à 3) : chaque passe d'un allié ajoute une charge, la balle devient plus lumineuse.
/// </summary>
public partial class Ball : RigidBody3D
{
    public const int MaxCharge = 3;
    public const float HitMinSpeed = 6f; // en dessous, la balle ne blesse pas

    // Halo visuel plus grand que la hitbox (rayon 0,11 m) : lisibilité en VR, gameplay inchangé.
    static readonly float[] HaloScale = { 1.0f, 1.15f, 1.35f, 1.6f };
    static readonly float[] HaloAlpha = { 0.25f, 0.4f, 0.55f, 0.75f };
    static readonly float[] PulseHz = { 0f, 1f, 2f, 2.5f };   // sous les 3 Hz (confort)
    const int TrailPoints = 10;
    const float TrailMinSpeed = 6f;                            // = seuil de blessure

    /// <summary>Identifiant réseau, attribué par l'hôte.</summary>
    public int NetId { get; set; }

    /// <summary>Côté client : simple image de la balle de l'hôte, sans physique.</summary>
    public bool IsPuppet { get; private set; }
    Vector3 netTarget;

    public int OwnerPlayerId { get; private set; } = -1;
    public int OwnerTeam { get; private set; } = -1;
    public int Charge { get; private set; }
    public bool IsHeld { get; private set; }

    /// <summary>Temps passé immobile au sol (s) : au-delà, la balle est recyclée et réapparaît dans une autre zone.</summary>
    public double RestTime { get; private set; }

    StandardMaterial3D haloMaterial = null!;
    MeshInstance3D halo = null!;
    MeshInstance3D shadow = null!;
    MeshInstance3D trail = null!;
    ImmediateMesh trailMesh = null!;
    readonly List<Vector3> trailPoints = new();
    double visualTime;

    /// <summary>Effet de courbe : vitesse angulaire du poignet au lâcher (rad/s, axe vertical).</summary>
    float curveSpin;

    public static Ball Create()
    {
        var ball = new Ball { Name = "Ball" };
        ball.AddChild(new CollisionShape3D { Shape = new SphereShape3D { Radius = 0.11f } });
        // Cœur blanc chaud toujours visible, halo additif à la couleur de l'équipe qui a lancé.
        ball.AddChild(new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f },
            MaterialOverride = Materials.Glow(Palette.BallCore, 1f),
        });
        ball.haloMaterial = Materials.Additive(Palette.BallCore);
        ball.halo = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = ball.haloMaterial,
        };
        ball.AddChild(ball.halo);
        // Ombre portée simplifiée : un disque lumineux au sol sous la balle (indice de hauteur).
        ball.shadow = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.14f, BottomRadius = 0.14f, Height = 0.005f, RadialSegments = 16, Rings = 1 },
            MaterialOverride = Materials.Additive(new Color(1f, 1f, 1f, 0.22f)),
            TopLevel = true,
        };
        ball.AddChild(ball.shadow);
        ball.trailMesh = new ImmediateMesh();
        ball.trail = new MeshInstance3D { Mesh = ball.trailMesh, MaterialOverride = Materials.Additive(Colors.White), TopLevel = true };
        ball.AddChild(ball.trail);
        ball.UpdateLook();
        return ball;
    }

    /// <summary>Balle marionnette (client) : gelée, déplacée uniquement par les snapshots de l'hôte.</summary>
    public static Ball CreatePuppet(int netId)
    {
        var ball = Create();
        ball.NetId = netId;
        ball.IsPuppet = true;
        ball.Freeze = true;
        ball.FreezeMode = FreezeModeEnum.Static;
        return ball;
    }

    /// <summary>Applique l'état envoyé par l'hôte (position lissée dans _Process).</summary>
    public void ApplyNet(Vector3 position, Vector3 velocity, int team, int ownerId, int charge, bool held)
    {
        bool first = netTarget == Vector3.Zero;
        netTarget = position;
        if (first || GlobalPosition.DistanceTo(position) > 3f) GlobalPosition = position;
        LinearVelocity = velocity;
        OwnerTeam = team;
        OwnerPlayerId = ownerId;
        Charge = charge;
        IsHeld = held;
        UpdateLook();
    }

    public override void _Ready()
    {
        AddToGroup("balls");
        Mass = 0.6f;
        GravityScale = 0.6f; // un peu flottante, plus lisible en VR
        ContinuousCd = true;
        LinearDamp = 0.3f;
        AngularDamp = 2f;
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.55f, Friction = 1.2f };
    }

    /// <summary>Un joueur attrape la balle. Passe d'un allié : +1 charge. Balle volée à l'adversaire : charge à zéro.</summary>
    public void Hold(Node3D hand, int playerId, int team)
    {
        if (OwnerTeam == team && OwnerPlayerId >= 0 && OwnerPlayerId != playerId) AddCharge();
        else if (OwnerTeam != team) ResetCharge();
        OwnerPlayerId = playerId;
        OwnerTeam = team;
        IsHeld = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        Freeze = true;
        FreezeMode = FreezeModeEnum.Kinematic;
        Reparent(hand, false);
        Position = Vector3.Zero;
        UpdateLook();
    }

    public void Release(Vector3 velocity, float spin)
    {
        var world = GetTree().CurrentScene;
        var xf = GlobalTransform;
        Reparent(world, false);
        GlobalTransform = xf;
        IsHeld = false;
        Freeze = false;
        curveSpin = spin;
        LinearVelocity = velocity;
    }

    /// <summary>La balle a touché : elle devient neutre (plus personne n'est blessé tant qu'elle n'est pas reprise).</summary>
    public void Spent()
    {
        OwnerPlayerId = -1;
        OwnerTeam = -1;
        ResetCharge();
        UpdateLook();
    }

    public void AddCharge()
    {
        Charge = Mathf.Min(Charge + 1, MaxCharge);
        UpdateLook();
    }

    public void ResetCharge()
    {
        Charge = 0;
        UpdateLook();
    }

    void UpdateLook()
    {
        var c = Palette.Team(OwnerTeam);
        c.A = HaloAlpha[Charge];
        haloMaterial.AlbedoColor = c;
    }

    public override void _Process(double delta)
    {
        visualTime += delta;
        if (IsPuppet) GlobalPosition = GlobalPosition.Lerp(netTarget, 1f - Mathf.Exp(-22f * (float)delta));
        float pulse = PulseHz[Charge] > 0f ? 1f + 0.08f * Mathf.Sin((float)visualTime * PulseHz[Charge] * Mathf.Tau) : 1f;
        halo.Scale = Vector3.One * HaloScale[Charge] * pulse;

        var p = GlobalPosition;
        shadow.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(p.X, 0.03f, p.Z));
        float h = Mathf.Clamp(1f - p.Y / 4f, 0.2f, 1f);
        shadow.Scale = new Vector3(h, 1f, h);

        UpdateTrail(p);
    }

    /// <summary>Traînée en ruban : longueur proportionnelle à la vitesse, aucune sous le seuil de blessure.</summary>
    void UpdateTrail(Vector3 p)
    {
        bool fast = !IsHeld && LinearVelocity.Length() >= TrailMinSpeed;
        if (fast) trailPoints.Add(p);
        if (trailPoints.Count > TrailPoints || (!fast && trailPoints.Count > 0)) trailPoints.RemoveAt(0);
        trailMesh.ClearSurfaces();
        var cam = GetViewport().GetCamera3D();
        if (trailPoints.Count < 2 || cam == null) return;
        var color = Palette.Team(OwnerTeam);
        trailMesh.SurfaceBegin(Mesh.PrimitiveType.TriangleStrip);
        for (int i = 0; i < trailPoints.Count; i++)
        {
            float t = (float)i / (trailPoints.Count - 1);   // 0 = queue, 1 = balle
            var a = trailPoints[i];
            var next = trailPoints[Mathf.Min(i + 1, trailPoints.Count - 1)];
            var prev = trailPoints[Mathf.Max(i - 1, 0)];
            var dir = (next - prev).Normalized();
            var side = dir.Cross((cam.GlobalPosition - a).Normalized()).Normalized() * 0.07f * t;
            var vc = new Color(color, 0.6f * t);
            trailMesh.SurfaceSetColor(vc); trailMesh.SurfaceAddVertex(a + side);
            trailMesh.SurfaceSetColor(vc); trailMesh.SurfaceAddVertex(a - side);
        }
        trailMesh.SurfaceEnd();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsPuppet) return;
        RestTime = !IsHeld && LinearVelocity.Length() < 0.4f ? RestTime + delta : 0;
        if (IsHeld || curveSpin == 0f) return;
        // Force latérale (Magnus simplifié) : perpendiculaire à la vitesse, dans le plan horizontal.
        var v = LinearVelocity;
        var side = v.Cross(Vector3.Up).Normalized();
        ApplyCentralForce(side * curveSpin * v.Length() * 0.05f);
        curveSpin = Mathf.MoveToward(curveSpin, 0f, (float)delta * 4f);
    }
}
