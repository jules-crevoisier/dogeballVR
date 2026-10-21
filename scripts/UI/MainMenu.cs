using System;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Net;

namespace DogeballVR.UI;

/// <summary>
/// Menu principal du mode PC, par-dessus le décor 3D (<see cref="MenuBackdrop"/>) : logo, colonne de gros boutons inclinés,
/// pages Jouer en ligne, Options et Crédits. À la toute première partie, on propose le tutoriel.
/// </summary>
public partial class MainMenu : CanvasLayer
{
    public event Action<NetMode, string>? Chosen;
    public event Action? TutorialChosen;

    Control root = null!;
    VBoxContainer column = null!;
    Control? overlay;
    ColorRect fade = null!;
    bool leaving;

    public override void _Ready()
    {
        Layer = 20;
        root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Dégradé sombre à gauche : les boutons restent lisibles quel que soit le décor.
        var grad = new Gradient { Offsets = new[] { 0f, 0.55f, 1f }, Colors = new[] { new Color(UiKit.Navy, 0.82f), new Color(UiKit.Navy, 0.45f), new Color(UiKit.Navy, 0f) } };
        var shade = new TextureRect { Texture = new GradientTexture2D { Gradient = grad, Width = 256, Height = 4 }, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = Control.MouseFilterEnum.Ignore };
        shade.SetAnchorsPreset(Control.LayoutPreset.LeftWide);
        shade.OffsetRight = 900;
        root.AddChild(shade);

        var logoBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(96, 48), Size = new Vector2(760, 200), RotationDegrees = -4f };
        root.AddChild(logoBox);
        var logoShadow = UiKit.Text("DOGEBALL", 132, UiKit.Yellow, 0);
        logoShadow.Position = new Vector2(8, 10);
        logoBox.AddChild(logoShadow);
        var logo = UiKit.Text("DOGEBALL", 132, UiKit.Orange, 28);
        logoBox.AddChild(logo);
        var tag = UiKit.Text("ESQUIVE · ATTRAPE · RENVOIE", 30, Colors.White, 10);
        tag.Position = new Vector2(14, 158);
        logoBox.AddChild(tag);
        UiKit.Punch(logoBox, 1.3f, 0.45);
        // Le logo respire doucement.
        var bob = logoBox.CreateTween().SetLoops();
        bob.TweenProperty(logoBox, "rotation_degrees", -2.5f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bob.TweenProperty(logoBox, "rotation_degrees", -4f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        column = new VBoxContainer { Position = new Vector2(110, 300) };
        column.AddThemeConstantOverride("separation", 14);
        root.AddChild(column);

        var version = UiKit.Text("Version de démonstration · clavier-souris et VR (EVA)", 18, new Color(1, 1, 1, 0.8f), 5, HorizontalAlignment.Right);
        version.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        version.OffsetLeft = -700; version.OffsetTop = -44; version.OffsetRight = -24; version.OffsetBottom = -16;
        root.AddChild(version);

        fade = new ColorRect { Color = new Color(0, 0, 0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(fade);
        fade.CreateTween().TweenProperty(fade, "color:a", 0.0, 0.6);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        ShowMain();
    }

    void Fill(params Control[] items)
    {
        foreach (var c in column.GetChildren()) c.QueueFree();
        int i = 0;
        foreach (var c in items)
        {
            column.AddChild(c);
            UiKit.SlideIn(c, 0.05f * i++);
        }
        if (items.Length > 0 && items[0] is Button b) b.CallDeferred(Control.MethodName.GrabFocus);
    }

    void ShowMain()
    {
        Fill(
            UiKit.Button("JOUER", UiKit.Orange, PlayBots, 38, new Vector2(460, 84)),
            UiKit.Button("ENTRAÎNEMENT", UiKit.Blue, () => Leave(() => TutorialChosen?.Invoke())),
            UiKit.Button("EN LIGNE", UiKit.Blue, ShowOnline),
            UiKit.Button("OPTIONS", UiKit.Blue, () => OpenOverlay(new OptionsMenu())),
            UiKit.Button("CRÉDITS", UiKit.Grey, ShowCredits),
            UiKit.Button("QUITTER", UiKit.Grey, () => GetTree().Quit()));
    }

    void ShowOnline()
    {
        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Adresse de l'hôte", CustomMinimumSize = new Vector2(440, 58), Alignment = HorizontalAlignment.Center };
        Fill(
            UiKit.Text("EN LIGNE", 44, UiKit.Yellow, 10),
            UiKit.Button("HÉBERGER UNE PARTIE", UiKit.Orange, () => Leave(() => Chosen?.Invoke(NetMode.Host, ""))),
            address,
            UiKit.Button("REJOINDRE", UiKit.Blue, () => Leave(() => Chosen?.Invoke(NetMode.Client, address.Text.Trim()))),
            UiKit.Button("RETOUR", UiKit.Grey, () => { Sfx.Play(this, SfxKind.UiBack); ShowMain(); }));
    }

    void PlayBots()
    {
        if (GameSettings.TutorialDone) { Leave(() => Chosen?.Invoke(NetMode.Offline, "")); return; }
        // Première partie : on propose le tutoriel (2 minutes), sans l'imposer.
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        card.AddChild(col);
        col.AddChild(UiKit.Text("PREMIÈRE PARTIE ?", 46, UiKit.Yellow, 10, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text("L'entraînement t'apprend lancer, attrape et dash en deux minutes.", 24, Colors.White, 6, HorizontalAlignment.Center));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        col.AddChild(row);
        var yes = UiKit.Button("ENTRAÎNEMENT", UiKit.Orange, () => Leave(() => TutorialChosen?.Invoke()), 28, new Vector2(320, 64));
        row.AddChild(yes);
        row.AddChild(UiKit.Button("JOUER DIRECT", UiKit.Blue, () =>
        {
            GameSettings.TutorialDone = true;
            GameSettings.Save();
            Leave(() => Chosen?.Invoke(NetMode.Offline, ""));
        }, 28, new Vector2(320, 64)));
        OpenOverlay(Centered(card));
        yes.CallDeferred(Control.MethodName.GrabFocus);
    }

    void ShowCredits()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        card.AddChild(col);
        col.AddChild(UiKit.Text("CRÉDITS", 50, UiKit.Yellow, 12, HorizontalAlignment.Center));
        foreach (var (who, what) in new[]
        {
            ("Conception et direction", "jules"),
            ("Moteur", "Godot Engine 4.3 (MIT)"),
            ("Police", "Lilita One, Juan Montoreano (SIL OFL)"),
            ("Modèles 3D", "faits maison avec Blender"),
            ("Sons", "voir assets/audio/CREDITS.md"),
            ("Inspiration", "Knockout City (Velan Studios), hommage"),
        })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 24);
            var a = UiKit.Text(who, 24, UiKit.Sky, 6, HorizontalAlignment.Right);
            a.CustomMinimumSize = new Vector2(330, 0);
            row.AddChild(a);
            row.AddChild(UiKit.Text(what, 24, Colors.White, 6));
            col.AddChild(row);
        }
        var back = UiKit.Button("RETOUR", UiKit.Orange, CloseOverlay, 26, new Vector2(300, 58));
        back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(back);
        OpenOverlay(Centered(card));
        back.CallDeferred(Control.MethodName.GrabFocus);
    }

    static Control Centered(Control card)
    {
        var holder = new Control();
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(UiKit.Dim(0.5f));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(center);
        center.AddChild(card);
        UiKit.Punch(card, 0.85f, 0.22);
        return holder;
    }

    void OpenOverlay(Control c)
    {
        CloseOverlay();
        column.Visible = false;
        overlay = c;
        if (c is OptionsMenu o) o.Closed += () => { overlay = null; column.Visible = true; ShowMain(); };
        root.AddChild(c);
        root.MoveChild(fade, -1);
    }

    void CloseOverlay()
    {
        if (overlay is OptionsMenu) return;   // l'écran d'options se ferme lui-même (et enregistre)
        overlay?.QueueFree();
        overlay = null;
        column.Visible = true;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (!e.IsActionPressed("pause") && !e.IsActionPressed("ui_cancel")) return;
        if (overlay != null && overlay is not OptionsMenu) { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); ShowMain(); GetViewport().SetInputAsHandled(); }
    }

    /// <summary>Fondu au noir puis lancement (la partie se construit derrière le noir, qui se dissipe ensuite).</summary>
    void Leave(Action then)
    {
        if (leaving) return;
        leaving = true;
        var tw = fade.CreateTween();
        tw.TweenProperty(fade, "color:a", 1.0, 0.35);
        tw.TweenCallback(Callable.From(then));
    }
}
