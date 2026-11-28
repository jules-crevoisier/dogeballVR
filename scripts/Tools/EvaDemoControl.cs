using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Mode opérateur pour la présentation en salle EVA (<c>--eva-demo</c>).
/// Permet d'enchaîner les démos sans repasser par le menu ni toucher aux fichiers :
/// <list type="bullet">
///   <item><c>F6</c> : relancer immédiatement la partie sur la même arène.</item>
///   <item><c>F7</c> : basculer entre les deux arènes vitrines (<c>croix_neon</c> ↔ <c>fosse_croisee</c>).</item>
///   <item><c>F8</c> : basculer entre la caméra joueur et la caméra spectateur orbitale (écran géant de la salle).</item>
///   <item><c>F10</c> : afficher ou masquer le bandeau discret des raccourcis opérateur.</item>
/// </list>
/// </summary>
public partial class EvaDemoControl : Node
{
    /// <summary>Arènes vitrines du pack démo EVA, parcourues avec F7.</summary>
    public static readonly string[] ShowcaseMaps = { "solaris_dome", "helios_concourse" };

    /// <summary>True si le jeu a été lancé avec <c>--eva-demo</c>.</summary>
    public static bool Active => DevFlags.Has("--eva-demo");

    /// <summary>Carte forcée lors d'un basculement F7 (lue au rechargement de la scène par Main).</summary>
    public static string? MapOverride { get; set; }

    string currentMap = "croix_neon";
    Camera3D? spectatorCam;
    Camera3D? previousCam;
    bool spectatorActive;
    double orbitTime;

    CanvasLayer? hudLayer;
    PanelContainer? banner;
    Label? toastLabel;
    double toastTimer;
    bool bannerVisible = true;

    public static void AttachIfAsked(Node parent, string mapName)
    {
        if (!Active || DisplayServer.GetName() == "headless") return;
        parent.AddChild(new EvaDemoControl { Name = "EvaDemoControl", currentMap = mapName });
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        BuildOverlay();
        ShowToast($"DÉMO EVA · {MapTitle(currentMap)}");
    }

    public override void _Process(double delta)
    {
        if (spectatorActive && spectatorCam != null && IsInstanceValid(spectatorCam))
        {
            orbitTime += delta;
            float angle = 0.65f + (float)(orbitTime * 0.14);
            spectatorCam.GlobalPosition = new Vector3(Mathf.Cos(angle) * 16.5f, 8.8f, Mathf.Sin(angle) * 12.5f);
            spectatorCam.LookAt(new Vector3(0, 1.0f, 0), Vector3.Up);
            spectatorCam.Current = true;
        }
        if (toastTimer > 0 && toastLabel != null)
        {
            toastTimer -= delta;
            if (toastTimer <= 0) toastLabel.Visible = false;
        }
    }

    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey k || !k.Pressed || k.Echo) return;
        switch (k.Keycode)
        {
            case Key.F6:
                ReloadArena(currentMap);
                GetViewport().SetInputAsHandled();
                break;
            case Key.F7:
                ReloadArena(NextShowcaseMap(currentMap));
                GetViewport().SetInputAsHandled();
                break;
            case Key.F8:
                ToggleSpectatorCamera();
                GetViewport().SetInputAsHandled();
                break;
            case Key.F10:
                bannerVisible = !bannerVisible;
                if (banner != null) banner.Visible = bannerVisible;
                GetViewport().SetInputAsHandled();
                break;
        }
    }

    /// <summary>Renvoie la carte vitrine suivante dans le cycle de démo.</summary>
    public static string NextShowcaseMap(string current)
    {
        for (int i = 0; i < ShowcaseMaps.Length; i++)
            if (ShowcaseMaps[i] == current) return ShowcaseMaps[(i + 1) % ShowcaseMaps.Length];
        return ShowcaseMaps[0];
    }

    void ReloadArena(string mapName)
    {
        MapOverride = mapName;
        Main.ReturnToMenu = false;
        Engine.TimeScale = 1.0;
        GetTree().Paused = false;
        GD.Print($"[DemoEVA] Relance sur {mapName}");
        GetTree().ReloadCurrentScene();
    }

    void ToggleSpectatorCamera()
    {
        spectatorActive = !spectatorActive;
        if (spectatorActive)
        {
            previousCam = GetViewport().GetCamera3D();
            if (spectatorCam == null || !IsInstanceValid(spectatorCam))
            {
                spectatorCam = new Camera3D { Name = "EvaSpectatorCam", Fov = 60f };
                GetParent().AddChild(spectatorCam);
            }
            spectatorCam.Current = true;
            ShowToast("CAMÉRA SPECTATEUR TV (F8)");
        }
        else
        {
            if (previousCam != null && IsInstanceValid(previousCam)) previousCam.Current = true;
            ShowToast("CAMÉRA JOUEUR (F8)");
        }
    }

    void BuildOverlay()
    {
        hudLayer = new CanvasLayer { Name = "EvaDemoHud", Layer = 85 };
        AddChild(hudLayer);

        var style = new StyleBoxFlat
        {
            BgColor = new Color(0.08f, 0.10f, 0.15f, 0.72f),
            CornerRadiusTopLeft = 8, CornerRadiusTopRight = 8,
            CornerRadiusBottomLeft = 8, CornerRadiusBottomRight = 8,
            ContentMarginLeft = 10, ContentMarginRight = 10,
            ContentMarginTop = 5, ContentMarginBottom = 5,
        };

        banner = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        banner.AddThemeStyleboxOverride("panel", style);
        banner.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        banner.Position = new Vector2(14, 14);
        var info = new Label
        {
            Text = "EVA DÉMO  ·  F6 Relancer  ·  F7 Arène  ·  F8 Caméra TV  ·  F10 Masquer",
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        info.AddThemeFontSizeOverride("font_size", 13);
        info.AddThemeColorOverride("font_color", new Color(0.92f, 0.95f, 1f, 0.92f));
        banner.AddChild(info);
        hudLayer.AddChild(banner);

        toastLabel = new Label
        {
            Visible = false,
            HorizontalAlignment = HorizontalAlignment.Center,
            MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        toastLabel.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        toastLabel.Position = new Vector2(-220, 74);
        toastLabel.Size = new Vector2(440, 32);
        toastLabel.AddThemeFontSizeOverride("font_size", 18);
        toastLabel.AddThemeColorOverride("font_color", new Color("FF7A1A"));
        toastLabel.AddThemeColorOverride("font_outline_color", new Color(0.05f, 0.07f, 0.12f, 0.9f));
        toastLabel.AddThemeConstantOverride("outline_size", 5);
        hudLayer.AddChild(toastLabel);
    }

    void ShowToast(string text)
    {
        if (toastLabel == null) return;
        toastLabel.Text = text;
        toastLabel.Visible = true;
        toastTimer = 3.0;
    }

    static string MapTitle(string map) => map switch
    {
        "croix_neon" => "CROIX NÉON",
        "fosse_croisee" => "FOSSE CROISÉE",
        _ => map.ToUpperInvariant(),
    };
}
