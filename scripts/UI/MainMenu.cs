using System;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Net;

namespace DogeballVR.UI;

/// <summary>
/// Menu principal plein écran par-dessus l'arène 3D ensoleillée :
/// - Barre de navigation horizontale épurée en haut (onglets clairs façon Overwatch 2 / The Finals / Rocket League).
/// - Carte blanche & orange flottante en bas à droite pour choisir la carte et lancer directement la partie.
/// - Zéro mur opaque à gauche : la scène 3D et les joueurs respirent en 100 % plein écran.
/// </summary>
public partial class MainMenu : CanvasLayer
{
    public event Action<NetMode, string>? Chosen;
    public event Action? TutorialChosen;
    /// <summary>Partie solo contre les bots sur la carte choisie (nom du fichier de maps/).</summary>
    public event Action<string>? SoloChosen;

    /// <summary>Actions en ligne (Steam / salle) : branchées par Main sur le module Online quand il est présent.</summary>
    public event Action? QuickMatch, CreatePrivate, HostLan;
    public event Action<string>? JoinCode, JoinAddress;

    /// <summary>Clavier virtuel (Steam : Deck, Big Picture) ; branché par Main.</summary>
    public static Func<string, string, int, Rect2I, Action<string?>, bool>? TextInput { get; set; }

    static readonly (string Map, string Name, string Format, Color Mood, string Info)[] SoloMaps =
    {
        ("solaris_dome", "SOLARIS DOME", "2V2", new Color("1f4d7a"), "Stade sous verrière : piliers Curve & murets Lob."),
        ("helios_concourse", "HELIOS CONCOURSE", "2V2", new Color("21415f"), "Grand hall ouvert EVA : couloirs libres & monolithes."),
        ("croix_neon", "CROIX NÉON", "2V2", new Color("274060"), "4 plateaux autour d'un moyeu avec rotor."),
        ("fosse_croisee", "FOSSE CROISÉE", "2V2", new Color("3b3159"), "Fosse centrale, tremplins et ring-outs."),
        ("tour_4v4", "TOUR 4V4", "4V4", new Color("5c2d3e"), "8 plaques autour d'une grande fosse 4c4."),
        ("la_fosse", "LA FOSSE", "2V2", new Color("233b4d"), "Zones face à face et plateaux élévateurs."),
        ("neon_court", "NÉON COURT", "2V2", new Color("162244"), "Arène nocturne néon en zones face à face."),
    };

    Control root = null!;
    Control homeGroup = null!;
    Control? overlay;
    ColorRect fade = null!;
    Button playBtn = null!;
    Label mapNameLabel = null!, mapFormatLabel = null!, mapSubLabel = null!;
    Panel mapBanner = null!;
    StyleBoxFlat mapBannerStyle = null!;
    TextureRect mapThumb = null!;
    int selectedMap;
    bool leaving;

