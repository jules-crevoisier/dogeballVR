using System.Collections.Generic;
using System.Linq;
using Godot;
using DogeballVR.Net;
using DogeballVR.Online.Steam;

namespace DogeballVR.Online;

/// <summary>
/// Passerelle entre le lobby et la couche Steam : salons Steam (partie rapide, privé entre amis, invitations et « Rejoindre
/// la partie » de la liste d'amis), transport par le relais de Valve (pas de port à ouvrir, IP jamais exposée), présence.
/// Sans Steam (CI, LAN, jeu lancé hors Steam), <see cref="Available"/> est faux et le lobby passe par ENet.
/// </summary>
public static class SteamBridge
{
    /// <summary>Salon Steam visé par un client (0 : connexion ENet), pour la reconnexion.</summary>
    public static ulong Target { get; set; }
    static ulong hostedLobby;
    static int generation;   // numéro de la demande Steam en cours : une réponse d'une demande annulée est ignorée
    static bool hooked;

    public static bool Available => Init();

    /// <summary>Démarre Steam une fois (sans effet s'il est absent) et branche invitations et identité.</summary>
    public static bool Init()
    {
        bool ok = SteamPlatform.Init();
        if (!ok || hooked) return ok;
        hooked = true;
        SteamHooks.LocalId = () => SteamPlatform.LocalSteamId.ToString();
        SteamHooks.PersonaName = () => SteamPlatform.PersonaName;
        // Invitation acceptée dans l'overlay, ou « Rejoindre la partie » depuis la liste d'amis.
        SteamPlatform.LobbyJoinRequested += id =>
        {
            if (Engine.GetMainLoop() is SceneTree tree) OnlineLobby.Ensure(tree, l => l.JoinSteamLobby(id));
        };
        RichPresence.Set(RichPresence.Menu);
        return true;
    }

    /// <summary>Salon demandé au lancement (`+connect_lobby`, invitation qui a lancé le jeu) : 0 si aucun.</summary>
    public static ulong PendingLaunchLobby() => Available ? SteamPlatform.ConsumePendingLobbyJoin() : 0;

    public static void QuickMatch(OnlineLobby lobby)
    {
        lobby.SetSearching("Recherche d'une partie en ligne…");
        int ticket = ++generation;
        SteamLobbies.Search(new Dictionary<string, string> { [SteamLobbies.KeyVersion] = NetHandshake.Fingerprint }, list =>
        {
            if (ticket != generation) return;   // recherche annulée entre-temps
            // Salles en attente d'abord (state=open), puis parties en cours qui acceptent un joueur à la place d'un bot.
            var pick = list.Where(l => l.Members < l.Max)
                .OrderBy(l => SteamLobbies.GetData(l.Id, "state") == "open" ? 0 : 1).ThenByDescending(l => l.Members).FirstOrDefault();
            if (pick.Id != 0) { GD.Print($"[Steam] Partie trouvée : salon {pick.Id} ({pick.Members}/{pick.Max})"); Join(lobby, pick.Id); }
            else { GD.Print("[Steam] Aucune partie ouverte : création d'un salon public"); Host(lobby, publicLobby: true, lobby.Map); }
        });
    }

    public static void Host(OnlineLobby lobby, bool publicLobby, string map)
    {
        lobby.SetSearching("Création du salon Steam…");
        int ticket = ++generation;
        SteamLobbies.Create(publicLobby, lobby.MaxPlayers, id =>
        {
            if (ticket != generation) { if (id is ulong) SteamLobbies.Leave(); return; }   // annulé pendant la création : on ne garde pas un salon fantôme
            if (id is not ulong lobbyId) { lobby.FailExternal("Steam : impossible de créer le salon"); return; }
            hostedLobby = lobbyId;
            SteamLobbies.SetData(lobbyId, SteamLobbies.KeyVersion, NetHandshake.Fingerprint);
            SteamLobbies.SetData(lobbyId, SteamLobbies.KeyMap, map);
            SteamLobbies.SetData(lobbyId, "state", "open");
            lobby.OpenRoomAsHost(SteamMultiplayerPeer.CreateServer(new SteamMessagesPipe()), map);
            lobby.SetCode(JoinCode.FromSteamLobby(lobbyId));
            RichPresence.Set(RichPresence.Lobby);
            RichPresence.SetJoinable(lobbyId, 1);
        });
    }

