using System;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Briques d'interface communes (menus, pause, options, fin de partie, HUD), DA « Affiche » (v4, 06/11/2026) : une affiche de
/// match imprimée en sérigraphie. Encre presque noire, papier crème, orange et bleu réservés aux équipes, vert acide en accent
/// (sélection, progression). Titres en Anton (capitales étroites, droites), texte en Barlow. Formes droites à coins vifs,
/// ombres franches décalées ; les titres ont un double tirage décalé (comme une impression mal calée).
/// Les anciens noms (Navy, Yellow, Violet…) sont gardés pour ne pas réécrire chaque écran : seule leur valeur change.
/// </summary>
public static class UiKit
{
    /// <summary>Encre : contours de texte, texte sur papier.</summary>
    public static readonly Color Navy = new("15141a");
    public static readonly Color NavySoft = new("27252e");
    public static readonly Color Orange = new("f2612d");
    public static readonly Color Blue = new("2e86de");
    /// <summary>Libellé secondaire neutre (papier à ~70 % sur l'encre). Le bleu reste réservé à l'équipe bleue.</summary>
    public static readonly Color Sky = new("b5afa3");
    /// <summary>Accent de la marque (vert acide) : sélection, progression, chiffres mis en avant. Ancien jaune.</summary>
    public static readonly Color Yellow = new("d2f53c");
    public static readonly Color Pink = new("ff4f7b");
    /// <summary>Papier crème : liserés, texte clair, barre de sélection.</summary>
    public static readonly Color Cream = new("f3ecdd");
    public static readonly Color Grey = new("5e5a54");
    public static readonly Color Green = new("5fdc84");
    // Aplats des panneaux : encre légèrement chaude, sans dégradé coloré.
    public static readonly Color VioletTop = new("1e1c24");
    public static readonly Color VioletMid = new("19181e");
    public static readonly Color NavyLow = new("131217");
    public static readonly Color Violet = new("2c2a33");
    public static readonly Color Gold = new("a6cb1a");
    /// <summary>Gris chaud : texte secondaire.</summary>
    public static readonly Color Lavender = new("a8a397");
    /// <summary>Alias lisibles de la DA « Affiche ».</summary>
    public static Color Paper => Cream;
    public static Color Lime => Yellow;
    public static readonly Color Ink = new("08080b");

    /// <summary>Fenêtre (options, pause, fin de match…) : le bandeau du menu en panneau penché, liseré crème, ombre et bande violette.</summary>
    public static KcBox PanelBox(float slant = 0f, int margin = 40)
    {
        var b = new KcBox
        {
            Top = VioletTop, Bottom = NavyLow, BorderWidth = 4, BorderColor = Cream, Slant = 0f,
            ShadowOffset = new Vector2(12, 12), ShadowColor = new Color(Ink, 0.7f),
            StripOffset = new Vector2(0, 0), StripColor = new Color(0, 0, 0, 0),
        };
        b.ContentMarginLeft = b.ContentMarginRight = margin;
        b.ContentMarginTop = b.ContentMarginBottom = margin * 0.7f;
        return b;
    }

