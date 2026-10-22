using System.Collections.Generic;
using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Gameplay;

/// <summary>
/// Balle de dodgeball. Côté hôte (autorité) : physique et touches. Côté clients : affichage.
/// Charge de passe (0 à 3) : une passe d'un allié donne le palier 1 (pas plus), la balle devient plus lumineuse.
/// </summary>
public partial class Ball : RigidBody3D
{
    public const int MaxCharge = 3;
    public const float HitMinSpeed = 5f; // en dessous, la balle ne blesse pas (vol minimal VR : 5,5 m/s au lancer, l'amortissement ne doit pas la désarmer)
    public const float LobHitMinSpeed = 4.5f;   // une cloche arrive presque à la verticale : seuil plus bas

    /// <summary>Réglages de la map (voir BallRulesDef) : gravité, et durée de vol après laquelle la balle ne blesse plus.</summary>
    public static float GravityFactor { get; set; } = 0.6f;
    public static float MaxFlightSeconds { get; set; }

    /// <summary>Distance au plus proche adversaire au moment du lancer (mesure de portée, remplie par le MatchController).</summary>
    public float ThrowDistance { get; set; } = -1f;
    public static event System.Action<Ball>? Released;
    /// <summary>Un adversaire attrape une balle lancée vite : (id du receveur, attrape parfaite). Le receveur regagne une vie.</summary>
    public static event System.Action<int, bool>? CaughtThrow;
    double flightTime;

    /// <summary>Balle spéciale (une famille par match, voir specialBalls dans les maps) : 0 normale, 1 bombe, 2 rebond.</summary>
    public const int KindNormal = 0, KindBomb = 1, KindBounce = 2, KindExploded = 3;   // 3 : bombe explosée, envoyée aux clients 0,15 s avant sa disparition
    public const float BombRadius = 3f;
    static readonly Color BombColor = new(1f, 0.8f, 0.1f), BounceColor = new(0.7f, 0.4f, 1f);
    /// <summary>Une bombe explose : position, équipe et id du tireur, id de la cible déjà touchée (exclue du souffle).</summary>
    public static event System.Action<Vector3, int, int, int>? Exploded;
    int kind;
    double sinceRelease;
    bool exploded;
    double explodedFor;
    public bool HasExploded => exploded || kind == KindExploded;   // client : la marionnette de type 3 compte aussi
    public int Kind
    {
        get => kind;
        set
        {
            kind = value;
            if (core != null) core.MaterialOverride = Materials.Glow(KindColor() ?? Palette.BallCore, 1f);
            ShowDecor();
            UpdateLook();
        }
    }
    /// <summary>Bandes, mèche et anneaux des modèles Blender (assets/models), un jeu par type de balle ; le cœur reste procédural pour garder la teinte par type.</summary>
    readonly Node3D?[] decor = new Node3D?[3];

    void AddDecor()
    {
        string[] names = { "balle_normale", "balle_bombe", "balle_rebond" };
        for (int i = 0; i < names.Length; i++)
        {
            var model = Visual.ModelLibrary.Instance(names[i]);
            if (model == null) continue;
            model.GetNodeOrNull<Node3D>("Balle")?.QueueFree();   // le cœur est déjà dans la balle
            model.Visible = i == kind || (kind > KindBounce && i == 0);
            decor[i] = model;
            AddChild(model);
        }
    }

    void ShowDecor()
    {
        for (int i = 0; i < decor.Length; i++)
            if (decor[i] != null) decor[i]!.Visible = i == (kind <= KindBounce ? kind : 0);
    }

    Color? KindColor() => kind == KindBomb ? BombColor : kind == KindBounce ? BounceColor : null;
    MeshInstance3D? core;

    // Halo visuel plus grand que la hitbox (rayon 0,11 m) : lisibilité en VR, gameplay inchangé.
    // Palier 0 discret, palier 1 un peu plus lumineux, palier 2 (KO) binaire et très marqué : double halo + anneau orbital (voir UpdateLook).
    static readonly float[] HaloScale = { 1.0f, 1.12f, 1.35f, 1.5f };
    static readonly float[] HaloAlpha = { 0.22f, 0.32f, 0.6f, 0.75f };
    static readonly float[] PulseHz = { 0f, 0f, 2.5f, 2.5f };   // sous les 3 Hz (confort)
    /// <summary>Vitesse de lancer retenue par l'hôte (m/s) : l'attrape parfaite exige encore 80 % de cette vitesse. 0 = inconnue.</summary>
    public float SpeedCap { get; set; }
    const int TrailPoints = 10;
    const float TrailMinSpeed = HitMinSpeed;                   // = seuil de blessure