    public static void Join(OnlineLobby lobby, ulong lobbyId)
    {
        Target = lobbyId;
        lobby.SetSearching("Entrée dans le salon Steam…");
        int ticket = ++generation;
        SteamLobbies.Join(lobbyId, ok =>
        {
            if (ticket != generation) { if (ok) SteamLobbies.Leave(); return; }   // annulé pendant l'entrée
            if (!ok) { lobby.FailExternal("Salon Steam introuvable ou plein"); return; }
            if (SteamLobbies.GetData(lobbyId, SteamLobbies.KeyVersion) is { Length: > 0 } v && v != NetHandshake.Fingerprint)
            {
                SteamLobbies.Leave();
                lobby.FailExternal("Version différente de celle de l'hôte : mettez le jeu à jour");
                return;
            }
            ConnectToOwner(lobby, lobbyId);
        });
    }

    static void ConnectToOwner(OnlineLobby lobby, ulong lobbyId)
    {
        ulong host = ulong.TryParse(SteamLobbies.GetData(lobbyId, SteamLobbies.KeyHost), out ulong h) && h != 0 ? h : SteamLobbies.Owner(lobbyId);
        if (host == 0 || host == SteamPlatform.LocalSteamId) { lobby.FailExternal("Hôte du salon introuvable"); return; }
        lobby.ConnectAsClient(SteamMultiplayerPeer.CreateClient(new SteamMessagesPipe(), host), "Connexion à l'hôte par Steam…");
        RichPresence.Set(RichPresence.Lobby);
        RichPresence.SetJoinable(lobbyId);
    }

    /// <summary>Reconnexion : toujours membre du salon, on rouvre le transport vers l'hôte ; sinon on rentre à nouveau dans le salon.</summary>
    public static void Reconnect(OnlineLobby lobby)
    {
        if (SteamLobbies.Current == Target) ConnectToOwner(lobby, Target);
        else
        {
            int ticket = ++generation;
            SteamLobbies.Join(Target, ok => { if (ok && ticket == generation) ConnectToOwner(lobby, Target); });
        }
    }

    public static void Leave()
    {
        generation++;   // les réponses Steam encore en route sont périmées
        if (!SteamPlatform.Available) return;
        SteamLobbies.Leave();
        hostedLobby = 0;
        Target = 0;
        RichPresence.Set(RichPresence.Menu);
    }

    public static void OnMatchStarted(OnlineLobby lobby)
    {
        if (!SteamPlatform.Available) return;
        // La salle reste ouverte aux entrées en cours de partie (à la place d'un bot), mais la partie rapide préfère les salles en attente.
        if (hostedLobby != 0) SteamLobbies.SetData(hostedLobby, "state", "match");
        RichPresence.Set(RichPresence.Match, new Dictionary<string, string> { ["map"] = lobby.Map, ["score"] = "" });
    }

    public static void OnRosterChanged(OnlineLobby lobby)
    {
        if (!SteamPlatform.Available || hostedLobby == 0) return;
        if (lobby.State == LobbyState.InRoom) SteamLobbies.SetData(hostedLobby, "state", "open");
        SteamLobbies.SetData(hostedLobby, SteamLobbies.KeyMap, lobby.Map);
        SteamLobbies.SetMemberLimit(hostedLobby, lobby.MaxPlayers);
        RichPresence.SetJoinable(hostedLobby, lobby.Members.Count);
    }

    public static void OpenInvite()
    {
        ulong id = hostedLobby != 0 ? hostedLobby : SteamLobbies.Current ?? 0;
        if (id != 0) SteamLobbies.OpenInviteOverlay(id);
    }
}