    /// <summary>Encart dans une fenêtre (description, ligne de tableau, carte) : aplat sombre penché, sans liseré.</summary>
    public static KcBox InsetBox(Color? fill = null, float slant = 0f, int border = 0, Color? borderColor = null)
    {
        var c = fill ?? new Color(Ink, 0.45f);
        var b = new KcBox { Top = c, Bottom = c, Slant = 0f, BorderWidth = border, BorderColor = borderColor ?? Cream };
        b.ContentMarginLeft = b.ContentMarginRight = 22;
        b.ContentMarginTop = 8; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Touche de clavier : pavé crème penché à coins vifs, tranche grise décalée dessous pour le relief.</summary>
    public static KcBox KeyBox()
    {
        var b = new KcBox { Top = Cream, Bottom = Cream, Slant = 0, ShadowOffset = new Vector2(0, 4), ShadowColor = new Color("8f877a") };
        b.ContentMarginLeft = b.ContentMarginRight = 11; b.ContentMarginTop = 1; b.ContentMarginBottom = 2;
        return b;
    }

    /// <summary>Bouton au repos : aplat d'encre à liseré papier, accent de couleur à gauche (transparent : aucun).</summary>
    public static KcBox ButtonIdle(Color accent, bool box = true)
    {
        var b = new KcBox
        {
            Top = box ? NavySoft : new Color(0, 0, 0, 0), Bottom = box ? NavySoft : new Color(0, 0, 0, 0),
            BorderWidth = box ? 2 : 0, BorderColor = new Color(Cream, 0.85f), Slant = 0,
            ShadowOffset = new Vector2(6, 6), ShadowColor = new Color(Ink, box ? 0.6f : 0f),
            AccentColor = accent, AccentWidth = 8,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 28; b.ContentMarginTop = 6; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Bouton sélectionné (survol ou clavier) : bande de papier, texte encre, languette vert acide à gauche, ombre franche.</summary>
    public static KcBox ButtonLit(bool pressed = false)
    {
        var b = new KcBox
        {
            Top = pressed ? Cream.Darkened(0.1f) : Cream, Bottom = pressed ? Cream.Darkened(0.1f) : Cream, Slant = 0,
            ShadowOffset = pressed ? new Vector2(3, 3) : new Vector2(7, 7), ShadowColor = new Color(Ink, 0.75f),
            AccentColor = Yellow, AccentWidth = 14,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 28; b.ContentMarginTop = 6; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Titre de fenêtre : capitales Anton en papier, double tirage vert acide décalé en bas à droite (impression mal calée).</summary>
    /// <summary>
    /// Texte à ne jamais traduire (nom de joueur) : sans cela un joueur nommé « Bob » s'afficherait « Bucket hat » en anglais,
    /// les Label et Label3D traduisant seuls leur texte.
    /// </summary>
    public static T Raw<T>(T node) where T : Node
    {
        node.AutoTranslateMode = Node.AutoTranslateModeEnum.Disabled;
        return node;
    }

    public static Label Title(string text, int size = 54, HorizontalAlignment align = HorizontalAlignment.Center, Color? color = null)
    {
        var l = Text(L.T(text).ToUpperInvariant(), size, color ?? Cream, 0, align);   // traduit avant les capitales (la clé est le texte d'origine)
        if (Display is Font d) l.AddThemeFontOverride("font", d);
        // Ombre lime réservée aux grands titres (logo, PAUSE) ; en dessous de 80 px elle baverait : ombre encre plus franche.
        bool lime = color == null && size >= 80;
        int o = lime ? Mathf.Max(2, size / 18) : Mathf.Max(3, size / 11);
        l.AddThemeColorOverride("font_shadow_color", lime ? Yellow : Ink);
        l.AddThemeConstantOverride("shadow_offset_x", o);
        l.AddThemeConstantOverride("shadow_offset_y", o);
        l.AddThemeConstantOverride("shadow_outline_size", 0);
        return l;
    }

    /// <summary>Texte courant : Barlow Bold (lisible petit, chiffres nets).</summary>
    public static Font? Font => font ??= Load("Barlow-Bold.ttf") ?? Load("LilitaOne-Regular.ttf");
    static Font? font;
    /// <summary>Titres, boutons et gros chiffres : Anton (capitales étroites d'affiche).</summary>
    public static Font? Display => display ??= Load("Anton-Regular.ttf") ?? Font;
    static Font? display;
    /// <summary>Texte fort (étiquettes, aides) : Barlow ExtraBold.</summary>
    public static Font? Heavy => heavy ??= Load("Barlow-ExtraBold.ttf") ?? Font;
    static Font? heavy;
    static Font? Load(string file) => ResourceLoader.Exists("res://assets/fonts/" + file) ? GD.Load<Font>("res://assets/fonts/" + file) : null;

    public static StyleBoxFlat Box(Color bg, int radius = 16, float skew = 0f, int border = 0, Color? borderColor = null, int bottom = 6)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg,   // coins vifs (DA du menu) : radius n'est plus utilisé, gardé pour les appels existants
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 10, ContentMarginBottom = 10,
            BorderWidthBottom = bottom + border, BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border,
            BorderColor = borderColor ?? new Color(0.04f, 0.06f, 0.14f, 0.95f),
            Skew = Vector2.Zero, AntiAliasing = true,   // DA « Affiche » : boîtes droites ; skew gardé pour les appels existants
        };
        return b;
    }

    /// <summary>Chiffres et mots d'affiche : Anton, sans contour, ombre dure décalée (encre par défaut).</summary>
    public static Label Poster(Label l, Color? shadow = null, int offset = 0)
    {
        if (Display is Font d) l.AddThemeFontOverride("font", d);
        l.AddThemeConstantOverride("outline_size", 0);
        int size = l.GetThemeFontSize("font_size");
        int o = offset > 0 ? offset : Mathf.Max(3, size / 16);
        l.AddThemeColorOverride("font_shadow_color", shadow ?? Ink);
        l.AddThemeConstantOverride("shadow_offset_x", o);
        l.AddThemeConstantOverride("shadow_offset_y", o);
        l.AddThemeConstantOverride("shadow_outline_size", 0);
        return l;
    }

    /// <summary>Thème global : à poser sur la racine de chaque écran (les enfants en héritent).</summary>
    public static Theme Theme => theme ??= BuildTheme();
    static Theme? theme;

    static Theme BuildTheme()
    {
        var t = new Theme();
        if (Font != null) t.DefaultFont = Font;
        t.DefaultFontSize = 24;
        t.SetColor("font_color", "Label", Colors.White);
        t.SetColor("font_outline_color", "Label", Navy);
        t.SetConstant("outline_size", "Label", 8);

        var disabled = ButtonIdle(new Color(0, 0, 0, 0));
        disabled.Top = new Color(Ink, 0.5f); disabled.Bottom = new Color(Ink, 0.5f); disabled.BorderColor = new Color(Cream, 0.3f);
        t.SetStylebox("normal", "Button", ButtonIdle(new Color(0, 0, 0, 0)));
        t.SetStylebox("hover", "Button", ButtonLit());
        t.SetStylebox("pressed", "Button", ButtonLit(true));
        t.SetStylebox("hover_pressed", "Button", ButtonLit(true));
        t.SetStylebox("focus", "Button", ButtonLit());
        t.SetStylebox("disabled", "Button", disabled);
        t.SetColor("font_color", "Button", Colors.White);
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "Button", Navy);
        t.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.4f));
        t.SetColor("font_outline_color", "Button", Navy);
        t.SetConstant("outline_size", "Button", 6);
        t.SetFontSize("font_size", "Button", 26);
        if (Italic != null) t.SetFont("font", "Button", Italic);

        // Glissière : rail encre penché à liseré crème, remplissage jaune → or (la barre du menu).
        var slider = new KcBox { Top = new Color(Ink, 0.7f), Bottom = new Color(Ink, 0.7f), BorderWidth = 2, BorderColor = new Color(Cream, 0.8f), Slant = 0 };
        slider.ContentMarginTop = 9; slider.ContentMarginBottom = 9;
        var fill = new KcBox { Top = Yellow, Bottom = Yellow, Slant = 0 };
        fill.ContentMarginTop = 6; fill.ContentMarginBottom = 6;
        t.SetStylebox("slider", "HSlider", slider);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", fill);
        t.SetIcon("grabber", "HSlider", Dot(26, Cream));
        t.SetIcon("grabber_highlight", "HSlider", Dot(30, Yellow));

        t.SetStylebox("panel", "PanelContainer", PanelBox());
        // Champ de saisie : encart encre penché à liseré crème ; jaune au focus.
        var field = InsetBox(new Color(Ink, 0.8f), 0, 3, new Color(Cream, 0.9f));
        field.ContentMarginTop = 6; field.ContentMarginBottom = 6;
        var fieldLit = InsetBox(new Color(Ink, 0.8f), 0, 4, Yellow);
        fieldLit.ContentMarginTop = 6; fieldLit.ContentMarginBottom = 6;
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", fieldLit);
        t.SetColor("font_color", "LineEdit", Colors.White);
        t.SetColor("font_placeholder_color", "LineEdit", new Color(Lavender, 0.8f));
        t.SetColor("caret_color", "LineEdit", Yellow);
        t.SetFontSize("font_size", "LineEdit", 24);

        foreach (var k in new[] { "normal", "hover", "pressed", "focus", "hover_pressed" })
            t.SetStylebox(k, "CheckButton", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckButton", Colors.White);
        t.SetColor("font_hover_color", "CheckButton", Yellow);
        t.SetColor("font_pressed_color", "CheckButton", Colors.White);
        t.SetColor("font_outline_color", "CheckButton", Navy);
        t.SetConstant("outline_size", "CheckButton", 6);

        t.SetStylebox("normal", "OptionButton", ButtonIdle(new Color(0, 0, 0, 0)));
        t.SetStylebox("hover", "OptionButton", ButtonLit());
        t.SetStylebox("pressed", "OptionButton", ButtonLit(true));
        t.SetStylebox("focus", "OptionButton", ButtonLit());
        t.SetColor("font_color", "OptionButton", Colors.White);
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "OptionButton", Navy);
        t.SetColor("font_outline_color", "OptionButton", Navy);
        t.SetConstant("outline_size", "OptionButton", 5);
        if (Italic != null) t.SetFont("font", "OptionButton", Italic);

        var popup = PanelBox(0, 14);
        popup.StripColor = new Color(0, 0, 0, 0); popup.BorderWidth = 4; popup.ShadowOffset = new Vector2(6, 6);
        t.SetStylebox("panel", "PopupMenu", popup);
        t.SetStylebox("hover", "PopupMenu", ButtonLit());
        t.SetColor("font_color", "PopupMenu", Colors.White);
        t.SetColor("font_hover_color", "PopupMenu", Navy);
        t.SetFontSize("font_size", "PopupMenu", 22);

        var scroll = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.12f), ContentMarginLeft = 4, ContentMarginRight = 4 };
        var grab = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.5f) };
        t.SetStylebox("scroll", "VScrollBar", scroll);
        t.SetStylebox("grabber", "VScrollBar", grab);
        t.SetStylebox("grabber_highlight", "VScrollBar", grab);
        t.SetStylebox("grabber_pressed", "VScrollBar", grab);
        return t;
    }

    static ImageTexture Dot(int size, Color c)
    {
        var img = Image.CreateEmpty(size, size, false, Image.Format.Rgba8);
        float r = size / 2f;
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = new Vector2(x + 0.5f - r, y + 0.5f - r).Length();
                var col = d < r - 4 ? c : d < r - 1 ? Navy : new Color(0, 0, 0, 0);
                if (d >= r - 1 && d < r) col = new Color(Navy, r - d);
                img.SetPixel(x, y, col);
            }
        return ImageTexture.CreateFromImage(img);
    }

    public static Label Text(string text, int size, Color? color = null, int outline = 8, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is Color c) l.AddThemeColorOverride("font_color", c);
        l.AddThemeConstantOverride("outline_size", outline);
        l.AddThemeColorOverride("font_outline_color", Navy);
        return l;
    }

    /// <summary>
    /// Bouton de fenêtre dans la DA du menu principal : lettres penchées blanches sur un aplat violet nuit penché (accent de la
    /// couleur donnée à gauche ; gris = action secondaire, texte lavande). Survol ou clavier : barre jaune → or avec l'étoile,
    /// texte marine qui glisse un peu vers la droite, son. Le survol de la souris prend le focus : une seule sélection à la fois.
    /// </summary>
    public static Button Button(string text, Color color, Action onPress, int fontSize = 30, Vector2? minSize = null)
    {
        var b = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(440, 66), FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        bool secondary = color == Grey;
        Style(b, ButtonIdle(secondary ? new Color(0, 0, 0, 0) : color), secondary ? Cream : Colors.White);   // secondaire : papier plein, pas « désactivé »
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>Habille un bouton : repos (idle), barre jaune au focus et au survol, texte qui glisse, son. Rest = couleur du texte au repos.</summary>
    public static void Style(Button b, KcBox idle, Color rest, float shift = 0f)
    {
        var lit = ButtonLit();
        var down = ButtonLit(true);
        // La barre jaune et le repos partagent les marges : le texte glisse de la même façon sur les deux.
        b.AddThemeStyleboxOverride("normal", idle);
        b.AddThemeStyleboxOverride("hover", lit);
        b.AddThemeStyleboxOverride("pressed", down);
        b.AddThemeStyleboxOverride("hover_pressed", down);
        b.AddThemeStyleboxOverride("focus", lit);
        if (Italic is Font it) b.AddThemeFontOverride("font", it);
        b.AddThemeColorOverride("font_color", rest);
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) b.AddThemeColorOverride(k, Navy);
        b.AddThemeColorOverride("font_outline_color", Navy);
        b.AddThemeConstantOverride("outline_size", 0);   // Anton sur aplat : pas de contour (il empâterait le texte encre sur papier)
        float baseLeft = idle.ContentMarginLeft;
        Tween? slide = null;
        void Shift(float to)
        {
            slide?.Kill();
            if (!b.IsInsideTree()) return;
            slide = b.CreateTween().SetParallel();
            foreach (var box in new[] { idle, lit, down })
                slide.TweenProperty(box, "content_margin_left", baseLeft + to, 0.12).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        b.FocusEntered += () => { Shift(shift); Sfx.Play(b, SfxKind.UiHover); };
        b.FocusExited += () => Shift(0f);
        b.MouseEntered += () => { if (!b.Disabled && b.FocusMode != Control.FocusModeEnum.None) b.GrabFocus(); };
    }

    /// <summary>Change l'aplat de repos d'un bouton habillé par Style (accent à gauche, remplissage ; null : violet nuit d'origine) et la couleur de son texte.</summary>
    public static void Tint(Button b, Color accent, Color? fill = null, Color? text = null)
    {
        if (b.GetThemeStylebox("normal") is not KcBox idle) return;
        var f = fill ?? NavySoft;
        idle.AccentColor = accent;
        idle.Top = f;
        idle.Bottom = fill is Color c ? c.Darkened(0.15f) : NavySoft;
        b.AddThemeColorOverride("font_color", text ?? Colors.White);
    }

    /// <summary>Bouton secondaire en texte seul (crédits, quitter) : lavande au repos, barre jaune au focus, comme le menu.</summary>
    public static Button TextButton(string text, Action onPress, int fontSize = 26)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(440, 48), FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        Style(b, ButtonIdle(new Color(0, 0, 0, 0), false), Lavender);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>
    /// Bouton de liste (pause) : exactement les items du menu principal, texte penché sans boîte au repos, barre jaune avec
    /// l'étoile sur l'élément sélectionné. Souris et clavier ne montrent jamais deux sélections à la fois.
    /// </summary>
    public static Button ListButton(string text, Action onPress, int fontSize = 28, Vector2? minSize = null)
    {
        var b = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(440, 62), FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        var idle = ButtonIdle(new Color(0, 0, 0, 0), false);
        idle.ContentMarginLeft = 34;   // place de la languette de sélection
        Style(b, idle, Colors.White);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>
    /// Touche de clavier dessinée (même dessin que le HUD) : pavé crème penché, tranche grise en bas (relief), texte marine.
    /// minWidth aligne les touches d'une colonne.
    /// </summary>
    public static Label KeyCap(string text, int size = 20, float minWidth = 0)
    {
        var l = Text(text, size, Navy, 0, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        if (minWidth > 0) l.CustomMinimumSize = new Vector2(minWidth, 0);
        l.AddThemeStyleboxOverride("normal", KeyBox());
        return l;
    }

    /// <summary>Aide de bas d'écran : [touche] action, à la Knockout City (« [Esc] FERMER »).</summary>
    public static HBoxContainer KeyHint(string key, string label, int size = 22)
    {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 10);
        h.AddChild(KeyCap(key, size - 2));
        var l = Text(label, size, Colors.White, 6);
        l.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(l);
        return h;
    }

    /// <summary>Gros bouton avec son raccourci dessiné à gauche du texte (« [Entrée] REJOUER »).</summary>
    public static Button KeyButton(string text, string key, Color color, Action onPress, int fontSize = 30, Vector2? minSize = null)
    {
        var b = Button("", color, onPress, fontSize, minSize);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.OffsetLeft = 14;   // place de la languette de sélection
        b.AddChild(center);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        center.AddChild(row);
        var cap = KeyCap(key, 20);
        row.AddChild(cap);
        var rest = color == Grey ? Cream : Colors.White;   // secondaire : papier plein, il ne doit pas sembler désactivé
        var l = Text(text, fontSize, rest, 0);
        if (Italic is Font it) l.AddThemeFontOverride("font", it);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        // Le texte est un Label à part : il suit lui-même la plaque papier (encre au focus) ; la touche passe en encre pour
        // rester une touche dessinée sur le papier.
        var capInk = KeyBox(); capInk.Top = capInk.Bottom = Navy; capInk.ShadowColor = Ink;
        var capPaper = KeyBox();
        b.FocusEntered += () => { l.AddThemeColorOverride("font_color", Navy); cap.AddThemeStyleboxOverride("normal", capInk); cap.AddThemeColorOverride("font_color", Cream); };
        b.FocusExited += () => { l.AddThemeColorOverride("font_color", rest); cap.AddThemeStyleboxOverride("normal", capPaper); cap.AddThemeColorOverride("font_color", Navy); };
        return b;
    }

    /// <summary>
    /// Police des titres et des items (ancien nom gardé pour les écrans existants) : Anton droite. La DA « Affiche » ne
    /// penche plus les lettres.
    /// </summary>
    public static Font? Italic => Display;

    /// <summary>Survol et focus : petit grossissement élastique et son ; s'applique à tout contrôle cliquable.</summary>
    public static void Juicy(Control c)
    {
        void Grow(float s)
        {
            if (!c.IsInsideTree()) return;
            c.PivotOffset = c.Size / 2f;
            var tw = c.CreateTween();
            tw.TweenProperty(c, "scale", Vector2.One * s, 0.12).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        c.MouseEntered += () => { if (c is BaseButton { Disabled: true }) return; Sfx.Play(c, SfxKind.UiHover); Grow(1.05f); };
        c.MouseExited += () => Grow(1f);
        c.FocusEntered += () => { Grow(1.05f); };
        c.FocusExited += () => Grow(1f);
    }

    /// <summary>Apparition en glissant depuis la gauche avec un léger retard (cascade de boutons).</summary>
    public static void SlideIn(Control c, float delay, float from = -60f)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        var tw = c.CreateTween();
        tw.TweenInterval(delay);
        tw.TweenCallback(Callable.From(() => { c.Position += new Vector2(from, 0); }));
        tw.SetParallel();
        tw.TweenProperty(c, "modulate:a", 1.0, 0.22).SetDelay(delay);
        tw.TweenProperty(c, "position:x", c.Position.X, 0.32).SetDelay(delay).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Coup de tampon : le contrôle apparaît gros et se pose (annonces, bandeaux).</summary>
    public static void Punch(Control c, float from = 1.6f, double time = 0.28)
    {
        c.PivotOffset = c.Size / 2f;
        c.Scale = Vector2.One * from;
        var tw = c.CreateTween();
        tw.TweenProperty(c, "scale", Vector2.One, time).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public static Control Spacer(float h) => new() { CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>Fond plein écran semi-opaque qui bloque les clics (menus par-dessus le jeu).</summary>
    /// <summary>Voile plein écran derrière un menu : le jeu flouté puis teinté bleu nuit (alpha = force de la teinte).</summary>
    public static ColorRect Dim(float alpha = 0.6f)
    {
        var r = new ColorRect { Color = new Color(new Color("0d0c10"), alpha), MouseFilter = Control.MouseFilterEnum.Stop, Material = BlurMaterial() };   // pas de cache static : un ShaderMaterial gardé en static fuit à la fermeture
        r.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        return r;
    }

    static ShaderMaterial BlurMaterial() => new()
    {
        Shader = new Shader
        {
            Code = @"shader_type canvas_item;
uniform sampler2D screen_tex : hint_screen_texture, filter_linear_mipmap;
void fragment() {
    vec2 px = SCREEN_PIXEL_SIZE * 3.0;
    vec3 b = textureLod(screen_tex, SCREEN_UV, 3.0).rgb * 0.4;
    b += textureLod(screen_tex, SCREEN_UV + vec2(px.x, px.y), 3.0).rgb * 0.15;
    b += textureLod(screen_tex, SCREEN_UV + vec2(-px.x, px.y), 3.0).rgb * 0.15;
    b += textureLod(screen_tex, SCREEN_UV + vec2(px.x, -px.y), 3.0).rgb * 0.15;
    b += textureLod(screen_tex, SCREEN_UV + vec2(-px.x, -px.y), 3.0).rgb * 0.15;
    float a = COLOR.a;
    COLOR = vec4(mix(b, COLOR.rgb, a), min(1.0, a * 1.6));
}",
        },
    };
}
