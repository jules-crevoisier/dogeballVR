using System;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Net;

namespace DogeballVR.UI;

/// <summary>
/// Menu principal du mode PC, par-dessus le décor 3D : grand bandeau diagonal plein à gauche (~40 % de l'écran, les
/// personnages occupent la droite), logo, items en grosses lettres penchées surlignés par une barre jaune inclinée qui
/// glisse de l'un à l'autre, comme le menu de Knockout City ; pages Jouer en ligne, Options et Crédits. À la toute
/// première partie, on propose le tutoriel.
/// </summary>
public partial class MainMenu : CanvasLayer
{
    public event Action<NetMode, string>? Chosen;
    public event Action? TutorialChosen;
    /// <summary>Partie solo contre les bots sur la carte choisie (nom du fichier de maps/).</summary>
    public event Action<string>? SoloChosen;

    /// <summary>Cartes proposées en solo, dans l'ordre du sélecteur. Zones : terrain en zones face à face (comme la salle VR), sinon arène ouverte.</summary>
    static readonly (string Map, string Name, string Players, bool Zones, string Info)[] SoloMaps =
    {
        ("croix_neon", "CROIX NÉON", "2 CONTRE 2", false, "Quatre plateaux autour d'un moyeu, le rotor balaie le centre."),
        ("fosse_croisee", "FOSSE CROISÉE", "2 CONTRE 2", false, "Le moulinet passe au-dessus d'une fosse : un tacle peut y pousser l'adversaire."),
        ("tour_4v4", "TOUR 4V4", "4 CONTRE 4", false, "Terrasses, fosse et rotor : la plus grande carte."),
        ("la_fosse", "LA FOSSE", "2 CONTRE 2", true, "Zones face à face : chacun garde la sienne, E pour esquiver."),
        ("neon_court", "NÉON COURT", "2 CONTRE 2", true, "Terrain court en zones : chacun garde la sienne, E pour esquiver."),
    };

    Control root = null!;
    VBoxContainer column = null!;
    Control? overlay;
    ColorRect fade = null!;
    MenuBand band = null!;
    SelectBar bar = null!;
    Control? barOn;   // item sous la barre jaune (null : barre cachée)
    Tween? barTween;
    Control escHint = null!;
    Action? back;   // page précédente (Échap) : null sur le menu racine
    bool leaving, compact;

