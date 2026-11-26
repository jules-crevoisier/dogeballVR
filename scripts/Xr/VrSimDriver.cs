using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Xr;

/// <summary>
/// Essai du joueur VR sans casque (<c>--vr-sim --vr-sim-throws</c>, builds de debug) : la branche casque de Main est construite
/// (origine tournée vers l'adversaire, mains HandThrower, recalage), la tête est posée à 1,65 m et la main droite rejoue des
/// gestes de bras enregistrés comme des trajectoires : tir tendu, effet à gauche, effet à droite, cloche par en dessous, tir chargé.
/// Les boutons passent par le même chemin que la manette (<see cref="HandThrower.SimButton"/>), le geste est lu par le vrai code.
/// Avec <c>--vr-sim-shots=DOSSIER</c> : captures du point de vue du joueur (charge, départ, vol). Quitte avec 0 si tout passe.
/// </summary>
public partial class VrSimDriver : Node
{
    public XROrigin3D Origin { get; init; } = null!;
    public XRCamera3D Camera { get; init; } = null!;
    public HandThrower Right { get; init; } = null!;
    /// <summary>Vrai quand la manche est lancée (après GO).</summary>
    public System.Func<bool> Live { get; init; } = () => true;
    /// <summary>Centre de la zone adverse la plus proche (monde), pour vérifier que le joueur démarre face à l'adversaire.</summary>
    public Vector3 EnemyZone { get; init; }

    sealed record Shot(string Name, string Expect, Vector3 Start, double Duration, System.Func<double, Vector3> Vel, bool Charge = false);

    static readonly Vector3 Rest = new(0.25f, 1.25f, -0.3f);
    readonly List<Shot> shots = new()
    {
        new("tendu", "tendu", new(0.25f, 1.85f, 0.3f), 0.25, t => new Vector3(0, -0.15f, -1f).Normalized() * 10f * Gauss(t, 0.2)),
        new("effet-gauche", "effet à gauche", new(0.6f, 1.33f, 0f), 0.2, t => Turn(t, 0.2, 9f, 10f)),
        new("effet-droite", "effet à droite", new(-0.1f, 1.33f, 0f), 0.2, t => Turn(t, 0.2, 9f, -10f)),
        new("cloche", "cloche", new(0.2f, 0.7f, 0f), 0.1, _ => new Vector3(0, 1f, -1f).Normalized() * 7f),
        new("charge", "chargé", new(0.25f, 1.85f, 0.3f), 0.25, t => new Vector3(0, -0.15f, -1f).Normalized() * 16f * Gauss(t, 0.2), true),
    };

    static float Gauss(double t, double at) => Mathf.Exp(-Mathf.Pow((float)((t - at) / 0.05), 2));
    static Vector3 Turn(double t, double T, float speed, float turn)
    {
        float h = turn * (float)(t - T);
        return new Vector3(-Mathf.Sin(h), 0, -Mathf.Cos(h)) * speed;
    }

    /// <summary>--vr-sim-cam=dos : caméra de spectateur derrière le joueur (on voit la courbe), sinon vue du joueur.</summary>
    readonly bool behind = Tools.DevFlags.Value("--vr-sim-cam=") == "dos";
    Camera3D? spectator;
    Node3D? glove;

    public override void _Ready()
    {
        Camera.Fov = 90f;   // champ du Quest 2 (~90°)
        glove = Origin.GetNodeOrNull<Node3D>("RightGlove");
        if (glove != null) glove.SetProcess(false);
        if (!behind) return;
        spectator = new Camera3D { Name = "SimSpectator", Fov = 70f, Position = new Vector3(-0.2f, 3.6f, 2.4f) };
        Origin.CallDeferred(Node.MethodName.AddChild, spectator);   // l'origine construit encore ses enfants
    }

    public override void _Process(double delta)
    {
        // Gant posé sur la main simulée (au casque, il suit la pose « grip » de la manette).
        if (glove != null) glove.Transform = Right.Transform;
        if (spectator != null && spectator.IsInsideTree() && !spectator.Current)
        {
            spectator.LookAt(Origin.ToGlobal(new Vector3(0, 0.6f, -10f)), Vector3.Up);
            spectator.MakeCurrent();
        }
    }

    int index = -1;
    double clock, phaseAt, liveAt = -1;
    string phase = "attente";
    bool ok = true, facingChecked;
    readonly string? shotsDir = Tools.DevFlags.Value("--vr-sim-shots=");

