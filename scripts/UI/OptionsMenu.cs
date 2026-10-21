using System;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Écran d'options (menu principal et pause) : quatre onglets, Contrôles, Touches, Audio, Vidéo.
/// Tout s'applique en direct ; l'enregistrement se fait en quittant l'écran.
/// </summary>
public partial class OptionsMenu : Control
{
    public event Action? Closed;

    VBoxContainer list = null!;
    readonly Button[] tabs = new Button[4];
    int tab;
    string? listening;          // action en attente d'une nouvelle touche
    Button? listeningButton;

    static readonly string[] TabNames = { "CONTRÔLES", "TOUCHES", "AUDIO", "VIDÉO" };

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(UiKit.Dim(0.55f));

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(980, 640) };
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        panel.AddChild(col);

        col.AddChild(UiKit.Text("OPTIONS", 54, UiKit.Yellow, 12, HorizontalAlignment.Center));
        var tabRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tabRow.AddThemeConstantOverride("separation", 10);
        col.AddChild(tabRow);
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            tabs[i] = UiKit.Button(TabNames[i], UiKit.NavySoft, () => ShowTab(k), 22, new Vector2(200, 50));
            tabRow.AddChild(tabs[i]);
        }

        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(920, 420), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, SizeFlagsVertical = SizeFlags.ExpandFill };
        col.AddChild(scroll);
        list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 10);
        scroll.AddChild(list);

        var bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bottom.AddThemeConstantOverride("separation", 16);
        col.AddChild(bottom);
        bottom.AddChild(UiKit.Button("PAR DÉFAUT", UiKit.Grey, ResetTab, 24, new Vector2(260, 56)));
        var back = UiKit.Button("RETOUR", UiKit.Orange, Close, 26, new Vector2(300, 56));
        bottom.AddChild(back);
        ShowTab(0);
        back.CallDeferred(Control.MethodName.GrabFocus);
        UiKit.Punch(panel, 0.9f, 0.2);
    }

    void Close()
    {
        GameSettings.Save();
        Closed?.Invoke();
        QueueFree();
    }

    public override void _Input(InputEvent e)
    {
        if (listening != null)
        {
            bool key = e is InputEventKey { Pressed: true, Echo: false };
            bool mouse = e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown);
            if (!key && !mouse) return;
            GetViewport().SetInputAsHandled();
            if (e is InputEventKey { PhysicalKeycode: Key.Escape } && listening != "pause") { StopListening(); return; }
            InputSetup.Rebind(listening, e);
            Sfx.Play(this, SfxKind.UiClick);
            StopListening();
            ShowTab(1);
            return;
        }
        if (e.IsActionPressed("pause") || e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Sfx.Play(this, SfxKind.UiBack);
            Close();
        }
    }

    void StopListening()
    {
        listening = null;
        listeningButton = null;
    }

    void ShowTab(int i)
    {
        tab = i;
        for (int k = 0; k < 4; k++)
            tabs[k].AddThemeStyleboxOverride("normal", UiKit.Box(k == i ? UiKit.Orange : UiKit.NavySoft, 14, -0.12f));
        foreach (var c in list.GetChildren()) c.QueueFree();
        switch (i)
        {
            case 0:
                Section("Souris");
                Slider("Sensibilité", GameSettings.Sensitivity / GameSettings.DefaultSensitivity, 0.25f, 4f, 0.05f, v => GameSettings.Sensitivity = v * GameSettings.DefaultSensitivity, v => $"{v:0.00}x");
                Toggle("Inverser l'axe vertical", GameSettings.InvertY, v => GameSettings.InvertY = v);
                Section("Caméra et visée");
                Slider("Champ de vision", GameSettings.Fov, 80f, 120f, 1f, v => GameSettings.Fov = v, v => $"{v:0}°");
                Slider("Aide à la visée", GameSettings.AimAssist, 0f, 1f, 0.05f, v => GameSettings.AimAssist = v, v => v < 0.01f ? "Désactivée" : $"{v * 100:0} %");
                Slider("Secousses de l'écran", GameSettings.ScreenShake, 0f, 1f, 0.05f, v => GameSettings.ScreenShake = v, v => v < 0.01f ? "Aucune" : $"{v * 100:0} %");
                Toggle("Arrêt sur image aux impacts", GameSettings.HitStop, v => GameSettings.HitStop = v);
                Toggle("Marqueur de touche", GameSettings.HitMarkers, v => GameSettings.HitMarkers = v);
                Toggle("S'accroupir : appui = bascule", GameSettings.ToggleCrouch, v => GameSettings.ToggleCrouch = v);
                break;
            case 1:
                Section("Clique sur une touche puis appuie sur la nouvelle (Échap annule)");
                foreach (var a in InputSetup.Actions) KeyRow(a);
                break;
            case 2:
                Slider("Volume général", GameSettings.MasterVolume, 0f, 1f, 0.05f, v => GameSettings.MasterVolume = v, Pct, true);
                Slider("Musique", GameSettings.MusicVolume, 0f, 1f, 0.05f, v => GameSettings.MusicVolume = v, Pct, true);
                Slider("Effets sonores", GameSettings.SfxVolume, 0f, 1f, 0.05f, v => GameSettings.SfxVolume = v, Pct, true);
                Slider("Interface", GameSettings.UiVolume, 0f, 1f, 0.05f, v => GameSettings.UiVolume = v, Pct, true);
                break;
            case 3:
                Toggle("Plein écran", GameSettings.Fullscreen, v => GameSettings.Fullscreen = v, true);
                Toggle("Synchronisation verticale", GameSettings.VSync, v => GameSettings.VSync = v, true);
                Choice("Images par seconde max", new[] { "Illimité", "60", "120", "144", "240" }, Array.IndexOf(new[] { 0, 60, 120, 144, 240 }, GameSettings.MaxFps), k => GameSettings.MaxFps = new[] { 0, 60, 120, 144, 240 }[k]);
                Choice("Qualité graphique", new[] { "Basse", "Moyenne", "Haute" }, GameSettings.Quality, k => GameSettings.Quality = k);
                Slider("Résolution de rendu", GameSettings.RenderScale, 0.5f, 1f, 0.05f, v => GameSettings.RenderScale = v, Pct, true);
                Toggle("Afficher les images par seconde", GameSettings.ShowFps, v => GameSettings.ShowFps = v);
                break;
        }
    }

    static string Pct(float v) => $"{v * 100:0} %";

    void ResetTab()
    {
        switch (tab)
        {
            case 0:
                GameSettings.Sensitivity = GameSettings.DefaultSensitivity; GameSettings.InvertY = false; GameSettings.Fov = 100f;
                GameSettings.AimAssist = 1f; GameSettings.ScreenShake = 1f; GameSettings.HitStop = true; GameSettings.HitMarkers = true; GameSettings.ToggleCrouch = false;
                break;
            case 1: InputSetup.ResetAll(); break;
            case 2: GameSettings.MasterVolume = 0.85f; GameSettings.MusicVolume = 0.6f; GameSettings.SfxVolume = 0.9f; GameSettings.UiVolume = 0.8f; break;
            case 3: GameSettings.Fullscreen = true; GameSettings.VSync = true; GameSettings.MaxFps = 0; GameSettings.Quality = 2; GameSettings.RenderScale = 1f; GameSettings.ShowFps = false; break;
        }
        GameSettings.Apply(GetViewport());
        ShowTab(tab);
    }

    void Section(string text)
    {
        var l = UiKit.Text(text, 22, UiKit.Sky, 6);
        list.AddChild(l);
    }

    HBoxContainer Row(string label)
    {
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 48) };
        row.AddThemeConstantOverride("separation", 18);
        var l = UiKit.Text(label, 24, Colors.White, 6);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        list.AddChild(row);
        return row;
    }

    void Slider(string label, float value, float min, float max, float step, Action<float> set, Func<float, string> show, bool apply = false)
    {
        var row = Row(label);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(320, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter };
        var v = UiKit.Text(show(value), 22, UiKit.Yellow, 6);
        v.CustomMinimumSize = new Vector2(140, 0);
        v.HorizontalAlignment = HorizontalAlignment.Right;
        v.VerticalAlignment = VerticalAlignment.Center;
        s.ValueChanged += x =>
        {
            set((float)x);
            v.Text = show((float)x);
            if (apply) GameSettings.Apply(GetViewport()); else GameSettings.Notify();
            if (label.StartsWith("Volume") || label == "Effets sonores") Sfx.Play(this, SfxKind.Tier, null, 1f, -6f);
        };
        UiKit.Juicy(s);
        row.AddChild(s);
        row.AddChild(v);
    }

    void Toggle(string label, bool value, Action<bool> set, bool apply = false)
    {
        var row = Row(label);
        var b = UiKit.Button(value ? "OUI" : "NON", value ? UiKit.Green.Darkened(0.2f) : UiKit.Grey, () => { }, 22, new Vector2(150, 46));
        b.Pressed += () =>
        {
            bool now = b.Text != "OUI";
            set(now);
            b.Text = now ? "OUI" : "NON";
            b.AddThemeStyleboxOverride("normal", UiKit.Box(now ? UiKit.Green.Darkened(0.2f) : UiKit.Grey, 14, -0.12f));
            b.AddThemeStyleboxOverride("hover", UiKit.Box((now ? UiKit.Green.Darkened(0.2f) : UiKit.Grey).Lightened(0.15f), 14, -0.12f, 3, UiKit.Yellow));
            if (apply) GameSettings.Apply(GetViewport()); else GameSettings.Notify();
        };
        row.AddChild(b);
    }

    void Choice(string label, string[] options, int selected, Action<int> set)
    {
        var row = Row(label);
        var o = new OptionButton { CustomMinimumSize = new Vector2(240, 46) };
        o.AddThemeFontSizeOverride("font_size", 22);
        foreach (var x in options) o.AddItem(x);
        o.Selected = Mathf.Clamp(selected, 0, options.Length - 1);
        o.ItemSelected += k => { set((int)k); GameSettings.Apply(GetViewport()); Sfx.Play(this, SfxKind.UiClick); };
        UiKit.Juicy(o);
        row.AddChild(o);
    }

    void KeyRow(InputSetup.ActionDef a)
    {
        var row = Row(a.Label);
        var b = UiKit.Button(InputSetup.KeyName(a.Name), UiKit.NavySoft, () => { }, 22, new Vector2(220, 46));
        b.Pressed += () =>
        {
            listening = a.Name;
            listeningButton = b;
            b.Text = "Appuie…";
            b.AddThemeStyleboxOverride("normal", UiKit.Box(UiKit.Pink, 14, -0.12f));
        };
        row.AddChild(b);
        if (a.Secondary != null)
        {
            var alt = UiKit.Text(InputSetup.Describe(a.Secondary), 20, new Color(1, 1, 1, 0.55f), 4);
            alt.CustomMinimumSize = new Vector2(110, 0);
            alt.VerticalAlignment = VerticalAlignment.Center;
            row.AddChild(alt);
        }
        else row.AddChild(new Control { CustomMinimumSize = new Vector2(110, 0) });
    }
}
