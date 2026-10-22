using System;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Menu pause (Échap) du joueur clavier-souris : reprendre, options, recommencer, menu principal, quitter ; carte des
/// commandes à droite, aide complète sur F1.
/// Hors ligne, le jeu se fige ; en ligne il continue (on ne fige pas les autres joueurs) et le joueur ne reçoit plus d'entrées.
/// </summary>
public partial class PauseMenu : CanvasLayer
{
    /// <summary>Vrai tant que le menu (ou un de ses sous-écrans) est affiché : le joueur ne reprend pas la souris au clic.</summary>
    public static bool IsOpen { get; private set; }

    /// <summary>Hors ligne : le monde se fige pendant la pause.</summary>
    public bool CanFreeze { get; set; } = true;
    public Action? Restart { get; set; }
    public Action? QuitToMenu { get; set; }
    /// <summary>Aide des commandes (texte multi-ligne).</summary>
    public Func<string>? HelpText { get; set; }

    Control root = null!;
    Control? sub;

    public override void _Ready()
    {
        Layer = 60;
        ProcessMode = ProcessModeEnum.Always;
        IsOpen = false;
        // Captures d'écran automatiques : --ui-test=pause | options | help ouvre l'écran au bout de 2 s.
        foreach (var arg in Tools.DevFlags.Args)
            if (arg.StartsWith("--ui-test="))
            {
                string what = arg["--ui-test=".Length..];
                GetTree().CreateTimer(2.0).Timeout += () =>
                {
                    Open();
                    if (what == "options") OpenSub(new OptionsMenu());
                    else if (what == "help") ShowHelp();
                };
            }
    }

    public override void _ExitTree()
    {
        IsOpen = false;
        if (CanFreeze && GetTree() != null) GetTree().Paused = false;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // F1 pendant la pause : l'aide complète (le HUD, lui, ignore F1 tant que la pause est ouverte).
        if (e.IsActionPressed("help") && IsOpen && sub == null) { GetViewport().SetInputAsHandled(); ShowHelp(); return; }
        if (!e.IsActionPressed("pause")) return;
        GetViewport().SetInputAsHandled();
        if (sub != null) return;   // le sous-écran gère son propre retour
        if (IsOpen) Close(); else Open();
    }

    /// <summary>Perte du focus de la fenêtre (Alt-Tab) : on ouvre la pause pour ne pas laisser le joueur à la merci des bots.</summary>
    public override void _Notification(int what)
    {
        if (what == NotificationApplicationFocusOut && !IsOpen && Input.MouseMode == Input.MouseModeEnum.Captured) Open();
    }

    public void Open()
    {
        if (IsOpen) return;
        IsOpen = true;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        if (CanFreeze) GetTree().Paused = true;
        Sfx.Play(this, SfxKind.UiClick, null, 0.8f);
        Build();
    }

    public void Close()
    {
        if (!IsOpen) return;
        IsOpen = false;
        root?.QueueFree();
        if (CanFreeze) GetTree().Paused = false;
        Input.MouseMode = Input.MouseModeEnum.Captured;
        Sfx.Play(this, SfxKind.UiBack);
    }

