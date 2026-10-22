using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Écran de fin de partie du joueur clavier-souris, en deux temps comme Knockout City : d'abord le podium (<see cref="EndPodium"/> :
/// l'équipe gagnante en 3D, le MVP devant, ~3 s), puis un glissement vers le tableau : VICTOIRE ou DÉFAITE (point de vue du
/// joueur local), manches, MVP mis en avant, tableau par joueur (hôte seulement : le client ne reçoit pas les statistiques),
/// REJOUER et MENU PRINCIPAL. Pendant le podium, Entrée, Échap, Espace ou un clic le passent.
/// S'ouvre 1,8 s après la fin (le temps du bandeau), se ferme seul quand une nouvelle partie commence.
/// Avec relance automatique (headless, --pc-autoplay) il affiche le décompte au lieu d'attendre un choix.
/// </summary>
public partial class EndScreen : CanvasLayer
{
    /// <summary>Vrai pendant l'affichage : le HUD s'efface, le joueur ne reprend pas la souris au clic.</summary>
    public static bool IsOpen { get; private set; }

    public MatchView View { get; set; } = null!;
    public Action? QuitToMenu { get; set; }
    public bool Autoplay { get; set; }
    /// <summary>Partie lancée depuis une salle en ligne : à la fin, tout le monde y retourne (compte à rebours au lieu de REJOUER).</summary>
    public bool BackToRoom { get; set; }
    public const double BackToRoomSeconds = 7.0;
    /// <summary>Délai du retour à la salle (format VR : durée de fin de l'opérateur EVA).</summary>
    public double BackToRoomAfter { get; set; } = BackToRoomSeconds;

    const double OpenDelay = 1.8;
    const double AutoRestartSeconds = 8.0;   // MatchController.MatchEndSeconds
    Control? root;
    double overFor = -1;
    Label? autoLabel;
    Sunburst? burst;
    Star? star;
    EndPodium? podium;     // première phase, libérée à la fin du glissement
    Control? page;         // tableau : null tant que le podium est à l'écran
    /// <summary>Le tableau est à l'écran (le bandeau d'XP attend ce moment).</summary>
    public bool ShowingStats => page != null;
    double podiumLeft;
    const double PodiumSeconds = 3.2, SlideSeconds = 0.5;

    public override void _Ready()
    {
        Layer = 55;   // sous le menu pause (60)
        IsOpen = false;
    }

    public override void _ExitTree() => IsOpen = false;

