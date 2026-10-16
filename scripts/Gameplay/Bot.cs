using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;
using DogeballVR.Zones;

namespace DogeballVR.Gameplay;

/// <summary>
/// Bot simple pour compléter les équipes : il esquive en allers-retours dans sa zone, rattrape les balles
/// qui passent près de lui et les renvoie vers un adversaire (ou à son allié, ce qui charge la balle).
/// </summary>
public partial class Bot : Node3D
{
    const float Speed = 4.5f;
    const float CatchRadius = 1.4f;

    public ZoneDef Zone { get; set; } = null!;
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public Hurtbox Hurtbox { get; set; } = null!;
    /// <summary>Positions des autres joueurs (visées).</summary>
    public IList<Node3D> Targets { get; set; } = new List<Node3D>();
    public IList<int> TargetTeams { get; set; } = new List<int>();

    Node3D holdPoint = null!;
    Ball? held;
    double throwDelay;
    float targetX;
    double changeDirTimer;
    readonly RandomNumberGenerator rng = new();

    public override void _Ready()
    {
        rng.Randomize();
        holdPoint = new Node3D { Name = "HoldPoint", Position = new Vector3(0, 1.4f, 0) };
        AddChild(holdPoint);
        AddChild(new MeshInstance3D
        {
            Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.7f },
            Position = new Vector3(0, 0.85f, 0),
        });
        Hurtbox.Position = new Vector3(0, 0.85f, 0);
        AddChild(Hurtbox);
    }

    public override void _PhysicsProcess(double delta)
    {
        if (Hurtbox.Eliminated) { Visible = false; return; }
        Visible = true;
        Dodge(delta);
        if (held == null) TryCatch();
        else ThinkThrow(delta);
    }

    void Dodge(double delta)
    {
        changeDirTimer -= delta;
        if (changeDirTimer <= 0)
        {
            targetX = rng.RandfRange(-(ZoneDepth * 0.5f - 0.5f), ZoneDepth * 0.5f - 0.5f);
            changeDirTimer = rng.RandfRange(0.4f, 1.2f);
        }
        // Déplacement dans le repère de la zone : l'axe « profondeur » devient latéral pour le bot.
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(Zone.FacingDegrees));
        var local = basis.Inverse() * (GlobalPosition - Zone.Center);
        local.Z = Mathf.MoveToward(local.Z, targetX, Speed * (float)delta);
        local.X = Mathf.Clamp(local.X, -(ZoneWidth * 0.5f - 0.5f), ZoneWidth * 0.5f - 0.5f);
        GlobalPosition = Zone.Center + basis * local;
    }

    void TryCatch()
    {
        foreach (var node in GetTree().GetNodesInGroup("balls"))
        {
            if (node is not Ball ball || ball.IsHeld) continue;
            if (ball.GlobalPosition.DistanceTo(GlobalPosition + Vector3.Up * 1.2f) > CatchRadius) continue;
            held = ball;
            ball.Hold(holdPoint, Hurtbox.PlayerId, Hurtbox.Team);
            throwDelay = rng.RandfRange(0.4f, 1.0f);
            return;
        }
    }

    void ThinkThrow(double delta)
    {
        throwDelay -= delta;
        if (throwDelay > 0 || held == null) return;
        // Cible : un adversaire de préférence, parfois l'allié pour charger la balle.
        var candidates = new List<Node3D>();
        bool pass = rng.Randf() < 0.2f;
        for (int i = 0; i < Targets.Count; i++)
        {
            bool enemy = TargetTeams[i] != Hurtbox.Team;
            if (Targets[i] != this && enemy != pass) candidates.Add(Targets[i]);
        }
        if (candidates.Count == 0) candidates = new List<Node3D>(Targets);
        var target = candidates[(int)(rng.Randi() % (uint)candidates.Count)];
        var aim = target.GlobalPosition + new Vector3(rng.RandfRange(-0.5f, 0.5f), 1.1f, rng.RandfRange(-0.5f, 0.5f));
        var dir = (aim - holdPoint.GlobalPosition).Normalized();
        held.Release(dir * rng.RandfRange(11f, 18f) + Vector3.Up * 1.5f, 0f);
        held = null;
    }
}