    void Build()
    {
        root = new Control { Theme = UiKit.Theme };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        root.AddChild(UiKit.Dim(0.5f));
        // Le bandeau penché du menu principal, à gauche : la pause en est la suite directe.
        var band = new MenuBand { MouseFilter = Control.MouseFilterEnum.Ignore };
        band.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        band.Resized += band.QueueRedraw;
        root.AddChild(band);
        UiKit.SlideIn(band, 0f, -200f);

        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        col.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        col.OffsetLeft = 96; col.OffsetRight = 680;
        col.AddThemeConstantOverride("separation", 14);
        root.AddChild(col);

        var title = UiKit.Title("PAUSE", 104, HorizontalAlignment.Left);
        col.AddChild(title);
        if (!CanFreeze) col.AddChild(UiKit.Text("La partie continue en ligne", 22, UiKit.Sky, 6));
        col.AddChild(UiKit.Spacer(10));
        // Items du menu principal : texte penché, barre jaune sur la sélection ; « Quitter le jeu » à part, plus bas.
        var resume = UiKit.ListButton("REPRENDRE", Close);
        col.AddChild(resume);
        col.AddChild(UiKit.ListButton("OPTIONS", () => OpenSub(new OptionsMenu())));
        if (Restart != null) col.AddChild(Confirmed("RECOMMENCER LA PARTIE", () => { Close(); Restart(); }));
        if (QuitToMenu != null) col.AddChild(Confirmed("MENU PRINCIPAL", () => { IsOpen = false; if (CanFreeze) GetTree().Paused = false; QuitToMenu(); }));
        col.AddChild(UiKit.Spacer(26));
        col.AddChild(Confirmed("QUITTER LE JEU", () => GetTree().Quit(), 24, new Vector2(440, 56), UiKit.Lavender));   // secondaire : lavande, comme QUITTER au menu

        // À droite : toutes les commandes en touches dessinées (la carte remplace l'ancien bouton « Commandes ») ; F1 = aide complète.
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", UiKit.PanelBox(18, 34));   // opaque : rien du jeu ne transparaît
        card.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        card.GrowHorizontal = Control.GrowDirection.Begin;
        card.GrowVertical = Control.GrowDirection.Both;
        card.OffsetLeft = -680; card.OffsetRight = -120; card.OffsetTop = 0; card.OffsetBottom = 0;   // hauteur : celle du contenu, centrée
        root.AddChild(card);
        var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        list.AddThemeConstantOverride("separation", 14);
        card.AddChild(list);
        list.AddChild(UiKit.Title("COMMANDES", 38, HorizontalAlignment.Left));
        list.AddChild(CommandGrid(24, 12));
        var hint = UiKit.KeyHint(InputSetup.KeyName("help"), "Aide complète", 22);
        hint.Modulate = new Color(1, 1, 1, 0.8f);
        list.AddChild(hint);
        UiKit.SlideIn(card, 0.1f, 80f);
        int i = 0;
        foreach (var c in col.GetChildren()) if (c is Control ctl) UiKit.SlideIn(ctl, 0.03f * i++);
        resume.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Bouton à confirmer : un premier appui demande « SÛR ? », le second agit (3 s pour confirmer).</summary>
    static Button Confirmed(string text, Action act, int fontSize = 28, Vector2? minSize = null, Color? rest = null)
    {
        Button? b = null;
        double armedUntil = 0;
        b = UiKit.ListButton(text, () =>
        {
            double now = Time.GetTicksMsec() / 1000.0;
            if (now < armedUntil) { act(); return; }
            armedUntil = now + 3.0;
            b!.Text = text + " ?  (CONFIRMER)";
            b.AddThemeColorOverride("font_color", UiKit.Yellow);
            b.GetTree().CreateTimer(3.0, true, false, true).Timeout += () =>
            {
                if (!IsInstanceValid(b)) return;
                b.Text = text;
                b.AddThemeColorOverride("font_color", rest ?? Colors.White);
            };
        }, fontSize, minSize);
        if (rest is Color c) b.AddThemeColorOverride("font_color", c);
        return b;
    }

    /// <summary>
    /// Grille des commandes en touches dessinées (carte de la pause et aide complète), avec les libellés du HUD
    /// (<see cref="InputSetup"/>) : déplacement sur une ligne, pause et aide omises (rappelées à côté).
    /// </summary>
    static GridContainer CommandGrid(int size, int vsep)
    {
        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 22);
        grid.AddThemeConstantOverride("v_separation", vsep);
        void Row(Control keys, string what)
        {
            grid.AddChild(keys);
            var l = UiKit.Text(what, size, Colors.White, 6);
            l.VerticalAlignment = VerticalAlignment.Center;
            l.SizeFlagsVertical = Control.SizeFlags.Fill;
            grid.AddChild(l);
        }
        var move = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        move.AddThemeConstantOverride("separation", 4);
        foreach (var a in new[] { "move_forward", "move_left", "move_back", "move_right" }) move.AddChild(UiKit.KeyCap(InputSetup.KeyName(a), size - 3));
        Row(move, "Se déplacer");
        foreach (var a in InputSetup.Actions)
        {
            if (a.Name.StartsWith("move_") || a.Name is "pause" or "help") continue;
            var key = UiKit.KeyCap(InputSetup.KeyName(a.Name), size - 3, 140);
            key.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            Row(key, a.Label);
        }
        return grid;
    }

    void OpenSub(Control screen)
    {
        root.Visible = false;
        sub = screen;
        if (screen is OptionsMenu o) o.Closed += () => { sub = null; root.Visible = true; };
        AddChild(screen);
    }

    void ShowHelp()
    {
        var panel = new HelpPanel { Text = HelpText?.Invoke() ?? "" };
        panel.Closed += () => { sub = null; root.Visible = true; };
        root.Visible = false;
        sub = panel;
        AddChild(panel);
    }

    /// <summary>Carte des commandes, plein écran, fermée par Échap ou le bouton.</summary>
    sealed partial class HelpPanel : Control
    {
        public string Text = "";
        public event Action? Closed;

        public override void _Ready()
        {
            Theme = UiKit.Theme;
            SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(UiKit.Dim(0.5f));
            var center = new CenterContainer();
            center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
            AddChild(center);
            var panel = new PanelContainer();
            panel.AddThemeStyleboxOverride("panel", UiKit.PanelBox(22, 40));   // opaque : les étiquettes des joueurs ne transparaissent plus
            center.AddChild(panel);
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 16);
            panel.AddChild(col);
            col.AddChild(UiKit.Title("COMMANDES", 52));
            col.AddChild(CommandGrid(26, 10));
            if (Text != "") col.AddChild(UiKit.Text(Text, 20, UiKit.Sky, 5));
            var back = UiKit.Button("RETOUR", UiKit.Orange, Close, 26, new Vector2(300, 56));
            back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            col.AddChild(back);
            back.CallDeferred(Control.MethodName.GrabFocus);
        }

        void Close() { Closed?.Invoke(); QueueFree(); }

        public override void _Input(InputEvent e)
        {
            if (!e.IsActionPressed("pause") && !e.IsActionPressed("ui_cancel") && !e.IsActionPressed("help")) return;
            GetViewport().SetInputAsHandled();
            Close();
        }
    }
}
