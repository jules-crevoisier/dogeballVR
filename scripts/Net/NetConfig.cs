using System.Collections.Generic;
using Godot;

namespace DogeballVR.Net;

public enum NetMode { Offline, Host, Client }

/// <summary>
/// Options de lancement : <c>--host</c>, <c>--join=ADRESSE</c>, <c>--zone=N</c> (client, 2 par défaut : équipe adverse de l'hôte),
/// <c>--port=N</c>, <c>--map=NOM</c> (neon_court par défaut), <c>--net-lag=MS</c> (latence simulée côté client), <c>--net-selftest</c> (le client joue seul, pour les tests automatiques).
/// Avec l'éditeur ou la ligne de commande : <c>godot --path . -- --host</c>.
/// </summary>
public class NetConfig
{
    public NetMode Mode { get; private set; } = NetMode.Offline;
    public string Address { get; private set; } = "127.0.0.1";
    public int Port { get; private set; } = NetSession.DefaultPort;
    public int Zone { get; private set; } = 2;
    public bool SelfTest { get; private set; }
    /// <summary>Latence simulée côté client, en ms par sens (test sans outil réseau) : `--net-lag=50`.</summary>
    public int LagMs { get; private set; }
    /// <summary>Nom de la map (dossier maps/). L'hôte et les clients doivent utiliser la même.</summary>
    public string Map { get; private set; } = "neon_court";

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
            else if (a == "--net-selftest") cfg.SelfTest = true;
        }
        return cfg;
    }
}
