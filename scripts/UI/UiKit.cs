using System;
using Godot;
using DogeballVR.Audio;

namespace DogeballVR.UI;

/// <summary>
/// Briques d'interface communes (menus, pause, options, fin de partie, HUD) dans la DA Knockout City :
/// police Lilita One, texte blanc cerné de marine, panneaux arrondis inclinés, boutons qui grossissent au survol avec un son.
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

        t.SetStylebox("normal", "Button", Box(Blue, 14, -0.12f));
        t.SetStylebox("hover", "Button", Box(Blue.Lightened(0.15f), 14, -0.12f, 3, Yellow));
        t.SetStylebox("pressed", "Button", Box(Blue.Darkened(0.2f), 14, -0.12f));
        t.SetStylebox("focus", "Button", Box(new Color(0, 0, 0, 0), 14, -0.12f, 3, Yellow, 0));
        t.SetStylebox("disabled", "Button", Box(Grey.Darkened(0.3f), 14, -0.12f));
        foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) t.SetColor(k, "Button", Colors.White);
        t.SetColor("font_disabled_color", "Button", new Color(1, 1, 1, 0.4f));
        t.SetColor("font_outline_color", "Button", Navy);
        t.SetConstant("outline_size", "Button", 6);
        t.SetFontSize("font_size", "Button", 26);

        var slider = new StyleBoxFlat { BgColor = new Color(0, 0, 0, 0.45f), CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6 };
        var fill = new StyleBoxFlat { BgColor = Orange, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6, ContentMarginTop = 6, ContentMarginBottom = 6 };
        t.SetStylebox("slider", "HSlider", slider);
        t.SetStylebox("grabber_area", "HSlider", fill);
        t.SetStylebox("grabber_area_highlight", "HSlider", fill);
        t.SetIcon("grabber", "HSlider", Dot(26, Colors.White));
        t.SetIcon("grabber_highlight", "HSlider", Dot(30, Yellow));

        t.SetStylebox("panel", "PanelContainer", Box(new Color(Navy, 0.92f), 22, 0f, 0, null, 8));
        t.SetStylebox("normal", "LineEdit", Box(new Color(1, 1, 1, 0.95f), 12, 0f, 0, null, 4));
        t.SetStylebox("focus", "LineEdit", Box(new Color(0, 0, 0, 0), 12, 0f, 3, Yellow, 0));
        t.SetColor("font_color", "LineEdit", Navy);
        t.SetColor("caret_color", "LineEdit", Navy);
        t.SetFontSize("font_size", "LineEdit", 24);

        foreach (var k in new[] { "normal", "hover", "pressed", "focus", "hover_pressed" })
            t.SetStylebox(k, "CheckButton", new StyleBoxEmpty());
        t.SetColor("font_color", "CheckButton", Colors.White);
        t.SetColor("font_hover_color", "CheckButton", Yellow);
        t.SetColor("font_pressed_color", "CheckButton", Colors.White);
        t.SetColor("font_outline_color", "CheckButton", Navy);
        t.SetConstant("outline_size", "CheckButton", 6);

        t.SetStylebox("normal", "OptionButton", Box(NavySoft, 12, -0.1f, 0, null, 4));
        t.SetStylebox("hover", "OptionButton", Box(NavySoft.Lightened(0.15f), 12, -0.1f, 3, Yellow, 4));
        t.SetStylebox("pressed", "OptionButton", Box(NavySoft.Darkened(0.2f), 12, -0.1f, 0, null, 4));
        t.SetStylebox("focus", "OptionButton", Box(new Color(0, 0, 0, 0), 12, -0.1f, 3, Yellow, 0));
        t.SetColor("font_outline_color", "OptionButton", Navy);
        t.SetConstant("outline_size", "OptionButton", 5);

        t.SetStylebox("panel", "PopupMenu", Box(Navy, 12, 0f, 2, Yellow, 2));
        t.SetColor("font_color", "PopupMenu", Colors.White);
        t.SetColor("font_hover_color", "PopupMenu", Yellow);
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

    /// <summary>Gros bouton incliné de menu : grossit au survol (son), s'enfonce au clic (son), couleur au choix.</summary>
    public static Button Button(string text, Color color, Action onPress, int fontSize = 30, Vector2? minSize = null)
    {
        var b = new Button { Text = text, CustomMinimumSize = minSize ?? new Vector2(440, 66), FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", fontSize);
        b.AddThemeStyleboxOverride("normal", Box(color, 14, -0.12f));
        b.AddThemeStyleboxOverride("hover", Box(color.Lightened(0.15f), 14, -0.12f, 3, Yellow));
        b.AddThemeStyleboxOverride("pressed", Box(color.Darkened(0.2f), 14, -0.12f, 0, null, 2));
        b.AddThemeStyleboxOverride("focus", Box(new Color(0, 0, 0, 0), 14, -0.12f, 3, Yellow, 0));
        Juicy(b);
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

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
        var r = new ColorRect { Color = new Color(Navy, alpha), MouseFilter = Control.MouseFilterEnum.Stop, Material = BlurMaterial() };   // pas de cache static : un ShaderMaterial gardé en static fuit à la fermeture
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
