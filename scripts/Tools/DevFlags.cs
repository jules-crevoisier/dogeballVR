using System;
using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Options de test de la ligne de commande (CI, captures, tests réseau) : <c>--pc-autoplay</c>, <c>--net-cut=</c>,
/// <c>--fake-version</c>, <c>--quit-file=</c>, <c>--transport=udp-p2p</c>.
/// Elles ne sont lues que dans l'éditeur (la CI et <c>tools/steam/shots.sh</c> lancent le binaire de l'éditeur) ou dans un
/// export de debug. Un export de sortie les ignore : le joueur automatique viserait et lancerait à la place d'un joueur en ligne.
/// </summary>
public static class DevFlags
{
    /// <summary>Vrai dans l'éditeur ou un export de debug ; faux dans l'export de sortie livré aux joueurs.</summary>
    public static bool Allowed => OS.HasFeature("editor") || OS.IsDebugBuild();

    /// <summary>Vrai si l'option <paramref name="flag"/> est sur la ligne de commande et que les options de test sont permises.</summary>
    public static bool Has(string flag) =>
        Allowed && (Array.IndexOf(OS.GetCmdlineUserArgs(), flag) >= 0 || Array.IndexOf(OS.GetCmdlineArgs(), flag) >= 0);

    /// <summary>Valeur de l'option <c>prefix…</c> (par exemple <c>--net-cut=</c>), ou null si absente ou non permise.</summary>
    public static string? Value(string prefix)
    {
        if (!Allowed) return null;
        return Array.Find(OS.GetCmdlineUserArgs(), a => a.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
    }
}
