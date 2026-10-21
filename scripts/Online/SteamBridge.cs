namespace DogeballVR.Online;

/// <summary>
/// Passerelle entre le lobby et la couche Steam (lobbies Steam, invitations, transport par relais Valve).
/// Sans Steam (CI, LAN, build sans Steam lancé), <see cref="Available"/> est faux et le lobby passe par ENet.
/// </summary>
public static partial class SteamBridge
{
    /// <summary>Lobby Steam visé par un client (0 : connexion ENet), pour la reconnexion.</summary>
    public static ulong Target { get; set; }
    public static bool Available => false;
    public static void QuickMatch(OnlineLobby lobby) { }
    public static void Host(OnlineLobby lobby, bool publicLobby, string map) { }
    public static void Join(OnlineLobby lobby, ulong steamLobby) { }
    public static void Reconnect(OnlineLobby lobby) { }
    public static void Leave() { }
    public static void OnMatchStarted(OnlineLobby lobby) { }
    public static void OnRosterChanged(OnlineLobby lobby) { }
    public static void OpenInvite() { }
}