    const string ItemMeta = "menu_item";
    const string FontMeta = "menu_font";
    const float BarLead = 46f;   // la barre commence un peu à gauche du texte
    static readonly string[] FontColors = { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" };

    public override void _Ready()
    {
        Layer = 20;
        root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Écran bas (interface agrandie : 1080 / 1,3 ≈ 830 px utiles) : logo réduit, colonne remontée, le pied de page reste dégagé.
        compact = root.GetViewportRect().Size.Y < 960f;
        float top = compact ? -44f : 0f;

        // Page pleine (DA v3, comme les menus de KC) : fond turquoise rayé, panneau violet et héros en pied à droite. Elle
        // couvre la rue 3D, qui reste derrière pour les autres pages.
        var stage = new MenuStage { MouseFilter = Control.MouseFilterEnum.Ignore };
        stage.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        stage.Resized += stage.QueueRedraw;
        root.AddChild(stage);
        var heroBox = new MenuHero();
        heroBox.AnchorLeft = 0.40f; heroBox.AnchorRight = 0.80f; heroBox.AnchorTop = -0.08f; heroBox.AnchorBottom = 1.02f;   // plus grand que son panneau : la tête en sort, comme chez KC
        heroBox.OffsetLeft = heroBox.OffsetRight = heroBox.OffsetTop = heroBox.OffsetBottom = 0;
        root.AddChild(heroBox);
        UiKit.SlideIn(heroBox, 0f, 200f);

        // Grand bandeau plein qui part du bord gauche et monte en biais : il porte le logo et les items.
        band = new MenuBand { MouseFilter = Control.MouseFilterEnum.Ignore };
        band.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        band.Resized += band.QueueRedraw;
        root.AddChild(band);
        UiKit.SlideIn(band, 0f, -260f);

        // Logo penché, en autocollant : ombre marine, contour crème épais, puis le texte orange cerné de marine.
        var logoScale = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Scale = Vector2.One * (compact ? 0.82f : 1f) };   // le logo « tape » (Punch remet son échelle à 1) : on réduit son parent
        root.AddChild(logoScale);
        var logoBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(72, compact ? 30 : 42), Size = new Vector2(700, 210), RotationDegrees = -6f };
        logoScale.AddChild(logoBox);
        // Logo en Barlow Condensed Black Italic (DA v2) : ombre encre décalée, épais contour crème d'autocollant, lettres orange
        // à fin liseré encre. Condensée : le mot tient en 150 px de haut sur la largeur du bandeau.
        void LogoLayer(Color color, int outline, Color outlineColor, Vector2 at)
        {
            var l = UiKit.Text("DOGEBALL", 124, color, 0);
            l.AddThemeConstantOverride("outline_size", outline);
            l.AddThemeColorOverride("font_outline_color", outlineColor);
            if (UiKit.Heavy is Font d) l.AddThemeFontOverride("font", d);
            l.Position = at;
            logoBox.AddChild(l);
        }
        LogoLayer(UiKit.InkText, 40, UiKit.InkText, new Vector2(10, 12));
        LogoLayer(UiKit.Cream, 40, UiKit.Cream, Vector2.Zero);
        LogoLayer(UiKit.Orange, 8, UiKit.InkText, Vector2.Zero);
        var tag = UiKit.Text("ESQUIVE · ATTRAPE · RENVOIE", 34, UiKit.Yellow, 6);
        if (UiKit.Italic is Font ti) tag.AddThemeFontOverride("font", ti);
        tag.AddThemeConstantOverride("outline_size", 0);
        tag.Position = new Vector2(26, 150);
        logoBox.AddChild(tag);
        UiKit.Punch(logoBox, 1.3f, 0.45);
        // Le logo respire doucement.
        var bob = logoBox.CreateTween().SetLoops();
        bob.TweenProperty(logoBox, "rotation_degrees", -4.5f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bob.TweenProperty(logoBox, "rotation_degrees", -6f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

        // Aides clavier en bas à gauche, en touches dessinées (« [Entrée] Valider  [Échap] Retour »).
        var hints = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        hints.AddThemeConstantOverride("separation", 36);
        hints.AddChild(UiKit.KeyHint("Entrée", "Valider", 24));
        escHint = UiKit.KeyHint(InputSetup.KeyName("pause"), "Retour", 24);   // sous-menus seulement : le menu racine n'a pas de retour
        escHint.Visible = false;
        hints.AddChild(escHint);
        hints.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        hints.GrowVertical = Control.GrowDirection.Begin;
        hints.OffsetLeft = 72; hints.OffsetTop = -36; hints.OffsetRight = 900; hints.OffsetBottom = -36;
        root.AddChild(hints);

        // Plaque du joueur en bas du bandeau, comme le niveau de KC en bas à gauche du menu : badge du niveau, pseudo, titre
        // et barre d'XP vers le niveau suivant.
        if (!compact) { root.AddChild(profilePlate = ProfilePlate()); root.AddChild(dayCard = MapOfTheDay()); }

        // Barre jaune de sélection, sous les items (elle suit le focus dans _Process).
        bar = new SelectBar { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        root.AddChild(bar);

        column = new VBoxContainer { Position = new Vector2(110, 300 + top) };
        column.AddThemeConstantOverride("separation", compact ? 10 : 14);
        root.AddChild(column);

        string ver = ProjectSettings.GetSetting("application/config/version", "").AsString();
        var version = UiKit.Text($"ACCÈS ANTICIPÉ · V{(ver == "" ? "0.1" : ver)}", 22, UiKit.InkText, 0, HorizontalAlignment.Right);   // sur la bande turquoise du bas
        if (UiKit.Italic is Font vi) version.AddThemeFontOverride("font", vi);
        version.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        version.OffsetLeft = -700; version.OffsetTop = -44; version.OffsetRight = -24; version.OffsetBottom = -16;
        root.AddChild(version);

        fade = new ColorRect { Color = new Color(0, 0, 0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(fade);
        fade.CreateTween().TweenProperty(fade, "color:a", 0.0, 0.6);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        Music.PlayMenu();
        ShowMain();
        if (Tools.DevFlags.Has("--profile-test")) ShowProfile();   // captures
        if (Tools.DevFlags.Has("--options-test")) OpenOverlay(new OptionsMenu { FirstTab = 1 });
        // Captures d'écran : --menu-page=online | lan | credits | first | maps ouvre la page au bout d'une seconde.
        foreach (var arg in Tools.DevFlags.Args)
            if (arg.StartsWith("--menu-page="))
            {
                string page = arg["--menu-page=".Length..];
                GetTree().CreateTimer(1.0).Timeout += () =>
                {
                    if (page == "online") ShowOnline();
                    else if (page == "lan") ShowLan();
                    else if (page == "credits") ShowCredits();
                    else if (page == "first") { GameSettings.TutorialDone = false; PlayBots(); }
                    else if (page == "maps") ShowMapPicker();
                };
            }
    }

    /// <summary>Plaque du joueur (bas du bandeau) : badge jaune du niveau, pseudo, titre et barre d'XP turquoise.</summary>
    static Control ProfilePlate()
    {
        var (level, into, need) = DogeballVR.Online.Progression.LevelOf(DogeballVR.Online.Progression.TotalXp);
        var plate = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        plate.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        plate.OffsetLeft = 72; plate.OffsetRight = 72 + 470; plate.OffsetTop = -190; plate.OffsetBottom = -96;
        var back = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(40, 10), Size = new Vector2(430, 76) };
        back.AddThemeStyleboxOverride("panel", UiKit.InsetBox(new Color(UiKit.InkText, 0.55f), 14));
        plate.AddChild(back);
        var badge = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(0, 0), Size = new Vector2(96, 96) };
        badge.AddThemeStyleboxOverride("panel", new KcBox { Top = UiKit.Yellow, Bottom = UiKit.Gold, Slant = 16, ShadowOffset = new Vector2(6, 6), ShadowColor = new Color(UiKit.InkText, 0.6f) });
        plate.AddChild(badge);
        var lvl = UiKit.Text(level.ToString(), 56, UiKit.InkText, 0, HorizontalAlignment.Center);
        if (UiKit.Display is Font d) lvl.AddThemeFontOverride("font", d);
        lvl.Position = new Vector2(0, 8); lvl.Size = new Vector2(96, 66);
        badge.AddChild(lvl);
        var niv = UiKit.Text("NIVEAU", 16, UiKit.InkText, 0, HorizontalAlignment.Center);
        if (UiKit.Italic is Font it) niv.AddThemeFontOverride("font", it);
        niv.Position = new Vector2(0, 64); niv.Size = new Vector2(96, 22);
        badge.AddChild(niv);
        var name = UiKit.Text(DogeballVR.Online.Identity.Name.ToUpperInvariant(), 30, Colors.White, 4);
        if (UiKit.Italic is Font it2) name.AddThemeFontOverride("font", it2);
        name.Position = new Vector2(118, 12); name.Size = new Vector2(330, 36);
        plate.AddChild(name);
        var title = UiKit.Text(DogeballVR.Online.Progression.TitleOf(level).ToUpperInvariant(), 22, UiKit.Cream, 0);
        if (UiKit.Italic is Font b) title.AddThemeFontOverride("font", b);
        title.Position = new Vector2(118, 42); title.Size = new Vector2(200, 28);
        plate.AddChild(title);
        var xp = UiKit.Text(need > 0 ? $"{into} / {need} XP" : "NIVEAU MAX", 22, UiKit.Teal, 0, HorizontalAlignment.Right);
        xp.Position = new Vector2(246, 42); xp.Size = new Vector2(200, 28);
        plate.AddChild(xp);
        var rail = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(114, 72), Size = new Vector2(330, 8) };
        rail.AddThemeStyleboxOverride("panel", new KcBox { Top = new Color(1, 1, 1, 0.14f), Bottom = new Color(1, 1, 1, 0.14f), Slant = 3, ShadowColor = new Color(0, 0, 0, 0) });
        plate.AddChild(rail);
        float k = need > 0 ? Mathf.Clamp(into / (float)need, 0f, 1f) : 1f;
        var fill = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore, Position = rail.Position, Size = new Vector2(Mathf.Max(8f, 330f * k), 8) };
        fill.AddThemeStyleboxOverride("panel", new KcBox { Top = UiKit.Teal, Bottom = UiKit.Teal, Slant = 3, ShadowColor = new Color(0, 0, 0, 0) });
        plate.AddChild(fill);
        UiKit.SlideIn(plate, 0.25f, -80f);
        return plate;
    }

