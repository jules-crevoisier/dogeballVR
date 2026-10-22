using System;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Briques d'interface communes (menus, pause, options, fin de partie, HUD) dans la DA Knockout City :
/// police Lilita One, texte blanc cerné de marine. Fenêtres et boutons reprennent le menu principal (voir KcBox) : panneaux
/// penchés violet profond → marine à liseré crème, ombre et bande violette en retrait ; boutons en lettres penchées qui passent
/// en marine sur une barre jaune inclinée (étoile devant le texte) au survol ou au clavier, et glissent un peu vers la droite.
/// </summary>
public static class UiKit
{
    public static readonly Color Navy = new("101830");
    public static readonly Color NavySoft = new("1c2a52");
    public static readonly Color Orange = new("ff6a1a");
    public static readonly Color Blue = new("3a7bd5");
    public static readonly Color Sky = new("6cc8ff");
    public static readonly Color Yellow = new("ffd23f");
    public static readonly Color Pink = new("ff3d7f");
    public static readonly Color Cream = new("fff4e0");
    public static readonly Color Grey = new("55607f");
    public static readonly Color Green = new("4fdc7a");
    // Couleurs du bandeau du menu principal.
    public static readonly Color VioletTop = new("2e1260");
    public static readonly Color VioletMid = new("221447");
    public static readonly Color NavyLow = new("121a3c");
    public static readonly Color Violet = new("5a1a8a");
    public static readonly Color Gold = new("ffb21f");
    public static readonly Color Lavender = new("a9a6cf");
    static readonly Color Ink = new("0a0f22");

    /// <summary>Fenêtre (options, pause, fin de match…) : le bandeau du menu en panneau penché, liseré crème, ombre et bande violette.</summary>
    public static KcBox PanelBox(float slant = 22f, int margin = 40)
    {
        var b = new KcBox
        {
            Top = VioletTop, Bottom = NavyLow, BorderWidth = 7, BorderColor = Cream, Slant = slant,
            ShadowOffset = new Vector2(12, 12), ShadowColor = new Color(Ink, 0.6f),
            StripOffset = new Vector2(26, 26), StripColor = new Color(Violet, 0.75f),
        };
        b.ContentMarginLeft = b.ContentMarginRight = margin + slant * 0.5f;
        b.ContentMarginTop = b.ContentMarginBottom = margin * 0.7f;
        return b;
    }

