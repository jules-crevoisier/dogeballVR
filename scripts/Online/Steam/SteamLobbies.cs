using System;
using System.Collections.Generic;
using Godot;
using Steamworks;

namespace DogeballVR.Online.Steam;

/// <summary>Un salon trouvé par <see cref="SteamLobbies.Search"/>.</summary>
public readonly record struct LobbyInfo(ulong Id, ulong Owner, string Map, string Version, int Members, int Max);

/// <summary>
/// Salons Steam (ISteamMatchmaking) : création, recherche, entrée, données, invitations.
/// Clés de données posées par l'hôte : <see cref="KeyMap"/>, <see cref="KeyVersion"/> (filtres de recherche) et
/// <see cref="KeyHost"/> (SteamID de l'hôte du transport P2P). Sans Steam, chaque appel répond tout de suite
/// par un échec (null, liste vide, false) : l'interface peut l'afficher sans cas particulier.
/// </summary>
public static class SteamLobbies
{
    public const string KeyMap = "map", KeyVersion = "version", KeyHost = "host", KeyGame = "game";
    /// <summary>Marque nos salons : l'AppId 480 (Spacewar) est partagé par tous les jeux en test.</summary>
    public const string GameTag = "dogeballvr";

    /// <summary>Salon où l'on se trouve (null : aucun).</summary>
    public static ulong? Current { get; private set; }

    /// <summary>Entrée, départ ou déconnexion d'un membre (ou données du salon modifiées) : id du salon.</summary>
    public static event Action<ulong>? MembersChanged;

    // Les CallResult doivent rester référencés jusqu'à leur réponse.
    static readonly HashSet<object> inFlight = new();
    static Callback<LobbyChatUpdate_t>? chatUpdateCb;
    static Callback<LobbyDataUpdate_t>? dataUpdateCb;

    internal static void OnSteamReady()
    {
        chatUpdateCb = Callback<LobbyChatUpdate_t>.Create(r => MembersChanged?.Invoke(r.m_ulSteamIDLobby));
        dataUpdateCb = Callback<LobbyDataUpdate_t>.Create(r => { if (r.m_bSuccess != 0) MembersChanged?.Invoke(r.m_ulSteamIDLobby); });
    }

    /// <summary>Crée un salon public (ou amis seulement) ; répond par son id, ou null en cas d'échec.</summary>
    public static void Create(bool publicLobby, int maxMembers, Action<ulong?> done)
    {
        if (!SteamPlatform.Available) { done(null); return; }
        var type = publicLobby ? ELobbyType.k_ELobbyTypePublic : ELobbyType.k_ELobbyTypeFriendsOnly;
        Call<LobbyCreated_t>(SteamMatchmaking.CreateLobby(type, maxMembers), (r, ioFailure) =>
        {
            if (ioFailure || r.m_eResult != EResult.k_EResultOK) { GD.Print($"[Steam] Création du salon impossible ({r.m_eResult})"); done(null); return; }
            Current = r.m_ulSteamIDLobby;
            var lobby = new CSteamID(r.m_ulSteamIDLobby);
            SteamMatchmaking.SetLobbyData(lobby, KeyGame, GameTag);
            SteamMatchmaking.SetLobbyData(lobby, KeyHost, SteamPlatform.LocalSteamId.ToString());
            GD.Print($"[Steam] Salon {r.m_ulSteamIDLobby} créé ({(publicLobby ? "public" : "amis")}, {maxMembers} places)");
            done(r.m_ulSteamIDLobby);
        });
    }

    /// <summary>Cherche des salons ouverts, monde entier, avec au moins une place libre. Filtres : clé = valeur exacte.</summary>
    public static void Search(Dictionary<string, string>? filters, Action<List<LobbyInfo>> done)
    {
        if (!SteamPlatform.Available) { done(new List<LobbyInfo>()); return; }
        SteamMatchmaking.AddRequestLobbyListStringFilter(KeyGame, GameTag, ELobbyComparison.k_ELobbyComparisonEqual);
        if (filters != null)
            foreach (var (k, v) in filters) SteamMatchmaking.AddRequestLobbyListStringFilter(k, v, ELobbyComparison.k_ELobbyComparisonEqual);
        SteamMatchmaking.AddRequestLobbyListDistanceFilter(ELobbyDistanceFilter.k_ELobbyDistanceFilterWorldwide);
        SteamMatchmaking.AddRequestLobbyListFilterSlotsAvailable(1);
        Call<LobbyMatchList_t>(SteamMatchmaking.RequestLobbyList(), (r, ioFailure) =>
        {
            var list = new List<LobbyInfo>();
            if (!ioFailure)
                for (int i = 0; i < r.m_nLobbiesMatching; i++)
                {
                    var id = SteamMatchmaking.GetLobbyByIndex(i);
                    list.Add(new LobbyInfo(id.m_SteamID, SteamMatchmaking.GetLobbyOwner(id).m_SteamID,
                        SteamMatchmaking.GetLobbyData(id, KeyMap), SteamMatchmaking.GetLobbyData(id, KeyVersion),
                        SteamMatchmaking.GetNumLobbyMembers(id), SteamMatchmaking.GetLobbyMemberLimit(id)));
                }
            done(list);
        });
    }

