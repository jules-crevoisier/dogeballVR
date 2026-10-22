using System;
using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Réglages du joueur PC, enregistrés dans user://settings.cfg (sections [pc], [controls], [audio], [video]).
/// Le fichier est relu puis réécrit à chaque sauvegarde : les sections des autres modules (réseau, Steam) sont conservées.
/// <see cref="Apply"/> pousse les valeurs vers le moteur (volumes des bus, fenêtre, ombres…) ; le joueur relit les siennes à chaque image.
/// </summary>
public static class GameSettings
{
    const string Path = "user://settings.cfg";
    public const float DefaultSensitivity = 0.0022f;
    public const float DefaultFov = 90f;   // 100° étirait trop les bords (balle en ovale géant près de l'objectif) ; le curseur va toujours de 80 à 120°

    // Contrôles
    public static float Sensitivity = DefaultSensitivity;   // radians par pixel de souris
    public static bool InvertY;
    public static float Fov = DefaultFov;                    // champ horizontal (degrés)
    public static float AimAssist = 1f;                      // 0 = aucune aide, 1 = aide normale
    public static bool ToggleCrouch;
    // Confort
    public static float ScreenShake = 1f;                    // 0 à 1
    public static float Haptics = 1f;                        // vibrations des manettes VR, 0 à 1
    public static bool HitStop = true;
    public static bool HitMarkers = true;
    public static bool ShowFps;
    // Audio (0 à 1)
    public static float MasterVolume = 0.85f, MusicVolume = 0.6f, SfxVolume = 0.9f, UiVolume = 0.8f;
    // Vidéo
    public static bool Fullscreen = true;
    public static bool VSync = true;
    public static int MaxFps = 0;                            // 0 = illimité
    public static int Quality = 2;                           // 0 bas, 1 moyen, 2 haut
    public static float RenderScale = 1f;
    // Première partie : on propose le tutoriel
    public static bool TutorialDone;

    public static event Action? Changed;
    static bool loaded;

    public static void Load()
    {
        if (loaded) return;
        loaded = true;
        var cfg = new ConfigFile();
        if (cfg.Load(Path) != Error.Ok) return;
        Sensitivity = Mathf.Clamp(F(cfg, "pc", "sensitivity", Sensitivity), 0.0005f, 0.01f);
        InvertY = B(cfg, "controls", "invert_y", InvertY);
        Fov = Mathf.Clamp(F(cfg, "controls", "fov", Fov), 80f, 120f);
        AimAssist = Mathf.Clamp(F(cfg, "controls", "aim_assist", AimAssist), 0f, 1f);
        ToggleCrouch = B(cfg, "controls", "toggle_crouch", ToggleCrouch);
        ScreenShake = Mathf.Clamp(F(cfg, "controls", "screen_shake", ScreenShake), 0f, 1f);
        Haptics = Mathf.Clamp(F(cfg, "controls", "haptics", Haptics), 0f, 1f);
        HitStop = B(cfg, "controls", "hit_stop", HitStop);
        HitMarkers = B(cfg, "controls", "hit_markers", HitMarkers);
        ShowFps = B(cfg, "video", "show_fps", ShowFps);
        MasterVolume = Mathf.Clamp(F(cfg, "audio", "master", MasterVolume), 0f, 1f);
        MusicVolume = Mathf.Clamp(F(cfg, "audio", "music", MusicVolume), 0f, 1f);
        SfxVolume = Mathf.Clamp(F(cfg, "audio", "sfx", SfxVolume), 0f, 1f);
        UiVolume = Mathf.Clamp(F(cfg, "audio", "ui", UiVolume), 0f, 1f);
        Fullscreen = B(cfg, "video", "fullscreen", Fullscreen);
        VSync = B(cfg, "video", "vsync", VSync);
        MaxFps = (int)F(cfg, "video", "max_fps", MaxFps);
        Quality = Mathf.Clamp((int)F(cfg, "video", "quality", Quality), 0, 2);
        RenderScale = Mathf.Clamp(F(cfg, "video", "render_scale", RenderScale), 0.5f, 1f);
        TutorialDone = B(cfg, "pc", "tutorial_done", TutorialDone);
        InputSetup.LoadBindings(cfg);
    }