    public override void _Ready()
    {
        Layer = 20;
        root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        homeGroup = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        homeGroup.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(homeGroup);

        BuildTopBar();
        BuildPlayCard();
        BuildFooter();

        fade = new ColorRect { Color = new Color(0, 0, 0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(fade);
        fade.CreateTween().TweenProperty(fade, "color:a", 0.0, 0.45);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        Music.PlayMenu();
        playBtn.CallDeferred(Control.MethodName.GrabFocus);

        if (Tools.DevFlags.Has("--profile-test")) ShowProfile();
        if (Tools.DevFlags.Has("--options-test")) OpenOverlay(new OptionsMenu { FirstTab = 1 });
        foreach (var arg in Tools.DevFlags.Args)
            if (arg.StartsWith("--menu-page="))
            {
                string page = arg["--menu-page=".Length..];
                GetTree().CreateTimer(1.0).Timeout += () =>
                {
                    if (page == "online") ShowOnline();
                    else if (page == "lan") ShowLan();
                    else if (page == "credits") ShowCredits();
                    else if (page == "first") { GameSettings.TutorialDone = false; PlaySelected(); }
                    else if (page == "maps") ShowMapPicker();
                };
            }
    }

    // ------------------------------------------------------------------ Barre de navigation supérieure (style AAA épuré)

    void BuildTopBar()
    {
        // Fine barre givrée claire en haut : laisse la scène 3D respirer à 100 %.
        var topBar = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        topBar.SetAnchorsPreset(Control.LayoutPreset.TopWide);
        topBar.OffsetTop = 22; topBar.OffsetBottom = 86; topBar.OffsetLeft = 36; topBar.OffsetRight = -36;
        homeGroup.AddChild(topBar);

        // Titre typographique minimaliste à gauche (sans badge/bouclier cartoon).
        var brand = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(0, 4) };
        brand.AddThemeConstantOverride("separation", 6);
        topBar.AddChild(brand);
        var b1 = UiKit.DisplayText("DODGE", 40, UiKit.InkDark, 0);
        b1.AddThemeColorOverride("font_shadow_color", new Color(1, 1, 1, 0.55f));
        b1.AddThemeConstantOverride("shadow_offset_y", 1);
        var b2 = UiKit.DisplayText("ARENA", 40, UiKit.Orange, 0);
        b2.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.25f));
        b2.AddThemeConstantOverride("shadow_offset_y", 2);
        brand.AddChild(b1);
        brand.AddChild(b2);

        // Pilule de navigation centrale blanche translucide avec onglets nets.
        var navCenter = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        navCenter.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        topBar.AddChild(navCenter);

        var navPill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var pillStyle = UiKit.Pill(new Color(0.97f, 0.98f, 1f, 0.80f), 14, 1, new Color(1f, 1f, 1f, 0.9f), 16, 0.14f);
        pillStyle.ContentMarginLeft = 8; pillStyle.ContentMarginRight = 8; pillStyle.ContentMarginTop = 6; pillStyle.ContentMarginBottom = 6;
        navPill.AddThemeStyleboxOverride("panel", pillStyle);
        navCenter.AddChild(navPill);

        var navRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        navRow.AddThemeConstantOverride("separation", 4);
        navPill.AddChild(navRow);

        navRow.AddChild(NavTab("JOUER", PlaySelected, active: true));
        navRow.AddChild(NavTab("ENTRAÎNEMENT", () => Leave(() => TutorialChosen?.Invoke())));
        navRow.AddChild(NavTab("EN LIGNE", ShowOnline));
        navRow.AddChild(NavTab("PROFIL", ShowProfile));
        navRow.AddChild(NavTab("OPTIONS", () => OpenOverlay(new OptionsMenu())));
        navRow.AddChild(NavTab("CRÉDITS", ShowCredits));

        // Bloc profil + Quitter discret en haut à droite.
        var rightBox = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        rightBox.AddThemeConstantOverride("separation", 10);
        rightBox.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        rightBox.OffsetLeft = -420; rightBox.OffsetRight = 0; rightBox.OffsetTop = 4; rightBox.OffsetBottom = 56;
        topBar.AddChild(rightBox);

        int xp = DogeballVR.Online.Progression.TotalXp;
        int lvl = DogeballVR.Online.Progression.LevelOf(xp).Level;
        var profilePill = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var pStyle = UiKit.Pill(new Color(0.97f, 0.98f, 1f, 0.84f), 12, 1, new Color(1f, 1f, 1f, 0.9f), 12, 0.12f);
        pStyle.ContentMarginLeft = 14; pStyle.ContentMarginRight = 16; pStyle.ContentMarginTop = 6; pStyle.ContentMarginBottom = 6;
        profilePill.AddThemeStyleboxOverride("panel", pStyle);
        var pRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        pRow.AddThemeConstantOverride("separation", 10);
        profilePill.AddChild(pRow);
        var avatarDot = new Panel { CustomMinimumSize = new Vector2(30, 30), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
        avatarDot.AddThemeStyleboxOverride("panel", UiKit.Pill(UiKit.Blue, 15, 0, null, 0));
        pRow.AddChild(avatarDot);
        var pCol = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        pCol.AddThemeConstantOverride("separation", -2);
        pRow.AddChild(pCol);
        pCol.AddChild(UiKit.DisplayText("JOUEUR", 18, UiKit.InkDark, 0));
        pCol.AddChild(UiKit.Text($"NIVEAU {lvl} · {xp} XP", 13, UiKit.Grey, 0));
        rightBox.AddChild(profilePill);

        var quit = NavTab("QUITTER", () => GetTree().Quit(), subtle: true);
        rightBox.AddChild(quit);
    }

