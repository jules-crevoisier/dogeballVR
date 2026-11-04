using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Réglages de la salle EVA, choisis par l'opérateur et pas par le porteur du casque (pas de menu dans le casque).
/// Lus au lancement en casque, dans cet ordre (le suivant l'emporte) :
/// 1. `user://eva.cfg`, section [eva] (sur Quest : Android/data/&lt;paquet&gt;/files/eva.cfg, poussé par l'opérateur avec adb) ;
/// 2. options de lancement : --eva-haptics=0..100, --eva-volume=0..100, --eva-reduce-flashes, --eva-end-hold=SECONDES.
/// Pourcentages : une valeur jusqu'à 1 (0.8, 1) est lue comme une fraction (80 %, 100 %), l'erreur la plus probable d'un opérateur.
/// Seuls les réglages présents remplacent ceux du joueur (settings.cfg) ; l'alerte de bord de zone garde
/// <see cref="SafetyFloor"/> quoi qu'il arrive. Ce sont des réglages de salle, valables aussi en version de sortie.
/// </summary>
public static class EvaSettings
{
    /// <summary>Force des vibrations des manettes, de 0 (aucune) à 1.</summary>
    public static float Haptics = 1f;
    /// <summary>Volume général, de 0 à 1.</summary>
    public static float Volume = 0.85f;
    public static bool ReduceFlashes;
    /// <summary>Durée de l'écran de fin dans le casque avant la partie suivante, en secondes (5 à 120).</summary>
    public static float EndHoldSeconds = 20f;

    public const string FilePath = "user://eva.cfg";
    /// <summary>Plancher de l'alerte de bord de zone (vibration et clic), même vibrations coupées ou volume bas.</summary>
    public const float SafetyFloor = 0.6f;

    static bool volumeSet, flashesSet;

    public static void Load()
    {
        volumeSet = flashesSet = false;
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) == Error.Ok)
        {
            Haptics = Percent(cfg.GetValue("eva", "haptics", Haptics * 100f).AsSingle());
            if (cfg.HasSectionKey("eva", "volume")) { Volume = Percent(cfg.GetValue("eva", "volume").AsSingle()); volumeSet = true; }
            if (cfg.HasSectionKey("eva", "reduce_flashes")) { ReduceFlashes = cfg.GetValue("eva", "reduce_flashes").AsBool(); flashesSet = true; }
            EndHoldSeconds = Hold(cfg.GetValue("eva", "end_hold", EndHoldSeconds).AsSingle());
        }
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--eva-haptics=") && float.TryParse(a["--eva-haptics=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h)) Haptics = Percent(h);
            else if (a.StartsWith("--eva-volume=") && float.TryParse(a["--eva-volume=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) { Volume = Percent(v); volumeSet = true; }
            else if (a == "--eva-reduce-flashes") { ReduceFlashes = true; flashesSet = true; }
            else if (a.StartsWith("--eva-end-hold=") && float.TryParse(a["--eva-end-hold=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) EndHoldSeconds = Hold(s);
        }
        GD.Print($"[EVA] Réglages de salle : vibrations {Haptics * 100:0} %, volume {(volumeSet ? $"{Volume * 100:0} %" : "du joueur")}, "
            + $"flashs {(!flashesSet ? "du joueur" : ReduceFlashes ? "réduits" : "normaux")}, fin de partie {EndHoldSeconds:0} s");
    }

    /// <summary>Applique les réglages de salle présents (volume, flashs) aux réglages du jeu ; les vibrations passent par <see cref="Haptics"/>.</summary>
    public static void Apply()
    {
        if (volumeSet) GameSettings.MasterVolume = Volume;
        if (flashesSet) GameSettings.ReduceFlashes = ReduceFlashes;
        if (volumeSet || flashesSet) GameSettings.Apply(null);
    }

    static float Percent(float v) => v > 0f && v <= 1f ? v : Mathf.Clamp(v, 0f, 100f) / 100f;   // 1 = 100 % (fraction), pas 1 %
    static float Hold(float s) => Mathf.Clamp(s, 5f, 120f);
}
