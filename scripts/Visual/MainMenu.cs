using System;
using Godot;
using DogeballVR.Net;

namespace DogeballVR.Visual;

/// <summary>Menu principal du mode PC : jouer contre les bots, héberger, rejoindre, quitter. Ciel de jour et gros boutons arrondis, comme le reste de la DA.</summary>
public partial class MainMenu : CanvasLayer
{
    public event Action<NetMode, string>? Chosen;

    static StyleBoxFlat Box(Color c, int radius = 18) => new()
    {
        BgColor = c, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 28, ContentMarginRight = 28, ContentMarginTop = 14, ContentMarginBottom = 14,
        BorderWidthBottom = 6, BorderColor = new Color(0.06f, 0.09f, 0.19f),
    };

    Button MakeButton(string text, Color color, Action onPress)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(420, 64), FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", 28);
        b.AddThemeColorOverride("font_color", Colors.White);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeStyleboxOverride("normal", Box(color));
        b.AddThemeStyleboxOverride("hover", Box(color.Lightened(0.18f)));
        b.AddThemeStyleboxOverride("pressed", Box(color.Darkened(0.2f)));
        b.AddThemeStyleboxOverride("focus", Box(color.Lightened(0.1f)));
        b.Pressed += onPress;
        return b;
    }

    public override void _Ready()
    {
        var bg = new ColorRect { Color = new Color("4C8DE8") };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);
        var glow = new ColorRect { Color = new Color(1f, 0.87f, 0.73f, 0.55f) };
        glow.SetAnchorsPreset(Control.LayoutPreset.BottomWide);
        glow.OffsetTop = -380;
        AddChild(glow);

        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        center.AddChild(col);

        var title = new Label { Text = "DOGEBALL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 110);
        title.AddThemeColorOverride("font_color", new Color("FF6A1A"));
        title.AddThemeColorOverride("font_outline_color", new Color("101830"));
        title.AddThemeConstantOverride("outline_size", 22);
        col.AddChild(title);
        var sub = new Label { Text = "2 contre 2 · esquive, attrape, renvoie", HorizontalAlignment = HorizontalAlignment.Center };
        sub.AddThemeFontSizeOverride("font_size", 26);
        sub.AddThemeColorOverride("font_color", Colors.White);
        sub.AddThemeColorOverride("font_outline_color", new Color("101830"));
        sub.AddThemeConstantOverride("outline_size", 8);
        col.AddChild(sub);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 24) });

        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Adresse de l'hôte", CustomMinimumSize = new Vector2(420, 52), Alignment = HorizontalAlignment.Center };
        address.AddThemeFontSizeOverride("font_size", 24);

        var play = MakeButton("JOUER CONTRE LES BOTS", new Color("FF6A1A"), () => Chosen?.Invoke(NetMode.Offline, ""));
        col.AddChild(play);
        col.AddChild(MakeButton("HÉBERGER UNE PARTIE", new Color("3A7BD5"), () => Chosen?.Invoke(NetMode.Host, "")));
        col.AddChild(address);
        col.AddChild(MakeButton("REJOINDRE", new Color("3A7BD5"), () => Chosen?.Invoke(NetMode.Client, address.Text.Trim())));
        col.AddChild(MakeButton("QUITTER", new Color("55607F"), () => GetTree().Quit()));

        var help = new Label { Text = "ZQSD bouger · Souris viser · Clic gauche lancer (maintenu = plus fort) · Clic droit attraper · E esquive · F1 aide", HorizontalAlignment = HorizontalAlignment.Center };
        help.AddThemeFontSizeOverride("font_size", 18);
        help.AddThemeColorOverride("font_color", new Color("101830"));
        col.AddChild(help);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        play.GrabFocus();
    }
}