    /// <summary>Encart dans une fenêtre (description, ligne de tableau, carte) : aplat sombre penché, sans liseré.</summary>
    public static KcBox InsetBox(Color? fill = null, float slant = 10f, int border = 0, Color? borderColor = null)
    {
        var c = fill ?? new Color(Ink, 0.45f);
        var b = new KcBox { Top = c, Bottom = c.Darkened(0.12f), Slant = slant, BorderWidth = border, BorderColor = borderColor ?? Cream };
        b.ContentMarginLeft = b.ContentMarginRight = 22 + Mathf.Abs(slant) * 0.5f;
        b.ContentMarginTop = 8; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Bouton au repos : aplat violet nuit penché à fin liseré crème, accent de couleur à gauche (transparent : aucun).</summary>
    public static KcBox ButtonIdle(Color accent, bool box = true)
    {
        var b = new KcBox
        {
            Top = box ? new Color("2a1458") : new Color(0, 0, 0, 0), Bottom = box ? new Color("17183c") : new Color(0, 0, 0, 0),
            BorderWidth = box ? 3 : 0, BorderColor = new Color(Cream, 0.9f), Slant = 12,
            ShadowOffset = new Vector2(6, 6), ShadowColor = new Color(Ink, box ? 0.55f : 0f),
            AccentColor = accent, AccentWidth = 9,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 28; b.ContentMarginTop = 6; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Bouton sélectionné (survol ou clavier) : la barre jaune du menu, dorée vers le bas, avec l'étoile marine.</summary>
    public static KcBox ButtonLit(bool pressed = false)
    {
        var b = new KcBox
        {
            Top = pressed ? Gold : Yellow, Bottom = pressed ? Gold.Darkened(0.15f) : Gold, Slant = 12,
            ShadowOffset = new Vector2(7, 7), ShadowColor = new Color(Ink, 0.55f), Star = true,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 28; b.ContentMarginTop = 6; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Titre de fenêtre : lettres penchées jaunes cernées de marine, ombre portée encre décalée (comme le logo).</summary>
    public static Label Title(string text, int size = 54, HorizontalAlignment align = HorizontalAlignment.Center, Color? color = null)
    {
        var l = Text(text, size, color ?? Yellow, Mathf.Max(8, size / 5), align);
        if (Italic is Font it) l.AddThemeFontOverride("font", it);
        l.AddThemeColorOverride("font_shadow_color", Ink);
        l.AddThemeConstantOverride("shadow_offset_x", Mathf.Max(3, size / 12));
        l.AddThemeConstantOverride("shadow_offset_y", Mathf.Max(4, size / 10));
        l.AddThemeConstantOverride("shadow_outline_size", Mathf.Max(8, size / 5));
        return l;
    }

    public static Font? Font => font ??= ResourceLoader.Exists("res://assets/fonts/LilitaOne-Regular.ttf") ? GD.Load<Font>("res://assets/fonts/LilitaOne-Regular.ttf") : null;
    static Font? font;

    public static StyleBoxFlat Box(Color bg, int radius = 16, float skew = 0f, int border = 0, Color? borderColor = null, int bottom = 6)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 26, ContentMarginRight = 26, ContentMarginTop = 10, ContentMarginBottom = 10,
            BorderWidthBottom = bottom + border, BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border,
            BorderColor = borderColor ?? new Color(0.04f, 0.06f, 0.14f, 0.95f),
            Skew = new Vector2(skew, 0), AntiAliasing = true,
        };
        return b;
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
        var slider = new KcBox { Top = new Color(Ink, 0.7f), Bottom = new Color(Ink, 0.7f), BorderWidth = 2, BorderColor = new Color(Cream, 0.8f), Slant = 6 };
        slider.ContentMarginTop = 9; slider.ContentMarginBottom = 9;
        var fill = new KcBox { Top = Yellow, Bottom = Gold, Slant = 6 };
        fill.ContentMarginTop = 6; fill.ContentMarginBottom = 6;
        t.SetStylebox("slider", "HSlider", slider);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", fill);
        t.SetIcon("grabber", "HSlider", Dot(26, Cream));
        t.SetIcon("grabber_highlight", "HSlider", Dot(30, Yellow));

        t.SetStylebox("panel", "PanelContainer", PanelBox());
        // Champ de saisie : encart encre penché à liseré crème ; jaune au focus.
        var field = InsetBox(new Color(Ink, 0.8f), 10, 3, new Color(Cream, 0.9f));
        field.ContentMarginTop = 6; field.ContentMarginBottom = 6;
        var fieldLit = InsetBox(new Color(Ink, 0.8f), 10, 4, Yellow);
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

        var popup = PanelBox(8, 14);
        popup.StripColor = new Color(0, 0, 0, 0); popup.BorderWidth = 4; popup.ShadowOffset = new Vector2(6, 6);
        t.SetStylebox("panel", "PopupMenu", popup);
        t.SetStylebox("hover", "PopupMenu", ButtonLit());
        t.SetColor("font_color", "PopupMenu", Colors.White);
        t.SetColor("font_hover_color", "PopupMenu", Navy);
        t.SetFontSize("font_size", "PopupMenu", 22);

        var scroll = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.12f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4, ContentMarginLeft = 4, ContentMarginRight = 4 };
        var grab = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.5f), CornerRadiusTopLeft = 4, CornerRadiusTopRight = 4, CornerRadiusBottomLeft = 4, CornerRadiusBottomRight = 4 };
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
        Style(b, ButtonIdle(secondary ? new Color(0, 0, 0, 0) : color), secondary ? Lavender : Colors.White);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>Habille un bouton : repos (idle), barre jaune au focus et au survol, texte qui glisse, son. Rest = couleur du texte au repos.</summary>
    public static void Style(Button b, KcBox idle, Color rest, float shift = 22f)
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
        b.AddThemeConstantOverride("outline_size", 6);
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
        b.FocusEntered += () => { b.AddThemeConstantOverride("outline_size", 1); Shift(shift); Sfx.Play(b, SfxKind.UiHover); };
        b.FocusExited += () => { b.AddThemeConstantOverride("outline_size", 6); Shift(0f); };
        b.MouseEntered += () => { if (!b.Disabled && b.FocusMode != Control.FocusModeEnum.None) b.GrabFocus(); };
    }

