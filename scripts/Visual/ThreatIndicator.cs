using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;

namespace DogeballVR.Visual;

/// <summary>
/// Signal d'alerte d'un tir adverse (même code pour la menace et le tireur) : dès que le temps avant impact prévu passe sous 0,6 s
/// (soit dès le lâcher d'un tir proche, soit 0,5 s avant l'impact d'un tir lointain), la balle s'orne d'une marque dans le monde,
/// à la couleur de l'équipe visée. Côté victime locale : bip spatialisé qui s'accélère, vibration croissante des deux mains (VR),
/// liseré d'écran (clavier-souris). Côté tireur local : liseré à la couleur de l'équipe visée. Pas de flèche fixée à la tête.
/// </summary>
public partial class ThreatIndicator : Node
{
    public const float WarnSeconds = 0.6f;
    const float LateralReach = 0.9f;

    public Hurtbox Local { get; set; } = null!;
    /// <summary>Vibration d'une main (amplitude 0..1, durée s) ; absent hors VR.</summary>
    public Action<float, float>? Haptic { get; set; }

    readonly HashSet<Ball> alerted = new();
    ColorRect[] edges = null!;
    double nextBeep;
    double edgeUntil;
    Color edgeColor;

    /// <summary>Temps avant impact (s) de la balle sur la hurtbox, ou +inf : elle doit se rapprocher, passer à moins de LateralReach de l'axe.</summary>
    public static float TimeToImpact(Vector3 ballPos, Vector3 velocity, Vector3 target)
    {
        float speed = velocity.Length();
        if (speed < Ball.HitMinSpeed) return float.PositiveInfinity;
        var dir = velocity / speed;
        var rel = target - ballPos;
        float along = rel.Dot(dir);
        if (along < 0f) return float.PositiveInfinity;
        if ((rel - dir * along).Length() > LateralReach) return float.PositiveInfinity;
        return along / speed;
    }

    public override void _Ready()
    {
        var layer = new CanvasLayer { Name = "ThreatEdges", Layer = 60 };
        AddChild(layer);
        edges = new ColorRect[4];
        for (int i = 0; i < 4; i++)
        {
            edges[i] = new ColorRect { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
            layer.AddChild(edges[i]);
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        var targets = new List<Hurtbox>();
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
            if (n is Hurtbox { Eliminated: false } h && !h.DashInvulnerable) targets.Add(h);

        float localT = float.PositiveInfinity, shooterT = float.PositiveInfinity;
        int shooterTarget = -1;
        var current = new HashSet<Ball>();
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } ball || ball.OwnerTeam < 0 || ball.IsPass || ball.HasExploded) continue;
            float best = float.PositiveInfinity;
            int targetTeam = -1;
            foreach (var h in targets)
            {
                if (h.Team == ball.OwnerTeam) continue;
                float t = TimeToImpact(ball.GlobalPosition, ball.LinearVelocity, h.GlobalPosition);
                if (t < best) { best = t; targetTeam = h.Team; }
                if (h == Local && t < localT) localT = t;
            }
            if (best > WarnSeconds) continue;
            current.Add(ball);
            if (alerted.Add(ball)) { ball.SetAlert(true, Palette.Team(targetTeam)); GD.Print($"[Alerte] tir de l'équipe {ball.OwnerTeam} sur l'équipe {targetTeam} : impact dans {best:0.00} s"); }
            if (ball.OwnerTeam == Local.Team && best < shooterT) { shooterT = best; shooterTarget = targetTeam; }
        }
        foreach (var b in alerted)
            if (!current.Contains(b) && IsInstanceValid(b)) b.SetAlert(false, Colors.White);
        alerted.RemoveWhere(b => !current.Contains(b));

        if (localT <= WarnSeconds)
        {
            float urgency = 1f - Mathf.Clamp(localT / WarnSeconds, 0f, 1f);   // 0 au début de l'alerte, 1 à l'impact
            if (now >= nextBeep)
            {
                nextBeep = now + Mathf.Lerp(0.18, 0.07, urgency);
                Sfx.Play(this, SfxKind.Alert, null, Mathf.Lerp(0.9f, 1.5f, urgency));
                Haptic?.Invoke(Mathf.Lerp(0.25f, 1f, urgency), 0.08f);
            }
            Show(Palette.Team(Local.Team), now);
        }
        else if (shooterT <= WarnSeconds && shooterTarget >= 0) Show(Palette.Team(shooterTarget), now);
        UpdateEdges(now);
    }

    void Show(Color color, double now) { edgeColor = color; edgeUntil = now + 0.12; }

    void UpdateEdges(double now)
    {
        bool on = now < edgeUntil;
        var size = GetViewport().GetVisibleRect().Size;
        const float w = 18f;
        var rects = new[] { new Rect2(0, 0, size.X, w), new Rect2(0, size.Y - w, size.X, w), new Rect2(0, 0, w, size.Y), new Rect2(size.X - w, 0, w, size.Y) };
        for (int i = 0; i < 4; i++)
        {
            edges[i].Visible = on;
            edges[i].Position = rects[i].Position;
            edges[i].Size = rects[i].Size;
            edges[i].Color = new Color(edgeColor, 0.85f);
        }
    }
}