    Button NavTab(string text, Action onPress, bool active = false, bool subtle = false)
    {
        var b = new Button
        {
            Text = text, FocusMode = Control.FocusModeEnum.All,
            CustomMinimumSize = new Vector2(0, 42),
        };
        if (UiKit.Display is Font df) b.AddThemeFontOverride("font", df);
        b.AddThemeFontSizeOverride("font_size", 21);

        StyleBox normal = active
            ? UiKit.Pill(Colors.White, 10, 0, null, 8, 0.14f)
            : subtle
                ? UiKit.Pill(new Color(0.97f, 0.98f, 1f, 0.72f), 10, 1, new Color(1f, 1f, 1f, 0.8f), 8, 0.1f)
                : new StyleBoxEmpty();
        normal.ContentMarginLeft = normal.ContentMarginRight = 18;
        normal.ContentMarginTop = normal.ContentMarginBottom = 6;

        var hover = UiKit.Pill(active ? Colors.White : new Color(1f, 1f, 1f, 0.92f), 10, 0, null, 8, 0.12f);
        hover.ContentMarginLeft = hover.ContentMarginRight = 18;
        hover.ContentMarginTop = hover.ContentMarginBottom = 6;

        var pressed = UiKit.Pill(UiKit.Orange, 10, 0, null, 4, 0.2f);
        pressed.ContentMarginLeft = pressed.ContentMarginRight = 18;
        pressed.ContentMarginTop = pressed.ContentMarginBottom = 6;

        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("hover", hover);
        b.AddThemeStyleboxOverride("focus", hover);
        b.AddThemeStyleboxOverride("pressed", pressed);
        b.AddThemeStyleboxOverride("hover_pressed", pressed);

        var restCol = active ? UiKit.InkDark : subtle ? UiKit.Grey : new Color("2b3242");
        b.AddThemeColorOverride("font_color", restCol);
        b.AddThemeColorOverride("font_hover_color", UiKit.Orange);
        b.AddThemeColorOverride("font_focus_color", UiKit.Orange);
        b.AddThemeColorOverride("font_pressed_color", Colors.White);
        b.AddThemeColorOverride("font_hover_pressed_color", Colors.White);
        b.AddThemeConstantOverride("outline_size", 0);
        b.MouseEntered += () => Sfx.Play(b, SfxKind.UiHover);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    // ------------------------------------------------------------------ Carte JOUER blanche & orange en bas à droite

    void BuildPlayCard()
    {
        var card = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Stop };
        var cardStyle = UiKit.Pill(new Color(0.98f, 0.99f, 1f, 0.96f), 20, 1, new Color(1f, 1f, 1f, 1f), 26, 0.24f);
        cardStyle.ContentMarginLeft = 18; cardStyle.ContentMarginRight = 18; cardStyle.ContentMarginTop = 16; cardStyle.ContentMarginBottom = 18;
        card.AddThemeStyleboxOverride("panel", cardStyle);
        card.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        card.OffsetRight = -42; card.OffsetBottom = -42; card.OffsetLeft = -420; card.OffsetTop = -340;
        homeGroup.AddChild(card);

        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 12);
        card.AddChild(col);

        // En-tête de la carte : "JOUER" + flèches de sélection d'arène
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(head);
        var hdr = UiKit.DisplayText("ARÈNE", 26, UiKit.InkDark, 0);
        hdr.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(hdr);
        head.AddChild(SmallSwitchBtn("‹", () => CycleMap(-1)));
        head.AddChild(SmallSwitchBtn("›", () => CycleMap(1)));

