using System;
using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Options de test de la ligne de commande : joueurs automatiques (<c>--pc-autoplay</c>, <c>--net-selftest</c>), tests CI
/// (<c>--assist-test</c>, <c>--special-test</c>, <c>--end-screen-test</c>…), simulation réseau (<c>--net-lag=</c>,
/// <c>--net-cut=</c>, <c>--fake-version</c>…), captures (<c>--shots=</c>, <c>--menu-page=</c>, <c>--look=</c>…).
/// Elles ne sont lues que dans l'éditeur (la CI et <c>tools/steam/shots.sh</c> lancent le binaire de l'éditeur) ou dans un
/// export de debug. Un export de sortie les ignore : le joueur automatique viserait et lancerait à la place d'un joueur en ligne.
/// Seuls les modes de lancement (<c>--host</c>, <c>--join=</c>, <c>--lobby-*</c>, <c>--map=</c>, <c>--menu</c>, <c>--tutorial</c>…)
/// lisent la ligne de commande sans passer par ici ; l'étape « Options de test » de la CI le vérifie.
/// </summary>
public static class DevFlags
{
    /// <summary>Vrai dans l'éditeur ou un export de debug ; faux dans l'export de sortie livré aux joueurs.</summary>
    public static bool Allowed => OS.HasFeature("editor") || OS.IsDebugBuild();

    /// <summary>Arguments utilisateur (après <c>--</c>) si les options de test sont permises, sinon aucun.</summary>
    public static string[] Args => Allowed ? OS.GetCmdlineUserArgs() : Array.Empty<string>();

    /// <summary>Vrai si l'option <paramref name="flag"/> est sur la ligne de commande et que les options de test sont permises.</summary>
    public static bool Has(string flag) =>
        Allowed && (Array.IndexOf(OS.GetCmdlineUserArgs(), flag) >= 0 || Array.IndexOf(OS.GetCmdlineArgs(), flag) >= 0);

    /// <summary>Valeur de l'option <c>prefix…</c> (par exemple <c>--net-cut=</c>), ou null si absente ou non permise.</summary>
    public static string? Value(string prefix) =>
        Array.Find(Args, a => a.StartsWith(prefix, StringComparison.Ordinal))?[prefix.Length..];
}