    public override void _Process(double delta)
    {
        bool over = View.State.Phase == MatchView.Phase.MatchOver;
        if (!over)
        {
            overFor = -1;
            if (root != null) Close(true);   // l'hôte a relancé : retour en jeu
            return;
        }
        overFor = overFor < 0 ? 0 : overFor + delta;
        // Animations dessinées pilotées d'ici : le _Process des classes imbriquées n'est pas appelé par Godot.
        if (burst != null) { burst.Angle += (float)delta * 0.12f; burst.QueueRedraw(); }
        if (star != null) { star.T += (float)delta; star.QueueRedraw(); }
        if (root == null && overFor >= OpenDelay) Open();
        if (page == null && podium != null && (podiumLeft -= delta) <= 0) ShowStats();
        // Le menu pause refermé reprend la souris : sur l'écran de fin, il faut la rendre pour cliquer.
        if (root != null && !Autoplay && !PauseMenu.IsOpen && Input.MouseMode == Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Visible;
        if (autoLabel != null) autoLabel.Text = BackToRoom
            ? $"Retour à la salle dans {Mathf.Max(0, Mathf.CeilToInt((float)(BackToRoomAfter - overFor)))} s"
            : $"Nouvelle partie dans {Mathf.Max(0, Mathf.CeilToInt((float)(AutoRestartSeconds - overFor)))} s";
    }

    /// <summary>Statistiques affichées : celles de l'hôte, ou celles du test de capture (voir <see cref="TestStats"/>).</summary>
    IReadOnlyDictionary<int, MatchController.PlayerStats>? stats;
    int winner = -1;

    void Open()
    {
        IsOpen = true;
        if (!Autoplay) Input.MouseMode = Input.MouseModeEnum.Visible;
        var s = View.State;
        winner = s.Winner;
        stats = TestStats(View, View.Stats, s.Winner);
        bool win = s.Winner == View.LocalTeam;
        Audio.Music.Stinger(win ? SfxKind.Victory : SfxKind.Defeat);   // jingle + foule, la musique de match s'efface

        root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        TestKeys();

        // Podium d'abord, s'il y a une équipe gagnante à montrer (toujours le cas en fin de partie, client compris).
        bool anyWinner = false;
        foreach (var p in View.Players) anyWinner |= p.Team == winner;
        if (winner >= 0 && anyWinner)
        {
            podium = new EndPodium { Name = "Podium", View = View, Winner = winner, Stats = stats, MvpId = MvpId() };
            podium.TreeExited += () => GD.Print("[Fin] Podium libéré");
            root.AddChild(podium);
            podiumLeft = PodiumSeconds;
            GD.Print($"[Fin] Podium : équipe {MatchController.TeamName(winner)}, MVP {(podium.MvpId >= 0 ? View.NameOf(podium.MvpId) : "inconnu")}");
            return;
        }
        ShowStats();
    }

    /// <summary>
    /// Deuxième phase : le tableau. Après le podium, il arrive de la droite pendant que le podium sort par la gauche,
    /// puis le podium (sa scène 3D) est libéré ; sa cascade d'apparition démarre au milieu du glissement.
    /// </summary>
    void ShowStats()
    {
        if (root == null || page != null) return;
        page = new Control { Name = "Tableau", MouseFilter = Control.MouseFilterEnum.Ignore };
        page.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(page);
        var old = podium;
        BuildStats(page, old != null ? (float)SlideSeconds * 0.6f : 0f, old == null);
        if (old == null) return;
        podium = null;
        float w = root.Size.X;
        page.Position = new Vector2(w, 0);
        var tw = root.CreateTween().SetParallel();
        tw.TweenProperty(page, "position:x", 0f, SlideSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tw.TweenProperty(old, "position:x", -w, SlideSeconds).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.InOut);
        tw.Chain().TweenCallback(Callable.From(old.QueueFree));
        Sfx.Play(this, SfxKind.Dash);
    }

    void BuildStats(Control host, float d0, bool mvpSound)
    {
        var s = View.State;
        bool win = winner == View.LocalTeam;
        var dim = UiKit.Dim(0.78f);
        host.AddChild(dim);
        dim.Modulate = new Color(1, 1, 1, 0);
        dim.CreateTween().TweenProperty(dim, "modulate:a", 1.0, 0.3);

        // Victoire : rayons dorés festifs. Défaite : pas de rayons, juste un halo gris-bleu éteint derrière le titre.
        burst = new Sunburst { Color = win ? UiKit.Yellow : new Color("8a96ad"), Rays = win, MouseFilter = Control.MouseFilterEnum.Ignore };
        burst.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        host.AddChild(burst);

        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        col.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        // Écran bas (interface agrandie : 1080 / 1,3 ≈ 830 px utiles) : titre plus petit et marges serrées, les boutons restent visibles.
        bool compact = host.GetViewportRect().Size.Y < 960f;
        col.OffsetTop = compact ? 12 : 40; col.OffsetBottom = compact ? -12 : -40;
        col.AddThemeConstantOverride("separation", compact ? 8 : 14);
        host.AddChild(col);

        var title = UiKit.Title(win ? "VICTOIRE !" : "DÉFAITE", compact ? 120 : 170, HorizontalAlignment.Center, win ? UiKit.Yellow : new Color("9fb4d8"));
        title.CustomMinimumSize = new Vector2(0, compact ? 136 : 190);
        col.AddChild(title);

        // Score des manches, aux couleurs des équipes, avec son unité sous les chiffres.
        var score = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        score.AddThemeConstantOverride("separation", 26);
        Label Team(string name, Color c)
        {
            var l = UiKit.Text(name, 40, c, 10);
            if (UiKit.Italic is Font it) l.AddThemeFontOverride("font", it);
            l.CustomMinimumSize = new Vector2(0, 80);   // hauteur des chiffres : le nom reste aligné sur eux, pas sur l'unité
            l.VerticalAlignment = VerticalAlignment.Center;
            l.SizeFlagsVertical = Control.SizeFlags.ShrinkBegin;
            return l;
        }
        score.AddChild(Team("ÉQUIPE ORANGE", UiKit.Orange));
        var mid = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        mid.AddThemeConstantOverride("separation", -10);
        var digits = UiKit.Title($"{s.Rounds0}  -  {s.Rounds1}", 64, HorizontalAlignment.Center, Colors.White);
        digits.CustomMinimumSize = new Vector2(0, 80);
        digits.VerticalAlignment = VerticalAlignment.Center;
        mid.AddChild(digits);
        mid.AddChild(UiKit.Text("MANCHES", 22, UiKit.Cream, 6, HorizontalAlignment.Center));
        score.AddChild(mid);
        score.AddChild(Team("ÉQUIPE BLEUE", UiKit.Sky));
        col.AddChild(score);
        col.AddChild(UiKit.Spacer(6));

        var middle = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        middle.AddThemeConstantOverride("separation", 34);
        col.AddChild(middle);
        Control? mvpCard = null;
        if (View.HasStats)
        {
            mvpCard = MvpCard();
            if (mvpCard != null) middle.AddChild(mvpCard);
            middle.AddChild(StatsTable());
        }
        else middle.AddChild(UiKit.Text("Statistiques par joueur tenues par l'hôte", 28, new Color(1, 1, 1, 0.7f), 6, HorizontalAlignment.Center));

        col.AddChild(UiKit.Spacer(12));
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 30);
        col.AddChild(buttons);
        Button? first = null;
        if (View.AutoRestart || BackToRoom)
        {
            autoLabel = UiKit.Text("", 34, UiKit.Sky, 8, HorizontalAlignment.Center);
            autoLabel.CustomMinimumSize = new Vector2(440, 70);
            autoLabel.VerticalAlignment = VerticalAlignment.Center;
            buttons.AddChild(autoLabel);
        }
        else if (View.CanRestart) buttons.AddChild(first = UiKit.KeyButton("REJOUER", "Entrée", UiKit.Orange, Replay, 34, new Vector2(420, 76)));
        else
        {
            var wait = UiKit.Text("En attente de l'hôte…", 32, UiKit.Sky, 8, HorizontalAlignment.Center);
            wait.CustomMinimumSize = new Vector2(400, 76);
            wait.VerticalAlignment = VerticalAlignment.Center;
            buttons.AddChild(wait);
        }
        var menu = UiKit.KeyButton("MENU PRINCIPAL", InputSetup.KeyName("pause"), UiKit.Grey, ToMenu, 30, new Vector2(460, 76));
        buttons.AddChild(menu);
        replayKey = first != null;
        (first ?? menu).CallDeferred(Control.MethodName.GrabFocus);

        // Entrée : le titre tape, le reste glisse en cascade (après d0 : le temps que le tableau ait glissé à sa place).
        title.Resized += () => title.PivotOffset = title.Size / 2;
        title.Scale = Vector2.One * 2.2f;
        title.Modulate = new Color(1, 1, 1, 0);
        var tw = title.CreateTween().SetParallel();
        tw.TweenProperty(title, "scale", Vector2.One, 0.45).SetDelay(d0).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(title, "modulate:a", 1.0, 0.2).SetDelay(d0);
        Appear(score, d0 + 0.25f);
        int i = 0;
        foreach (var c in middle.GetChildren()) if (c is Control ctl) Appear(ctl, d0 + 0.35f + 0.15f * i++);
        Appear(buttons, d0 + 0.7f);
        if (mvpCard != null)
        {
            var t2 = mvpCard.CreateTween();
            t2.TweenInterval(d0 + 0.9);
            // Le son de MVP a déjà retenti sur le podium : ici, le coup de tampon seul.
            t2.TweenCallback(Callable.From(() => { UiKit.Punch(mvpCard, 1.15f, 0.3); if (mvpSound) Sfx.Play(this, SfxKind.Perfect); }));
        }
    }

