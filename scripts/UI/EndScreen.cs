using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Écran de fin de partie du joueur clavier-souris : VICTOIRE ou DÉFAITE (point de vue du joueur local), manches,
/// MVP mis en avant, tableau par joueur (hôte seulement : le client ne reçoit pas les statistiques), REJOUER et MENU PRINCIPAL.
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

    const double OpenDelay = 1.8;
    const double AutoRestartSeconds = 8.0;   // MatchController.MatchEndSeconds
    Control? root;
    double overFor = -1;
    Label? autoLabel;
    Sunburst? burst;
    Star? star;

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
        // Le menu pause refermé reprend la souris : sur l'écran de fin, il faut la rendre pour cliquer.
        if (root != null && !Autoplay && !PauseMenu.IsOpen && Input.MouseMode == Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Visible;
        if (autoLabel != null) autoLabel.Text = BackToRoom
            ? $"Retour à la salle dans {Mathf.Max(0, Mathf.CeilToInt((float)(BackToRoomSeconds - overFor)))} s"
            : $"Nouvelle partie dans {Mathf.Max(0, Mathf.CeilToInt((float)(AutoRestartSeconds - overFor)))} s";
    }

    void Open()
    {
        IsOpen = true;
        if (!Autoplay) Input.MouseMode = Input.MouseModeEnum.Visible;
        var s = View.State;
        bool win = s.Winner == View.LocalTeam;
        Audio.Music.Stinger(win ? SfxKind.Victory : SfxKind.Defeat);   // jingle + foule, la musique de match s'efface

        root = new Control { Theme = UiKit.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var dim = UiKit.Dim(0.78f);
        root.AddChild(dim);
        dim.Modulate = new Color(1, 1, 1, 0);
        dim.CreateTween().TweenProperty(dim, "modulate:a", 1.0, 0.3);

        burst = new Sunburst { Color = win ? UiKit.Yellow : UiKit.Sky, MouseFilter = Control.MouseFilterEnum.Ignore };
        burst.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(burst);

        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        col.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        col.OffsetTop = 40; col.OffsetBottom = -40;
        col.AddThemeConstantOverride("separation", 14);
        root.AddChild(col);

        var title = UiKit.Text(win ? "VICTOIRE !" : "DÉFAITE", 170, win ? UiKit.Yellow : new Color("9fb4d8"), 30, HorizontalAlignment.Center);
        title.CustomMinimumSize = new Vector2(0, 190);
        col.AddChild(title);

        // Score des manches, aux couleurs des équipes.
        var score = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        score.AddThemeConstantOverride("separation", 26);
        score.AddChild(UiKit.Text("ÉQUIPE ORANGE", 40, UiKit.Orange, 10));
        score.AddChild(UiKit.Text($"{s.Rounds0}  -  {s.Rounds1}", 64, Colors.White, 14));
        score.AddChild(UiKit.Text("ÉQUIPE BLEUE", 40, UiKit.Sky, 10));
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
        else if (View.CanRestart) buttons.AddChild(first = UiKit.Button("REJOUER", UiKit.Orange, Replay, 34, new Vector2(400, 76)));
        else
        {
            var wait = UiKit.Text("En attente de l'hôte…", 32, UiKit.Sky, 8, HorizontalAlignment.Center);
            wait.CustomMinimumSize = new Vector2(400, 76);
            wait.VerticalAlignment = VerticalAlignment.Center;
            buttons.AddChild(wait);
        }
        var menu = UiKit.Button("MENU PRINCIPAL", UiKit.Grey, () => { Close(false); QuitToMenu?.Invoke(); }, 30, new Vector2(400, 76));
        buttons.AddChild(menu);
        (first ?? menu).CallDeferred(Control.MethodName.GrabFocus);

        // Entrée : le titre tape, le reste glisse en cascade.
        title.Resized += () => title.PivotOffset = title.Size / 2;
        title.Scale = Vector2.One * 2.2f;
        title.Modulate = new Color(1, 1, 1, 0);
        var tw = title.CreateTween().SetParallel();
        tw.TweenProperty(title, "scale", Vector2.One, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(title, "modulate:a", 1.0, 0.2);
        Appear(score, 0.25f);
        int i = 0;
        foreach (var c in middle.GetChildren()) if (c is Control ctl) Appear(ctl, 0.35f + 0.15f * i++);
        Appear(buttons, 0.7f);
        if (mvpCard != null)
        {
            var t2 = mvpCard.CreateTween();
            t2.TweenInterval(0.9);
            t2.TweenCallback(Callable.From(() => { UiKit.Punch(mvpCard, 1.15f, 0.3); Sfx.Play(this, SfxKind.Perfect); }));
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
        Close(true);
        View.Restart();
    }

    void Close(bool capture)
    {
        IsOpen = false;
        root?.QueueFree();
        root = null;
        autoLabel = null;
        burst = null;
        star = null;
        if (capture && !Autoplay) Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    /// <summary>Meilleur joueur : note du MVP, puis KO à égalité.</summary>
    int MvpId()
    {
        int best = -1, bestScore = -1, bestKos = -1;
        foreach (var p in View.Players)
        {
            if (View.Stats == null || !View.Stats.TryGetValue(p.Id, out var st)) continue;
            if (st.Score > bestScore || (st.Score == bestScore && st.Kos > bestKos)) { best = p.Id; bestScore = st.Score; bestKos = st.Kos; }
        }
        return bestScore > 0 ? best : -1;
    }

    Control? MvpCard()
    {
        int id = MvpId();
        if (id < 0 || View.Stats == null) return null;
        var st = View.Stats[id];
        int team = View.TeamOf(id);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(360, 0) };
        var box = UiKit.Box(TeamRow(team, true), 22, 0f, 5, UiKit.Yellow, 8);
        box.AntiAliasingSize = 1.25f;
        panel.Rotation = -0.03f;
        box.ContentMarginTop = 18; box.ContentMarginBottom = 22;
        panel.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 4);
        panel.AddChild(col);
        star = new Star { CustomMinimumSize = new Vector2(0, 90), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(star);
        col.AddChild(UiKit.Text("MVP", 64, UiKit.Yellow, 14, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text(View.NameOf(id), 46, Colors.White, 10, HorizontalAlignment.Center));
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(UiKit.Text($"{st.Kos} KO   ·   {Plural(st.Hits, "touche")}", 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text($"{Plural(st.Catches, "attrape")}   ·   {Plural(st.PerfectCatches, "parfaite")}", 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        panel.Resized += () => panel.PivotOffset = panel.Size / 2;
        return panel;
    }

    static string Plural(int n, string word) => $"{n} {word}{(n > 1 ? "s" : "")}";

    Control StatsTable()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsVertical = Control.SizeFlags.ShrinkCenter };
        var box = UiKit.Box(UiKit.Navy, 20, 0f, 3, new Color("2a3a6a"), 8);   // opaque : la partie ne transparaît plus
        box.ContentMarginLeft = 24; box.ContentMarginRight = 24; box.ContentMarginTop = 16; box.ContentMarginBottom = 18;
        panel.AddThemeStyleboxOverride("panel", box);
        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);
        rows.AddChild(Row(null, "JOUEUR", new[] { "KO", "TOUCHES", "ATTRAPES", "PARFAITES", "LANCERS" }, false));
        int mvp = MvpId();
        // Mon équipe d'abord, puis l'adverse ; dans chaque équipe, du meilleur score MVP au moins bon (KO à égalité).
        var ordered = new List<MatchView.PlayerInfo>();
        foreach (var p in View.Players) if (View.Stats!.ContainsKey(p.Id)) ordered.Add(p);
        int Score(int id) => View.Stats![id].Score;
        int Kos(int id) => View.Stats![id].Kos;
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
            var st = View.Stats![p.Id];
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
        var box = UiKit.Box(bg, 8, 0f, local ? 4 : 0, UiKit.Yellow, header ? 0 : 3);
        box.ContentMarginTop = 3; box.ContentMarginBottom = 3; box.ContentMarginLeft = 16; box.ContentMarginRight = 16;
        if (local) { box.ShadowColor = new Color(UiKit.Yellow, 0.35f); box.ShadowSize = 10; }
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 6);
        row.AddChild(line);
        int size = header ? 20 : 30;
        var color = header ? UiKit.Sky : Colors.White;
        var n = UiKit.Text(name, size, color, local ? 9 : 6);   // blanc : le jaune ne se lit pas sur l'orange
        n.CustomMinimumSize = new Vector2(220, 0);
        line.AddChild(n);
        foreach (var c in cells)
        {
            var l = UiKit.Text(c, size, color, 6, HorizontalAlignment.Center);
            l.CustomMinimumSize = new Vector2(128, 0);
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
        public float Angle;
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 260);
            float r = Size.Length();
            const int n = 18;
            for (int i = 0; i < n; i++)
            {
                float a = Angle + Mathf.Tau * i / n;
                var tip0 = c + Vector2.FromAngle(a) * r;
                var tip1 = c + Vector2.FromAngle(a + Mathf.Tau / n * 0.5f) * r;
                DrawPolygon(new[] { c, tip0, tip1 }, new[] { new Color(Color, 0.28f), new Color(Color, 0.04f), new Color(Color, 0.04f) });
            }
            for (int k = 6; k >= 1; k--) DrawCircle(c, 70f * k, new Color(Color, 0.035f));   // halo doux derrière le titre
        }
    }

    /// <summary>Étoile dorée du MVP, qui respire.</summary>
    sealed partial class Star : Control
    {
        public float T;
        public override void _Draw()
        {
            var c = Size / 2;
            float t = T;
            float r = 40f * (1f + 0.06f * Mathf.Sin(t * 4f));
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