    public override void _PhysicsProcess(double delta)
    {
        clock += delta;
        Camera.Position = new Vector3(0, 1.65f, 0);
        Camera.Rotation = new Vector3(Mathf.DegToRad(-4f), 0, 0);
        if (!facingChecked && clock > 0.5)
        {
            facingChecked = true;
            var fwd = -Camera.GlobalBasis.Z; fwd.Y = 0;
            var to = EnemyZone - Camera.GlobalPosition; to.Y = 0;
            float ang = Mathf.RadToDeg(fwd.Normalized().AngleTo(to.Normalized()));
            Check("Départ face à l'adversaire", ang < 60f, $"écart entre le regard et la zone adverse : {ang:0} °");
        }
        if (liveAt < 0) { if (Live()) liveAt = clock; else { Right.Position = Rest; return; } }
        if (clock - liveAt < 1.0) { Right.Position = Rest; return; }
        if (clock - liveAt > 40) { Check("Fin des essais", false, "délai dépassé"); Finish(); return; }

        double t = clock - phaseAt;
        switch (phase)
        {
            case "attente":
                if (++index >= shots.Count) { Finish(); return; }
                Next("prise");
                break;
            case "prise":
                Right.Position = Rest;
                if (Right.HeldBall == null)
                {
                    var ball = FreeBall();
                    if (ball != null) { Right.SimButton("grip_click", true); Right.SimCatch(ball); }
                }
                if (Right.HeldBall != null && t > 0.4)
                {
                    if (shots[index].Charge) { Right.SimButton("trigger_click", true); Next("charge"); }
                    else { Right.Position = shots[index].Start; Next("place"); }
                }
                else if (t > 3) { Check(shots[index].Name, false, "pas de balle en main"); Next("attente"); }
                break;
            case "charge":
                Right.Position = Rest + new Vector3(0.03f, 0.12f, 0.0f);   // balle levée devant l'épaule
                if (t > 0.6 && t - delta <= 0.6) Shoot("charge");
                if (t > 0.95) { Right.Position = shots[index].Start; Next("place"); }
                break;
            case "place":   // main immobile au départ du geste : le saut depuis le repos sort de la fenêtre lue au lâcher
                Right.Position = shots[index].Start;
                if (t >= 0.2) Next("geste");
                break;
            case "geste":
                var s = shots[index];
                Right.Position += s.Vel(t) * (float)delta;
                if (t >= s.Duration)
                {
                    Right.SimButton("grip_click", false);
                    var g = Right.LastGesture;
                    Check(s.Name, g.Kind.StartsWith(s.Expect) || g.Kind.Contains(s.Expect), $"lu « {g.Kind} » à {g.Velocity.Length():0.0} m/s");
                    Next("vol");
                }
                break;
            case "vol":
                Right.Position = Right.Position.Lerp(Rest, (float)delta * 4f);
                foreach (double at in new[] { 0.12, 0.3, 0.5, 0.75, 1.0 })
                    if (t > at && t - delta <= at) Shoot($"{shots[index].Name}-{(int)(at * 100):000}");
                if (t > 1.6) Next("attente");
                break;
        }
    }

    void Next(string p) { phase = p; phaseAt = clock; }

    Ball? FreeBall()
    {
        foreach (var n in GetTree().GetNodesInGroup("balls"))
            if (n is Ball { IsHeld: false, HasExploded: false } b && IsInstanceValid(b)) return b;
        return null;
    }

    void Shoot(string name)
    {
        if (string.IsNullOrEmpty(shotsDir) || DisplayServer.GetName() == "headless") return;
        DirAccess.MakeDirRecursiveAbsolute(shotsDir);
        var img = GetViewport().GetTexture().GetImage();
        string path = $"{shotsDir.TrimEnd('/')}/{index + 1:00}-{name}.png";
        img.SavePng(path);
        GD.Print($"[VrSim] Capture {path}");
    }

    void Check(string name, bool pass, string detail)
    {
        GD.Print($"[VrSim] {(pass ? "OK  " : "FAIL")} {name} : {detail}");
        ok &= pass;
    }

    void Finish()
    {
        SetPhysicsProcess(false);
        GD.Print(ok ? "[VrSim] TOUT OK" : "[VrSim] ÉCHEC");
        GetTree().Quit(ok ? 0 : 1);
    }
}
