using System.Collections.Generic;
using Godot;
using Steamworks;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Présence Steam (liste d'amis). <c>steam_display</c> pointe vers un jeton du fichier de localisation
/// docs/steam/rich_presence_fr_en.vdf (à téléverser dans Steamworks > Présence enrichie) : <c>#Status_Menu</c>,
/// <c>#Status_Lobby</c>, <c>#Status_Match</c> (variables <c>%map%</c>, <c>%score%</c>).
/// <c>connect</c> = « +connect_lobby &lt;id&gt; » rend le bouton « Rejoindre la partie » des amis actif.
/// </summary>
public static class RichPresence
{
    public const string Menu = "Menu", Lobby = "Lobby", Match = "Match";

    /// <summary>Dernier état demandé (aussi hors Steam : utile pour les journaux et les tests).</summary>
    public static string Status { get; private set; } = "";
    public static IReadOnlyDictionary<string, string> Details => details;
    static readonly Dictionary<string, string> details = new();

    /// <summary>
    /// Change l'état affiché : <paramref name="status"/> ∈ {Menu, Lobby, Match} (jeton #Status_…),
    /// <paramref name="details"/> = variables du jeton (ex. map, score).
    /// </summary>
    public static void Set(string status, IReadOnlyDictionary<string, string>? details = null)
    {
        Status = status;
        RichPresence.details.Clear();
        if (details != null) foreach (var (k, v) in details) RichPresence.details[k] = v;
        if (!SteamPlatform.Available) return;
        foreach (var (k, v) in RichPresence.details) SteamFriends.SetRichPresence(k, v);
        SteamFriends.SetRichPresence("status", status);   // texte brut de repli (anciens clients, « Voir les infos de jeu »)
        if (!SteamFriends.SetRichPresence("steam_display", "#Status_" + status))
            GD.Print($"[Steam] Présence refusée pour #Status_{status} (jeton absent du fichier de localisation ?)");
    }

    /// <summary>Rend la partie joignable depuis la liste d'amis (0 : plus joignable) et regroupe les membres du salon.</summary>
    public static void SetJoinable(ulong lobbyId, int groupSize = 0)
    {
        if (!SteamPlatform.Available) return;
        if (lobbyId == 0)
        {
            SteamFriends.SetRichPresence("connect", "");
            SteamFriends.SetRichPresence("steam_player_group", "");
            SteamFriends.SetRichPresence("steam_player_group_size", "");
            return;
        }
        SteamFriends.SetRichPresence("connect", $"+connect_lobby {lobbyId}");
        SteamFriends.SetRichPresence("steam_player_group", lobbyId.ToString());
        if (groupSize > 0) SteamFriends.SetRichPresence("steam_player_group_size", groupSize.ToString());
    }

    public static void Clear()
    {
        Status = "";
        details.Clear();
        if (SteamPlatform.Available) SteamFriends.ClearRichPresence();
    }
}