    /// <summary>
    /// Apparition dans un conteneur (qui place lui-même ses enfants : on ne peut pas glisser en position) :
    /// fondu et léger rebond d'échelle autour du centre, avec un retard pour la cascade.
    /// </summary>
    static void Appear(Control c, float delay)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        c.Resized += () => c.PivotOffset = c.Size / 2;
        var tw = c.CreateTween();
        tw.TweenInterval(delay);
        tw.TweenCallback(Callable.From(() => { c.PivotOffset = c.Size / 2; c.Scale = Vector2.One * 0.8f; }));
        tw.SetParallel();
        tw.TweenProperty(c, "modulate:a", 1.0, 0.2);
        tw.TweenProperty(c, "scale", Vector2.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    void Replay()
    {
        GD.Print("[Fin] Rejouer");
        Close(true);
        View.Restart();
    }

    void ToMenu()
    {
        GD.Print("[Fin] Retour au menu principal");
        Close(false);
        QuitToMenu?.Invoke();
    }

    bool replayKey;   // Entrée relance (seulement si l'on peut relancer : hors ligne ou hôte)

    /// <summary>
    /// Raccourcis affichés sur les boutons : Entrée = rejouer, Échap (touche de pause) = menu principal.
    /// Pendant le podium, ces touches, Espace ou un clic le passent seulement (pas de relance ni de départ par mégarde).
    /// Passe avant le menu pause, qui sinon s'ouvrirait sur Échap ; rien tant que la pause est ouverte par-dessus.
    /// </summary>
    public override void _Input(InputEvent e)
    {
        if (root == null || PauseMenu.IsOpen || Autoplay) return;
        if (page == null)
        {
            bool skip = e is InputEventKey { Pressed: true, Echo: false } sk && sk.Keycode is Key.Enter or Key.KpEnter or Key.Space
                || e.IsActionPressed("pause")
                || e is InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left or MouseButton.Right };
            if (!skip) return;
            GetViewport().SetInputAsHandled();
            GD.Print("[Fin] Podium passé");
            ShowStats();
            return;
        }
        if (e is InputEventKey { Pressed: true, Echo: false } k && k.Keycode is Key.Enter or Key.KpEnter)
        {
            if (!replayKey) return;
            GetViewport().SetInputAsHandled();
            Replay();
        }
        else if (e.IsActionPressed("pause"))
        {
            GetViewport().SetInputAsHandled();
            Sfx.Play(this, SfxKind.UiBack);
            ToMenu();
        }
    }

