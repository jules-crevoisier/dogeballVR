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
        AddChild(BuildStage());
        // voile sombre à gauche pour la lisibilité des boutons, la scène 3D reste à droite
        var fade = new Gradient();
        fade.SetColor(0, new Color(0.06f, 0.09f, 0.19f, 0.75f));
        fade.SetColor(1, new Color(0.06f, 0.09f, 0.19f, 0f));
        var shade = new TextureRect { Texture = new GradientTexture2D { Gradient = fade, Width = 256, Height = 4 }, StretchMode = TextureRect.StretchModeEnum.Scale,
            AnchorRight = 0.55f, AnchorBottom = 1f, MouseFilter = Control.MouseFilterEnum.Ignore };
        AddChild(shade);

        var center = new CenterContainer { AnchorRight = 0.5f, AnchorBottom = 1f };
        AddChild(center);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        center.AddChild(col);

        var title = new Label { Text = "DOGEBALL", HorizontalAlignment = HorizontalAlignment.Center };
        title.AddThemeFontSizeOverride("font_size", 130);
        title.PivotOffset = new Vector2(240, 70);
        title.Rotation = Mathf.DegToRad(-4);
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
        play.CustomMinimumSize = new Vector2(480, 88);
        play.AddThemeFontSizeOverride("font_size", 36);
        col.AddChild(play);
        col.AddChild(MakeButton("HÉBERGER UNE PARTIE", new Color("3A7BD5"), () => Chosen?.Invoke(NetMode.Host, "")));
        col.AddChild(address);
        col.AddChild(MakeButton("REJOINDRE", new Color("3A7BD5"), () => Chosen?.Invoke(NetMode.Client, address.Text.Trim())));
        col.AddChild(MakeButton("QUITTER", new Color("55607F"), () => GetTree().Quit()));

        var help = new Label { Text = "ZQSD bouger · Souris viser · Clic gauche lancer (maintenu = plus fort) · Clic droit attraper · E esquive · F1 aide", HorizontalAlignment = HorizontalAlignment.Center,
            AutowrapMode = TextServer.AutowrapMode.WordSmart, CustomMinimumSize = new Vector2(420, 0) };
        help.AddThemeFontSizeOverride("font_size", 18);
        help.AddThemeColorOverride("font_color", Colors.White);
        col.AddChild(help);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        play.GrabFocus();
    }

    /// <summary>Scène de fond : le toit de la map, Riko qui célèbre et Nova au repos, cadrés sur la droite de l'écran.</summary>
    static Node3D BuildStage()
    {
        var stage = new Node3D { Name = "MenuStage" };
        Maps.ArenaBuilder.AddKcEnvironment(stage, pcProfile: true);
        foreach (var name in new[] { "toit", "toit_ville" })
            if (ModelLibrary.Instance(name) is Node3D decor) stage.AddChild(decor);
        void Hero(string model, Vector3 pos, float yaw, string anim)
        {
            if (ModelLibrary.Instance(model) is not Node3D hero) return;
            hero.Position = pos;
            hero.RotationDegrees = new Vector3(0, yaw, 0);
            stage.AddChild(hero);
            if (hero.FindChild("AnimationPlayer", true, false) is AnimationPlayer ap && ap.HasAnimation(anim))
            {
                ap.GetAnimation(anim).LoopMode = Animation.LoopModeEnum.Linear;
                ap.Play(anim);
            }
        }
        // les modèles regardent vers -Z : demi-tour pour faire face à la caméra
        Hero("avatar_bleu_rig", new Vector3(-0.4f, 0, 3.0f), 180 + 20, "Repos");
        Hero("avatar_orange_rig", new Vector3(0.7f, 0, 3.3f), 180 - 25, "Celebration");
        // contre-jours aux couleurs d'équipe derrière chaque perso
        stage.AddChild(new OmniLight3D { Position = new Vector3(1.6f, 2.4f, 1.6f), LightColor = new Color("FFB27A"), LightEnergy = 0.35f, OmniRange = 3f });
        stage.AddChild(new OmniLight3D { Position = new Vector3(-1.2f, 2.4f, 1.6f), LightColor = new Color("6FD8FF"), LightEnergy = 0.4f, OmniRange = 3f });
        var cam = new Camera3D { Fov = 30, Current = true };
        stage.AddChild(cam);
        cam.Position = new Vector3(-1.0f, 0.95f, 7.6f);
        cam.LookAtFromPosition(cam.Position, new Vector3(-0.95f, 1.15f, 3.2f));   // légère contre-plongée, persos dans la moitié droite
        return stage;
    }
}