    /// <summary>Change l'aplat de repos d'un bouton habillé par Style (accent à gauche, remplissage ; null : violet nuit d'origine) et la couleur de son texte.</summary>
    public static void Tint(Button b, Color accent, Color? fill = null, Color? text = null)
    {
        if (b.GetThemeStylebox("normal") is not KcBox idle) return;
        var f = fill ?? new Color("2a1458");
        idle.AccentColor = accent;
        idle.Top = f;
        idle.Bottom = fill is Color c ? c.Darkened(0.3f) : new Color("17183c");
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
        idle.ContentMarginLeft = 54;   // place de l'étoile
        Style(b, idle, Colors.White, 26f);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>
    /// Touche de clavier dessinée (même dessin que le HUD) : cadre clair arrondi, liseré gris, tranche plus foncée en bas
    /// (relief), texte marine. minWidth aligne les touches d'une colonne.
    /// </summary>
    public static Label KeyCap(string text, int size = 20, float minWidth = 0)
    {
        var l = Text(text, size, Navy, 0, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        if (minWidth > 0) l.CustomMinimumSize = new Vector2(minWidth, 0);
        l.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("f6f1e7"), AntiAliasing = true,
            CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 5, BorderColor = new Color("8d97ad"),
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 1, ContentMarginBottom = 3,
            ShadowColor = new Color(Navy, 0.6f), ShadowSize = 3,
        });
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
        center.OffsetLeft = 24;   // place de l'étoile de la barre jaune
        b.AddChild(center);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        center.AddChild(row);
        row.AddChild(KeyCap(key, 20));
        var l = Text(text, fontSize, color == Grey ? Lavender : Colors.White, 6);
        if (Italic is Font it) l.AddThemeFontOverride("font", it);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        // Le texte est un Label à part : il suit lui-même la barre jaune (marine au focus).
        b.FocusEntered += () => { l.AddThemeColorOverride("font_color", Navy); l.AddThemeConstantOverride("outline_size", 1); };
        b.FocusExited += () => { l.AddThemeColorOverride("font_color", color == Grey ? Lavender : Colors.White); l.AddThemeConstantOverride("outline_size", 6); };
        return b;
    }

    /// <summary>
    /// Lilita One penchée et un peu grasse (pas d'italique dans la police) : titres et items à la Knockout City.
    /// La matrice passe telle quelle à FreeType (repère y vers le haut) : le biais va dans la composante y de l'axe x
    /// (x' = x + 0,22·y), sinon c'est la ligne de base qui penche au lieu des lettres.
    /// </summary>
    public static Font? Italic => italic ??= Font == null ? null : new FontVariation { BaseFont = Font, VariationTransform = new Transform2D(new Vector2(1, 0.22f), new Vector2(0, 1), Vector2.Zero), VariationEmbolden = 0.35f };
    static Font? italic;

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
        var r = new ColorRect { Color = new Color(new Color("140c34"), alpha), MouseFilter = Control.MouseFilterEnum.Stop, Material = BlurMaterial() };   // pas de cache static : un ShaderMaterial gardé en static fuit à la fermeture
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