    /// <summary>
    /// Test des raccourcis (--end-screen-keys=entree | echap | clic, avec --end-screen-test) : 1,5 s après l'ouverture, un
    /// premier appui (la touche, ou un clic gauche pour « clic ») passe le podium ; 1 s plus tard, la touche (Entrée pour « clic »)
    /// est injectée comme un vrai appui. Le journal doit montrer « [Fin] Podium passé » puis « [Fin] Rejouer » ou
    /// « [Fin] Retour au menu principal », et « [Fin] Podium libéré ».
    /// </summary>
    void TestKeys()
    {
        foreach (var a in Tools.DevFlags.Args)
        {
            if (!a.StartsWith("--end-screen-keys=")) continue;
            var key = a.EndsWith("echap") ? Key.Escape : Key.Enter;
            bool click = a.EndsWith("clic");
            void Press(Key k)
            {
                GD.Print($"[Fin] Test : appui sur {(k == Key.Escape ? "Échap" : "Entrée")}");
                Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = true });
                Input.ParseInputEvent(new InputEventKey { Keycode = k, PhysicalKeycode = k, Pressed = false });
            }
            GetTree().CreateTimer(1.5).Timeout += () =>
            {
                if (!click) { Press(key); return; }
                GD.Print("[Fin] Test : clic");
                var at = GetViewport().GetVisibleRect().Size / 2;
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true, Position = at, GlobalPosition = at });
                Input.ParseInputEvent(new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false, Position = at, GlobalPosition = at });
            };
            GetTree().CreateTimer(2.5).Timeout += () => Press(key);
        }
    }

    void Close(bool capture)
    {
        IsOpen = false;
        root?.QueueFree();   // emporte le podium (et sa scène 3D) s'il est encore là
        root = null;
        podium = null;
        page = null;
        autoLabel = null;
        burst = null;
        star = null;
        if (capture && !Autoplay) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>
    /// MVP pris dans l'équipe gagnante (comme Knockout City, qui sacre le meilleur des vainqueurs) : note du MVP, puis KO à égalité.
    /// Partie sans vainqueur : le meilleur des deux équipes.
    /// </summary>
    int MvpId()
    {
        int best = -1, bestScore = -1, bestKos = -1;
        foreach (var p in View.Players)
        {
            if (winner >= 0 && p.Team != winner) continue;
            if (stats == null || !stats.TryGetValue(p.Id, out var st)) continue;
            if (st.Score > bestScore || (st.Score == bestScore && st.Kos > bestKos)) { best = p.Id; bestScore = st.Score; bestKos = st.Kos; }
        }
        return bestScore > 0 ? best : -1;
    }

    /// <summary>
    /// Test de capture (--end-screen-test) : la partie est déclarée finie d'office avant que personne n'ait marqué, d'où un
    /// tableau à zéro. Pour juger l'écran, on y montre une partie crédible (3-2 : les vainqueurs un peu devant). Seulement
    /// dans ce test et seulement si personne n'a marqué : une vraie partie garde ses chiffres.
    /// </summary>
    internal static IReadOnlyDictionary<int, MatchController.PlayerStats>? TestStats(MatchView view, IReadOnlyDictionary<int, MatchController.PlayerStats>? real, int champion)
    {
        if (real == null || Array.FindIndex(Tools.DevFlags.Args, a => a.StartsWith("--end-screen-test")) < 0) return real;
        foreach (var st in real.Values) if (st.Kos > 0) return real;
        // Par équipe, du meilleur au moins bon : KO, touches, balles attrapées, arrêts parfaits, lancers.
        int[][] winners = { new[] { 5, 8, 4, 2, 17 }, new[] { 3, 6, 2, 0, 14 }, new[] { 2, 4, 1, 1, 11 } };
        int[][] losers = { new[] { 4, 7, 3, 1, 16 }, new[] { 2, 5, 1, 0, 12 }, new[] { 1, 3, 2, 0, 10 } };
        var fake = new Dictionary<int, MatchController.PlayerStats>();
        int[] rank = new int[2];
        foreach (var p in view.Players)
        {
            if (p.Team is < 0 or > 1) continue;
            var table = p.Team == champion ? winners : losers;
            var v = table[Mathf.Min(rank[p.Team]++, table.Length - 1)];
            fake[p.Id] = new MatchController.PlayerStats { Kos = v[0], Hits = v[1], Catches = v[2], PerfectCatches = v[3], Throws = v[4] };
        }
        return fake;
    }

    Control? MvpCard()
    {
        int id = MvpId();
        if (id < 0 || stats == null) return null;
        var st = stats[id];
        int team = View.TeamOf(id);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(360, 0) };
        // Carte penchée à la couleur de l'équipe, liseré jaune, ombre et bande violette : la fenêtre du menu, version trophée.
        var box = UiKit.PanelBox(18, 26);
        box.Top = TeamRow(team, true); box.Bottom = TeamRow(team, false).Darkened(0.2f); box.BorderColor = UiKit.Yellow;
        panel.Rotation = -0.03f;
        box.ContentMarginTop = 18; box.ContentMarginBottom = 22;
        panel.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 4);
        panel.AddChild(col);
        star = new Star { CustomMinimumSize = new Vector2(0, 90), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(star);
        col.AddChild(UiKit.Title("MVP", 64));
        col.AddChild(UiKit.Text(View.NameOf(id), 46, Colors.White, 10, HorizontalAlignment.Center));
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(UiKit.Text($"{st.Kos} KO   ·   {Plural(st.Hits, "touche", "touches")}", 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text(Plural(st.Catches, "balle attrapée", "balles attrapées"), 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text(Plural(st.PerfectCatches, "arrêt parfait", "arrêts parfaits"), 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        panel.Resized += () => panel.PivotOffset = panel.Size / 2;
        return panel;
    }

    static string Plural(int n, string one, string many) => $"{n} {(n > 1 ? many : one)}";

    Control StatsTable()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var box = UiKit.PanelBox(16, 26);   // opaque : la partie ne transparaît plus
        panel.AddThemeStyleboxOverride("panel", box);
        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);
        rows.AddChild(Row(null, "JOUEUR", new[] { "KO", "TOUCHES", "BALLES\nATTRAPÉES", "ARRÊTS\nPARFAITS", "LANCERS" }, false));
        int mvp = MvpId();
        // Mon équipe d'abord, puis l'adverse ; dans chaque équipe, du meilleur score MVP au moins bon (KO à égalité).
        var ordered = new List<MatchView.PlayerInfo>();
        foreach (var p in View.Players) if (stats!.ContainsKey(p.Id)) ordered.Add(p);
        int Score(int id) => stats![id].Score;
        int Kos(int id) => stats![id].Kos;
        ordered.Sort((a, b) =>
        {
            bool ma = a.Team == View.LocalTeam, mb = b.Team == View.LocalTeam;
            if (ma != mb) return ma ? -1 : 1;
            if (a.Team != b.Team) return a.Team.CompareTo(b.Team);
            int c = Score(b.Id).CompareTo(Score(a.Id));
            return c != 0 ? c : Kos(b.Id).CompareTo(Kos(a.Id));
        });
        int i = 0, lastTeam = -1;
        foreach (var p in ordered)
        {
            var st = stats![p.Id];
            if (lastTeam >= 0 && p.Team != lastTeam) rows.AddChild(UiKit.Spacer(8));   // les deux équipes, bien séparées
            lastTeam = p.Team;
            var row = Row(p, View.NameOf(p.Id) + (p.Id == mvp ? "  ★" : ""), new[] { st.Kos.ToString(), st.Hits.ToString(), st.Catches.ToString(), st.PerfectCatches.ToString(), st.Throws.ToString() }, p.Id == mvp);
            rows.AddChild(row);
            UiKit.SlideIn(row, 0.25f + 0.05f * i++, 60f);   // SlideIn attend deux fois le retard : 0,5 s puis 0,1 s par ligne
        }
        return panel;
    }

    Control Row(MatchView.PlayerInfo? p, string name, string[] cells, bool mvp)
    {
        bool header = p == null;
        bool local = p != null && p.Id == View.LocalId;
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        // Couleurs pleines et vives par équipe (orange / bleu) ; ma ligne plus claire, cerclée de jaune avec un halo.
        var bg = header ? new Color(0, 0, 0, 0) : TeamRow(p!.Team, local);
        // Lignes penchées comme la barre du menu ; la mienne cerclée de jaune.
        var box = new KcBox { Top = bg, Bottom = header ? bg : bg.Darkened(0.18f), Slant = 8, BorderWidth = local ? 4 : 0, BorderColor = UiKit.Yellow, ShadowOffset = new Vector2(5, 5), ShadowColor = new Color("0a0f22", header ? 0f : 0.5f) };
        box.ContentMarginTop = 3; box.ContentMarginBottom = 3; box.ContentMarginLeft = 20; box.ContentMarginRight = 20;
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 6);
        row.AddChild(line);
        int size = header ? 20 : 30;
        var color = header ? UiKit.Sky : Colors.White;
        var n = UiKit.Text(name, size, color, local ? 9 : 6);   // blanc : le jaune ne se lit pas sur l'orange
        n.CustomMinimumSize = new Vector2(220, 0);
        n.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(n);
        foreach (var c in cells)
        {
            var l = UiKit.Text(c, size, color, 6, HorizontalAlignment.Center);
            l.CustomMinimumSize = new Vector2(136, 0);
            l.VerticalAlignment = VerticalAlignment.Center;
            if (header) l.AddThemeConstantOverride("line_spacing", -4);   // en-têtes sur deux lignes, serrées
            line.AddChild(l);
        }
        return row;
    }

    /// <summary>Fond de ligne du tableau (et de la carte MVP) : orange ou bleu vifs et opaques, plus clairs pour le joueur local.</summary>
    static Color TeamRow(int team, bool bright) => team == 0
        ? (bright ? new Color("ff7a1f") : new Color("d9520c"))
        : (bright ? new Color("2f8fff") : new Color("1f63c9"));

    /// <summary>Rayons lumineux qui tournent lentement derrière le titre, avec un halo au centre (animés par l'écran).</summary>
    sealed partial class Sunburst : Control
    {
        public Color Color = UiKit.Yellow;
        public bool Rays = true;
        public float Angle;
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 260);
            float r = Size.Length();
            const int n = 18;
            for (int i = 0; i < (Rays ? n : 0); i++)
            {
                float a = Angle + Mathf.Tau * i / n;
                var tip0 = c + Vector2.FromAngle(a) * r;
                var tip1 = c + Vector2.FromAngle(a + Mathf.Tau / n * 0.5f) * r;
                DrawPolygon(new[] { c, tip0, tip1 }, new[] { new Color(Color, 0.28f), new Color(Color, 0.04f), new Color(Color, 0.04f) });
            }
            for (int k = 6; k >= 1; k--) DrawCircle(c, 70f * k, new Color(Color, 0.035f));   // halo doux derrière le titre
        }
    }

    /// <summary>Étoile dorée du MVP, qui respire (animée par l'écran qui la montre : tableau ou podium).</summary>
    internal sealed partial class Star : Control
    {
        public float T;
        public float Radius = 40f;
        public override void _Draw()
        {
            var c = Size / 2;
            float t = T;
            float r = Radius * (1f + 0.06f * Mathf.Sin(t * 4f));
            Vector2[] Pts(float outer, float inner)
            {
                var pts = new Vector2[10];
                for (int i = 0; i < 10; i++) pts[i] = c + Vector2.FromAngle(-Mathf.Pi / 2 + Mathf.Pi * i / 5f + t * 0.4f) * (i % 2 == 0 ? outer : inner);
                return pts;
            }
            DrawColoredPolygon(Pts(r + 7, r * 0.5f + 5), UiKit.Navy);
            DrawColoredPolygon(Pts(r, r * 0.5f), UiKit.Yellow);
        }
    }
}