    public static void Save()
    {
        var cfg = new ConfigFile();
        cfg.Load(Path);
        cfg.SetValue("pc", "sensitivity", Sensitivity);
        cfg.SetValue("pc", "tutorial_done", TutorialDone);
        cfg.SetValue("controls", "invert_y", InvertY);
        cfg.SetValue("controls", "fov", Fov);
        cfg.SetValue("controls", "aim_assist", AimAssist);
        cfg.SetValue("controls", "toggle_crouch", ToggleCrouch);
        cfg.SetValue("controls", "screen_shake", ScreenShake);
        cfg.SetValue("controls", "haptics", Haptics);
        cfg.SetValue("controls", "hit_stop", HitStop);
        cfg.SetValue("controls", "hit_markers", HitMarkers);
        cfg.SetValue("audio", "master", MasterVolume);
        cfg.SetValue("audio", "music", MusicVolume);
        cfg.SetValue("audio", "sfx", SfxVolume);
        cfg.SetValue("audio", "ui", UiVolume);
        cfg.SetValue("video", "fullscreen", Fullscreen);
        cfg.SetValue("video", "vsync", VSync);
        cfg.SetValue("video", "max_fps", MaxFps);
        cfg.SetValue("video", "quality", Quality);
        cfg.SetValue("video", "render_scale", RenderScale);
        cfg.SetValue("video", "show_fps", ShowFps);
        InputSetup.SaveBindings(cfg);
        cfg.Save(Path);
    }

    /// <summary>Applique au moteur : bus audio, fenêtre, synchronisation verticale, qualité, échelle de rendu.</summary>
    public static void Apply(Viewport? viewport = null)
    {
        AudioBuses.Ensure();
        AudioBuses.SetVolume("Master", MasterVolume);
        AudioBuses.SetVolume("Music", MusicVolume);
        AudioBuses.SetVolume("SFX", SfxVolume);
        AudioBuses.SetVolume("UI", UiVolume);
        if (DisplayServer.GetName() != "headless" && !LaunchedFromCommandLine)
        {
            var mode = DisplayServer.WindowGetMode();
            bool isFull = mode is DisplayServer.WindowMode.Fullscreen or DisplayServer.WindowMode.ExclusiveFullscreen;
            if (Fullscreen && !isFull) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Fullscreen);
            else if (!Fullscreen && isFull) DisplayServer.WindowSetMode(DisplayServer.WindowMode.Windowed);
            DisplayServer.WindowSetVsyncMode(VSync ? DisplayServer.VSyncMode.Enabled : DisplayServer.VSyncMode.Disabled);
        }
        if (!LaunchedFromCommandLine) Engine.MaxFps = MaxFps;   // ne pas écraser --max-fps des tests et de la CI
        if (viewport != null)
        {
            viewport.Scaling3DScale = RenderScale;
            viewport.Msaa3D = Quality >= 2 ? Viewport.Msaa.Msaa4X : Quality == 1 ? Viewport.Msaa.Msaa2X : Viewport.Msaa.Disabled;
            viewport.PositionalShadowAtlasSize = Quality >= 1 ? 4096 : 1024;
        }
        RenderingServer.DirectionalShadowAtlasSetSize(Quality >= 2 ? 4096 : Quality == 1 ? 2048 : 1024, true);
        Changed?.Invoke();
    }

    /// <summary>Notifie les écouteurs sans écrire (curseurs en cours de glissement).</summary>
    public static void Notify() => Changed?.Invoke();

    /// <summary>Lancement en ligne de commande (CI, tests, captures) : on ne touche pas à la fenêtre.</summary>
    public static bool LaunchedFromCommandLine => OS.GetCmdlineUserArgs().Length > 0 || OS.GetCmdlineArgs().Length > 1;

    static float F(ConfigFile c, string s, string k, float d) => c.HasSectionKey(s, k) ? (float)c.GetValue(s, k).AsDouble() : d;
    static bool B(ConfigFile c, string s, string k, bool d) => c.HasSectionKey(s, k) ? c.GetValue(s, k).AsBool() : d;
}

/// <summary>Bus audio Master / Music / SFX / UI, créés au démarrage si le projet n'a pas de disposition de bus.</summary>
public static class AudioBuses
{
    public static void Ensure()
    {
        foreach (var name in new[] { "Music", "SFX", "UI" })
        {
            if (AudioServer.GetBusIndex(name) >= 0) continue;
            int i = AudioServer.BusCount;
            AudioServer.AddBus(i);
            AudioServer.SetBusName(i, name);
            AudioServer.SetBusSend(i, "Master");
        }
    }

    public static void SetVolume(string bus, float linear)
    {
        int i = AudioServer.GetBusIndex(bus);
        if (i < 0) return;
        AudioServer.SetBusVolumeDb(i, linear <= 0.001f ? -80f : Mathf.LinearToDb(linear));
        AudioServer.SetBusMute(i, linear <= 0.001f);
    }
}
