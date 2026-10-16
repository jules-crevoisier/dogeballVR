using Godot;

namespace DogeballVR.Net;

/// <summary>
/// Session réseau de la démo : un joueur héberge (autorité sur les balles, les touches et les scores),
/// les 3 autres rejoignent. Même principe que pour un serveur dédié plus tard (4v4, inter-lieux).
/// TODO : synchroniser les balles (MultiplayerSynchronizer) et les mains/têtes des avatars.
/// </summary>
public partial class NetSession : Node
{
    public const int DefaultPort = 7777;
    public const int MaxPlayers = 4;

    public bool IsHost => Multiplayer.IsServer();

    public Error Host(int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateServer(port, MaxPlayers - 1);
        if (err == Error.Ok) Multiplayer.MultiplayerPeer = peer;
        return err;
    }

    public Error Join(string address, int port = DefaultPort)
    {
        var peer = new ENetMultiplayerPeer();
        var err = peer.CreateClient(address, port);
        if (err == Error.Ok) Multiplayer.MultiplayerPeer = peer;
        return err;
    }
}
