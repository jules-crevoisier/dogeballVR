using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;
using DogeballVR.UI;

namespace DogeballVR.Visual;

/// <summary>
/// Retour sensoriel commun à l'hôte et aux clients, déduit des états visibles (balles et hurtboxes) : sons de lancer par palier,
/// d'attrape, de palier, de KO ; impacts de touche et de KO visibles par tous (<see cref="ImpactFx"/>) ; autocollants « PASSE ! »,
/// « PRISE ! », « BAM ! », « KO ! » (<see cref="SpeechBubble"/>) réservés à ce qui concerne le joueur local (il touche, il est touché,
/// il attrape, on attrape sa balle, KO par ou contre lui : le reste du terrain ne pose pas de mots) ; nom et cœurs au-dessus des autres
/// joueurs (<see cref="PlayerTag"/>) ;
/// écran désaturé du joueur KO (clavier-souris : en VR, un voile plein champ gênerait ; le spectateur y reste libre de ses mouvements).
/// Au clavier-souris, le KO n'a pas d'autocollant : le HUD l'annonce déjà (bandeau « KO ! », fil des KO, grand titre du joueur KO).
/// </summary>
public partial class Feedback : Node
{
    public Hurtbox Local { get; set; } = null!;
    public Func<int, int> Lives { get; set; } = _ => 0;
    public int MaxLives { get; set; } = 2;
    public bool GrayWhenKo { get; set; }
    /// <summary>Noms des joueurs (facultatif, à brancher par Main, par exemple sur MatchView.NameOf). Sinon déduits ici.</summary>
    public Func<int, string>? NameOf { get; set; }
    /// <summary>VR : vibration d'une manette (main 0 gauche, 1 droite, -1 les deux ; amplitude 0..1 ; durée s). Null au clavier-souris.</summary>
    public Action<int, float, float>? Haptic { get; set; }
    bool Vr => !GrayWhenKo;

    record struct BallState(bool Held, int Charge, int Owner, int OwnerId, bool Pass, float Speed, Vector3 Pos, bool Exploded, int Hand);
    // Balle adverse qui frôle le joueur local : distance mini à la tête et vies au début du passage (pas de « frôlé » si elle a touché).
    readonly Dictionary<Ball, (float Min, int Lives)> nearMiss = new();
    readonly Dictionary<Ball, BallState> balls = new();
    readonly Dictionary<Hurtbox, bool> eliminated = new();
    readonly Dictionary<Hurtbox, int> lastLives = new();
    readonly Dictionary<int, (int id, ulong at)> lastShooter = new();
    static readonly string[] HitWords = { "POW !", "BAM !", "WHAM !", "BOUM !" };
    int wordIndex;
    readonly HashSet<Hurtbox> withTags = new();
    MatchController? match;
    ColorRect? gray;
    ShaderMaterial? grayMaterial;
    Tween? grayTween;

    public override void _Ready()
    {
        if (!GrayWhenKo) return;
        // Sous le HUD (calque 5) et les crochets de verrou (4) : seul le monde 3D se désature, le titre « KO ! » et le score restent
        // nets et en couleur. Shader plein écran sur la copie de l'écran (rendus Compatibilité, Mobile et Forward+).
        var layer = new CanvasLayer { Name = "KoLayer", Layer = 3 };
        grayMaterial = new ShaderMaterial { Shader = new Shader { Code = GrayShader } };   // pas de cache static : un ShaderMaterial gardé en static fuit à la fermeture
        gray = new ColorRect { Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore, Material = grayMaterial };
        gray.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(gray);
        AddChild(layer);
    }

    /// <summary>Monde désaturé à 85 %, un peu assombri et refroidi ; amount fait le fondu d'entrée.</summary>
    const string GrayShader = @"shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear;
uniform float amount = 1.0;
void fragment() {
    vec3 c = texture(screen_tex, SCREEN_UV).rgb;
    float l = dot(c, vec3(0.299, 0.587, 0.114));
    vec3 g = mix(c, vec3(l), 0.85 * amount);
    g = mix(g, g * vec3(0.72, 0.75, 0.84), amount);
    COLOR = vec4(g, 1.0);
}";

