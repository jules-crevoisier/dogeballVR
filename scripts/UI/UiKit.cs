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
    public static readonly Color Navy = new("090e1a");
    public static readonly Color NavySoft = new("152038");
    public static readonly Color Orange = new("ff5e1a");
    public static readonly Color Blue = new("1ea7ff");
    public static readonly Color Sky = new("56cbff");
    public static readonly Color Yellow = new("d6ff2e");   // Volt arcade : sélection, alerte, balle
    public static readonly Color Pink = new("ff2e6d");
    public static readonly Color Cream = new("f4f7fc");    // Blanc froid net (plus de crème cartoon)
    public static readonly Color Grey = new("4b5a78");
    public static readonly Color Green = new("2fe680");
    // Surfaces sombres type diffuseur e-sport EVA (carbone / verre fumé).
    public static readonly Color VioletTop = new("0d1528");
    public static readonly Color VioletMid = new("0a1120");
    public static readonly Color NavyLow = new("070b15");
    public static readonly Color Violet = new("1a2a4a");
    public static readonly Color Gold = new("b8e619");
    public static readonly Color Lavender = new("8b9bb8");
    static readonly Color Ink = new("04070f");
    static readonly Color Hairline = new(1f, 1f, 1f, 0.16f);

    /// <summary>Fenêtre (options, pause, fin de match…) : verre carbone sombre, filet 2 px net, ombre douce.</summary>
    public static KcBox PanelBox(float slant = 10f, int margin = 36)
    {
        var b = new KcBox
        {
            Top = new Color(VioletTop, 0.94f), Bottom = new Color(NavyLow, 0.96f), BorderWidth = 2, BorderColor = Hairline, Slant = slant,
            ShadowOffset = new Vector2(0, 12), ShadowColor = new Color(Ink, 0.65f),
            StripOffset = new Vector2(0, 0), StripColor = new Color(0, 0, 0, 0),
            AccentColor = Orange, AccentWidth = 4,
        };
        b.ContentMarginLeft = b.ContentMarginRight = margin + slant * 0.5f;
        b.ContentMarginTop = b.ContentMarginBottom = margin * 0.72f;
        return b;
    }

    /// <summary>Encart dans une fenêtre (description, ligne de tableau, carte) : aplat carbone discret, filet fin.</summary>
    public static KcBox InsetBox(Color? fill = null, float slant = 6f, int border = 1, Color? borderColor = null)
    {
        var c = fill ?? new Color(Ink, 0.55f);
        var b = new KcBox { Top = c, Bottom = c.Darkened(0.08f), Slant = slant, BorderWidth = border, BorderColor = borderColor ?? Hairline };
        b.ContentMarginLeft = b.ContentMarginRight = 20 + Mathf.Abs(slant) * 0.5f;
        b.ContentMarginTop = 8; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Touche de clavier : pavé sombre usiné à bordure nette, lisible sur fond clair comme foncé.</summary>
    public static KcBox KeyBox()
    {
        var b = new KcBox
        {
            Top = new Color("1b2742"), Bottom = new Color("11192d"), Slant = 3,
            BorderWidth = 1.5f, BorderColor = new Color(1f, 1f, 1f, 0.28f),
            ShadowOffset = new Vector2(0, 2), ShadowColor = new Color(Ink, 0.7f),
        };
        b.ContentMarginLeft = b.ContentMarginRight = 10; b.ContentMarginTop = 2; b.ContentMarginBottom = 3;
        return b;
    }

    /// <summary>Bouton au repos : carbone translucide à liseré fin et accent latéral net.</summary>
    public static KcBox ButtonIdle(Color accent, bool box = true)
    {
        var b = new KcBox
        {
            Top = box ? new Color("131d36", 0.92f) : new Color(0, 0, 0, 0), Bottom = box ? new Color("0b1224", 0.95f) : new Color(0, 0, 0, 0),
            BorderWidth = box ? 1.5f : 0, BorderColor = Hairline, Slant = 8,
            ShadowOffset = new Vector2(0, 4), ShadowColor = new Color(Ink, box ? 0.5f : 0f),
            AccentColor = accent, AccentWidth = 5,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 26; b.ContentMarginTop = 8; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Bouton sélectionné (survol ou clavier) : barre Volt arcade ultra nette avec chevron e-sport.</summary>
    public static KcBox ButtonLit(bool pressed = false)
    {
        var b = new KcBox
        {
            Top = pressed ? Gold : Yellow, Bottom = pressed ? Gold.Darkened(0.12f) : Gold, Slant = 8,
            ShadowOffset = new Vector2(0, 6), ShadowColor = new Color(Ink, 0.55f), Star = true,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 26; b.ContentMarginTop = 8; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Titre de fenêtre : Barlow Condensed ExtraBold Italic, ombre nette sans gros contour cartoon.</summary>
    public static Label Title(string text, int size = 54, HorizontalAlignment align = HorizontalAlignment.Center, Color? color = null)
    {
        var l = Text(text, size, color ?? Cream, 2, align);
        if (Italic is Font it) l.AddThemeFontOverride("font", it);
        l.AddThemeColorOverride("font_shadow_color", new Color(Ink, 0.85f));
        l.AddThemeConstantOverride("shadow_offset_x", 0);
        l.AddThemeConstantOverride("shadow_offset_y", Mathf.Max(3, size / 14));
        l.AddThemeConstantOverride("shadow_outline_size", 4);
        return l;
    }

    public static Font? Font => font ??= LoadFont("res://assets/fonts/Barlow-SemiBold.ttf", "res://assets/fonts/LilitaOne-Regular.ttf");
    static Font? font;

    /// <summary>Police sport condensée droite : chiffres de score, chrono, en-têtes techniques.</summary>
    public static Font? Display => display ??= LoadFont("res://assets/fonts/BarlowCondensed-ExtraBold.ttf", "res://assets/fonts/Barlow-SemiBold.ttf");
    static Font? display;

    static Font? LoadFont(string primary, string fallback) =>
        ResourceLoader.Exists(primary) ? GD.Load<Font>(primary) :
        ResourceLoader.Exists(fallback) ? GD.Load<Font>(fallback) : null;

    public static StyleBoxFlat Box(Color bg, int radius = 4, float skew = 0f, int border = 0, Color? borderColor = null, int bottom = 3)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 8, ContentMarginBottom = 8,
            BorderWidthBottom = bottom + border, BorderWidthLeft = border, BorderWidthRight = border, BorderWidthTop = border,
            BorderColor = borderColor ?? new Color(1f, 1f, 1f, 0.14f),
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
        t.SetColor("font_outline_color", "Label", new Color(Ink, 0.85f));
        t.SetConstant("outline_size", "Label", 2);

        var disabled = ButtonIdle(new Color(0, 0, 0, 0));
        disabled.Top = new Color(Ink, 0.5f); disabled.Bottom = new Color(Ink, 0.5f); disabled.BorderColor = new Color(Cream, 0.15f);
        t.SetStylebox("normal", "Button", ButtonIdle(new Color(0, 0, 0, 0)));
        t.SetStylebox("hover", "Button", ButtonLit());
        t.SetStylebox("pressed", "Button", ButtonLit(true));
        t.SetStylebox("hover_pressed", "Button", ButtonLit(true));
        t.SetStylebox("focus", "Button", ButtonLit());
        t.SetStylebox("disabled", "Button", disabled);
        t.SetColor("font_color", "Button", Colors.White);
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "Button", Navy);
        t.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.38f));
        t.SetColor("font_outline_color", "Button", new Color(Ink, 0.7f));
        t.SetConstant("outline_size", "Button", 0);
        t.SetFontSize("font_size", "Button", 26);
        if (Italic != null) t.SetFont("font", "Button", Italic);

        // Glissière : rail carbone fin, remplissage Volt arcade.
        var slider = new KcBox { Top = new Color(Ink, 0.8f), Bottom = new Color(Ink, 0.8f), BorderWidth = 1.5f, BorderColor = Hairline, Slant = 4 };
        slider.ContentMarginTop = 8; slider.ContentMarginBottom = 8;
        var fill = new KcBox { Top = Yellow, Bottom = Gold, Slant = 4 };
        fill.ContentMarginTop = 6; fill.ContentMarginBottom = 6;
        t.SetStylebox("slider", "HSlider", slider);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", fill);
        t.SetIcon("grabber", "HSlider", Dot(22, Cream));
        t.SetIcon("grabber_highlight", "HSlider", Dot(26, Yellow));

        t.SetStylebox("panel", "PanelContainer", PanelBox());
        // Champ de saisie : encart carbone fin ; Volt au focus.
        var field = InsetBox(new Color(Ink, 0.82f), 6, 1, new Color(1f, 1f, 1f, 0.24f));
        field.ContentMarginTop = 6; field.ContentMarginBottom = 6;
        var fieldLit = InsetBox(new Color(Ink, 0.88f), 6, 2, Yellow);
        fieldLit.ContentMarginTop = 6; fieldLit.ContentMarginBottom = 6;
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", fieldLit);
        t.SetColor("font_color", "LineEdit", Colors.White);
        t.SetColor("font_placeholder_color", "LineEdit", new Color(Lavender, 0.75f));
        t.SetColor("caret_color", "LineEdit", Yellow);
        t.SetFontSize("font_size", "LineEdit", 24);

        foreach (var k in new[] { "normal", "hover", "pressed", "focus", "hover_pressed" })
            t.SetStylebox(k, "CheckButton", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckButton", Colors.White);
        t.SetColor("font_hover_color", "CheckButton", Yellow);
        t.SetColor("font_pressed_color", "CheckButton", Colors.White);
        t.SetColor("font_outline_color", "CheckButton", Ink);
        t.SetConstant("outline_size", "CheckButton", 2);

        t.SetStylebox("normal", "OptionButton", ButtonIdle(new Color(0, 0, 0, 0)));
        t.SetStylebox("hover", "OptionButton", ButtonLit());
        t.SetStylebox("pressed", "OptionButton", ButtonLit(true));
        t.SetStylebox("focus", "OptionButton", ButtonLit());
        t.SetColor("font_color", "OptionButton", Colors.White);
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "OptionButton", Navy);
        t.SetColor("font_outline_color", "OptionButton", Ink);
        t.SetConstant("outline_size", "OptionButton", 0);
        if (Italic != null) t.SetFont("font", "OptionButton", Italic);

        var popup = PanelBox(6, 12);
        popup.StripColor = new Color(0, 0, 0, 0); popup.BorderWidth = 1.5f; popup.ShadowOffset = new Vector2(0, 8);
        t.SetStylebox("panel", "PopupMenu", popup);
        t.SetStylebox("hover", "PopupMenu", ButtonLit());
        t.SetColor("font_color", "PopupMenu", Colors.White);
        t.SetColor("font_hover_color", "PopupMenu", Navy);
        t.SetFontSize("font_size", "PopupMenu", 22);

        var scroll = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.1f), ContentMarginLeft = 4, ContentMarginRight = 4 };
        var grab = new StyleBoxFlat { BgColor = new Color(1, 1, 1, 0.4f) };
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
                var col = d < r - 3 ? c : d < r - 1 ? Navy : new Color(0, 0, 0, 0);
                if (d >= r - 1 && d < r) col = new Color(Navy, r - d);
                img.SetPixel(x, y, col);
            }
        return ImageTexture.CreateFromImage(img);
    }

    public static Label Text(string text, int size, Color? color = null, int outline = 4, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = new Label { Text = text, HorizontalAlignment = align, MouseFilter = Control.MouseFilterEnum.Ignore };
        l.AddThemeFontSizeOverride("font_size", size);
        if (color is Color c) l.AddThemeColorOverride("font_color", c);
        // Contours mesurés (pas de grosses bulles cartoon) + ombre portée nette pour la lisibilité sur 3D.
        int clamped = outline <= 0 ? 0 : Mathf.Clamp(outline / 2, 1, 5);
        l.AddThemeConstantOverride("outline_size", clamped);
        l.AddThemeColorOverride("font_outline_color", new Color(Ink, 0.9f));
        if (outline > 0)
        {
            l.AddThemeColorOverride("font_shadow_color", new Color(Ink, 0.72f));
            l.AddThemeConstantOverride("shadow_offset_x", 0);
            l.AddThemeConstantOverride("shadow_offset_y", 2);
        }
        return l;
    }

    /// <summary>Texte sport condensé droit (scores, chronos, compteurs e-sport).</summary>
    public static Label DisplayText(string text, int size, Color? color = null, int outline = 4, HorizontalAlignment align = HorizontalAlignment.Left)
    {
        var l = Text(text, size, color, outline, align);
        if (Display is Font d) l.AddThemeFontOverride("font", d);
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
        b.AddThemeColorOverride("font_outline_color", Ink);
        b.AddThemeConstantOverride("outline_size", 0);
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
        b.FocusEntered += () => { b.AddThemeConstantOverride("outline_size", 0); Shift(shift); Sfx.Play(b, SfxKind.UiHover); };
        b.FocusExited += () => { b.AddThemeConstantOverride("outline_size", 0); Shift(0f); };
        b.MouseEntered += () => { if (!b.Disabled && b.FocusMode != Control.FocusModeEnum.None) b.GrabFocus(); };
    }

    /// <summary>Change l'aplat de repos d'un bouton habillé par Style (accent à gauche, remplissage ; null : carbone) et la couleur de son texte.</summary>
    public static void Tint(Button b, Color accent, Color? fill = null, Color? text = null)
    {
        if (b.GetThemeStylebox("normal") is not KcBox idle) return;
        var f = fill ?? new Color("131d36", 0.92f);
        idle.AccentColor = accent;
        idle.Top = f;
        idle.Bottom = fill is Color c ? c.Darkened(0.25f) : new Color("0b1224", 0.95f);
        b.AddThemeColorOverride("font_color", text ?? Colors.White);
    }

    /// <summary>Bouton secondaire en texte seul (crédits, quitter) : gris bleuté au repos, barre Volt au focus.</summary>
    public static Button TextButton(string text, Action onPress, int fontSize = 26)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(440, 48), FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        Style(b, ButtonIdle(new Color(0, 0, 0, 0), false), Lavender);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>
    /// Bouton de liste (pause) : texte penché sans boîte au repos, barre Volt avec chevron sur l'élément sélectionné.
    /// </summary>
    public static Button ListButton(string text, Action onPress, int fontSize = 28, Vector2? minSize = null)
    {
        var b = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(440, 62), FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        var idle = ButtonIdle(new Color(0, 0, 0, 0), false);
        idle.ContentMarginLeft = 48;
        Style(b, idle, Colors.White, 22f);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>
    /// Touche de clavier usinée : pavé sombre à bordure fine et texte clair, lisible sur n'importe quel fond.
    /// </summary>
    public static Label KeyCap(string text, int size = 20, float minWidth = 0)
    {
        var l = DisplayText(text.ToUpperInvariant(), size, Cream, 0, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        if (minWidth > 0) l.CustomMinimumSize = new Vector2(minWidth, 0);
        l.AddThemeStyleboxOverride("normal", KeyBox());
        return l;
    }

    /// <summary>Aide de bas d'écran : [touche] action.</summary>
    public static HBoxContainer KeyHint(string key, string label, int size = 20)
    {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 10);
        h.AddChild(KeyCap(key, size - 2));
        var l = Text(label.ToUpperInvariant(), size - 2, new Color(Cream, 0.85f), 2);
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
        center.OffsetLeft = 24;
        b.AddChild(center);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        center.AddChild(row);
        row.AddChild(KeyCap(key, 18));
        var l = Text(text, fontSize, color == Grey ? Lavender : Colors.White, 0);
        if (Italic is Font it) l.AddThemeFontOverride("font", it);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        b.FocusEntered += () => { l.AddThemeColorOverride("font_color", Navy); l.AddThemeConstantOverride("outline_size", 0); };
        b.FocusExited += () => { l.AddThemeColorOverride("font_color", color == Grey ? Lavender : Colors.White); l.AddThemeConstantOverride("outline_size", 0); };
        return b;
    }

    /// <summary>Vraie police Barlow Condensed ExtraBold Italic pour les titres, boutons et annonces e-sport.</summary>
    public static Font? Italic => italic ??= LoadFont("res://assets/fonts/BarlowCondensed-ExtraBoldItalic.ttf", "res://assets/fonts/BarlowCondensed-ExtraBold.ttf");
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
        c.MouseEntered += () => { if (c is BaseButton { Disabled: true }) return; Sfx.Play(c, SfxKind.UiHover); Grow(1.04f); };
        c.MouseExited += () => Grow(1f);
        c.FocusEntered += () => { Grow(1.04f); };
        c.FocusExited += () => Grow(1f);
    }

    /// <summary>Apparition en glissant depuis la gauche avec un léger retard (cascade de boutons).</summary>
    public static void SlideIn(Control c, float delay, float from = -48f)
    {
        c.Modulate = new Color(1, 1, 1, 0);
        var tw = c.CreateTween();
        tw.TweenInterval(delay);
        tw.TweenCallback(Callable.From(() => { c.Position += new Vector2(from, 0); }));
        tw.SetParallel();
        tw.TweenProperty(c, "modulate:a", 1.0, 0.20).SetDelay(delay);
        tw.TweenProperty(c, "position:x", c.Position.X, 0.28).SetDelay(delay).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
    }

    /// <summary>Coup de tampon : le contrôle apparaît gros et se pose (annonces, bandeaux).</summary>
    public static void Punch(Control c, float from = 1.45f, double time = 0.24)
    {
        c.PivotOffset = c.Size / 2f;
        c.Scale = Vector2.One * from;
        var tw = c.CreateTween();
        tw.TweenProperty(c, "scale", Vector2.One, time).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public static Control Spacer(float h) => new() { CustomMinimumSize = new Vector2(0, h), MouseFilter = Control.MouseFilterEnum.Ignore };

    /// <summary>Voile plein écran derrière un menu : le jeu flouté puis teinté carbone sombre.</summary>
    public static ColorRect Dim(float alpha = 0.62f)
    {
        var r = new ColorRect { Color = new Color(new Color("060a14"), alpha), MouseFilter = Control.MouseFilterEnum.Stop, Material = BlurMaterial() };
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
