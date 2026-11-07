using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Langue de l'interface. Les textes sont écrits en français dans le code et servent de clés : `assets/i18n/textes.csv` donne
/// leur version anglaise (le français n'a pas de colonne, la clé suffit). Les Label et Button traduisent seuls un texte fixe
/// (traduction automatique de Godot) ; un texte composé passe par <see cref="F"/> avec un gabarit « …{0}… » qui est lui-même une clé.
/// Relever les textes : `python3 tools/i18n/extract.py`.
/// </summary>
public static class L
{
    /// <summary>Langue en cours : "fr" ou "en".</summary>
    public static string Locale => TranslationServer.GetLocale().StartsWith("fr") ? "fr" : "en";

    /// <summary>Interface en anglais ?</summary>
    public static bool English => Locale == "en";

    /// <summary>Texte traduit (la clé est le texte français).</summary>
    public static string T(string fr) => TranslationServer.Translate(fr);

    /// <summary>Gabarit traduit puis rempli : <c>L.F("NIVEAU {0} !", n)</c>.</summary>
    public static string F(string fr, params object[] args) => string.Format(System.Globalization.CultureInfo.InvariantCulture, T(fr), args);

    /// <summary>Accord : <paramref name="one"/> pour 0 ou 1 en français, pour 1 seulement en anglais (« 0 hits »), sinon <paramref name="many"/> (gabarits avec {0}).</summary>
    public static string Plural(int n, string one, string many) => F(IsOne(n) ? one : many, n);

    /// <summary>Le nombre prend-il le singulier ? Français : 0 et 1 ; anglais : 1.</summary>
    public static bool IsOne(int n) => English ? n == 1 : n <= 1;

    /// <summary>
    /// Applique le réglage : "fr", "en", ou "auto" (langue du système si c'est le français, l'anglais sinon). Sans effet si la langue
    /// est déjà la bonne (les textes se retraduisent seuls à chaque changement). Casque Quest (Android) : "auto" reste en français,
    /// les salles EVA sont françaises et un casque de salle est souvent réglé en anglais.
    /// </summary>
    public static void Apply(string? setting)
    {
        string want = setting is "fr" or "en" ? setting : OS.GetLocaleLanguage() == "fr" || OS.HasFeature("android") ? "fr" : "en";
        if (Tools.DevFlags.Value("--lang=") is { } forced && forced is "fr" or "en") want = forced;   // captures et tests
        if (TranslationServer.GetLocale() != want) TranslationServer.SetLocale(want);
    }
}
