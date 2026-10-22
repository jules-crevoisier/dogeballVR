using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Réglages de la salle EVA, choisis par l'opérateur et pas par le porteur du casque (pas de menu dans le casque).
/// Lus au lancement en casque, dans cet ordre (le suivant l'emporte) :
/// 1. `user://eva.cfg`, section [eva] (sur Quest : Android/data/&lt;paquet&gt;/files/eva.cfg, poussé par l'opérateur avec adb) ;
/// 2. options de lancement : --eva-haptics=0..100, --eva-volume=0..100, --eva-reduce-flashes, --eva-end-hold=SECONDES.
/// Ce sont des réglages de salle, valables aussi en version de sortie (pas des options de test).
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

    public static void Load()
    {
        var cfg = new ConfigFile();
        if (cfg.Load(FilePath) == Error.Ok)
        {
            Haptics = Percent(cfg.GetValue("eva", "haptics", Haptics * 100f).AsSingle());
            Volume = Percent(cfg.GetValue("eva", "volume", Volume * 100f).AsSingle());
            ReduceFlashes = cfg.GetValue("eva", "reduce_flashes", ReduceFlashes).AsBool();
            EndHoldSeconds = Hold(cfg.GetValue("eva", "end_hold", EndHoldSeconds).AsSingle());
        }
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--eva-haptics=") && float.TryParse(a["--eva-haptics=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float h)) Haptics = Percent(h);
            else if (a.StartsWith("--eva-volume=") && float.TryParse(a["--eva-volume=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float v)) Volume = Percent(v);
            else if (a == "--eva-reduce-flashes") ReduceFlashes = true;
            else if (a.StartsWith("--eva-end-hold=") && float.TryParse(a["--eva-end-hold=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float s)) EndHoldSeconds = Hold(s);
        }
        GD.Print($"[EVA] Réglages de salle : vibrations {Haptics * 100:0} %, volume {Volume * 100:0} %, flashs {(ReduceFlashes ? "réduits" : "normaux")}, fin de partie {EndHoldSeconds:0} s");
    }

    /// <summary>Applique les réglages de salle aux réglages du jeu (volume, flashs) ; les vibrations passent par <see cref="Haptics"/>.</summary>
    public static void Apply()
    {
        GameSettings.MasterVolume = Volume;
        GameSettings.ReduceFlashes = ReduceFlashes;
        GameSettings.Apply(null);
    }

    static float Percent(float v) => Mathf.Clamp(v, 0f, 100f) / 100f;
    static float Hold(float s) => Mathf.Clamp(s, 5f, 120f);
}