    /// <summary>Identifiant réseau, attribué par l'hôte.</summary>
    public int NetId { get; set; }

    /// <summary>Côté client : simple image de la balle de l'hôte, sans physique.</summary>
    public bool IsPuppet { get; private set; }
    /// <summary>Client : tant que ce joueur local tient la marionnette, elle suit sa main sans attendre les snapshots (pas de retard, taille réduite).</summary>
    public Node3D? LocalHoldOverride { get; set; }
    bool overridden;
    Vector3 netTarget;

    public int OwnerPlayerId { get; private set; } = -1;
    public int OwnerTeam { get; private set; } = -1;
    /// <summary>Dernière équipe à avoir tenu la balle (reste valable après une touche) : une balle perdue revient à l'autre camp.</summary>
    public int LastTeam { get; private set; } = -1;
    public int Charge { get; private set; }
    public bool IsHeld { get; private set; }

    /// <summary>Mode de lancer : 0 tir tendu, 1 lob (cloche, blesse), 2 passe (cloche guidée vers un allié, ne blesse personne).</summary>
    public const int ModeLob = 1, ModePass = 2;
    public int ThrowMode { get; private set; }
    public bool IsPass => ThrowMode == ModePass;
    public const float ArcGravity = 9.8f, ArcMaxSeconds = 2.6f;   // en cloche la balle retombe à pleine gravité ; durée de vol plafonnée
    double arcTime;

    /// <summary>Temps passé immobile au sol (s) : au-delà, la balle est recyclée et réapparaît dans une autre zone.</summary>
    public double RestTime { get; private set; }

