using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;

namespace DogeballVR.Visual;

/// <summary>
/// Retour sensoriel commun à l'hôte et aux clients, déduit des états visibles (balles et hurtboxes) : sons de lancer par palier,
/// d'attrape, de palier, de KO ; impacts de touche et de KO visibles par tous (<see cref="ImpactFx"/>) ; autocollants « PASSE ! »,
/// « PRISE ! », « BAM ! », « KO ! » (<see cref="SpeechBubble"/>) ; nom et cœurs au-dessus des autres joueurs (<see cref="PlayerTag"/>) ;
/// écran grisé du joueur KO (clavier-souris : en VR, un voile gris plein champ gênerait ; le spectateur y reste libre de ses mouvements).
/// </summary>
public partial class Feedback : Node
{
    public Hurtbox Local { get; set; } = null!;
    public Func<int, int> Lives { get; set; } = _ => 0;
    public int MaxLives { get; set; } = 2;
    public bool GrayWhenKo { get; set; }
    /// <summary>Noms des joueurs (facultatif, à brancher par Main, par exemple sur MatchView.NameOf). Sinon déduits ici.</summary>
    public Func<int, string>? NameOf { get; set; }

    record struct BallState(bool Held, int Charge, int Owner, bool Pass, float Speed, Vector3 Pos);
    readonly Dictionary<Ball, BallState> balls = new();
    readonly Dictionary<Hurtbox, bool> eliminated = new();
    readonly Dictionary<Hurtbox, int> lastLives = new();
    readonly Dictionary<int, (int id, ulong at)> lastShooter = new();
    static readonly string[] HitWords = { "POW !", "BAM !", "WHAM !", "BOUM !" };
    int wordIndex;
    readonly HashSet<Hurtbox> withTags = new();
    MatchController? match;
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

    public override void _EnterTree() => MatchController.HitScored += OnHitScored;
    public override void _ExitTree() => MatchController.HitScored -= OnHitScored;

    /// <summary>Touche validée (hôte, ou relayée au client) : on retient le tireur pour doser l'arrêt sur image.</summary>
    void OnHitScored(int shooter, int target, bool kill) => lastShooter[target] = (shooter, Time.GetTicksMsec());

    /// <summary>Nom affiché au-dessus d'un joueur : celui fourni par Main s'il y en a un, sinon nom de salle en ligne, « Bot N » ou « Joueur N ».</summary>
    string NameFor(int id)
    {
        if (NameOf?.Invoke(id) is { Length: > 0 } given) return given;
        if (MatchView.HumanName?.Invoke(id) is { Length: > 0 } human) return human;
        if (match == null || !IsInstanceValid(match)) match = GetTree().CurrentScene?.FindChild("MatchController", true, false) as MatchController;
        return match?.IsBot?.Invoke(id) == true ? $"Bot {id + 1}" : $"Joueur {id + 1}";
    }

