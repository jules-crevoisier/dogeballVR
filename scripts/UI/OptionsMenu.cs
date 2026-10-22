using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Écran d'options (menu principal et pause) : quatre onglets, Contrôles, Touches, Audio, Vidéo.
/// Tout s'applique en direct ; l'enregistrement se fait en quittant l'écran. La ligne survolée (ou sélectionnée au clavier)
/// s'allume en entier et sa description s'affiche en bas du panneau, comme dans Knockout City.
/// </summary>
public partial class OptionsMenu : Control
{
    public event Action? Closed;

    VBoxContainer list = null!;
    PanelContainer panel = null!;
    Control holder = null!;
    MarginContainer margin = null!;
    const float TopMargin = 48f;
    ScrollContainer scroll = null!;
    Label desc = null!;
    readonly System.Collections.Generic.List<(PanelContainer row, string text)> rows = new();
    PanelContainer? lit;
    TextureRect moreBelow = null!;
    readonly Button[] tabs = new Button[4];
    int tab;
    string? listening;          // action en attente d'une nouvelle touche
    Button? listeningButton;

    static readonly string[] TabNames = { "CONTRÔLES", "TOUCHES", "AUDIO", "VIDÉO" };
    /// <summary>Texte du bas quand aucune ligne n'est survolée.</summary>
    static readonly string[] TabHints =
    {
        "Survole une option pour voir ce qu'elle fait.",
        "Clique sur une touche puis appuie sur la nouvelle (Échap annule).",
        "Les volumes s'appliquent tout de suite.",
        "Les réglages vidéo s'appliquent tout de suite.",
    };

    /// <summary>Description de chaque option (en bas du panneau), par libellé : une ligne ajoutée ailleurs y met la sienne.</summary>
    static readonly System.Collections.Generic.Dictionary<string, string> Help = new()
    {
        ["Sensibilité"] = "Vitesse de la caméra quand tu bouges la souris. 1,00× = réglage d'origine.",
        ["Inverser l'axe vertical"] = "Pousser la souris vers l'avant fait regarder vers le bas, comme un manche d'avion.",
        ["Champ de vision"] = "Angle de vue horizontal. Plus large : tu vois mieux autour de toi, mais tout paraît plus loin.",
        ["Aide à la visée"] = "Balle en main, la visée ralentit sur un adversaire et ton lancer s'infléchit vers lui. 0 % : aucune aide.",
        ["Secousses de l'écran"] = "Force des tremblements de caméra aux impacts, aux KO et aux explosions.",
        ["Arrêt sur image aux impacts"] = "Le jeu se fige une fraction de seconde quand une balle touche ou est attrapée, pour bien sentir le choc.",
        ["Marqueur de touche"] = "Une marque s'affiche sur le viseur quand ta balle touche un adversaire.",
        ["S'accroupir : appui = bascule"] = "OUI : un appui pour s'accroupir, un autre pour se relever. NON : tu restes accroupi tant que la touche est tenue.",
        ["Volume général"] = "Volume de tout le jeu.",
        ["Musique"] = "Musique des menus et des parties.",
        ["Effets sonores"] = "Lancers, impacts, foule et annonces.",
        ["Interface"] = "Sons des menus : survol et clic.",
        ["Plein écran"] = "Le jeu occupe tout l'écran. NON : fenêtre redimensionnable.",
        ["Synchronisation verticale"] = "Supprime les déchirures de l'image en calant les images sur l'écran, au prix d'un peu de latence.",
        ["Images par seconde max"] = "Plafond d'images par seconde : utile pour limiter la chauffe d'un portable.",
        ["Qualité graphique"] = "Ombres, lumière et effets. Basse : pour les machines modestes.",
        ["Résolution de rendu"] = "Calcule l'image en plus petit puis l'agrandit : plus fluide, un peu plus flou.",
        ["Afficher les images par seconde"] = "Compteur d'images par seconde dans un coin de l'écran.",
        ["Taille du texte et de l'interface"] = "Agrandit tout le texte et l'interface, menus et HUD compris.",
        ["Réduire les flashs"] = "Atténue les éclairs blancs et les clignotements (impacts, KO, explosions).",
        ["Symboles d'équipe (daltonisme)"] = "Ajoute un symbole à la couleur de chaque équipe, pour ne pas dépendre de l'orange et du bleu.",
    };

    readonly KcBox rowIdle = RowBox(false), rowLit = RowBox(true);