    /// <summary>Voile du joueur KO : fondu d'entrée de 0,3 s, retrait immédiat au retour.</summary>
    void SetGray(bool on)
    {
        if (gray == null || grayMaterial == null || gray.Visible == on) return;
        gray.Visible = on;
        grayTween?.Kill();
        if (!on) return;
        var mat = grayMaterial;
        mat.SetShaderParameter("amount", 0f);
        grayTween = gray.CreateTween();
        grayTween.TweenMethod(Callable.From<float>(a => mat.SetShaderParameter("amount", a)), 0f, 1f, 0.3);
    }

    public override void _EnterTree() { current = this; MatchController.HitScored += OnHitScored; Ball.CaughtThrow += OnCaughtThrow; PerfectCatchNoted += OnPerfectCatch; Ball.Exploded += OnBombExploded; }
    public override void _ExitTree() { if (current == this) current = null; MatchController.HitScored -= OnHitScored; Ball.CaughtThrow -= OnCaughtThrow; PerfectCatchNoted -= OnPerfectCatch; Ball.Exploded -= OnBombExploded; }

    /// <summary>Attrape parfaite signalée hors de l'hôte (client PC) : l'autocollant devient « PRISE PARFAITE ! ».</summary>
    public static void NotePerfectCatch(int playerId) => PerfectCatchNoted?.Invoke(playerId);
    static event System.Action<int>? PerfectCatchNoted;
    readonly Dictionary<int, ulong> perfectAt = new();
    (int Id, ulong Ms) lastSteal = (-1, 0);

    static Feedback? current;
    /// <summary>Petit autocollant au-dessus du joueur local (geste : feinte, tacle…). Faux s'il n'y a pas de Feedback actif.</summary>
    public static bool LocalSticker(string text)
    {
        if (current == null || !IsInstanceValid(current) || !current.IsInsideTree() || !IsInstanceValid(current.Local)) return false;
        current.Sticker(text, current.Local, Palette.Team(current.Local.Team), SpeechBubble.Size.Small);
        return true;
    }

    void OnCaughtThrow(int catcherId, bool perfect) { if (perfect) OnPerfectCatch(catcherId); }

    /// <summary>Une seule annonce par attrape : si « PRISE ! » vient d'être posé pour ce joueur, il est remplacé par « PRISE PARFAITE ! ».</summary>
    void OnPerfectCatch(int catcherId)
    {
        perfectAt[catcherId] = Time.GetTicksMsec();
        if (lastSteal.Id == catcherId && Time.GetTicksMsec() - lastSteal.Ms < 500 && HurtboxOf(catcherId) is { } catcher)
        {
            lastSteal = (-1, 0);
            Sticker("PRISE PARFAITE !", catcher, Palette.Team(catcher.Team), SpeechBubble.Size.Small);
        }
    }

    /// <summary>Touche validée (hôte, ou relayée au client) : on retient le tireur pour doser l'arrêt sur image.</summary>
    void OnHitScored(int shooter, int target, bool kill) => lastShooter[target] = (shooter, Time.GetTicksMsec());

    /// <summary>Nom affiché au-dessus d'un joueur : celui fourni par Main s'il y en a un, sinon nom de salle en ligne, surnom du bot (MatchView.BotName) ou « Joueur N ».</summary>
    string NameFor(int id)
    {
        if (NameOf?.Invoke(id) is { Length: > 0 } given) return given;
        if (MatchView.HumanName?.Invoke(id) is { Length: > 0 } human) return human;
        if (match == null || !IsInstanceValid(match)) match = GetTree().CurrentScene?.FindChild("MatchController", true, false) as MatchController;
        return match?.IsBot?.Invoke(id) == true ? MatchView.BotName(id) : $"Joueur {id + 1}";
    }

    Hurtbox? HurtboxOf(int playerId)
    {
        if (playerId < 0) return null;
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
            if (n is Hurtbox h && h.PlayerId == playerId && !h.Eliminated) return h;
        return null;
    }

    bool IsLocal(int playerId) => playerId >= 0 && IsInstanceValid(Local) && Local.PlayerId == playerId;

