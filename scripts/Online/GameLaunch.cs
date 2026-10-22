using Godot;
using DogeballVR.Net;

namespace DogeballVR.Online;

/// <summary>
/// Configuration de la prochaine partie, posée par le lobby (ou le menu) avant de recharger la scène principale.
/// <see cref="Main"/> la lit au démarrage ; sans elle, ce sont les options de ligne de commande (<see cref="NetConfig.Parse"/>).
/// </summary>
public static class GameLaunch
{
    static NetConfig? pending;

    /// <summary>Vrai si une partie attend d'être lancée : le menu principal ne doit pas s'afficher.</summary>
    public static bool HasPending => pending != null;

    /// <summary>Vrai si la scène doit rouvrir l'écran du lobby (retour de fin de partie, salle déjà ouverte).</summary>
    public static bool ShowLobby { get; set; }

    public static void Set(NetConfig cfg) => pending = cfg;

    /// <summary>Rend la configuration en attente (une seule fois), sinon celle de la ligne de commande.</summary>
    public static NetConfig Take()
    {
        var cfg = pending ?? NetConfig.Parse();
        pending = null;
        if (cfg.Token == "") cfg.Token = Identity.Token;
        if (cfg.PlayerName == "") cfg.PlayerName = Identity.Name;
        return cfg;
    }
}

/// <summary>
/// Identité du joueur : nom affiché et jeton de reconnexion. Avec Steam, le SteamID ; sinon un jeton aléatoire gardé dans <c>user://identity.cfg</c>.
/// </summary>
public static class Identity
{
    const string Path = "user://identity.cfg";
    static string? token, name;

    public static string Token
    {
        get
        {
            if (token != null) return token;
            if (Arg("--token=") is { } forced) return token = forced;   // tests : plusieurs joueurs sur une machine
            string? steam = SteamHooks.LocalId?.Invoke();
            if (!string.IsNullOrEmpty(steam)) return token = "steam:" + steam;
            var cfg = new ConfigFile();
            cfg.Load(Path);
            token = (string)cfg.GetValue("player", "token", "");
            if (token.Length < 8)
            {
                var rng = new RandomNumberGenerator();
                token = $"{rng.Randi():x8}{rng.Randi():x8}";
                cfg.SetValue("player", "token", token);
                cfg.Save(Path);
            }
            return token;
        }
    }

    public static string Name
    {
        get
        {
            if (name != null) return name;
            if (Arg("--name=") is { } forced) return name = Clean(forced);
            string? steam = SteamHooks.PersonaName?.Invoke();
            if (!string.IsNullOrEmpty(steam)) return name = Clean(steam);
            var cfg = new ConfigFile();
            cfg.Load(Path);
            name = (string)cfg.GetValue("player", "name", "");
            if (name == "")
            {
                name = OS.GetEnvironment("USERNAME");
                if (name == "") name = OS.GetEnvironment("USER");
                if (name == "" || name == "root") name = $"Joueur {Token[^4..].ToUpperInvariant()}";
            }
            return name = Clean(name);
        }
        set
        {
            name = Clean(value);
            var cfg = new ConfigFile();
            cfg.Load(Path);
            cfg.SetValue("player", "name", name);
            cfg.Save(Path);
        }
    }

    static string? Arg(string prefix)
    {
        foreach (var a in OS.GetCmdlineUserArgs()) if (a.StartsWith(prefix) && a.Length > prefix.Length) return a[prefix.Length..];
        return null;
    }

    /// <summary>Nom affichable : 20 caractères au plus, sans sauts de ligne.</summary>
    public static string Clean(string s)
    {
        s = (s ?? "").Replace("\n", " ").Replace("\r", " ").Trim();
        if (s.Length > 20) s = s[..20];
        return s == "" ? "Joueur" : s;
    }
}

/// <summary>
/// Points d'accroche vers la couche Steam (renseignés par elle à l'initialisation) : le lobby et l'identité
/// fonctionnent sans Steam (LAN, adresse IP) et utilisent Steam dès qu'il est disponible.
/// </summary>
public static class SteamHooks
{
    public static System.Func<string?>? LocalId { get; set; }
    public static System.Func<string?>? PersonaName { get; set; }
}

/// <summary>
/// Secret de reconnexion remis par l'hôte à l'entrée en partie. Gardé en mémoire et dans <c>user://identity.cfg</c>
/// (un crash puis un relancement permettent de revenir), effacé quand on quitte la salle. Valable 30 minutes.
/// </summary>
public static class ReconnectSecret
{
    const string Path = "user://identity.cfg";
    const double MaxAgeSeconds = 1800;
    static string? secret;

    // Tests : plusieurs joueurs sur une machine, chacun sa clé (`--token=`).
    static string Key
    {
        get
        {
            return Tools.DevFlags.Value("--token=") is { Length: > 0 } token ? "secret_" + token : "secret";
        }
    }

    public static string Get()
    {
        if (secret != null) return secret;
        var cfg = new ConfigFile();
        cfg.Load(Path);
        var saved = (string)cfg.GetValue("reconnect", Key, "");
        double at = cfg.GetValue("reconnect", Key + "_at", 0).AsDouble();
        secret = saved.Length == 32 && Time.GetUnixTimeFromSystem() - at < MaxAgeSeconds ? saved : "";
        return secret;
    }

    public static void Set(string value)
    {
        if (value == secret) return;
        secret = value;
        var cfg = new ConfigFile();
        cfg.Load(Path);
        cfg.SetValue("reconnect", Key, value);
        // En secondes entières : un double s'écrit « 1.79263e+09 » dans le fichier (jusqu'à 5000 s perdues, secret jugé périmé).
        cfg.SetValue("reconnect", Key + "_at", (long)Time.GetUnixTimeFromSystem());
        cfg.Save(Path);
    }

    public static void Clear() => Set("");
}
