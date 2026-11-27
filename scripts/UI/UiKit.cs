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
    public static readonly Color Navy = new("141821");
    public static readonly Color NavySoft = new("232938");
    public static readonly Color Orange = new("ff6414");
    public static readonly Color Blue = new("148bff");
    public static readonly Color Sky = new("52b8ff");
    public static readonly Color Yellow = new("ff9f1c");   // Ambre sportif chaud (fini le vert fluo IA dans l'UI)
    public static readonly Color Pink = new("ff2e63");
    public static readonly Color Cream = new("ffffff");
    public static readonly Color Grey = new("647087");
    public static readonly Color Green = new("22c55e");
    // Surfaces propres jeu vidéo AAA (verre fumé doux / blanc givré / charbon).
    public static readonly Color VioletTop = new("181d29");
    public static readonly Color VioletMid = new("141821");
    public static readonly Color NavyLow = new("10141d");
    public static readonly Color Violet = new("252c3d");
    public static readonly Color Gold = new("ff7a29");
    public static readonly Color Lavender = new("94a0b8");
    public static readonly Color CardWhite = new("f8f9fc");
    public static readonly Color InkDark = new("141821");
    static readonly Color Ink = new("090c14");
    static readonly Color Hairline = new(1f, 1f, 1f, 0.16f);

    /// <summary>StyleBox arrondi propre avec ombre douce (pilules, cartes blanches, boutons AAA).</summary>
    public static StyleBoxFlat Pill(Color bg, int radius = 14, int border = 0, Color? borderColor = null, int shadow = 12, float shadowAlpha = 0.22f)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius,
            CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            BorderWidthTop = border, BorderWidthBottom = border, BorderWidthLeft = border, BorderWidthRight = border,
            BorderColor = borderColor ?? new Color(1f, 1f, 1f, 0.18f),
            ShadowColor = new Color(0f, 0f, 0f, shadow > 0 ? shadowAlpha : 0f),
            ShadowSize = shadow, ShadowOffset = new Vector2(0, shadow > 0 ? 4 : 0),
            AntiAliasing = true, AntiAliasingSize = 1.2f,
        };
        return b;
    }

    /// <summary>Fenêtre modale (options, pause, fin de match…) : panneau charbon propre, sans biais exagéré.</summary>
    public static KcBox PanelBox(float slant = 0f, int margin = 32)
    {
        var b = new KcBox
        {
            Top = new Color(VioletTop, 0.95f), Bottom = new Color(NavyLow, 0.97f), BorderWidth = 1.5f, BorderColor = Hairline, Slant = 0,
            ShadowOffset = new Vector2(0, 10), ShadowColor = new Color(Ink, 0.5f),
            StripOffset = Vector2.Zero, StripColor = new Color(0, 0, 0, 0),
            AccentColor = Orange, AccentWidth = 4,
        };
        b.ContentMarginLeft = b.ContentMarginRight = margin;
        b.ContentMarginTop = b.ContentMarginBottom = margin * 0.78f;
        return b;
    }

    /// <summary>Encart dans une fenêtre (description, ligne de tableau, carte).</summary>
    public static KcBox InsetBox(Color? fill = null, float slant = 0f, int border = 1, Color? borderColor = null)
    {
        var c = fill ?? new Color(Ink, 0.45f);
        var b = new KcBox { Top = c, Bottom = c.Darkened(0.06f), Slant = 0, BorderWidth = border, BorderColor = borderColor ?? new Color(1f, 1f, 1f, 0.12f) };
        b.ContentMarginLeft = b.ContentMarginRight = 18;
        b.ContentMarginTop = 8; b.ContentMarginBottom = 8;
        return b;
    }

    /// <summary>Touche de clavier discrète et nette : pastille arrondie translucide.</summary>
    public static StyleBoxFlat KeyBox()
    {
        var b = Pill(new Color(Ink, 0.52f), 6, 1, new Color(1f, 1f, 1f, 0.35f), 0);
        b.ContentMarginLeft = b.ContentMarginRight = 8; b.ContentMarginTop = 2; b.ContentMarginBottom = 2;
        return b;
    }

    /// <summary>Bouton au repos : surface arrondie sobre.</summary>
    public static KcBox ButtonIdle(Color accent, bool box = true)
    {
        var b = new KcBox
        {
            Top = box ? new Color("1f2536", 0.94f) : new Color(0, 0, 0, 0), Bottom = box ? new Color("181d2b", 0.96f) : new Color(0, 0, 0, 0),
            BorderWidth = box ? 1.5f : 0, BorderColor = Hairline, Slant = 0,
            ShadowOffset = new Vector2(0, 4), ShadowColor = new Color(Ink, box ? 0.32f : 0f),
            AccentColor = accent, AccentWidth = 4,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 24; b.ContentMarginTop = 10; b.ContentMarginBottom = 10;
        return b;
    }

    /// <summary>Bouton sélectionné (survol ou clavier) : orange vif sportif.</summary>
    public static KcBox ButtonLit(bool pressed = false)
    {
        var b = new KcBox
        {
            Top = pressed ? Orange.Darkened(0.12f) : Orange, Bottom = pressed ? Orange.Darkened(0.22f) : Orange.Darkened(0.08f), Slant = 0,
            ShadowOffset = new Vector2(0, 5), ShadowColor = new Color(Orange, 0.35f), Star = false,
        };
        b.ContentMarginLeft = b.ContentMarginRight = 24; b.ContentMarginTop = 10; b.ContentMarginBottom = 10;
        return b;
    }

    /// <summary>Titre de fenêtre : Barlow Condensed ExtraBold propre, ombre douce.</summary>
    public static Label Title(string text, int size = 50, HorizontalAlignment align = HorizontalAlignment.Center, Color? color = null)
    {
        var l = DisplayText(text, size, color ?? Cream, 0, align);
        l.AddThemeColorOverride("font_shadow_color", new Color(Ink, 0.45f));
        l.AddThemeConstantOverride("shadow_offset_x", 0);
        l.AddThemeConstantOverride("shadow_offset_y", 2);
        return l;
    }

    public static Font? Font => font ??= LoadFont("res://assets/fonts/Barlow-SemiBold.ttf", "res://assets/fonts/LilitaOne-Regular.ttf");
    static Font? font;

    /// <summary>Police sport condensée droite : chiffres de score, chrono, en-têtes.</summary>
    public static Font? Display => display ??= LoadFont("res://assets/fonts/BarlowCondensed-ExtraBold.ttf", "res://assets/fonts/Barlow-SemiBold.ttf");
    static Font? display;

    static Font? LoadFont(string primary, string fallback) =>
        ResourceLoader.Exists(primary) ? GD.Load<Font>(primary) :
        ResourceLoader.Exists(fallback) ? GD.Load<Font>(fallback) : null;

    public static StyleBoxFlat Box(Color bg, int radius = 10, float skew = 0f, int border = 0, Color? borderColor = null, int bottom = 0)
    {
        var b = new StyleBoxFlat
        {
            BgColor = bg,
            CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
            ContentMarginLeft = 20, ContentMarginRight = 20, ContentMarginTop = 8, ContentMarginBottom = 8,
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
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.38f));
        t.SetColor("font_outline_color", "Button", new Color(Ink, 0.7f));
        t.SetConstant("outline_size", "Button", 0);
        t.SetFontSize("font_size", "Button", 24);
        if (Display != null) t.SetFont("font", "Button", Display);

        // Glissière propre
        var slider = Pill(new Color(Ink, 0.65f), 6, 1, Hairline, 0);
        slider.ContentMarginTop = 7; slider.ContentMarginBottom = 7;
        var fill = Pill(Orange, 6, 0, null, 0);
        fill.ContentMarginTop = 5; fill.ContentMarginBottom = 5;
        t.SetStylebox("slider", "HSlider", slider);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", fill);
        t.SetIcon("grabber", "HSlider", Dot(22, Cream));
        t.SetIcon("grabber_highlight", "HSlider", Dot(26, Orange));

        t.SetStylebox("panel", "PanelContainer", PanelBox());
        var field = InsetBox(new Color(Ink, 0.75f), 0, 1, new Color(1f, 1f, 1f, 0.24f));
        field.ContentMarginTop = 6; field.ContentMarginBottom = 6;
        var fieldLit = InsetBox(new Color(Ink, 0.85f), 0, 2, Orange);
        fieldLit.ContentMarginTop = 6; fieldLit.ContentMarginBottom = 6;
        t.SetStylebox("normal", "LineEdit", field);
        t.SetStylebox("focus", "LineEdit", fieldLit);
        t.SetColor("font_color", "LineEdit", Colors.White);
        t.SetColor("font_placeholder_color", "LineEdit", new Color(Lavender, 0.75f));
        t.SetColor("caret_color", "LineEdit", Orange);
        t.SetFontSize("font_size", "LineEdit", 22);

        foreach (var k in new[] { "normal", "hover", "pressed", "focus", "hover_pressed" })
            t.SetStylebox(k, "CheckButton", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckButton", Colors.White);
        t.SetColor("font_hover_color", "CheckButton", Orange);
        t.SetColor("font_pressed_color", "CheckButton", Colors.White);
        t.SetColor("font_outline_color", "CheckButton", Ink);
        t.SetConstant("outline_size", "CheckButton", 0);

        t.SetStylebox("normal", "OptionButton", ButtonIdle(new Color(0, 0, 0, 0)));
        t.SetStylebox("hover", "OptionButton", ButtonLit());
        t.SetStylebox("pressed", "OptionButton", ButtonLit(true));
        t.SetStylebox("focus", "OptionButton", ButtonLit());
        t.SetColor("font_color", "OptionButton", Colors.White);
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "OptionButton", Colors.White);
        t.SetColor("font_outline_color", "OptionButton", Ink);
        t.SetConstant("outline_size", "OptionButton", 0);
        if (Display != null) t.SetFont("font", "OptionButton", Display);

        var popup = Pill(new Color(VioletTop, 0.96f), 12, 1, Hairline, 14, 0.4f);
        popup.ContentMarginLeft = popup.ContentMarginRight = 10; popup.ContentMarginTop = popup.ContentMarginBottom = 8;
        t.SetStylebox("panel", "PopupMenu", popup);
        t.SetStylebox("hover", "PopupMenu", ButtonLit());
        t.SetColor("font_color", "PopupMenu", Colors.White);
        t.SetColor("font_hover_color", "PopupMenu", Colors.White);
        t.SetFontSize("font_size", "PopupMenu", 20);

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
        foreach (var k in new[] { "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) b.AddThemeColorOverride(k, Colors.White);
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

    /// <summary>Change l'aplat de repos d'un bouton habillé par Style.</summary>
    public static void Tint(Button b, Color accent, Color? fill = null, Color? text = null)
    {
        if (b.GetThemeStylebox("normal") is not KcBox idle) return;
        var f = fill ?? new Color("1f2536", 0.94f);
        idle.AccentColor = accent;
        idle.Top = f;
        idle.Bottom = fill is Color c ? c.Darkened(0.18f) : new Color("181d2b", 0.96f);
        b.AddThemeColorOverride("font_color", text ?? Colors.White);
    }

    /// <summary>Bouton secondaire en texte seul.</summary>
    public static Button TextButton(string text, Action onPress, int fontSize = 24)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(440, 48), FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        Style(b, ButtonIdle(new Color(0, 0, 0, 0), false), Lavender);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>Bouton de liste.</summary>
    public static Button ListButton(string text, Action onPress, int fontSize = 26, Vector2? minSize = null)
    {
        var b = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(440, 58), FocusMode = Control.FocusModeEnum.All, Alignment = HorizontalAlignment.Left };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        var idle = ButtonIdle(new Color(0, 0, 0, 0), false);
        idle.ContentMarginLeft = 24;
        Style(b, idle, Colors.White, 12f);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    /// <summary>Touche de clavier discrète : pastille arrondie translucide, texte blanc net.</summary>
    public static Label KeyCap(string text, int size = 18, float minWidth = 0)
    {
        var l = DisplayText(text.ToUpperInvariant(), size, Cream, 0, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        if (minWidth > 0) l.CustomMinimumSize = new Vector2(minWidth, 0);
        l.AddThemeStyleboxOverride("normal", KeyBox());
        return l;
    }

    /// <summary>Aide de bas d'écran : [touche] action.</summary>
    public static HBoxContainer KeyHint(string key, string label, int size = 18)
    {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        h.AddThemeConstantOverride("separation", 8);
        h.AddChild(KeyCap(key, size - 2));
        var l = Text(label.ToUpperInvariant(), size - 2, new Color(Cream, 0.9f), 2);
        l.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(l);
        return h;
    }

    /// <summary>Gros bouton avec son raccourci dessiné à gauche du texte.</summary>
    public static Button KeyButton(string text, string key, Color color, Action onPress, int fontSize = 28, Vector2? minSize = null)
    {
        var b = Button("", color, onPress, fontSize, minSize);
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        b.AddChild(center);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 14);
        center.AddChild(row);
        row.AddChild(KeyCap(key, 17));
        var l = DisplayText(text, fontSize, color == Grey ? Lavender : Colors.White, 0);
        l.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(l);
        b.FocusEntered += () => { l.AddThemeColorOverride("font_color", Colors.White); };
        b.FocusExited += () => { l.AddThemeColorOverride("font_color", color == Grey ? Lavender : Colors.White); };
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
