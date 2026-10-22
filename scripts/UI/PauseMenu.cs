using System;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Menu pause (Échap) du joueur clavier-souris : reprendre, options, commandes, recommencer, menu principal, quitter.
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
        foreach (var arg in OS.GetCmdlineUserArgs())
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
        root.AddChild(UiKit.Dim(0.62f));

        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        col.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        col.OffsetLeft = 120; col.OffsetRight = 640;
        col.AddThemeConstantOverride("separation", 14);
        root.AddChild(col);

        var title = UiKit.Text("PAUSE", 96, UiKit.Yellow, 18);
        col.AddChild(title);
        if (!CanFreeze) col.AddChild(UiKit.Text("La partie continue en ligne", 22, UiKit.Sky, 6));
        col.AddChild(UiKit.Spacer(10));
        var resume = UiKit.Button("REPRENDRE", UiKit.Orange, Close);
        col.AddChild(resume);
        col.AddChild(UiKit.Button("OPTIONS", UiKit.Blue, () => OpenSub(new OptionsMenu())));
        col.AddChild(UiKit.Button("COMMANDES", UiKit.Blue, ShowHelp));
        if (Restart != null) col.AddChild(UiKit.Button("RECOMMENCER LA PARTIE", UiKit.Blue, () => { Close(); Restart(); }));
        if (QuitToMenu != null) col.AddChild(UiKit.Button("MENU PRINCIPAL", UiKit.Grey, () => { IsOpen = false; if (CanFreeze) GetTree().Paused = false; QuitToMenu(); }));
        col.AddChild(UiKit.Button("QUITTER LE JEU", UiKit.Grey, () => GetTree().Quit()));
        int i = 0;
        foreach (var c in col.GetChildren()) if (c is Control ctl) UiKit.SlideIn(ctl, 0.03f * i++);
        resume.CallDeferred(Control.MethodName.GrabFocus);
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
            center.AddChild(panel);
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 12);
            panel.AddChild(col);
            col.AddChild(UiKit.Text("COMMANDES", 48, UiKit.Yellow, 12, HorizontalAlignment.Center));
            var grid = new GridContainer { Columns = 2 };
            grid.AddThemeConstantOverride("h_separation", 40);
            grid.AddThemeConstantOverride("v_separation", 6);
            col.AddChild(grid);
            foreach (var a in InputSetup.Actions)
            {
                if (a.Name is "pause" or "help" or "move_back" or "move_left" or "move_right") continue;
                bool move = a.Name == "move_forward";
                string keys = move ? $"{InputSetup.KeyName("move_forward")} {InputSetup.KeyName("move_left")} {InputSetup.KeyName("move_back")} {InputSetup.KeyName("move_right")}" : InputSetup.KeyName(a.Name);
                var k = UiKit.Text(keys, 24, UiKit.Yellow, 6, HorizontalAlignment.Right);
                k.CustomMinimumSize = new Vector2(160, 0);
                grid.AddChild(k);
                grid.AddChild(UiKit.Text(move ? "Se déplacer" : a.Label, 24, Colors.White, 6));
            }
            if (Text != "") col.AddChild(UiKit.Text(Text, 20, UiKit.Sky, 5));
            var back = UiKit.Button("RETOUR", UiKit.Orange, Close, 26, new Vector2(300, 56));
            back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
            col.AddChild(back);
            back.CallDeferred(Control.MethodName.GrabFocus);
        }

        void Close() { Closed?.Invoke(); QueueFree(); }

        public override void _Input(InputEvent e)
        {
            if (!e.IsActionPressed("pause") && !e.IsActionPressed("ui_cancel")) return;
            GetViewport().SetInputAsHandled();
            Close();
        }
    }
}