        // Vignette visuelle avec vraie capture 3D de la carte, découpée aux coins arrondis.
        mapBanner = new Panel
        {
            CustomMinimumSize = new Vector2(0, 128),
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ClipChildren = CanvasItem.ClipChildrenMode.AndDraw,
        };
        mapBannerStyle = UiKit.Pill(SoloMaps[0].Mood, 12, 0, null, 0);
        mapBanner.AddThemeStyleboxOverride("panel", mapBannerStyle);
        col.AddChild(mapBanner);

        mapThumb = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered,
        };
        mapThumb.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mapBanner.AddChild(mapThumb);

        // Dégradé sombre discret en bas pour que le nom et le sous-titre ressortent sur toutes les cartes (jour & nuit).
        var grad = new Gradient();
        grad.SetColor(0, new Color(0.05f, 0.07f, 0.12f, 0.08f));
        grad.SetColor(1, new Color(0.05f, 0.07f, 0.12f, 0.88f));
        var scrim = new TextureRect
        {
            MouseFilter = Control.MouseFilterEnum.Ignore,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            Texture = new GradientTexture2D { Gradient = grad, FillFrom = new Vector2(0, 0.15f), FillTo = new Vector2(0, 1f), Width = 4, Height = 64 },
        };
        scrim.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        mapBanner.AddChild(scrim);

        // Badge format 2V2 / 4V4 en haut à droite de la vignette
        var badgePanel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var bStyle = UiKit.Pill(Colors.White, 7, 0, null, 4, 0.22f);
        bStyle.ContentMarginLeft = 10; bStyle.ContentMarginRight = 10; bStyle.ContentMarginTop = 3; bStyle.ContentMarginBottom = 3;
        badgePanel.AddThemeStyleboxOverride("panel", bStyle);
        badgePanel.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        badgePanel.OffsetRight = -10; badgePanel.OffsetTop = 10; badgePanel.OffsetLeft = -70; badgePanel.OffsetBottom = 38;
        mapBanner.AddChild(badgePanel);
        mapFormatLabel = UiKit.DisplayText("2V2", 17, UiKit.InkDark, 0, HorizontalAlignment.Center);
        badgePanel.AddChild(mapFormatLabel);

        mapNameLabel = UiKit.DisplayText("CROIX NÉON", 28, Colors.White, 2);
        mapNameLabel.Position = new Vector2(14, 62);
        mapNameLabel.Size = new Vector2(310, 34);
        mapBanner.AddChild(mapNameLabel);

        mapSubLabel = UiKit.Text(SoloMaps[0].Info, 13, new Color(1f, 1f, 1f, 0.90f), 0);
        mapSubLabel.Position = new Vector2(14, 96);
        mapSubLabel.Size = new Vector2(316, 24);
        mapSubLabel.ClipText = true;
        mapBanner.AddChild(mapSubLabel);

        // Gros bouton tactile JOUER orange vif
        playBtn = new Button
        {
            Text = "JOUER", CustomMinimumSize = new Vector2(0, 64), FocusMode = Control.FocusModeEnum.All,
        };
        if (UiKit.Display is Font pdf) playBtn.AddThemeFontOverride("font", pdf);
        playBtn.AddThemeFontSizeOverride("font_size", 34);
        var ctaNormal = UiKit.Pill(UiKit.Orange, 13, 0, null, 12, 0.30f);
        var ctaHover = UiKit.Pill(UiKit.Orange.Lightened(0.08f), 13, 0, null, 16, 0.38f);
        var ctaPress = UiKit.Pill(UiKit.Orange.Darkened(0.12f), 13, 0, null, 6, 0.25f);
        playBtn.AddThemeStyleboxOverride("normal", ctaNormal);
        playBtn.AddThemeStyleboxOverride("hover", ctaHover);
        playBtn.AddThemeStyleboxOverride("focus", ctaHover);
        playBtn.AddThemeStyleboxOverride("pressed", ctaPress);
        playBtn.AddThemeStyleboxOverride("hover_pressed", ctaPress);
        foreach (var k in new[] { "font_color", "font_hover_color", "font_focus_color", "font_pressed_color", "font_hover_pressed_color" })
            playBtn.AddThemeColorOverride(k, Colors.White);
        playBtn.AddThemeConstantOverride("outline_size", 0);
        playBtn.MouseEntered += () => Sfx.Play(playBtn, SfxKind.UiHover);
        playBtn.Pressed += () => { Sfx.Play(playBtn, SfxKind.UiClick); PlaySelected(); };
        UiKit.Juicy(playBtn);
        col.AddChild(playBtn);

        RefreshMapCard();
    }

    Button SmallSwitchBtn(string glyph, Action onPress)
    {
        var b = new Button { Text = glyph, CustomMinimumSize = new Vector2(34, 34), FocusMode = Control.FocusModeEnum.All };
        if (UiKit.Display is Font df) b.AddThemeFontOverride("font", df);
        b.AddThemeFontSizeOverride("font_size", 24);
        var n = UiKit.Pill(new Color("e6eaf2"), 8, 0, null, 0);
        var h = UiKit.Pill(UiKit.Orange, 8, 0, null, 0);
        b.AddThemeStyleboxOverride("normal", n);
        b.AddThemeStyleboxOverride("hover", h);
        b.AddThemeStyleboxOverride("focus", h);
        b.AddThemeStyleboxOverride("pressed", h);
        b.AddThemeColorOverride("font_color", UiKit.InkDark);
        foreach (var k in new[] { "font_hover_color", "font_focus_color", "font_pressed_color" }) b.AddThemeColorOverride(k, Colors.White);
        b.AddThemeConstantOverride("outline_size", 0);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    void CycleMap(int delta)
    {
        selectedMap = (selectedMap + delta + SoloMaps.Length) % SoloMaps.Length;
        RefreshMapCard();
    }

    void RefreshMapCard()
    {
        var m = SoloMaps[selectedMap];
        mapNameLabel.Text = m.Name;
        mapFormatLabel.Text = m.Format;
        mapSubLabel.Text = m.Info;
        mapBannerStyle.BgColor = m.Mood;
        string thumbPath = $"res://assets/ui/maps/{m.Map}.png";
        mapThumb.Texture = ResourceLoader.Exists(thumbPath) ? GD.Load<Texture2D>(thumbPath) : null;
    }

    void BuildFooter()
    {
        string ver = ProjectSettings.GetSetting("application/config/version", "").AsString();
        var version = UiKit.Text($"EVA DEMO · v{(ver == "" ? "0.1" : ver)}", 15, new Color(1, 1, 1, 0.78f), 2);
        version.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        version.OffsetLeft = 36; version.OffsetTop = -40; version.OffsetRight = 320; version.OffsetBottom = -16;
        homeGroup.AddChild(version);
    }

    // ------------------------------------------------------------------ Sous-pages et modales

    void PlaySelected() => FirstMatch(() => Leave(() => SoloChosen?.Invoke(SoloMaps[selectedMap].Map)));

    void ShowProfile() => OpenOverlay(new ProfileScreen { Back = () => { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); } });

    static void PadKeyboard(LineEdit field, string description, Func<Control?> next)
    {
        field.GuiInput += e =>
        {
            if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A }) { OpenKeyboard(field, description, next); field.AcceptEvent(); }
        };
    }

    static bool OpenKeyboard(LineEdit field, string description, Func<Control?> next)
    {
        if (TextInput == null) return false;
        var xf = field.GetViewport().GetFinalTransform() * field.GetGlobalTransformWithCanvas();
        var r = xf * new Rect2(Vector2.Zero, field.Size);
        var rect = new Rect2I((Vector2I)r.Position.Round(), (Vector2I)r.Size.Round());
        return TextInput(description, field.Text, field.MaxLength > 0 ? field.MaxLength : 64, rect, text =>
        {
            if (!IsInstanceValid(field)) return;
            if (text != null) { field.Text = text; field.CaretColumn = text.Length; }
            next()?.GrabFocus();
        });
    }

    void ShowOnline()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        card.AddChild(col);
        col.AddChild(UiKit.Title("JOUER EN LIGNE", 44));
        var quick = UiKit.Button("PARTIE RAPIDE", UiKit.Orange, () => FirstMatch(() => Online(QuickMatch, () => Chosen?.Invoke(NetMode.Host, ""))), 26, new Vector2(460, 58));
        col.AddChild(quick);
        col.AddChild(UiKit.Button("CRÉER UNE SALLE PRIVÉE", UiKit.Blue, () => Online(CreatePrivate, () => Chosen?.Invoke(NetMode.Host, "")), 24, new Vector2(460, 54)));
        var code = new LineEdit { PlaceholderText = "Code de la salle", CustomMinimumSize = new Vector2(460, 50), Alignment = HorizontalAlignment.Center, MaxLength = 12 };
        col.AddChild(code);
        Button? join = null;
        PadKeyboard(code, "Code de la salle", () => join);
        join = UiKit.Button("REJOINDRE AVEC LE CODE", UiKit.Blue, () =>
        {
            var c = code.Text.Trim().ToUpperInvariant();
            if (c == "") { code.GrabFocus(); if (InputSetup.UsingPad) OpenKeyboard(code, "Code de la salle", () => join); return; }
            if (JoinCode != null) Leave(() => JoinCode(c));
        }, 24, new Vector2(460, 54));
        col.AddChild(join);
        col.AddChild(UiKit.Button("RÉSEAU LOCAL (LAN / EVA)", UiKit.Grey, ShowLan, 22, new Vector2(460, 50)));
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(UiKit.Button("RETOUR", UiKit.Grey, CloseOverlay, 22, new Vector2(460, 48)));
        OpenOverlay(Centered(card));
        quick.CallDeferred(Control.MethodName.GrabFocus);
    }

    void ShowLan()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 12);
        card.AddChild(col);
        col.AddChild(UiKit.Title("RÉSEAU LOCAL (EVA)", 44));
        var host = UiKit.Button("HÉBERGER LA PARTIE", UiKit.Orange, () => Online(HostLan, () => Chosen?.Invoke(NetMode.Host, "")), 26, new Vector2(460, 58));
        col.AddChild(host);
        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Adresse IP de l'hôte", CustomMinimumSize = new Vector2(460, 50), Alignment = HorizontalAlignment.Center };
        col.AddChild(address);
        Button? join = null;
        PadKeyboard(address, "Adresse IP de l'hôte", () => join);
        join = UiKit.Button("REJOINDRE L'HÔTE", UiKit.Blue, () =>
        {
            var ip = address.Text.Trim();
            if (JoinAddress != null) Leave(() => JoinAddress(ip)); else Leave(() => Chosen?.Invoke(NetMode.Client, ip));
        }, 24, new Vector2(460, 54));
        col.AddChild(join);
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(UiKit.Button("RETOUR", UiKit.Grey, ShowOnline, 22, new Vector2(460, 48)));
        OpenOverlay(Centered(card));
        host.CallDeferred(Control.MethodName.GrabFocus);
    }

    void Online(Action? handler, Action fallback) => Leave(handler ?? fallback);

    void FirstMatch(Action play)
    {
        if (GameSettings.TutorialDone) { play(); return; }
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        card.AddChild(col);
        col.AddChild(UiKit.Title("PREMIÈRE PARTIE ?", 44));
        col.AddChild(UiKit.Text("L'entraînement t'apprend à lancer, attraper et esquiver en deux minutes.", 21, UiKit.Lavender, 0, HorizontalAlignment.Center));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 14);
        col.AddChild(row);
        var yes = UiKit.Button("ENTRAÎNEMENT", UiKit.Orange, () => Leave(() => TutorialChosen?.Invoke()), 26, new Vector2(280, 58));
        row.AddChild(yes);
        row.AddChild(UiKit.Button("JOUER DIRECT", UiKit.Blue, () =>
        {
            GameSettings.TutorialDone = true;
            GameSettings.Save();
            play();
        }, 26, new Vector2(280, 58)));
        OpenOverlay(Centered(card));
        yes.CallDeferred(Control.MethodName.GrabFocus);
    }

    void ShowMapPicker()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        card.AddChild(col);
        col.AddChild(UiKit.Title("CHOISIS L'ARÈNE", 44));
        var info = UiKit.Text("", 20, UiKit.Lavender, 0, HorizontalAlignment.Center);
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.CustomMinimumSize = new Vector2(520, 56);
        Button? first = null;
        foreach (var (map, name, format, _, text) in SoloMaps)
        {
            var b = UiKit.ListButton($"{name}  ·  {format}", () => Leave(() => SoloChosen?.Invoke(map)), 26, new Vector2(520, 54));
            b.FocusEntered += () => info.Text = text;
            col.AddChild(b);
            first ??= b;
        }
        col.AddChild(info);
        OpenOverlay(Centered(card));
        first?.CallDeferred(Control.MethodName.GrabFocus);
    }

    void ShowCredits()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        card.AddChild(col);
        col.AddChild(UiKit.Title("CRÉDITS", 46));
        foreach (var (who, what) in new[]
        {
            ("Conception et direction", "jules"),
            ("Moteur", "Godot Engine 4.3 (MIT)"),
            ("Polices", "Barlow & Barlow Condensed (SIL OFL)"),
            ("Modèles 3D", "faits maison avec Blender"),
            ("Sons", "voir assets/audio/CREDITS.md"),
            ("Inspiration", "Knockout City (Velan Studios)"),
        })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 20);
            var a = UiKit.Text(who, 20, UiKit.Sky, 0, HorizontalAlignment.Right);
            a.CustomMinimumSize = new Vector2(280, 0);
            row.AddChild(a);
            row.AddChild(UiKit.Text(what, 20, Colors.White, 0));
            col.AddChild(row);
        }
        var backBtn = UiKit.Button("RETOUR", UiKit.Orange, CloseOverlay, 24, new Vector2(260, 52));
        backBtn.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        col.AddChild(UiKit.Spacer(8));
        col.AddChild(backBtn);
        OpenOverlay(Centered(card));
        backBtn.CallDeferred(Control.MethodName.GrabFocus);
    }

    static Control Centered(Control card)
    {
        var holder = new Control();
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(UiKit.Dim(0.48f));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(center);
        center.AddChild(card);
        UiKit.Punch(card, 0.92f, 0.2);
        return holder;
    }

    void OpenOverlay(Control c)
    {
        CloseOverlay();
        homeGroup.Visible = false;
        overlay = c;
        if (c is OptionsMenu o) o.Closed += () => { overlay = null; homeGroup.Visible = true; playBtn.CallDeferred(Control.MethodName.GrabFocus); };
        root.AddChild(c);
        root.MoveChild(fade, -1);
    }

    void CloseOverlay()
    {
        if (overlay is OptionsMenu) return;
        overlay?.QueueFree();
        overlay = null;
        homeGroup.Visible = true;
        playBtn?.CallDeferred(Control.MethodName.GrabFocus);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (overlay is not OptionsMenu && !leaving && GetViewport().GuiGetFocusOwner() is not LineEdit)
            foreach (var (move, ui) in new[] { ("move_forward", "ui_up"), ("move_back", "ui_down"), ("move_left", "ui_left"), ("move_right", "ui_right") })
                if (e is InputEventKey && e.IsActionPressed(move, true))
                {
                    Input.ParseInputEvent(new InputEventAction { Action = ui, Pressed = true });
                    Input.ParseInputEvent(new InputEventAction { Action = ui, Pressed = false });
                    GetViewport().SetInputAsHandled();
                    return;
                }
        if (!e.IsActionPressed("pause") && !e.IsActionPressed("ui_cancel")) return;
        if (overlay != null && overlay is not OptionsMenu) { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); GetViewport().SetInputAsHandled(); }
    }

    void Leave(Action then)
    {
        if (leaving) return;
        leaving = true;
        var tw = fade.CreateTween();
        tw.TweenProperty(fade, "color:a", 1.0, 0.32);
        tw.TweenCallback(Callable.From(then));
    }
}
