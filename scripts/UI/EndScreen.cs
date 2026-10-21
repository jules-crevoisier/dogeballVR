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

    const double OpenDelay = 1.8;
    const double AutoRestartSeconds = 8.0;   // MatchController.MatchEndSeconds
    Control? root;
    double overFor = -1;
    Label? autoLabel;

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
        if (root == null && overFor >= OpenDelay) Open();
        // Le menu pause refermé reprend la souris : sur l'écran de fin, il faut la rendre pour cliquer.
        if (root != null && !Autoplay && !PauseMenu.IsOpen && Input.MouseMode == Input.MouseModeEnum.Captured) Input.MouseMode = Input.MouseModeEnum.Visible;
        if (autoLabel != null) autoLabel.Text = $"Nouvelle partie dans {Mathf.Max(0, Mathf.CeilToInt((float)(AutoRestartSeconds - overFor)))} s";
    }

    void Open()
    {
        IsOpen = true;
        if (!Autoplay) Input.MouseMode = Input.MouseModeEnum.Visible;
        var s = View.State;
        bool win = s.Winner == View.LocalTeam;
        Sfx.Play(this, win ? SfxKind.Victory : SfxKind.Defeat);

        root = new Control { Theme = UiKit.Theme };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        var dim = UiKit.Dim(0.78f);
        root.AddChild(dim);
        dim.Modulate = new Color(1, 1, 1, 0);
        dim.CreateTween().TweenProperty(dim, "modulate:a", 1.0, 0.3);

        var burst = new Sunburst { Color = win ? UiKit.Yellow : UiKit.Sky, MouseFilter = Control.MouseFilterEnum.Ignore };
        burst.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        burst.OffsetLeft = -500; burst.OffsetRight = 500; burst.OffsetTop = -380; burst.OffsetBottom = 620;
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
        if (View.AutoRestart)
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
        var box = UiKit.Box(team == 0 ? UiKit.Orange.Darkened(0.15f) : UiKit.Blue, 22, -0.06f, 5, UiKit.Yellow, 8);
        box.ContentMarginTop = 18; box.ContentMarginBottom = 22;
        panel.AddThemeStyleboxOverride("panel", box);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 4);
        panel.AddChild(col);
        var star = new Star { CustomMinimumSize = new Vector2(0, 90), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(star);
        col.AddChild(UiKit.Text("MVP", 64, UiKit.Yellow, 14, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text(View.NameOf(id), 46, Colors.White, 10, HorizontalAlignment.Center));
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(UiKit.Text($"{st.Kos} KO   ·   {st.Hits} touches", 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text($"{st.Catches} attrapes   ·   {st.PerfectCatches} parfaites", 26, UiKit.Cream, 6, HorizontalAlignment.Center));
        panel.Resized += () => panel.PivotOffset = panel.Size / 2;
        return panel;
    }

    Control StatsTable()
    {
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Box(new Color(UiKit.NavySoft, 0.92f), 20, 0f, 0, null, 8);
        box.ContentMarginLeft = 24; box.ContentMarginRight = 24; box.ContentMarginTop = 16; box.ContentMarginBottom = 18;
        panel.AddThemeStyleboxOverride("panel", box);
        var rows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        rows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(rows);
        rows.AddChild(Row(null, "JOUEUR", new[] { "KO", "TOUCHES", "ATTRAPES", "PARFAITES", "LANCERS" }, false));
        int mvp = MvpId();
        var ordered = new List<MatchView.PlayerInfo>(View.Players);
        ordered.Sort((a, b) => a.Team != b.Team ? a.Team.CompareTo(b.Team) : (View.Stats![b.Id].Score).CompareTo(View.Stats![a.Id].Score));
        int i = 0;
        foreach (var p in ordered)
        {
            if (!View.Stats!.TryGetValue(p.Id, out var st)) continue;
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
        var bg = header ? new Color(0, 0, 0, 0) : new Color(Palette.Team(p!.Team), local ? 0.6f : 0.3f);
        var box = UiKit.Box(bg, 8, -0.1f, local ? 3 : 0, UiKit.Yellow, 0);
        box.ContentMarginTop = 3; box.ContentMarginBottom = 3; box.ContentMarginLeft = 16; box.ContentMarginRight = 16;
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 6);
        row.AddChild(line);
        int size = header ? 20 : 30;
        var color = header ? UiKit.Sky : mvp ? UiKit.Yellow : Colors.White;
        var n = UiKit.Text(name, size, color, 6);
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

    /// <summary>Rayons qui tournent lentement derrière le titre.</summary>
    sealed partial class Sunburst : Control
    {
        public Color Color = UiKit.Yellow;
        float angle;
        public override void _Process(double delta) { angle += (float)delta * 0.25f; QueueRedraw(); }
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 470);
            float r = Size.X * 0.75f;
            for (int i = 0; i < 16; i++)
            {
                float a = angle + Mathf.Tau * i / 16f;
                DrawColoredPolygon(new[] { c, c + Vector2.FromAngle(a) * r, c + Vector2.FromAngle(a + 0.17f) * r }, new Color(Color, 0.07f));
            }
        }
    }

    /// <summary>Étoile dorée du MVP, qui respire.</summary>
    sealed partial class Star : Control
    {
        float t;
        public override void _Process(double delta) { t += (float)delta; QueueRedraw(); }
        public override void _Draw()
        {
            var c = Size / 2;
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