    StandardMaterial3D haloMaterial = null!;
    StandardMaterial3D halo2Material = null!;
    MeshInstance3D halo = null!;
    MeshInstance3D halo2 = null!;
    MeshInstance3D orbit = null!;
    MeshInstance3D? alertMarker;
    MeshInstance3D? bombRing;
    bool alert;
    Color alertColor = Colors.White;
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
        // DA v3 : sphère de 0,30 m (hitbox 0,22 m), contour sombre de 1,8 cm, halo additif à la couleur de l'équipe qui a lancé.
        ball.core = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.15f, Height = 0.30f },
            MaterialOverride = Materials.Glow(Palette.BallCore, 1f),
        };
        ball.AddChild(ball.core);
        ball.AddDecor();
        var outline = Materials.Flat(Palette.Panel);
        outline.CullMode = BaseMaterial3D.CullModeEnum.Front;   // coque inversée : seul le bord dépasse
        ball.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.168f, Height = 0.336f, RadialSegments = 24, Rings = 12 }, MaterialOverride = outline });
        ball.haloMaterial = Materials.Additive(Palette.BallCore);
        ball.halo = new MeshInstance3D
        {
            Mesh = new SphereMesh { Radius = 0.16f, Height = 0.32f, RadialSegments = 16, Rings = 8 },
            MaterialOverride = ball.haloMaterial,
        };
        ball.AddChild(ball.halo);
        // Palier 2 (KO) : second halo plus large et anneau orbital, invisibles aux paliers 0 et 1.
        ball.halo2Material = Materials.Additive(Palette.BallCore);
        ball.halo2 = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.26f, Height = 0.52f, RadialSegments = 16, Rings = 8 }, MaterialOverride = ball.halo2Material, Visible = false };
        ball.AddChild(ball.halo2);
        ball.orbit = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.27f, OuterRadius = 0.31f, Rings = 24, RingSegments = 8 }, MaterialOverride = ball.haloMaterial, Visible = false };
        ball.AddChild(ball.orbit);
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
    public void ApplyNet(Vector3 position, Vector3 velocity, int team, int ownerId, int charge, bool held, bool pass = false, int kind = 0)
    {
        if (kind != Kind) Kind = kind;
        ThrowMode = pass ? ModePass : 0;   // marionnette : sert au HUD et à la prise automatique d'une passe
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
        GravityScale = GravityFactor; // un peu flottante, plus lisible en VR
        ContinuousCd = true;
        LinearDamp = 0.1f;   // faible : la portée vient surtout de la gravité réduite et du vol limité
        AngularDamp = 2f;
        PhysicsMaterialOverride = new PhysicsMaterial { Bounce = 0.55f, Friction = 1.2f };
        if (!IsPuppet)
        {
            ContactMonitor = true;   // bombe : explose au contact du décor
            MaxContactsReported = 4;
            BodyEntered += OnContact;
        }
    }

    /// <summary>
    /// Palier après une prise. Balle reçue d'un allié : palier 1 au plus, comme Knockout City (sinon deux passes donnent un KO
    /// en un coup sans risque) ; un palier déjà plus haut est gardé. Balle prise à l'adversaire : palier 0, ou 1 sur attrape parfaite.
    /// </summary>
    public static int ChargeOnCatch(int charge, bool fromAlly, bool fromOpponent, bool perfect)
    {
        if (fromAlly) return Mathf.Max(charge, 1);
        if (fromOpponent) return perfect ? 1 : 0;
        return charge;
    }

    /// <summary>Un joueur attrape la balle. Passe d'un allié : palier 1. Balle volée à l'adversaire : charge à zéro (1 si parfaite).</summary>
    public void Hold(Node3D hand, int playerId, int team, bool perfect = false)
    {
        if (OwnerTeam >= 0 && OwnerTeam != team && LinearVelocity.Length() >= HitMinSpeed) CaughtThrow?.Invoke(playerId, perfect);
        bool fromAlly = OwnerTeam == team && OwnerPlayerId >= 0 && OwnerPlayerId != playerId;
        int next = ChargeOnCatch(Charge, fromAlly, OwnerTeam != team, perfect);
        if (next != Charge) { ResetCharge(); for (int i = 0; i < next; i++) AddCharge(); }
        EndArc();
        SpeedCap = 0;
        OwnerPlayerId = playerId;
        OwnerTeam = team;
        LastTeam = team;
        IsHeld = true;
        LinearVelocity = Vector3.Zero;
        AngularVelocity = Vector3.Zero;
        Freeze = true;
        FreezeMode = FreezeModeEnum.Kinematic;
        Reparent(hand, false);
        Position = Vector3.Zero;
        UpdateLook();
    }

    public void Release(Vector3 velocity, float spin, int mode = 0)
    {
        var world = GetTree().CurrentScene;
        var xf = GlobalTransform;
        Reparent(world, false);
        GlobalTransform = xf.Orthonormalized();   // la main du joueur PC est réduite : la balle lâchée reprend sa taille
        IsHeld = false;
        Freeze = false;
        curveSpin = spin;
        LinearVelocity = velocity;
        flightTime = 0;
        sinceRelease = 0;
        ThrowMode = mode;
        arcTime = 0;
        LinearDampMode = mode == 0 ? DampMode.Combine : DampMode.Replace;   // en cloche : aucun frottement (le défaut du projet s'ajouterait sinon)
        LinearDamp = mode == 0 ? 0.1f : 0f;
        GravityScale = mode == 0 ? GravityFactor : 1f;   // cloche : gravité pleine (9,8 m/s², voir ArcGravity)
        if (kind == KindBomb && velocity.Length() < LobHitMinSpeed) Spent();   // lâchée sans élan (tacle, KO) : bombe neutre, pas d'explosion aux pieds
        Released?.Invoke(this);
    }

    /// <summary>La balle a touché : elle devient neutre (plus personne n'est blessé tant qu'elle n'est pas reprise).</summary>
    void EndArc()
    {
        ThrowMode = 0;
        LinearDampMode = DampMode.Combine;
        LinearDamp = 0.1f;
        GravityScale = GravityFactor;
    }

    /// <summary>Test headless : fait passer la balle pour un tir de ce joueur.</summary>
    public void MarkThrown(int team, int playerId)
    {
        OwnerTeam = team;
        OwnerPlayerId = playerId;
        LastTeam = team;
        sinceRelease = 1;
        UpdateLook();
    }

    ulong lastBounceMs;

    void OnContact(Node body)
    {
        // son de rebond au contact du décor (hôte seulement : les clients n'ont pas la physique) ; aucune incidence sur la trajectoire
        if (!IsHeld && kind != KindBomb && body is not Ball && body is not Hurtbox && LinearVelocity.Length() is float v && v > 1.5f && Time.GetTicksMsec() - lastBounceMs > 110)
        {
            lastBounceMs = Time.GetTicksMsec();
            Audio.Sfx.Play(this, Audio.SfxKind.Bounce, GlobalPosition, 1.05f - Mathf.Min(v, 20f) * 0.008f, Mathf.Clamp(-16f + v * 1.1f, -16f, 0f));
        }
        if (exploded || kind != KindBomb || IsHeld || body is Ball || body is Hurtbox || OwnerTeam < 0 || IsPass) return;
        if (sinceRelease < 0.12) return;   // pas dans la main qui la lâche
        Explode(-1, OwnerTeam, OwnerPlayerId);
    }

    /// <summary>Explosion de la bombe : la balle est consommée, le MatchController applique les dégâts autour d'elle.</summary>
    public void Explode(int exceptId, int team, int shooter)
    {
        if (exploded || IsPuppet) return;
        exploded = true;
        Spent();
        kind = KindExploded;
        RemoveFromGroup("balls");   // plus ramassable ni comptée ; le snapshot l'envoie encore via le groupe « exploded_balls »
        AddToGroup("exploded_balls");
        SetDeferred(RigidBody3D.PropertyName.Freeze, true);   // différé : on est dans un callback physique
        Visible = false;
        SetDeferred(CollisionObject3D.PropertyName.CollisionLayer, 0u);
        SetDeferred(CollisionObject3D.PropertyName.CollisionMask, 0u);
        Exploded?.Invoke(GlobalPosition, team, shooter, exceptId);
    }

    public void Spent()
    {
        EndArc();
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
        if (PhysicsMaterialOverride != null) PhysicsMaterialOverride.Bounce = kind == KindBounce && OwnerTeam >= 0 ? 0.95f : 0.55f;   // rebond seulement une fois lancée : neutre, elle reste sur la plaque
        var c = OwnerTeam < 0 && KindColor() is Color kc ? kc : Palette.Team(OwnerTeam);
        c.A = HaloAlpha[Charge];
        haloMaterial.AlbedoColor = c;
        var c2 = c; c2.A = 0.3f;
        halo2Material.AlbedoColor = c2;
        halo2.Visible = Charge >= 2;
        orbit.Visible = Charge >= 2;
    }

    public override void _Process(double delta)
    {
        visualTime += delta;
        if (IsPuppet && IsHeld && LocalHoldOverride != null && IsInstanceValid(LocalHoldOverride))
        {
            GlobalTransform = LocalHoldOverride.GlobalTransform;
            Visible = true;
            netTarget = GlobalPosition;
            overridden = true;
        }
        else if (IsPuppet)
        {
            if (kind == KindExploded) Visible = false;
            else if (!Visible) Visible = true;      // masquée à l'envoi d'un lancer, visible dès que l'hôte l'a lâchée
            if (overridden) { Scale = Vector3.One; GlobalTransform = new Transform3D(GlobalTransform.Basis.Orthonormalized(), GlobalPosition); overridden = false; }
            // Extrapolation entre deux snapshots (vitesse reçue), puis lissage vers la cible.
            netTarget += LinearVelocity * (float)delta;
            GlobalPosition = GlobalPosition.Lerp(netTarget, 1f - Mathf.Exp(-22f * (float)delta));
        }
        float pulse = PulseHz[Charge] > 0f ? 1f + 0.08f * Mathf.Sin((float)visualTime * PulseHz[Charge] * Mathf.Tau) : 1f;
        halo.Scale = Vector3.One * HaloScale[Charge] * pulse;
        if (Charge >= 2)
        {
            halo2.Scale = Vector3.One * (1.0f + 0.1f * Mathf.Sin((float)visualTime * 2.5f * Mathf.Tau + 1f));
            orbit.Rotation = new Vector3(Mathf.Pi / 2 + 0.5f * Mathf.Sin((float)visualTime), (float)visualTime * 3f, 0);   // anneau qui tourne : lisible de loin
        }
        UpdateAlertAndRing();

        var p = GlobalPosition;
        shadow.GlobalTransform = new Transform3D(Basis.Identity, new Vector3(p.X, 0.03f, p.Z));
        float h = Mathf.Clamp(1f - p.Y / 4f, 0.2f, 1f);
        shadow.Scale = new Vector3(h, 1f, h);

        UpdateTrail(p);
    }

    /// <summary>Marque d'alerte sur la balle (menace imminente pour le joueur local) : éclat pulsant à 2,5 Hz, dans la couleur de l'équipe lanceuse.</summary>
    public void SetAlert(bool on, Color color)
    {
        alert = on;
        alertColor = color;
        if (on && alertMarker == null)
        {
            alertMarker = new MeshInstance3D
            {
                Mesh = new SphereMesh { Radius = 0.18f, Height = 0.36f, RadialSegments = 16, Rings = 8 },
                MaterialOverride = Materials.Additive(color), TopLevel = false,
            };
            AddChild(alertMarker);
        }
        if (alertMarker != null) alertMarker.Visible = on;
    }

    void UpdateAlertAndRing()
    {
        if (alert && alertMarker != null)
        {
            float s = 1.2f + 0.5f * Mathf.Abs(Mathf.Sin((float)visualTime * 2.5f * Mathf.Pi));
            alertMarker.Scale = Vector3.One * s;
            var c = alertColor; c.A = 0.45f;
            ((StandardMaterial3D)alertMarker.MaterialOverride!).AlbedoColor = c;
        }
        // Bombe lancée : anneau de sa zone d'effet au sol (rayon BombRadius), pour voir qui sera pris dans le souffle.
        bool ring = kind == KindBomb && !IsHeld && OwnerTeam >= 0 && !exploded;
        if (ring && bombRing == null)
        {
            var m = Materials.Additive(new Color(BombColor, 0.55f));
            bombRing = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = BombRadius - 0.06f, OuterRadius = BombRadius, Rings = 48, RingSegments = 4 }, MaterialOverride = m, TopLevel = true, Scale = new Vector3(1, 0.05f, 1) };
            AddChild(bombRing);
        }
        if (bombRing != null)
        {
            bombRing.Visible = ring;
            var p = GlobalPosition;
            bombRing.GlobalTransform = new Transform3D(Basis.Identity.Scaled(new Vector3(1, 0.05f, 1)), new Vector3(p.X, 0.05f, p.Z));
        }
    }

    /// <summary>Parade de la paume : une balle de palier 0 est déviée et devient neutre (les chargées ne se parent pas).</summary>
    public bool Deflect(Vector3 palmNormal)
    {
        if (Charge > 0 || IsHeld || exploded || IsPass) return false;
        LinearVelocity = LinearVelocity.Bounce(palmNormal.Normalized()) * 0.6f;
        Spent();
        return true;
    }

    /// <summary>Traînée en ruban : longueur proportionnelle à la vitesse, aucune sous le seuil de blessure.</summary>
    void UpdateTrail(Vector3 p)
    {
        bool fast = !IsHeld && LinearVelocity.Length() >= TrailMinSpeed;
        if (fast) trailPoints.Add(p);
        if (trailPoints.Count > TrailPoints || (!fast && trailPoints.Count > 0)) trailPoints.RemoveAt(0);
        bool hadTrail = trailMesh.GetSurfaceCount() > 0;
        if (!fast && !hadTrail && trailPoints.Count == 0) return;
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
            // Près de la caméra le ruban s'efface : sinon il barre l'écran quand une balle passe à côté de la tête.
            float nearCam = Mathf.Clamp((cam.GlobalPosition.DistanceTo(a) - 0.8f) / 1.5f, 0f, 1f);
            var side = dir.Cross((cam.GlobalPosition - a).Normalized()).Normalized() * 0.07f * t * nearCam;
            var vc = new Color(color, 0.6f * t * nearCam);
            trailMesh.SurfaceSetColor(vc); trailMesh.SurfaceAddVertex(a + side);
            trailMesh.SurfaceSetColor(vc); trailMesh.SurfaceAddVertex(a - side);
        }
        trailMesh.SurfaceEnd();
    }

    public override void _PhysicsProcess(double delta)
    {
        if (IsPuppet) return;
        if (exploded) { if ((explodedFor += delta) > 0.15) QueueFree(); return; }
        sinceRelease += delta;
        if (kind == KindBomb && !IsHeld && OwnerTeam >= 0 && !IsPass && sinceRelease > 0.12)
            foreach (var b in GetCollidingBodies()) if (b is not Ball) { Explode(-1, OwnerTeam, OwnerPlayerId); return; }   // déjà au contact au lâcher
        // Vol plafonné : au-delà de MaxFlightSeconds la balle devient neutre et retombe.
        if (ThrowMode != 0 && !IsHeld && ((arcTime += delta) > ArcMaxSeconds || (arcTime > 0.3 && GlobalPosition.Y < 0.25f))) Spent();   // fin de l'arc : au sol la balle redevient une balle neutre ramassable par tous   // une cloche qui n'a touché personne devient neutre
        if (!IsHeld && ThrowMode == 0 && OwnerTeam >= 0 && MaxFlightSeconds > 0f && (flightTime += delta) > MaxFlightSeconds * (kind == KindBounce ? 2.5f : 1f)) Spent();
        RestTime = !IsHeld && LinearVelocity.Length() < 0.4f ? RestTime + delta : 0;
        if (IsHeld || curveSpin == 0f) return;
        // Force latérale (Magnus simplifié) : perpendiculaire à la vitesse, dans le plan horizontal.
        var v = LinearVelocity;
        var side = v.Cross(Vector3.Up).Normalized();
        ApplyCentralForce(side * curveSpin * v.Length() * 0.05f);
        curveSpin = Mathf.MoveToward(curveSpin, 0f, (float)delta * 4f);
    }
}