    /// <summary>Entre dans un salon ; répond true si l'entrée est acceptée.</summary>
    public static void Join(ulong lobbyId, Action<bool> done)
    {
        if (!SteamPlatform.Available || lobbyId == 0) { done(false); return; }
        Call<LobbyEnter_t>(SteamMatchmaking.JoinLobby(new CSteamID(lobbyId)), (r, ioFailure) =>
        {
            bool ok = !ioFailure && r.m_EChatRoomEnterResponse == (uint)EChatRoomEnterResponse.k_EChatRoomEnterResponseSuccess;
            if (ok) Current = r.m_ulSteamIDLobby;
            GD.Print(ok ? $"[Steam] Entrée dans le salon {lobbyId}" : $"[Steam] Entrée refusée dans le salon {lobbyId} ({(EChatRoomEnterResponse)r.m_EChatRoomEnterResponse})");
            done(ok);
        });
    }

    public static void Leave()
    {
        if (Current is not ulong lobby) return;
        Current = null;
        if (!SteamPlatform.Available) return;
        SteamMatchmaking.LeaveLobby(new CSteamID(lobby));
        RichPresence.SetJoinable(0);
        GD.Print($"[Steam] Salon {lobby} quitté");
    }

    /// <summary>Donnée du salon (seul le propriétaire peut écrire) ; false sans Steam ou si refusé.</summary>
    public static bool SetData(ulong lobbyId, string key, string value) =>
        SteamPlatform.Available && SteamMatchmaking.SetLobbyData(new CSteamID(lobbyId), key, value);

    /// <summary>Places du salon (propriétaire seulement) : suit le nombre de zones de la carte choisie.</summary>
    public static bool SetMemberLimit(ulong lobbyId, int max) =>
        SteamPlatform.Available && SteamMatchmaking.SetLobbyMemberLimit(new CSteamID(lobbyId), max);

    public static string GetData(ulong lobbyId, string key) =>
        SteamPlatform.Available ? SteamMatchmaking.GetLobbyData(new CSteamID(lobbyId), key) : "";

    public static ulong Owner(ulong lobbyId) =>
        SteamPlatform.Available ? SteamMatchmaking.GetLobbyOwner(new CSteamID(lobbyId)).m_SteamID : 0;

    public static List<ulong> Members(ulong lobbyId)
    {
        var list = new List<ulong>();
        if (!SteamPlatform.Available) return list;
        var lobby = new CSteamID(lobbyId);
        int n = SteamMatchmaking.GetNumLobbyMembers(lobby);
        for (int i = 0; i < n; i++) list.Add(SteamMatchmaking.GetLobbyMemberByIndex(lobby, i).m_SteamID);
        return list;
    }

    /// <summary>Vrai si l'utilisateur est dans le salon courant (filtre d'acceptation des sessions P2P).</summary>
    public static bool IsMemberOfCurrent(ulong steamId) => Current is ulong lobby && Members(lobby).Contains(steamId);

    /// <summary>Vrai si un ami Steam (hors soi) est dans le salon courant : succès « jouer en ligne avec un ami ».</summary>
    public static bool FriendInCurrent()
    {
        if (!SteamPlatform.Available || Current is not ulong lobby) return false;
        foreach (var m in Members(lobby))
            if (m != SteamPlatform.LocalSteamId && SteamFriends.HasFriend(new CSteamID(m), EFriendFlags.k_EFriendFlagImmediate)) return true;
        return false;
    }

    /// <summary>Ouvre l'overlay d'invitation d'amis pour ce salon.</summary>
    public static void OpenInviteOverlay(ulong lobbyId)
    {
        if (SteamPlatform.Available) SteamFriends.ActivateGameOverlayInviteDialog(new CSteamID(lobbyId));
    }

    static void Call<T>(SteamAPICall_t handle, Action<T, bool> done) where T : struct
    {
        CallResult<T>? cr = null;
        cr = CallResult<T>.Create((r, ioFailure) =>
        {
            inFlight.Remove(cr!);
            done(r, ioFailure);
        });
        inFlight.Add(cr);
        cr.Set(handle);
    }
}