    Hurtbox? NearestOfTeam(int team, Vector3 pos, float within)
    {
        Hurtbox? best = null;
        float bestD = within;
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (n is not Hurtbox h || h.Team != team || h.Eliminated) continue;
            float d = h.GlobalPosition.DistanceTo(pos);
            if (d < bestD) { best = h; bestD = d; }
        }
        return best;
    }

    /// <summary>Point d'impact : la balle libre la plus proche de la cible (position de l'image), ramenée contre le corps ; sinon la poitrine.</summary>
    Vector3 ImpactPoint(Hurtbox h)
    {
        var chest = h.GlobalPosition + new Vector3(0, 0.25f, 0);
        var best = chest;
        float bestD = 1.8f;
        foreach (var st in balls.Values)
        {
            float d = st.Pos.DistanceTo(chest);
            if (d < bestD && !st.Held) { bestD = d; best = st.Pos; }
        }
        return chest + (best - chest).LimitLength(0.45f);
    }

    static Vector3 HeadOf(Hurtbox h) => h.GlobalPosition + new Vector3(0, 0.95f, 0);

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
                    if (steal) Sticker("PRISE !", cur.Owner, pos, SpeechBubble.Size.Normal);
                }
                else if (prev.Held && !cur.Held)
                {
                    if (ball.Kind != Ball.KindBomb) Sfx.Play(this, SfxKind.Throw, pos, 0.8f + 0.35f * prev.Charge);
                }
                else if (cur.Held && cur.Charge > prev.Charge) Sfx.Play(this, SfxKind.Tier, pos, 0.8f + 0.3f * cur.Charge);
                if (!prev.Pass && cur.Pass && cur.Owner >= 0) Sticker("PASSE !", cur.Owner, pos, SpeechBubble.Size.Small);
            }
            if (cur.Speed > 7f && !cur.Held) Juice.TrailGhost(this, pos, cur.Charge >= 2 ? new Color(1f, 0.35f, 0.75f) : new Color(1f, 0.85f, 0.3f), 0.1f + 0.03f * cur.Charge);
            balls[ball] = cur;
        }
        foreach (var dead in new List<Ball>(balls.Keys)) if (!seen.Contains(dead)) balls.Remove(dead);

        var cam = GetViewport().GetCamera3D();
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (n is not Hurtbox h) continue;
            if (h != Local && withTags.Add(h))
            {
                int id = h.PlayerId;
                AddChild(new PlayerTag
                {
                    Name = $"Tag{id}", Target = h, Max = MaxLives, Lives = () => Lives(id), PlayerName = () => NameFor(id),
                    ViewerTeam = () => IsInstanceValid(Local) ? Local.Team : -1,
                });
            }
            int lives = Lives(h.PlayerId);
            bool newKo = h.Eliminated && !(eliminated.TryGetValue(h, out var was) && was);
            bool lostLife = lastLives.TryGetValue(h, out var before) && lives < before;
            if (newKo) OnTouched(h, cam, true);
            else if (lostLife && !h.Eliminated) OnTouched(h, cam, false);
            lastLives[h] = lives;
            eliminated[h] = h.Eliminated;
        }
        if (gray != null) gray.Visible = Local.Eliminated;
        if (ImpactTest) RunImpactTest(cam, delta);
    }

    // --impact-test : rejoue chaque seconde, droit devant la caméra (la caméra automatique regarde souvent un mur),
    // une touche à 7 m, une touche à 20 m, un KO à 9 m et une prise à 6 m : vérification des effets sur captures.
    static readonly bool ImpactTest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--impact-test") >= 0;
    double testClock = 3.2;
    int testStep;

    void RunImpactTest(Camera3D? cam, double delta)
    {
        testClock -= delta;
        if (testClock > 0 || cam == null) return;
        testClock = 1.0;
        var fwd = -cam.GlobalTransform.Basis.Z;
        fwd = new Vector3(fwd.X, 0, fwd.Z).Normalized();
        int step = testStep++ % 4;
        float dist = step switch { 0 => 7f, 1 => 20f, 2 => 9f, _ => 6f };
        var right = cam.GlobalTransform.Basis.X;
        var pos = cam.GlobalPosition + fwd * dist + right * (step % 2 == 0 ? 2.2f : -2.2f);   // à côté de l'avatar local, pas derrière
        pos.Y = (IsInstanceValid(Local) ? Local.GlobalPosition.Y : cam.GlobalPosition.Y - 1f) + 0.25f;
        var head = pos + new Vector3(0, 0.7f, 0);
        var color = Palette.Team(step % 2);
        GD.Print($"[Impact] test {step} à {dist} m, image {Engine.GetFramesDrawn()}");
        switch (step)
        {
            case 0:
            case 1:
                ImpactFx.Hit(this, pos, color);
                SpeechBubble.Spawn(this, HitWords[wordIndex++ % HitWords.Length], head, color, SpeechBubble.Size.Normal, null);
                break;
            case 2:
                ImpactFx.Ko(this, pos, color);
                SpeechBubble.Spawn(this, "KO !", head, color, SpeechBubble.Size.Big, null);
                break;
            default:
                SpeechBubble.Spawn(this, "PRISE !", head, color, SpeechBubble.Size.Normal, null);
                break;
        }
    }

    /// <summary>Touche ou KO sur un joueur, vu par tout le monde : impact, autocollant, son ; au PC, secousse et arrêt sur image dosés.</summary>
    void OnTouched(Hurtbox h, Camera3D? cam, bool ko)
    {
        int shooter = lastShooter.TryGetValue(h.PlayerId, out var s) && Time.GetTicksMsec() - s.at < 1500 ? s.id : -1;
        var hit = ImpactPoint(h);
        var shooterColor = Palette.Team(h.Team == 0 ? 1 : 0);   // pas de dégâts entre alliés : le tireur est dans l'autre équipe
        bool localTarget = h == Local;
        bool localShooter = IsInstanceValid(Local) && shooter >= 0 && shooter == Local.PlayerId;
        float dist = cam != null ? cam.GlobalPosition.DistanceTo(hit) : 999f;
        bool near = !localTarget && dist < 16f && ScreenSize.OnScreen(cam, hit, 0f);
        float closeness = Mathf.Clamp(1f - dist / 16f, 0f, 1f);
        GD.Print($"[Impact] {(ko ? "KO" : "touche")} sur {h.PlayerId} (équipe {h.Team}), tireur {shooter}, à l'écran et proche : {near}, image {Engine.GetFramesDrawn()}");

        if (ko)
        {
            ImpactFx.Ko(this, hit, shooterColor);
            Sticker("KO !", h, shooterColor, SpeechBubble.Size.Big);
            if (localTarget || localShooter || near) Juice.HitStop(this, 0.12f, 0.08f);
            if (GrayWhenKo) CameraFx.Shake(localTarget ? 0.75f : near ? 0.1f + 0.15f * closeness : 0f);
            Sfx.Play(this, SfxKind.Ko, hit);
            return;
        }
        ImpactFx.Hit(this, hit, shooterColor);
        Sticker(HitWords[wordIndex++ % HitWords.Length], h, shooterColor, SpeechBubble.Size.Normal);
        Sfx.Play(this, SfxKind.Hit, hit, 0.95f + 0.1f * (wordIndex % 3));
        if (localTarget || localShooter) Juice.HitStop(this, localTarget ? 0.09f : 0.06f);
        else if (near) Juice.HitStop(this, 0.04f, 0.3f);   // touche entre d'autres joueurs sous nos yeux : à peine marquée
        if (localTarget && GrayWhenKo)
        {
            // Joueur clavier-souris touché : la vue encaisse (secousse, coup de côté), son plus sourd que pour les autres.
            CameraFx.Shake(0.5f);
            CameraFx.Kick(new Vector2((wordIndex % 2 == 0 ? 1f : -1f) * 2.5f, 1.2f));
            Sfx.Play(this, SfxKind.HurtSelf);
        }
        else if (GrayWhenKo && near) CameraFx.Shake(0.05f + 0.12f * closeness);   // touche proche, à l'écran : petite secousse
    }

    void Sticker(string text, Hurtbox h, Color rim, SpeechBubble.Size size) => SpeechBubble.Spawn(this, text, HeadOf(h), rim, size, h);

    /// <summary>Autocollant d'une action de balle (prise, passe) : posé sur le joueur de l'équipe le plus proche de la balle.</summary>
    void Sticker(string text, int team, Vector3 near, SpeechBubble.Size size)
    {
        var h = NearestOfTeam(team, near, 2.5f);
        if (h != null) Sticker(text, h, Palette.Team(team), size);
        else SpeechBubble.Spawn(this, text, near + new Vector3(0, 0.5f, 0), Palette.Team(team), size, null);
    }
}