    /// <summary>
    /// Carte du jour, en bas à droite sur le décor (comme les tuiles d'événement de KC ou Fortnite) : nom, étiquettes et plan
    /// tracé depuis le JSON. Un clic lance directement un match contre les bots sur cette carte.
    /// </summary>
    Control MapOfTheDay()
    {
        var m = SoloMaps[DateTime.Now.DayOfYear % SoloMaps.Length];
        var card = new Button { FocusMode = Control.FocusModeEnum.None, MouseDefaultCursorShape = Control.CursorShape.PointingHand };
        card.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        card.OffsetLeft = -560; card.OffsetRight = -60; card.OffsetTop = -440; card.OffsetBottom = -80;
        var idle = new KcBox { Top = new Color(UiKit.VioletTop, 0.94f), Bottom = new Color(UiKit.NavyLow, 0.94f), Slant = 18, BorderWidth = 4, BorderColor = UiKit.Cream,
            ShadowOffset = new Vector2(10, 12), ShadowColor = new Color(0, 0, 0, 0.55f), StripOffset = new Vector2(-12, 0), StripColor = UiKit.Yellow };
        var lit = (KcBox)idle.Duplicate();
        lit.BorderColor = UiKit.Yellow;
        card.AddThemeStyleboxOverride("normal", idle);
        card.AddThemeStyleboxOverride("focus", new StyleBoxEmpty());
        foreach (var st in new[] { "hover", "pressed", "hover_pressed" }) card.AddThemeStyleboxOverride(st, lit);
        card.Pressed += () => { Sfx.Play(card, SfxKind.UiClick); FirstMatch(() => Leave(() => SoloChosen?.Invoke(m.Map))); };
        card.MouseEntered += () => Sfx.Play(card, SfxKind.UiHover);

        var head = UiKit.Text("CARTE DU JOUR", 24, UiKit.InkText, 0);
        if (UiKit.Italic is Font it) head.AddThemeFontOverride("font", it);
        var headBox = new KcBox { Top = UiKit.Yellow, Bottom = UiKit.Gold, Slant = 10, ShadowColor = new Color(0, 0, 0, 0) };
        headBox.ContentMarginLeft = headBox.ContentMarginRight = 18; headBox.ContentMarginTop = 0; headBox.ContentMarginBottom = 2;
        head.AddThemeStyleboxOverride("normal", headBox);
        head.Position = new Vector2(30, -18);
        card.AddChild(head);
        var name = UiKit.Title(m.Name, 50, HorizontalAlignment.Left);
        name.Position = new Vector2(36, 22); name.Size = new Vector2(300, 60);
        card.AddChild(name);
        var tags = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(38, 86) };
        tags.AddThemeConstantOverride("separation", 10);
        tags.AddChild(Tag(m.Players, UiKit.Teal));
        tags.AddChild(Tag(m.Zones ? "EN ZONES" : "ARÈNE OUVERTE", UiKit.Cream));
        card.AddChild(tags);
        var plan = new MapPlan { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(36, 134), Size = new Vector2(428, 200) };
        try { plan.Map = Maps.MapData.Load($"res://maps/{m.Map}.json"); } catch (Exception) { plan.Map = null; }
        card.AddChild(plan);
        var go = UiKit.Text("JOUER  ›", 30, UiKit.Yellow, 4, HorizontalAlignment.Right);
        if (UiKit.Display is Font d) go.AddThemeFontOverride("font", d);
        go.Position = new Vector2(300, 34); go.Size = new Vector2(164, 40);
        card.AddChild(go);
        card.Modulate = new Color(1, 1, 1, 0);   // ancrée à droite : fondu seul (SlideIn lit la position avant la mise en page)
        card.CreateTween().TweenProperty(card, "modulate:a", 1.0, 0.3).SetDelay(0.35);
        return card;
    }

    void ShowProfile() => OpenOverlay(new ProfileScreen { Back = () => { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); ShowMain(); } });

    public override void _Process(double delta)
    {
        escHint.Visible = back != null || overlay != null;

        // La barre jaune suit l'item qui a le focus (clavier ou survol) et glisse de l'un à l'autre.
        var f = column.Visible ? root.GetViewport().GuiGetFocusOwner() : null;
        Control? on = f != null && f.HasMeta(ItemMeta) && column.IsAncestorOf(f) ? f : null;
        if (on == null) { bar.Visible = false; barOn = null; return; }
        var want = BarRect(on);
        if (on != barOn)
        {
            barTween?.Kill();
            barTween = bar.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            if (barOn == null)
            {
                // Première apparition (nouvelle page) : la barre se déroule depuis la gauche.
                bar.Position = want.Position;
                bar.Size = new Vector2(0, want.Size.Y);
                barTween.TweenProperty(bar, "size", want.Size, 0.18);
            }
            else
            {
                barTween.TweenProperty(bar, "position", want.Position, 0.12);
                barTween.TweenProperty(bar, "size", want.Size, 0.12);
            }
            barOn = on;
            bar.Visible = true;
        }
        else if (barTween == null || !barTween.IsRunning())
        {
            bar.Position = want.Position;   // la colonne a pu bouger (mise en page)
            bar.Size = want.Size;
        }
        bar.QueueRedraw();
    }

    /// <summary>Rectangle de la barre pour un item : du texte jusqu'un peu au-delà du liseré du bandeau, comme dans KC.</summary>
    Rect2 BarRect(Control item)
    {
        float y = column.Position.Y + item.Position.Y;
        float x0 = column.Position.X - BarLead;
        float x1 = band.EdgeX(y + item.Size.Y / 2f) + 44f;
        return new Rect2(x0, y + 3, x1 - x0, item.Size.Y - 6);
    }

    void Fill(params Control[] items)
    {
        foreach (var c in column.GetChildren()) c.QueueFree();
        // Les items du menu racine se touchent (la barre fait l'espacement) ; les pages à boutons gardent leur écart.
        column.AddThemeConstantOverride("separation", Array.Exists(items, c => c.HasMeta(ItemMeta)) ? 0 : compact ? 10 : 14);
        int i = 0;
        foreach (var c in items)
        {
            // Items en lettres penchées et grasses, comme les menus de Knockout City.
            var f = c.HasMeta(FontMeta) && c.GetMeta(FontMeta).AsString() == "display" ? UiKit.Heavy : UiKit.Italic;
            if (f != null && (c is Button || (c is Label && !c.HasThemeFontOverride("font")))) c.AddThemeFontOverride("font", f);
            column.AddChild(c);
            UiKit.SlideIn(c, 0.05f * i++);
        }
        // Focus sur le premier bouton (les pages En ligne / Réseau local commencent par un titre) : clavier utilisable d'emblée.
        if (Array.Find(items, c => c is Button) is Button b) b.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>
    /// Item du menu racine : texte XXL penché, sans boîte. Le focus (clavier, ou survol de la souris qui le prend) le passe
    /// en marine sur la barre jaune et le pousse un peu vers la droite. Les items secondaires sont plus petits et grisés.
    /// </summary>
    Button Item(string text, Action onPress, bool main = true, Color? restColor = null)
    {
        var pad = new StyleBoxEmpty { ContentMarginLeft = 0, ContentMarginRight = 8, ContentMarginTop = 0, ContentMarginBottom = 4 };
        var b = new Button { Text = text, Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(560, main ? 76 : 50) };
        b.SetMeta(ItemMeta, true);
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) b.AddThemeStyleboxOverride(st, pad);
        int size = main ? 56 : 38;   // la grasse large (Heavy) : 56 px font la largeur des 70 px de la condensée
        b.AddThemeFontSizeOverride("font_size", size);
        b.SetMeta(FontMeta, main ? "display" : "italic");
        b.AddThemeColorOverride("font_outline_color", UiKit.InkText);
        var rest = restColor ?? (main ? Colors.White : UiKit.Lavender);   // gris lavande : 3e niveau de hiérarchie, comme « SETTINGS » dans KC
        int restOutline = 0;
        // Ombre nette décalée au repos (relief des titres de KC), aucune sur la barre jaune.
        b.AddThemeConstantOverride("shadow_offset_x", Mathf.Max(2, size / 22));
        b.AddThemeConstantOverride("shadow_offset_y", Mathf.Max(3, size / 16));
        b.AddThemeConstantOverride("shadow_outline_size", 0);
        void Paint(Color c, int outline)
        {
            foreach (var k in FontColors) b.AddThemeColorOverride(k, c);
            b.AddThemeConstantOverride("outline_size", outline);
            b.AddThemeColorOverride("font_shadow_color", c == UiKit.Navy ? new Color(0, 0, 0, 0) : new Color(UiKit.InkText, 0.75f));
        }
        Paint(rest, restOutline);
        Tween? slide = null;
        void Shift(float to)
        {
            slide?.Kill();
            slide = b.CreateTween();
            slide.TweenProperty(pad, "content_margin_left", to, 0.12).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        b.FocusEntered += () => { if (bar.Visible) Sfx.Play(b, SfxKind.UiHover); Paint(UiKit.Navy, 0); Shift(main ? 36f : 30f); };
        b.FocusExited += () => { Paint(rest, restOutline); Shift(0f); };
        b.MouseEntered += () => { if (!b.Disabled && !leaving) b.GrabFocus(); };
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>Item principal (JOUER) plus gros que les autres : l'action du menu se voit d'abord (revue de la refonte).</summary>
    static Button Hero(Button b)
    {
        b.AddThemeFontSizeOverride("font_size", 76);
        b.CustomMinimumSize = new Vector2(b.CustomMinimumSize.X, 100);
        b.AddThemeConstantOverride("shadow_offset_x", 4);
        b.AddThemeConstantOverride("shadow_offset_y", 6);
        return b;
    }

    void ShowMain()
    {
        back = null;
        Fill(
            Hero(Item("JOUER", PlayBots)),
            Item("ENTRAÎNEMENT", () => Leave(() => TutorialChosen?.Invoke())),
            Item("EN LIGNE", ShowOnline),
            Item("PROFIL", ShowProfile),
            Item("OPTIONS", () => OpenOverlay(new OptionsMenu())),
            UiKit.Spacer(14),
            Item("CRÉDITS", ShowCredits, false),
            Item("QUITTER", () => GetTree().Quit(), false));
    }

    /// <summary>
    /// Actions en ligne (Steam / salle) : branchées par Main sur le module Online quand il est présent.
    /// Sans écouteur, la partie rapide et la salle privée retombent sur l'hébergement réseau local.
    /// </summary>
    public event Action? QuickMatch, CreatePrivate, HostLan;
    public event Action<string>? JoinCode, JoinAddress;

    /// <summary>Clavier virtuel (Steam : Deck, Big Picture) ; branché par Main. Faux s'il n'a pas pu s'ouvrir.</summary>
    public static Func<string, string, int, Rect2I, Action<string?>, bool>? TextInput { get; set; }

    /// <summary>
    /// Champ de texte jouable à la manette : A sur le champ (ou un bouton qui le demande vide) ouvre le clavier virtuel de Steam.
    /// Sur Deck le clavier flottant tape directement dans le champ ; ailleurs le texte revient à la fermeture, puis le focus passe à <paramref name="next"/>.
    /// </summary>
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
        back = ShowMain;
        var code = new LineEdit { PlaceholderText = "Code de la salle", CustomMinimumSize = new Vector2(440, 58), Alignment = HorizontalAlignment.Center, MaxLength = 12 };
        Button? join = null;
        PadKeyboard(code, "Code de la salle", () => join);
        // Mêmes items que le menu racine (barre jaune qui glisse), en taille secondaire.
        Fill(
            UiKit.Title("EN LIGNE", 56, HorizontalAlignment.Left),
            Item("PARTIE RAPIDE", () => FirstMatch(() => Online(QuickMatch, () => Chosen?.Invoke(NetMode.Host, ""))), false, Colors.White),
            Item("CRÉER UNE SALLE PRIVÉE", () => Online(CreatePrivate, () => Chosen?.Invoke(NetMode.Host, "")), false, Colors.White),
            code,
            join = Item("REJOINDRE AVEC LE CODE", () =>
            {
                var c = code.Text.Trim().ToUpperInvariant();
                if (c == "") { code.GrabFocus(); if (InputSetup.UsingPad) OpenKeyboard(code, "Code de la salle", () => join); return; }
                if (JoinCode != null) Leave(() => JoinCode(c));
            }, false, Colors.White),
            Item("RÉSEAU LOCAL", ShowLan, false, Colors.White),
            UiKit.Spacer(10),
            Item("RETOUR", () => { Sfx.Play(this, SfxKind.UiBack); ShowMain(); }, false));
    }

    void ShowLan()
    {
        back = ShowOnline;
        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Adresse IP de l'hôte", CustomMinimumSize = new Vector2(440, 58), Alignment = HorizontalAlignment.Center };
        Button? join = null;
        PadKeyboard(address, "Adresse IP de l'hôte", () => join);
        Fill(
            UiKit.Title("RÉSEAU LOCAL", 56, HorizontalAlignment.Left),
            Item("HÉBERGER", () => Online(HostLan, () => Chosen?.Invoke(NetMode.Host, "")), false, Colors.White),
            address,
            join = Item("REJOINDRE", () =>
            {
                var ip = address.Text.Trim();
                if (JoinAddress != null) Leave(() => JoinAddress(ip)); else Leave(() => Chosen?.Invoke(NetMode.Client, ip));
            }, false, Colors.White),
            UiKit.Spacer(10),
            Item("RETOUR", () => { Sfx.Play(this, SfxKind.UiBack); ShowOnline(); }, false));
    }

    void Online(Action? handler, Action fallback) => Leave(handler ?? fallback);

    void PlayBots() => FirstMatch(ShowMapPicker);   // solo : choix de la carte (la première fois, après la proposition d'entraînement)

    /// <summary>Contre les bots ou en partie rapide : la toute première fois, on propose l'entraînement avant <paramref name="play"/>.</summary>
    void FirstMatch(Action play)
    {
        if (GameSettings.TutorialDone) { play(); return; }
        // Première partie : on propose le tutoriel (2 minutes), sans l'imposer.
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        card.AddChild(col);
        col.AddChild(UiKit.Header("PREMIÈRE PARTIE ?", 52));
        col.AddChild(UiKit.Text("L'entraînement t'apprend à lancer, attraper et esquiver en deux minutes.", 24, Colors.White, 6, HorizontalAlignment.Center));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        col.AddChild(row);
        var yes = UiKit.Button("ENTRAÎNEMENT", UiKit.Orange, () => Leave(() => TutorialChosen?.Invoke()), 28, new Vector2(320, 64));
        row.AddChild(yes);
        row.AddChild(UiKit.Button("JOUER DIRECT", UiKit.Blue, () =>
        {
            GameSettings.TutorialDone = true;
            GameSettings.Save();
            play();
        }, 28, new Vector2(320, 64)));
        OpenOverlay(Centered(card));
        yes.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>
    /// Solo : choix de la carte, à la Knockout City (partie privée) : liste à gauche, fiche de la carte sélectionnée à droite
    /// (nom en grand, étiquettes « 2 CONTRE 2 » et vue, description). La vue est dite d'emblée : les cartes en zones (comme
    /// la salle VR) se jouent à la 1re personne, les cartes ouvertes à la 3e personne.
    /// </summary>
    void ShowMapPicker()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        card.AddChild(col);
        col.AddChild(UiKit.Header("CHOISIS LA CARTE", 58));
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 34);
        col.AddChild(row);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        row.AddChild(list);

        // Fiche : encart sombre penché, nom de la carte, étiquettes, description.
        var sheet = new PanelContainer { CustomMinimumSize = new Vector2(520, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        var sheetBox = UiKit.InsetBox(new Color(UiKit.InkText, 0.6f), 14);
        sheetBox.ContentMarginLeft = 34; sheetBox.ContentMarginRight = 30; sheetBox.ContentMarginTop = 22; sheetBox.ContentMarginBottom = 22;
        sheet.AddThemeStyleboxOverride("panel", sheetBox);
        row.AddChild(sheet);
        var info = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        info.AddThemeConstantOverride("separation", 14);
        sheet.AddChild(info);
        var mapName = UiKit.Title("", 52, HorizontalAlignment.Left, UiKit.Yellow);
        info.AddChild(mapName);
        var tags = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tags.AddThemeConstantOverride("separation", 12);
        info.AddChild(tags);
        var desc = UiKit.Text("", 26, UiKit.Cream, 0);
        if (UiKit.Body is Font body) desc.AddThemeFontOverride("font", body);
        desc.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        desc.CustomMinimumSize = new Vector2(450, 70);
        info.AddChild(desc);
        // Plan de la carte vu de dessus, tracé depuis son JSON (zones, couverts, fosse, points de balle).
        var plan = new MapPlan { CustomMinimumSize = new Vector2(450, 330), MouseFilter = Control.MouseFilterEnum.Ignore };
        info.AddChild(plan);
        var legend = UiKit.Text("VERT : BALLES   ·   CRÈME : COUVERTS   ·   JAUNE : MOYEU, FOSSE", 22, UiKit.Cream, 0, HorizontalAlignment.Center);
        if (UiKit.Italic is Font lf) legend.AddThemeFontOverride("font", lf);
        info.AddChild(legend);
        var plans = new System.Collections.Generic.Dictionary<string, Maps.MapData?>();

        Button? first = null;
        foreach (var m in SoloMaps)
        {
            var b = UiKit.ListButton(m.Name, () => Leave(() => SoloChosen?.Invoke(m.Map)), 36, new Vector2(420, 62));
            b.FocusEntered += () =>
            {
                mapName.Text = m.Name;
                desc.Text = m.Info;
                if (!plans.TryGetValue(m.Map, out var data))
                {
                    try { data = Maps.MapData.Load($"res://maps/{m.Map}.json"); } catch (System.Exception) { data = null; }
                    plans[m.Map] = data;
                }
                plan.Map = data;
                foreach (var t in tags.GetChildren()) t.QueueFree();
                tags.AddChild(Tag(m.Players, UiKit.Teal));
                tags.AddChild(Tag(m.Zones ? "EN ZONES" : "ARÈNE OUVERTE", UiKit.Cream));
            };
            list.AddChild(b);
            first ??= b;
        }
        OpenOverlay(Centered(card));
        first?.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>Étiquette penchée pleine (texte encre), comme les mentions de mode de KC.</summary>
    static Control Tag(string text, Color fill)
    {
        var p = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = new KcBox { Top = fill, Bottom = fill.Darkened(0.1f), Slant = 8, ShadowColor = new Color(0, 0, 0, 0) };
        box.ContentMarginLeft = 16; box.ContentMarginRight = 16; box.ContentMarginTop = 2; box.ContentMarginBottom = 4;
        p.AddThemeStyleboxOverride("panel", box);
        var l = UiKit.Text(text, 22, UiKit.InkText, 0);
        if (UiKit.Italic is Font it) l.AddThemeFontOverride("font", it);
        p.AddChild(l);
        return p;
    }

    void ShowCredits()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        card.AddChild(col);
        col.AddChild(UiKit.Header("CRÉDITS", 56));
        foreach (var (who, what) in new[]
        {
            ("Conception et direction", "jules"),
            ("Moteur", "Godot Engine 4.3 (MIT)"),
            ("Police", "Barlow Condensed, Jeremy Tribby (SIL OFL)"),
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

    /// <summary>Plaque de profil et carte du jour : cachées sous une fenêtre (elles dépasseraient du flou, revue de la refonte).</summary>
    Control? profilePlate, dayCard;

    void SetBackdropCards(bool on)
    {
        if (profilePlate != null) profilePlate.Visible = on;
        if (dayCard != null) dayCard.Visible = on;
    }

    void OpenOverlay(Control c)
    {
        CloseOverlay();
        column.Visible = false;
        SetBackdropCards(false);
        overlay = c;
        if (c is OptionsMenu o) o.Closed += () => { overlay = null; column.Visible = true; SetBackdropCards(true); ShowMain(); };
        root.AddChild(c);
        root.MoveChild(fade, -1);
    }

    void CloseOverlay()
    {
        if (overlay is OptionsMenu) return;   // l'écran d'options se ferme lui-même (et enregistre)
        overlay?.QueueFree();
        overlay = null;
        column.Visible = true;
        SetBackdropCards(true);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // ZQSD / WASD (touches de déplacement, réglables) : mêmes sauts de focus que les flèches. Les flèches sont déjà
        // consommées par l'interface ; on laisse l'écran d'options et les champs de saisie gérer leurs touches.
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
        if (overlay != null && overlay is not OptionsMenu) { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); ShowMain(); GetViewport().SetInputAsHandled(); }
        else if (overlay == null && back != null && !leaving) { Sfx.Play(this, SfxKind.UiBack); back(); GetViewport().SetInputAsHandled(); }   // En ligne, Réseau local : page précédente
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

    /// <summary>Barre de sélection jaune inclinée, qui vire à l'or vers le bas, avec l'étoile marine devant le texte (« ✦ JUGAR »).</summary>
    sealed partial class SelectBar : Control
    {
        static readonly Color Gold = new("ffb21f");

        public override void _Draw()
        {
            float w = Size.X, h = Size.Y;
            if (w < 2f) return;
            float s = Mathf.Min(h * 0.3f, w);   // biais : le haut part vers la droite, comme le texte penché
            Vector2[] Quad(Vector2 o) => new[] { o + new Vector2(s, 0), o + new Vector2(w, 0), o + new Vector2(w - s, h), o + new Vector2(0, h) };
            DrawColoredPolygon(Quad(new Vector2(7, 7)), new Color(0.02f, 0.03f, 0.1f, 0.55f));
            DrawPolygon(Quad(Vector2.Zero), new[] { UiKit.Yellow, UiKit.Yellow, Gold, Gold });
            if (w < BarLead + 24f) return;
            var c = new Vector2(BarLead + 10f, h / 2f);
            float r = Mathf.Min(h * 0.24f, 15f), k = r * 0.3f;
            DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(k, -k), c + new Vector2(r, 0), c + new Vector2(k, k), c + new Vector2(0, r), c + new Vector2(-k, k), c + new Vector2(-r, 0), c + new Vector2(-k, -k) }, UiKit.Navy);
        }
    }
}
