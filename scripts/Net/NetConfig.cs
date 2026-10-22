using System.Collections.Generic;
using Godot;

namespace DogeballVR.Net;

public enum NetMode { Offline, Host, Client }

/// <summary>
/// Options de lancement : <c>--host</c>, <c>--join=ADRESSE</c>, <c>--zone=N</c> (client, 2 par défaut : équipe adverse de l'hôte),
/// <c>--port=N</c>, <c>--map=NOM</c> (neon_court par défaut), <c>--net-lag=MS</c> (latence simulée côté client), <c>--net-selftest</c> (le client joue seul, pour les tests automatiques),
/// <c>--net-loss=0.05</c> (perte simulée), <c>--token=ID</c> (identité de reconnexion), <c>--name=NOM</c>.
/// Le lobby (<see cref="DogeballVR.Online.OnlineLobby"/>) remplit la même structure via <see cref="DogeballVR.Online.GameLaunch"/>.
/// Avec l'éditeur ou la ligne de commande : <c>godot --path . -- --host</c>.
/// </summary>
public class NetConfig
{
    public NetMode Mode { get; set; } = NetMode.Offline;
    public string Address { get; set; } = "127.0.0.1";
    public int Port { get; set; } = NetSession.DefaultPort;
    public int Zone { get; set; } = 2;
    public bool SelfTest { get; set; }
    /// <summary>Latence simulée côté client, en ms par sens (test sans outil réseau) : `--net-lag=50`.</summary>
    public int LagMs { get; set; }
    /// <summary>Nom de la map (dossier maps/). L'hôte et les clients doivent utiliser la même.</summary>
    public string Map { get; set; } = "neon_court";
    /// <summary>Perte de paquets simulée (0 à 1) côté client, sur les envois non fiables et les snapshots : `--net-loss=0.05`.</summary>
    public float LossRate { get; set; }
    /// <summary>Zone de l'hôte (0 par défaut ; le lobby la choisit selon l'équipe).</summary>
    public int HostZone { get; set; }
    /// <summary>Zone du joueur local, quel que soit le mode.</summary>
    public int LocalZone => Mode == NetMode.Client ? Zone : HostZone;
    /// <summary>Le lobby a déjà ouvert la connexion (ENet ou Steam) : la session réseau la reprend au lieu d'en créer une.</summary>
    public bool Preconnected { get; set; }
    /// <summary>Identité du joueur pour la reconnexion (jeton aléatoire gardé dans user://, ou SteamID).</summary>
    public string Token { get; set; } = "";
    public string PlayerName { get; set; } = "";
    /// <summary>Format de partie PC en ligne (façon Knockout City, <see cref="Rules.MatchRules.UseOnlineFormat"/>) au lieu des 5 manches de la VR.</summary>
    public bool OnlineFormat { get; set; }

    /// <summary>Choix fait depuis le menu principal.</summary>
    public void Choose(NetMode mode, string address = "127.0.0.1") { Mode = mode; Address = address; Map = "croix_neon"; }

    public static NetConfig Parse()
    {
        var cfg = new NetConfig();
        var args = new List<string>(OS.GetCmdlineArgs());
        args.AddRange(OS.GetCmdlineUserArgs());
        foreach (var a in args)
        {
            if (a == "--host") cfg.Mode = NetMode.Host;
            else if (a.StartsWith("--join=")) { cfg.Mode = NetMode.Client; cfg.Address = a["--join=".Length..]; }
            else if (a.StartsWith("--zone=") && int.TryParse(a["--zone=".Length..], out int z)) cfg.Zone = z;
            else if (a.StartsWith("--port=") && int.TryParse(a["--port=".Length..], out int p)) cfg.Port = p;
            else if (a.StartsWith("--map=")) cfg.Map = a["--map=".Length..];
            else if (a.StartsWith("--net-lag=") && int.TryParse(a["--net-lag=".Length..], out int lag)) cfg.LagMs = lag;
            else if (a.StartsWith("--net-loss=") && float.TryParse(a["--net-loss=".Length..], System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out float loss)) cfg.LossRate = Mathf.Clamp(loss, 0f, 0.9f);
            else if (a.StartsWith("--token=")) cfg.Token = a["--token=".Length..];
            else if (a.StartsWith("--name=")) cfg.PlayerName = a["--name=".Length..];
            else if (a == "--net-selftest") cfg.SelfTest = Tools.DevFlags.Allowed;   // le client joue seul : jamais dans l'export de sortie
            else if (a == "--format=online") cfg.OnlineFormat = true;
        }
        // L'entraînement se joue sur la vraie carte du jeu, pas sur l'arène de test.
        if (args.Contains("--tutorial") && !args.Exists(a => a.StartsWith("--map="))) cfg.Map = "croix_neon";
        return cfg;
    }
}