    /// <summary>Ligne d'option : transparente au repos ; allumée, un aplat violet penché avec l'accent jaune à gauche (comme le menu).</summary>
    static KcBox RowBox(bool on)
    {
        var fill = on ? new Color(UiKit.Violet, 0.55f) : new Color(0, 0, 0, 0);
        var b = new KcBox { Top = fill, Bottom = on ? new Color(UiKit.VioletMid, 0.55f) : fill, Slant = 8, AccentColor = on ? UiKit.Yellow : new Color(0, 0, 0, 0), AccentWidth = 7 };
        b.ContentMarginLeft = 22; b.ContentMarginRight = 12; b.ContentMarginTop = 2; b.ContentMarginBottom = 2;
        return b;
    }

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        ProcessMode = ProcessModeEnum.Always;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(UiKit.Dim(0.82f));

        // Panneau centré en largeur, posé à TopMargin du haut : il prend la hauteur de son contenu (les onglets ne bougent pas
        // d'un onglet à l'autre, seul le bas suit), jusqu'à la hauteur de l'écran ; au-delà, la liste défile (voir FitHeight).
        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.TopWide);
        center.OffsetTop = center.OffsetBottom = TopMargin;
        AddChild(center);
        panel = new PanelContainer { CustomMinimumSize = new Vector2(1120, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelBox(22, 34));   // opaque : la fenêtre du menu principal
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        panel.AddChild(col);

        col.AddChild(UiKit.Title("OPTIONS", 56));
        var tabRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        tabRow.AddThemeConstantOverride("separation", 10);
        col.AddChild(tabRow);
        for (int i = 0; i < 4; i++)
        {
            int k = i;
            tabs[i] = UiKit.Button(TabNames[i], UiKit.NavySoft, () => ShowTab(k), 22, new Vector2(200, 50));
            tabRow.AddChild(tabs[i]);
        }

        // Zone de défilement à la hauteur de la liste, plafonnée par l'écran ; si la liste dépasse (onglet Touches, interface
        // agrandie), un fondu en bas annonce la suite au lieu d'une ligne coupée net.
        holder = new Control { CustomMinimumSize = new Vector2(1060, 160) };
        col.AddChild(holder);
        scroll = new ScrollContainer { HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        scroll.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        holder.AddChild(scroll);
        var fadeGrad = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(UiKit.NavyLow, 0f), UiKit.NavyLow } };
        moreBelow = new TextureRect { Texture = new GradientTexture2D { Gradient = fadeGrad, Width = 4, Height = 64, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0, 1) }, StretchMode = TextureRect.StretchModeEnum.Scale, MouseFilter = MouseFilterEnum.Ignore };
        moreBelow.SetAnchorsPreset(LayoutPreset.BottomWide);
        moreBelow.OffsetTop = -56; moreBelow.OffsetRight = -24;
        holder.AddChild(moreBelow);
        // Marge à droite : les valeurs et les boutons ne touchent pas la barre de défilement.
        margin = new MarginContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        margin.AddThemeConstantOverride("margin_right", 28);
        margin.AddThemeConstantOverride("margin_left", 4);
        margin.AddThemeConstantOverride("margin_bottom", 6);
        scroll.AddChild(margin);
        list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 4);
        margin.AddChild(list);

        // Description de l'option survolée (ou sélectionnée au clavier), en bas du panneau.
        var descBox = new PanelContainer { CustomMinimumSize = new Vector2(0, 66) };
        var db = UiKit.InsetBox(null, 10);
        descBox.AddThemeStyleboxOverride("panel", db);
        col.AddChild(descBox);
        desc = UiKit.Text("", 22, UiKit.Cream, 5);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.VerticalAlignment = VerticalAlignment.Center;
        desc.SizeFlagsVertical = SizeFlags.Fill;
        descBox.AddChild(desc);

        var bottom = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        bottom.AddThemeConstantOverride("separation", 16);
        col.AddChild(bottom);
        col.AddChild(UiKit.Spacer(4));
        bottom.AddChild(UiKit.Button("PAR DÉFAUT", UiKit.Grey, ResetTab, 24, new Vector2(260, 56)));
        var back = UiKit.Button("RETOUR", UiKit.Orange, Close, 26, new Vector2(300, 56));
        bottom.AddChild(back);
        ShowTab(0);
        back.CallDeferred(Control.MethodName.GrabFocus);
        UiKit.Punch(panel, 0.9f, 0.2);
    }

    /// <summary>
    /// Hauteur de la zone de défilement : celle de la liste de l'onglet, sans dépasser l'écran (marge TopMargin en haut et en bas,
    /// quel que soit le facteur d'échelle de l'interface). Une ligne ajoutée à un onglet agrandit le panneau d'elle-même.
    /// </summary>
    void FitHeight()
    {
        float others = panel.Size.Y - holder.Size.Y;   // titre, onglets, description, boutons et marges du panneau
        float room = GetViewportRect().Size.Y - 2f * TopMargin - others;
        float want = Mathf.Max(120f, Mathf.Min(margin.GetCombinedMinimumSize().Y, room));
        if (Mathf.Abs(holder.CustomMinimumSize.Y - want) < 0.5f) return;
        holder.CustomMinimumSize = new Vector2(holder.CustomMinimumSize.X, want);
        panel.ResetSize();   // le panneau rétrécit aussi (un conteneur ne fait que grandir de lui-même)
    }

    void Close()
    {
        GameSettings.Save();
        Closed?.Invoke();
        QueueFree();
    }

    /// <summary>Ligne allumée : celle sous la souris (dans la zone visible), sinon celle qui contient le focus clavier.</summary>
    public override void _Process(double delta)
    {
        FitHeight();
        var bar = scroll.GetVScrollBar();
        moreBelow.Visible = bar.MaxValue - bar.Page - scroll.ScrollVertical > 4;
        PanelContainer? want = null;
        var mouse = GetGlobalMousePosition();
        if (scroll.GetGlobalRect().HasPoint(mouse))
            foreach (var (row, _) in rows)
                if (IsInstanceValid(row) && row.GetGlobalRect().HasPoint(mouse)) { want = row; break; }
        if (want == null && GetViewport().GuiGetFocusOwner() is Control f)
            foreach (var (row, _) in rows)
                if (IsInstanceValid(row) && row.IsAncestorOf(f)) { want = row; break; }
        if (want == lit) return;
        if (lit != null && IsInstanceValid(lit)) lit.AddThemeStyleboxOverride("panel", rowIdle);
        lit = want;
        lit?.AddThemeStyleboxOverride("panel", rowLit);
        desc.Text = lit == null ? TabHints[tab] : rows.Find(r => r.row == lit).text;
    }

    public override void _Input(InputEvent e)
    {
        if (listening != null)
        {
            bool key = e is InputEventKey { Pressed: true, Echo: false };
            bool mouse = e is InputEventMouseButton { Pressed: true } mb && mb.ButtonIndex is not (MouseButton.WheelUp or MouseButton.WheelDown);
            if (!key && !mouse) return;
            GetViewport().SetInputAsHandled();
            if (e is InputEventKey { PhysicalKeycode: Key.Escape } && listening != "pause") { StopListening(); ShowTab(1); return; }   // remet la touche affichée
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
            UiKit.Tint(tabs[k], k == i ? UiKit.Yellow : new Color(0, 0, 0, 0), k == i ? UiKit.Violet : null, k == i ? UiKit.Yellow : null);   // onglet ouvert : violet clair, accent et texte jaunes
        foreach (var c in list.GetChildren()) c.QueueFree();
        rows.Clear();
        lit = null;
        desc.Text = TabHints[i];
        switch (i)
        {
            case 0:
                Section("Souris", true);
                Slider("Sensibilité", GameSettings.Sensitivity / GameSettings.DefaultSensitivity, 0.25f, 4f, 0.05f, v => GameSettings.Sensitivity = v * GameSettings.DefaultSensitivity, v => $"{v.ToString("0.00", System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))}×");
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
                GameSettings.Sensitivity = GameSettings.DefaultSensitivity; GameSettings.InvertY = false; GameSettings.Fov = GameSettings.DefaultFov;
                GameSettings.AimAssist = 1f; GameSettings.ScreenShake = 1f; GameSettings.HitStop = true; GameSettings.HitMarkers = true; GameSettings.ToggleCrouch = false;
                break;
            case 1: InputSetup.ResetAll(); break;
            case 2: GameSettings.MasterVolume = 0.85f; GameSettings.MusicVolume = 0.6f; GameSettings.SfxVolume = 0.9f; GameSettings.UiVolume = 0.8f; break;
            case 3: GameSettings.Fullscreen = true; GameSettings.VSync = true; GameSettings.MaxFps = 0; GameSettings.Quality = 2; GameSettings.RenderScale = 1f; GameSettings.ShowFps = false; break;
        }
        GameSettings.Apply(GetViewport());
        ShowTab(tab);
    }

    /// <summary>Intertitre : gros, crème, penché, suivi d'un filet jaune jusqu'au bord.</summary>
    void Section(string text, bool first = false)
    {
        if (!first) list.AddChild(UiKit.Spacer(8));
        var h = new HBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 16);
        var l = UiKit.Text(text.ToUpperInvariant(), 30, UiKit.Cream, 8);
        if (UiKit.Italic is Font it) l.AddThemeFontOverride("font", it);
        h.AddChild(l);
        var rule = new ColorRect { Color = new Color(UiKit.Yellow, 0.55f), CustomMinimumSize = new Vector2(0, 3), SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsVertical = SizeFlags.ShrinkCenter, MouseFilter = MouseFilterEnum.Ignore };
        h.AddChild(rule);
        list.AddChild(h);
    }

    /// <summary>Ligne d'option : fond allumé en entier au survol ; le texte d'aide va dans la description du bas.</summary>
    HBoxContainer Row(string label, string help, out PanelContainer panel)
    {
        panel = new PanelContainer { MouseFilter = MouseFilterEnum.Pass };
        panel.AddThemeStyleboxOverride("panel", rowIdle);
        var row = new HBoxContainer { CustomMinimumSize = new Vector2(0, 46) };
        row.AddThemeConstantOverride("separation", 18);
        var l = UiKit.Text(label, 24, Colors.White, 6);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        panel.AddChild(row);
        list.AddChild(panel);
        rows.Add((panel, help));
        return row;
    }

    HBoxContainer Row(string label) => Row(label, Help.GetValueOrDefault(label, ""), out _);
    HBoxContainer Row(string label, string help) => Row(label, help, out _);

    void Slider(string label, float value, float min, float max, float step, Action<float> set, Func<float, string> show, bool apply = false)
    {
        var row = Row(label);
        var s = new HSlider { MinValue = min, MaxValue = max, Step = step, Value = value, CustomMinimumSize = new Vector2(320, 30), SizeFlagsVertical = SizeFlags.ShrinkCenter, SizeFlagsHorizontal = SizeFlags.ExpandFill, SizeFlagsStretchRatio = 1.1f };
        var v = UiKit.Text(show(value), 22, UiKit.Yellow, 6);
        v.CustomMinimumSize = new Vector2(150, 0);
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

    /// <summary>Interrupteur OUI / NON : un clic n'importe où sur la ligne le bascule aussi.</summary>
    void Toggle(string label, bool value, Action<bool> set, bool apply = false)
    {
        var row = Row(label, Help.GetValueOrDefault(label, ""), out var panel);
        var b = UiKit.Button(value ? "OUI" : "NON", UiKit.NavySoft, () => { }, 22, new Vector2(150, 44));
        void Paint(bool on) => UiKit.Tint(b, on ? UiKit.Green : new Color(0, 0, 0, 0), on ? new Color("1f6b45") : null, on ? Colors.White : UiKit.Lavender);
        Paint(value);
        b.Pressed += () =>
        {
            bool now = b.Text != "OUI";
            set(now);
            b.Text = now ? "OUI" : "NON";
            Paint(now);
            if (apply) GameSettings.Apply(GetViewport()); else GameSettings.Notify();
        };
        panel.GuiInput += e =>
        {
            if (e is not InputEventMouseButton { Pressed: true, ButtonIndex: MouseButton.Left }) return;
            b.GrabFocus();
            b.EmitSignal(BaseButton.SignalName.Pressed);
        };
        row.AddChild(b);
    }

    void Choice(string label, string[] options, int selected, Action<int> set)
    {
        var row = Row(label);
        var o = new OptionButton { CustomMinimumSize = new Vector2(240, 44) };
        o.AddThemeFontSizeOverride("font_size", 22);
        foreach (var x in options) o.AddItem(x);
        o.Selected = Mathf.Clamp(selected, 0, options.Length - 1);
        o.ItemSelected += k => { set((int)k); GameSettings.Apply(GetViewport()); Sfx.Play(this, SfxKind.UiClick); };
        UiKit.Juicy(o);
        row.AddChild(o);
    }

    void KeyRow(InputSetup.ActionDef a)
    {
        string help = a.Secondary != null
            ? $"Clique puis appuie sur la nouvelle touche. {InputSetup.Describe(a.Secondary)} marche aussi (touche fixe)."
            : "Clique puis appuie sur la nouvelle touche. Échap annule.";
        var row = Row(a.Label, help);
        var b = UiKit.Button(InputSetup.KeyName(a.Name), UiKit.NavySoft, () => { }, 22, new Vector2(220, 44));
        b.Pressed += () =>
        {
            listening = a.Name;
            listeningButton = b;
            b.Text = "Appuie…";
            UiKit.Tint(b, UiKit.Pink, UiKit.Pink.Darkened(0.35f));
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
