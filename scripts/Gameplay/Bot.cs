using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;
using DogeballVR.Visual;
using DogeballVR.Zones;

namespace DogeballVR.Gameplay;

/// <summary>
/// Bot simple pour compléter les équipes : il esquive en allers-retours dans sa zone, rattrape les balles
/// qui passent près de lui et les renvoie vers un adversaire (ou à son allié, ce qui charge la balle).
/// </summary>
public partial class Bot : Node3D
{
    const float Speed = 3.0f;
    const float CatchRadius = 1.6f;

    public ZoneDef Zone { get; set; } = null!;
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public Hurtbox Hurtbox { get; set; } = null!;
    public Avatar? Avatar { get; private set; }
    /// <summary>Positions des autres joueurs (visées).</summary>
    public IList<Node3D> Targets { get; set; } = new List<Node3D>();
    public IList<int> TargetTeams { get; set; } = new List<int>();

    Node3D holdPoint = null!;
    Ball? held;
    double throwDelay;
    double catchCooldown; // évite de rattraper aussitôt la balle qu'on vient de lancer
    float targetX;
    double changeDirTimer;
    readonly RandomNumberGenerator rng = new();

    public override void _Ready()
    {
        rng.Randomize();
        holdPoint = new Node3D { Name = "HoldPoint", Position = new Vector3(0, 1.4f, 0) };
        AddChild(holdPoint);
        Avatar = Avatar.Create(Hurtbox.Team, bot: true);
        AddChild(Avatar);
        Hurtbox.Position = new Vector3(0, 0.85f, 0);
        AddChild(Hurtbox);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Hurtbox.Eliminated) { DropHeld(); Visible = false; return; }
        Visible = true;
        if (held != null && !IsInstanceValid(held)) held = null; // balle supprimée au changement de manche
        catchCooldown -= delta;
        TrackTargets(delta);
        Dodge(delta);
        if (held == null && catchCooldown <= 0) TryCatch();
        else ThinkThrow(delta);
    }

    /// <summary>Le bot disparaît (remplacé par un joueur) : il lâche sa balle au lieu de l'emporter.</summary>
    public void DropHeld()
    {
        if (held != null && IsInstanceValid(held)) held.Release(Vector3.Zero, 0f);
        held = null;
    }

    void Dodge(double delta)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(Zone.FacingDegrees));
        var local = basis.Inverse() * (GlobalPosition - Zone.Center);
        float limitX = ZoneWidth * 0.5f - 0.5f;
        float limitZ = ZoneDepth * 0.5f - 0.5f;

        // Sans balle en main : aller chercher la balle libre la plus proche qui est dans sa zone.
        Vector3? goal = null;
        if (held == null)
        {
            float best = float.MaxValue;
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } ball) continue;
                var bl = basis.Inverse() * (ball.GlobalPosition - Zone.Center);
                if (Mathf.Abs(bl.X) > limitX + 0.5f || Mathf.Abs(bl.Z) > limitZ + 0.5f) continue;
                float d = ball.GlobalPosition.DistanceTo(GlobalPosition);
                if (d < best) { best = d; goal = bl; }
            }
        }

        if (goal is Vector3 g)
        {
            local = local.MoveToward(new Vector3(g.X, 0, g.Z), Speed * (float)delta);
        }
        else
        {
            // Esquive : allers-retours latéraux dans la zone.
            changeDirTimer -= delta;
            if (changeDirTimer <= 0)
            {
                targetX = rng.RandfRange(-limitX, limitX);
                changeDirTimer = rng.RandfRange(0.4f, 1.2f);
            }
            local.X = Mathf.MoveToward(local.X, targetX, Speed * (float)delta);
        }
        local.X = Mathf.Clamp(local.X, -limitX, limitX);
        local.Z = Mathf.Clamp(local.Z, -limitZ, limitZ);
        local.Y = 0;
        GlobalPosition = Zone.Center + basis * local;
    }

    void TryCatch()
    {
        foreach (var node in GetTree().GetNodesInGroup("balls"))
        {
            if (node is not Ball ball || ball.IsHeld) continue;
            if (ball.IsPass && ball.OwnerTeam != Hurtbox.Team) continue;   // une passe adverse ne s'intercepte pas
            if (ball.GlobalPosition.DistanceTo(GlobalPosition + Vector3.Up * 0.8f) > CatchRadius) continue;
            // Un tir ennemi rapide n'est attrapé qu'une fois sur trois : les bots ne neutralisent plus la moitié des tirs.
            if (ball.OwnerTeam >= 0 && ball.OwnerTeam != Hurtbox.Team && ball.LinearVelocity.Length() > 8f && rng.Randf() > 0.3f)
            {
                catchCooldown = 0.5f;
                return;
            }
            held = ball;
            ball.Hold(holdPoint, Hurtbox.PlayerId, Hurtbox.Team);
            throwDelay = rng.RandfRange(0.3f, 0.7f);
            return;
        }
    }

    const float MaxUsefulRange = 16f;
    int waitedForRange;
    readonly Dictionary<Node3D, Vector3> lastPositions = new();
    readonly Dictionary<Node3D, Vector3> velocities = new();

    /// <summary>Vitesse estimée de chaque cible (pour l'anticipation du tir).</summary>
    void TrackTargets(double delta)
    {
        if (delta <= 0) return;
        foreach (var t in Targets)
        {
            if (!IsInstanceValid(t)) continue;
            var p = t.GlobalPosition;
            if (lastPositions.TryGetValue(t, out var prev))
                velocities[t] = velocities.TryGetValue(t, out var old) ? old.Lerp((p - prev) / (float)delta, 0.3f) : (p - prev) / (float)delta;
            lastPositions[t] = p;
        }
    }

    void ThinkThrow(double delta)
    {
        throwDelay -= delta;
        if (throwDelay > 0 || held == null) return;
        // Cible : l'adversaire le plus proche de préférence, parfois l'allié pour charger la balle.
        bool pass = rng.Randf() < 0.15f;
        Node3D? target = null;
        float bestDist = float.MaxValue;
        for (int i = 0; i < Targets.Count; i++)
        {
            bool enemy = TargetTeams[i] != Hurtbox.Team;
            if (Targets[i] == this || enemy == pass) continue;
            float d = Targets[i].GlobalPosition.DistanceTo(GlobalPosition);
            if (d < bestDist) { bestDist = d; target = Targets[i]; }
        }
        target ??= Targets[(int)(rng.Randi() % (uint)Targets.Count)];
        // Hors de portée utile (18 m et plus : aucune touche mesurée) : on attend un peu que la cible se rapproche.
        if (!pass && bestDist > MaxUsefulRange && waitedForRange < 3) { waitedForRange++; throwDelay = 0.5; return; }
        waitedForRange = 0;
        float speed = rng.RandfRange(16f, 24f);
        // Anticipation : on vise là où la cible sera à l'arrivée de la balle.
        var lead = velocities.TryGetValue(target, out var v) ? v * (bestDist / speed) : Vector3.Zero;
        var aim = target.GlobalPosition + lead + new Vector3(rng.RandfRange(-0.1f, 0.1f), 1.1f, rng.RandfRange(-0.1f, 0.1f));
        var dir = (aim - holdPoint.GlobalPosition).Normalized();
        held.Release(Assist.PlanFor(held, dir * speed + Vector3.Up * 1.2f, 0), 0f);
        held = null;
        catchCooldown = 0.6;
    }
}
