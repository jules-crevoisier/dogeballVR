using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;

namespace DogeballVR.Visual;

/// <summary>
/// Retour sensoriel commun à l'hôte et aux clients, déduit des états visibles (balles et hurtboxes) : sons de lancer par palier,
/// d'attrape, de palier, de KO ; bulles « PASSE ! », « PRISE ! », « KO ! » ; cœurs au-dessus des autres avatars ; écran grisé du joueur KO (clavier-souris :
/// en VR, un voile gris plein champ gênerait ; le spectateur y reste libre de ses mouvements).
/// </summary>
public partial class Feedback : Node
{
    public Hurtbox Local { get; set; } = null!;
    public Func<int, int> Lives { get; set; } = _ => 0;
    public int MaxLives { get; set; } = 2;
    public bool GrayWhenKo { get; set; }

    record struct BallState(bool Held, int Charge, int Owner, bool Pass, float Speed, Vector3 Pos);
    readonly Dictionary<Ball, BallState> balls = new();
    readonly Dictionary<Hurtbox, bool> eliminated = new();
    readonly Dictionary<Hurtbox, int> lastLives = new();
    static readonly string[] HitWords = { "POW !", "BAM !", "WHAM !", "BOUM !" };
    int wordIndex;
    readonly HashSet<Hurtbox> withHearts = new();
    ColorRect? gray;

    public override void _Ready()
    {
        if (!GrayWhenKo) return;
        var layer = new CanvasLayer { Name = "KoLayer", Layer = 50 };
        gray = new ColorRect { Color = new Color(0.45f, 0.47f, 0.52f, 0.55f), Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        gray.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(gray);
        AddChild(layer);
    }

    public override void _PhysicsProcess(double delta)
    {
        var seen = new HashSet<Ball>();
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball ball || ball.IsQueuedForDeletion()) continue;
            seen.Add(ball);
            var pos = ball.GlobalPosition;
            var cur = new BallState(ball.IsHeld, ball.Charge, ball.OwnerTeam, ball.IsPass, ball.LinearVelocity.Length(), pos);
            if (balls.TryGetValue(ball, out var prev))
            {
                if (!prev.Held && cur.Held)
                {
                    bool steal = prev.Owner >= 0 && cur.Owner != prev.Owner && prev.Speed >= Ball.HitMinSpeed && !prev.Pass;
                    Sfx.Play(this, steal ? SfxKind.Perfect : SfxKind.Catch, pos, steal ? 1f : 1f + 0.1f * cur.Charge);
                    if (steal) SpeechBubble.Spawn(this, "PRISE !", pos + new Vector3(0, 0.5f, 0), Palette.Team(cur.Owner));
                }
                else if (prev.Held && !cur.Held)
                {
                    if (ball.Kind != Ball.KindBomb) Sfx.Play(this, SfxKind.Throw, pos, 0.8f + 0.35f * prev.Charge);
                }
                else if (cur.Held && cur.Charge > prev.Charge) Sfx.Play(this, SfxKind.Tier, pos, 0.8f + 0.3f * cur.Charge);
                if (!prev.Pass && cur.Pass && cur.Owner >= 0) SpeechBubble.Spawn(this, "PASSE !", pos + new Vector3(0, 0.5f, 0), Palette.Team(cur.Owner));
            }
            if (cur.Speed > 7f && !cur.Held) Juice.TrailGhost(this, pos, cur.Charge >= 2 ? new Color(1f, 0.35f, 0.75f) : new Color(1f, 0.85f, 0.3f), 0.1f + 0.03f * cur.Charge);
            balls[ball] = cur;
        }
        foreach (var dead in new List<Ball>(balls.Keys)) if (!seen.Contains(dead)) balls.Remove(dead);

        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (n is not Hurtbox h) continue;
            if (h != Local && withHearts.Add(h))
            {
                var color = Palette.Team(h.Team);
                var hearts = HeartsDisplay.Create(() => Lives(h.PlayerId), MaxLives, color, 0.0035f, true);
                hearts.Follow = h;
                AddChild(hearts);
            }
            int lives = Lives(h.PlayerId);
            if (lastLives.TryGetValue(h, out var before) && lives < before && !h.Eliminated)
            {
                var hit = h.GlobalPosition + new Vector3(0, 1.2f, 0);
                var teamColor = Palette.Team(h.Team);
                Juice.Burst(this, hit, teamColor);
                Juice.Shock(this, hit, Colors.White);
                SpeechBubble.Spawn(this, HitWords[wordIndex++ % HitWords.Length], hit + new Vector3(0, 0.4f, 0), Colors.White);
                Sfx.Play(this, SfxKind.Hit, hit, 0.95f + 0.1f * (wordIndex % 3));
                Juice.HitStop(this, h == Local ? 0.09f : 0.06f);
                if (h == Local && GrayWhenKo)
                {
                    // Joueur clavier-souris touché : la vue encaisse (secousse, coup de côté), son plus sourd que pour les autres.
                    CameraFx.Shake(0.5f);
                    CameraFx.Kick(new Vector2((wordIndex % 2 == 0 ? 1f : -1f) * 2.5f, 1.2f));
                    Sfx.Play(this, SfxKind.HurtSelf);
                }
                else if (GrayWhenKo) CameraFx.Shake(0.18f * Mathf.Clamp(1f - hit.DistanceTo(Local.GlobalPosition) / 15f, 0f, 1f));   // touche proche : petite secousse
            }
            lastLives[h] = lives;
            bool was = eliminated.TryGetValue(h, out var w) && w;
            if (h.Eliminated && !was)
            {
                var pos = h.GlobalPosition + new Vector3(0, 0.9f, 0);
                SpeechBubble.Spawn(this, "KO !", pos, Palette.Team(h.Team));
                Juice.Burst(this, pos, Palette.Team(h.Team), 60, 8f);
                Juice.Shock(this, pos, Palette.Team(h.Team), 2.2f);
                Juice.HitStop(this, 0.12f, 0.08f);
                if (GrayWhenKo) CameraFx.Shake(h == Local ? 0.75f : 0.22f);
                Sfx.Play(this, SfxKind.Ko, pos);
            }
            eliminated[h] = h.Eliminated;
        }
        if (gray != null) gray.Visible = Local.Eliminated;
    }
}