    /// <summary>Point d'impact : la balle libre la plus proche de la cible (position de l'image), ramenée contre le corps ; sinon la poitrine.
    /// Donne aussi le lanceur de cette balle (−1 si aucune) : repli quand la touche validée n'est pas relayée au client.</summary>
    (Vector3 pos, int thrower) ImpactPoint(Hurtbox h)
    {
        var chest = h.GlobalPosition + new Vector3(0, 0.25f, 0);
        var best = chest;
        int thrower = -1;
        float bestD = 1.8f;
        foreach (var st in balls.Values)
        {
            float d = st.Pos.DistanceTo(chest);
            if (d < bestD && !st.Held) { bestD = d; best = st.Pos; thrower = st.Owner >= 0 && st.Owner != h.Team ? st.OwnerId : -1; }
        }
        return (chest + (best - chest).LimitLength(0.45f), thrower);
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
            bool mine = ball.IsHeld && IsInstanceValid(Local) && ball.OwnerPlayerId == Local.PlayerId;
            var cur = new BallState(ball.IsHeld, ball.Charge, ball.OwnerTeam, ball.OwnerPlayerId, ball.IsPass, ball.LinearVelocity.Length(), pos,
                ball.HasExploded, mine ? Local.HeldHand : -1);
            if (balls.TryGetValue(ball, out var prev))
            {
                if (!prev.Held && cur.Held)
                {
                    bool steal = prev.Owner >= 0 && cur.Owner != prev.Owner && prev.Speed >= Ball.HitMinSpeed && !prev.Pass;
                    Sfx.Play(this, steal ? SfxKind.Perfect : SfxKind.Catch, pos, steal ? 1f : 1f + 0.1f * cur.Charge);
                    if (IsLocal(cur.OwnerId)) OnLocalCatch(steal, cur.Hand);
                    // « PRISE ! » seulement si le joueur local attrape, ou si c'est sa balle qu'on attrape (pas pour un ramassage de bot).
                    if (steal && (IsLocal(cur.OwnerId) || IsLocal(prev.OwnerId)) && HurtboxOf(cur.OwnerId) is { } catcher)
                    {
                        // Attrape parfaite (notée juste avant par l'hôte) : un seul mot, « PRISE PARFAITE ! », jamais « PRISE ! » + un second message.
                        bool perfect = perfectAt.TryGetValue(cur.OwnerId, out var at) && Time.GetTicksMsec() - at < 500;
                        if (perfect) perfectAt.Remove(cur.OwnerId);
                        lastSteal = perfect ? (-1, 0) : (cur.OwnerId, Time.GetTicksMsec());
                        Sticker(perfect ? "PRISE PARFAITE !" : "PRISE !", catcher, Palette.Team(catcher.Team), SpeechBubble.Size.Normal);
                    }
                }
                else if (prev.Held && !cur.Held)
                {
                    // Passe : souffle plus aigu et plus doux qu'un tir (on entend tout de suite si c'est une passe ou une attaque).
                    if (ball.Kind != Ball.KindBomb && cur.Speed > 1.5f) Sfx.Play(this, SfxKind.Throw, pos, cur.Pass ? 1.3f : 0.8f + 0.35f * prev.Charge, cur.Pass ? -6f : 0f);
                    // Vrai lancer seulement (une balle lâchée au KO ou au décompte ne vibre pas).
                    if (IsLocal(prev.OwnerId) && cur.Speed > 1.5f) Haptic?.Invoke(prev.Hand, cur.Pass ? 0.25f : 0.35f + 0.15f * prev.Charge, 0.05f);
                }
                else if (cur.Held && cur.Charge > prev.Charge)
                {
                    Sfx.Play(this, SfxKind.Tier, pos, 0.8f + 0.3f * cur.Charge);
                    if (IsLocal(cur.OwnerId)) Haptic?.Invoke(cur.Hand, 0.25f + 0.2f * cur.Charge, 0.05f);   // un cran plus fort à chaque palier
                }
                if (!prev.Exploded && cur.Exploded) BombSound(pos);   // client : la marionnette passe à l'état explosé
                NearMiss(ball, cur);
                if (!prev.Pass && cur.Pass && IsLocal(cur.OwnerId)) Sticker("PASSE !", Local, Palette.Team(Local.Team), SpeechBubble.Size.Small);
            }
            balls[ball] = cur;
        }
        foreach (var dead in new List<Ball>(balls.Keys)) if (!seen.Contains(dead)) { balls.Remove(dead); nearMiss.Remove(dead); }

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
        SetGray(IsInstanceValid(Local) && Local.Eliminated);
        if (ImpactTest) RunImpactTest(cam, delta);
    }

    // --impact-test : rejoue toutes les 0,8 s, après le « GO ! », sur l'adversaire le plus central à l'écran (s'il y en a un) : le joueur
    // local le touche, le local met un adversaire KO, le local est touché, le local attrape (« PRISE ! »). Mots, étiquettes et réaction du modèle
    // (Avatar.React) passent par les mêmes chemins qu'en match. Sans adversaire visible : effets seuls, droit devant la caméra.
    static readonly bool ImpactTest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--impact-test") >= 0;
    double testClock = 4.0;
    int testStep;

    Hurtbox? CentralEnemy(Camera3D cam, Hurtbox? not = null)
    {
        Hurtbox? best = null;
        float bestD = float.MaxValue;
        var center = cam.GetViewport().GetVisibleRect().Size * 0.5f;
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
        {
            if (n is not Hurtbox h || h == Local || h == not || h.Eliminated || (IsInstanceValid(Local) && h.Team == Local.Team)) continue;
            if (!ScreenSize.OnScreen(cam, h.GlobalPosition, -0.1f)) continue;
            float d = cam.UnprojectPosition(h.GlobalPosition).DistanceTo(center);
            if (d < bestD) { best = h; bestD = d; }
        }
        return best;
    }

    void RunImpactTest(Camera3D? cam, double delta)
    {
        testClock -= delta;
        if (testClock > 0 || cam == null) return;
        testClock = 0.8;
        int step = testStep++ % 4;
        var enemy = CentralEnemy(cam);
        if (enemy == null && step == 2)   // « le local est touché » : le tireur peut être hors champ (l'adversaire visible est à terre)
            foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
                if (n is Hurtbox h && h != Local && !h.Eliminated && IsInstanceValid(Local) && h.Team != Local.Team) { enemy = h; break; }
        GD.Print($"[Impact] test {step} sur {(enemy != null ? enemy.PlayerId : -1)}, image {Engine.GetFramesDrawn()}");
        if (enemy != null && IsInstanceValid(Local))
        {
            ulong now = Time.GetTicksMsec();
            switch (step)
            {
                case 0:   // le local touche l'adversaire
                    lastShooter[enemy.PlayerId] = (Local.PlayerId, now);
                    OnTouched(enemy, cam, false);
                    Avatar.React(Local.PlayerId, enemy.PlayerId, false);
                    break;
                case 2:   // le local est touché
                    lastShooter[Local.PlayerId] = (enemy.PlayerId, now);
                    OnTouched(Local, cam, false);
                    Avatar.React(enemy.PlayerId, Local.PlayerId, false);
                    break;
                case 1:   // le local met un adversaire KO (un autre que le précédent s'il y en a un)
                    var ko = CentralEnemy(cam, enemy) ?? enemy;
                    lastShooter[ko.PlayerId] = (Local.PlayerId, now);
                    Avatar.React(Local.PlayerId, ko.PlayerId, true);
                    // Comme un vrai KO : la cible passe à terre (bot masqué, sa copie joue la chute, étiquette cachée), ce qui déclenche
                    // l'impact et le mot par le chemin normal ; elle revient 1,6 s plus tard (le test ne touche ni aux vies ni au score).
                    ko.Eliminated = true;
                    GetTree().CreateTimer(1.6, false).Timeout += () => { if (IsInstanceValid(ko) && Lives(ko.PlayerId) > 0) ko.Eliminated = false; };
                    break;
                default:  // le local attrape
                    Sticker("PRISE !", Local, Palette.Team(Local.Team), SpeechBubble.Size.Normal);
                    break;
            }
            return;
        }
        var fwd = -cam.GlobalTransform.Basis.Z;
        fwd = new Vector3(fwd.X, 0, fwd.Z).Normalized();
        var pos = cam.GlobalPosition + fwd * 8f;
        pos.Y = (IsInstanceValid(Local) ? Local.GlobalPosition.Y : cam.GlobalPosition.Y - 1f) + 0.25f;
        if (step == 1) ImpactFx.Ko(this, pos, Palette.Team(1));
        else ImpactFx.Hit(this, pos, Palette.Team(step % 2));
    }

    ulong lastBombMs;
    /// <summary>Explosion de la bombe (elle était muette) : chez l'hôte par l'événement, chez le client par l'état de la marionnette.</summary>
    void OnBombExploded(Vector3 pos, int a, int b, int c) => BombSound(pos);
    void BombSound(Vector3 pos)
    {
        if (Time.GetTicksMsec() - lastBombMs < 400) return;
        lastBombMs = Time.GetTicksMsec();
        Sfx.Play(this, SfxKind.Bomb, pos);
        if (IsInstanceValid(Local) && pos.DistanceTo(Local.GlobalPosition) < 6f) Haptic?.Invoke(-1, 0.7f, 0.2f);
    }

    /// <summary>Le joueur local prend une balle : vibration de la main qui la tient ; attrape d'un tir adverse : double coup « clac-clac »
    /// et liseré doré en VR. Le son et le mot sont posés par l'appelant.</summary>
    void OnLocalCatch(bool steal, int hand)
    {
        if (Haptic == null) return;
        if (!steal) { Haptic(hand, 0.45f, 0.05f); return; }
        Haptic(hand, 1f, 0.09f);
        var h = Haptic;
        GetTree().CreateTimer(0.12, false).Timeout += () => h(hand, 0.55f, 0.05f);
        bool perfect = perfectAt.ContainsKey(Local.PlayerId);
        VrVignette.Flash(perfect ? UiKit.Gold : Palette.Team(Local.Team), perfect ? 0.55f : 0.3f, 0.35f);
    }

    /// <summary>
    /// Balle adverse lancée qui passe à moins de 0,9 m de la tête du joueur local sans le toucher : souffle qui file (« frôlé »), petite
    /// vibration en VR. Esquiver est une action : elle doit s'entendre autant qu'une attrape.
    /// </summary>
    void NearMiss(Ball ball, BallState cur)
    {
        if (!IsInstanceValid(Local) || Local.Eliminated || cur.Held || cur.Pass || cur.Owner < 0 || cur.Owner == Local.Team || cur.Speed < Ball.HitMinSpeed)
        {
            nearMiss.Remove(ball);
            return;
        }
        var head = Local.Head?.GlobalPosition ?? HeadOf(Local);
        float d = cur.Pos.DistanceTo(head);
        int lives = Lives(Local.PlayerId);
        if (!nearMiss.TryGetValue(ball, out var n)) { if (d < 3f) nearMiss[ball] = (d, lives); return; }
        if (d < n.Min) { nearMiss[ball] = (d, n.Lives); return; }
        if (n.Min < 0.9f && d > n.Min + 0.3f)
        {
            nearMiss.Remove(ball);
            if (lives < n.Lives) return;   // elle a touché
            Sfx.Play(this, SfxKind.Dash, cur.Pos, 1.35f, -2f);
            if (Haptic != null)
            {
                // Côté de la balle par rapport à la tête : la manette de ce côté vibre.
                var right = Local.Head?.GlobalTransform.Basis.X ?? Vector3.Right;
                Haptic((cur.Pos - head).Dot(right) >= 0 ? 1 : 0, 0.3f, 0.04f);
            }
        }
    }

    /// <summary>Arrêt sur image : au clavier-souris tel quel ; en VR plus court et moins appuyé (un monde figé dans le casque ressemble à une saccade).</summary>
    void Stop(float seconds, float scale = 0.12f)
    {
        if (Vr) Juice.HitStop(this, seconds * 0.5f, Mathf.Max(scale, 0.35f));
        else Juice.HitStop(this, seconds, scale);
    }

    /// <summary>Touche ou KO sur un joueur, vu par tout le monde : impact, autocollant, son ; au PC, secousse et arrêt sur image dosés.</summary>
    void OnTouched(Hurtbox h, Camera3D? cam, bool ko)
    {
        var (hit, thrower) = ImpactPoint(h);
        int shooter = lastShooter.TryGetValue(h.PlayerId, out var s) && Time.GetTicksMsec() - s.at < 1500 ? s.id : thrower;
        var shooterColor = Palette.Team(h.Team == 0 ? 1 : 0);   // pas de dégâts entre alliés : le tireur est dans l'autre équipe
        bool localTarget = h == Local;
        bool localShooter = !localTarget && IsLocal(shooter);
        bool word = localTarget || localShooter;   // mots d'impact : seulement ce qui concerne le joueur local
        float dist = cam != null ? cam.GlobalPosition.DistanceTo(hit) : 999f;
        bool near = !localTarget && dist < 16f && ScreenSize.OnScreen(cam, hit, 0f);
        float closeness = Mathf.Clamp(1f - dist / 16f, 0f, 1f);
        GD.Print($"[Impact] {(ko ? "KO" : "touche")} sur {h.PlayerId} (équipe {h.Team}), tireur {shooter}, à l'écran et proche : {near}, image {Engine.GetFramesDrawn()}");

        if (ko)
        {
            ImpactFx.Ko(this, hit, shooterColor);
            // Au clavier-souris, pas d'autocollant « KO ! » : le KO d'un adversaire a déjà le bandeau et le fil des KO,
            // le mien le grand titre central et le bandeau « KO ! par X ». En VR (sans HUD), l'autocollant reste.
            if (word && !GrayWhenKo) Sticker("KO !", h, shooterColor, SpeechBubble.Size.Big);
            if (localTarget || localShooter || near) Stop(0.12f, 0.08f);
            if (Vr && localTarget)
            {
                Haptic?.Invoke(-1, 1f, 0.35f);
                VrVignette.Flash(new Color(0.9f, 0.08f, 0.1f), 0.85f, 0.9f);
                Sfx.Play(this, SfxKind.HurtSelf);
            }
            if (Vr && localShooter)
            {
                Haptic?.Invoke(-1, 0.8f, 0.12f);
                Sfx.Play(this, SfxKind.KoConfirm);   // au clavier-souris, DesktopHud le joue déjà
            }
            if (GrayWhenKo) CameraFx.Shake(localTarget ? 0.75f : near ? 0.1f + 0.15f * closeness : 0f);
            Sfx.Play(this, SfxKind.Ko, hit);
            return;
        }
        ImpactFx.Hit(this, hit, shooterColor);
        if (word) Sticker(HitWords[wordIndex % HitWords.Length], h, shooterColor, SpeechBubble.Size.Normal);
        wordIndex++;
        Sfx.Play(this, SfxKind.Hit, hit, 0.95f + 0.1f * (wordIndex % 3));
        if (localTarget || localShooter) Stop(localTarget ? 0.09f : 0.06f);
        else if (near && !Vr) Juice.HitStop(this, 0.04f, 0.3f);   // touche entre d'autres joueurs sous nos yeux : à peine marquée
        if (Vr && localTarget)
        {
            // VR : pas de secousse. Les deux manettes encaissent, liseré rouge sur les bords, son sourd.
            Haptic?.Invoke(-1, 1f, 0.14f);
            VrVignette.Flash(new Color(0.9f, 0.08f, 0.1f), 0.6f, 0.45f);
            Sfx.Play(this, SfxKind.HurtSelf);
        }
        if (Vr && localShooter)
        {
            Haptic?.Invoke(-1, 0.5f, 0.07f);
            Sfx.Play(this, SfxKind.Click, null, 1.15f);   // confirmation de touche (« clic » du HUD clavier-souris)
        }
        if (localTarget && GrayWhenKo)
        {
            // Joueur clavier-souris touché : la vue encaisse (secousse, coup de côté), son plus sourd que pour les autres.
            CameraFx.Shake(0.5f);
            CameraFx.Kick(new Vector2((wordIndex % 2 == 0 ? 1f : -1f) * 2.5f, 1.2f));
            Sfx.Play(this, SfxKind.HurtSelf);
        }
        else if (GrayWhenKo && near) CameraFx.Shake(0.05f + 0.12f * closeness);   // touche proche, à l'écran : petite secousse
    }

    void Sticker(string text, Hurtbox h, Color rim, SpeechBubble.Size size)
    {
        // Joueur local dont la tête n'est pas à l'écran (vue subjective, caméra collée) : mot posé devant la caméra, sous le réticule,
        // plutôt qu'accroché à un point hors champ que le recadrage pousserait dans un coin.
        var cam = GetViewport().GetCamera3D();
        if (h == Local && cam != null && cam is not XRCamera3D && (!ScreenSize.OnScreen(cam, HeadOf(h), -0.05f) || cam.GlobalPosition.DistanceTo(HeadOf(h)) < 1.5f))
        {
            var b = cam.GlobalTransform.Basis;
            SpeechBubble.Spawn(this, text, cam.GlobalPosition - b.Z * 5f - b.Y * 1.3f, rim, size, null);
            return;
        }
        SpeechBubble.Spawn(this, text, HeadOf(h), rim, size, h);
    }
}
