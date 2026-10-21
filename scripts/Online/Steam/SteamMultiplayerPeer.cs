using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using Godot;

namespace DogeballVR.Online.Steam;

/// <summary>
/// MultiplayerPeer de Godot au-dessus d'un <see cref="IP2PPipe"/> (Steam relayé, ou UDP local pour les tests).
/// Topologie client-serveur comme ENet : l'hôte a l'id 1, chaque client tire un id 2..int.MaxValue et l'annonce
/// (hello) ; l'hôte confirme (hello-ack) puis émet peer_connected. Les clients ne parlent qu'à l'hôte, qui relaie
/// (server relay de SceneMultiplayer). Battement toutes les 1 s, pair muet 10 s = déconnecté.
/// Le canal Godot voyage dans l'en-tête : le tuyau n'a qu'un flux fiable ordonné (canal 0).
/// </summary>
public partial class SteamMultiplayerPeer : MultiplayerPeerExtension
{
    const byte MsgData = 0, MsgHello = 1, MsgHelloAck = 2, MsgBye = 3, MsgKeepAlive = 4;
    const ushort ProtocolVersion = 1;
    const int DataHeader = 7;   // type, mode, canal, séquence (ordre des paquets non fiables ordonnés)
    const double KeepAliveSeconds = 1.0, TimeoutSeconds = 10.0, HelloRetrySeconds = 1.0;
    const int PipeChannel = 0;

    sealed class Remote
    {
        public int Id;
        public ulong Address;
        public double LastHeard, LastSent;
    }

    readonly record struct Packet(int From, byte[] Data, int Channel, TransferModeEnum Mode);

    IP2PPipe? pipe;
    bool server;
    int uniqueId;
    ulong hostAddress;
    double connectStartedAt, lastHelloAt;
    ConnectionStatus status = ConnectionStatus.Disconnected;
    bool refuseNew;
    int targetPeer, transferChannel;
    TransferModeEnum transferMode = TransferModeEnum.Reliable;
    readonly Dictionary<ulong, Remote> byAddress = new();
    readonly Dictionary<int, Remote> byId = new();
    readonly Queue<Packet> incoming = new();
    readonly Dictionary<(int from, int channel), uint> lastOrderedSeq = new();
    readonly Dictionary<int, uint> nextOrderedSeq = new();
    readonly Queue<int> pendingDisconnects = new();
    Action? quitHook;

    /// <summary>Le tuyau sous-jacent (journaux, compteurs de test).</summary>
    public IP2PPipe? Pipe => pipe;

    static double Now => Time.GetTicksMsec() / 1000.0;

    // ------------------------------------------------------------------ fabriques

    public static MultiplayerPeer CreateServer(IP2PPipe pipe)
    {
        var peer = new SteamMultiplayerPeer { pipe = pipe, server = true, uniqueId = 1, status = ConnectionStatus.Connected };
        GD.Print($"[P2P] Hôte en écoute (adresse {pipe.LocalAddress})");
        peer.HookQuit();
        return peer;
    }

    public static MultiplayerPeer CreateClient(IP2PPipe pipe, ulong hostAddress)
    {
        var peer = new SteamMultiplayerPeer
        {
            pipe = pipe, server = false, hostAddress = hostAddress, uniqueId = Random.Shared.Next(2, int.MaxValue),
            status = ConnectionStatus.Connecting, connectStartedAt = Now, lastHelloAt = -10,
        };
        peer.SendHello();
        GD.Print($"[P2P] Connexion à l'hôte {hostAddress} (id local {peer.uniqueId})");
        peer.HookQuit();
        return peer;
    }

    /// <summary>Vrai si la ligne de commande demande le transport de test <c>--transport=udp-p2p</c> (au lieu d'ENet).</summary>
    public static bool UdpTransportRequested =>
        Array.IndexOf(OS.GetCmdlineUserArgs(), "--transport=udp-p2p") >= 0 || Array.IndexOf(OS.GetCmdlineArgs(), "--transport=udp-p2p") >= 0;

    /// <summary>Hôte de test : peer Steam sur UDP local, port d'écoute <paramref name="port"/>.</summary>
    public static Error CreateUdpServer(int port, out MultiplayerPeer peer)
    {
        peer = null!;
        try { peer = CreateServer(new UdpPipe(port)); return Error.Ok; }
        catch (System.Net.Sockets.SocketException e) { GD.PrintErr($"[P2P] Port {port} indisponible : {e.Message}"); return Error.CantCreate; }
    }

    /// <summary>Client de test : écoute sur <paramref name="localPort"/>, hôte sur 127.0.0.1:<paramref name="hostPort"/>.</summary>
    public static Error CreateUdpClient(int localPort, int hostPort, out MultiplayerPeer peer)
    {
        peer = null!;
        try { peer = CreateClient(new UdpPipe(localPort), UdpPipe.AddressOf(hostPort)); return Error.Ok; }
        catch (System.Net.Sockets.SocketException e) { GD.PrintErr($"[P2P] Port {localPort} indisponible : {e.Message}"); return Error.CantCreate; }
    }

    // ------------------------------------------------------------------ MultiplayerPeer

    public override int _GetUniqueId() => uniqueId;
    public override bool _IsServer() => server;
    public override bool _IsServerRelaySupported() => true;
    public override ConnectionStatus _GetConnectionStatus() => status;
    public override void _SetRefuseNewConnections(bool pEnable) => refuseNew = pEnable;
    public override bool _IsRefusingNewConnections() => refuseNew;
    public override void _SetTargetPeer(int pPeer) => targetPeer = pPeer;
    public override void _SetTransferChannel(int pChannel) => transferChannel = pChannel;
    public override int _GetTransferChannel() => transferChannel;
    public override void _SetTransferMode(TransferModeEnum pMode) => transferMode = pMode;
    public override TransferModeEnum _GetTransferMode() => transferMode;
    public override int _GetMaxPacketSize() => (pipe?.MaxPacketSize ?? 1200) - DataHeader;
    public override int _GetAvailablePacketCount() => incoming.Count;
    public override int _GetPacketPeer() => incoming.Count > 0 ? incoming.Peek().From : 0;
    public override int _GetPacketChannel() => incoming.Count > 0 ? incoming.Peek().Channel : 0;
    public override TransferModeEnum _GetPacketMode() => incoming.Count > 0 ? incoming.Peek().Mode : TransferModeEnum.Reliable;
    public override byte[] _GetPacketScript() => incoming.Count > 0 ? incoming.Dequeue().Data : Array.Empty<byte>();

    public override Error _PutPacketScript(byte[] pBuffer)
    {
        if (pipe == null || status != ConnectionStatus.Connected) return Error.Unconfigured;
        bool reliable = transferMode == TransferModeEnum.Reliable;
        uint seq = 0;
        if (transferMode == TransferModeEnum.UnreliableOrdered)
        {
            nextOrderedSeq.TryGetValue(transferChannel, out seq);
            nextOrderedSeq[transferChannel] = seq + 1;
        }
        var data = new byte[DataHeader + pBuffer.Length];
        data[0] = MsgData;
        data[1] = (byte)transferMode;
        data[2] = (byte)transferChannel;
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(3), seq);
        pBuffer.CopyTo(data, DataHeader);
        if (!server) { SendTo(hostAddress, data, reliable); return Error.Ok; }
        if (targetPeer > 0)
        {
            if (!byId.TryGetValue(targetPeer, out var r)) return Error.InvalidParameter;
            SendTo(r.Address, data, reliable);
            return Error.Ok;
        }
        foreach (var r in byId.Values)
            if (targetPeer == 0 || r.Id != -targetPeer) SendTo(r.Address, data, reliable);
        return Error.Ok;
    }

    public override void _Poll()
    {
        if (pipe == null) return;
        while (pendingDisconnects.Count > 0) EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, (long)pendingDisconnects.Dequeue());
        double now = Now;
        foreach (var (source, data, _) in pipe.Receive())
        {
            if (pipe == null) return;   // fermé pendant le traitement (bye de l'hôte)
            if (data.Length > 0) Handle(source, data, now);
        }
        if (pipe == null) return;

        if (!server && status == ConnectionStatus.Connecting)
        {
            if (now - connectStartedAt > TimeoutSeconds) { GD.Print("[P2P] Pas de réponse de l'hôte : connexion abandonnée"); Shutdown(false); return; }
            if (now - lastHelloAt >= HelloRetrySeconds) SendHello();
            return;
        }
        foreach (var r in new List<Remote>(byId.Values))
        {
            if (now - r.LastHeard > TimeoutSeconds)
            {
                GD.Print($"[P2P] Pair {r.Id} muet depuis {TimeoutSeconds:0} s : déconnecté");
                Drop(r, sendBye: true);
                continue;
            }
            if (now - r.LastSent >= KeepAliveSeconds) SendTo(r.Address, new[] { MsgKeepAlive }, false);
        }
    }

    public override void _DisconnectPeer(int pPeer, bool pForce)
    {
        if (!byId.TryGetValue(pPeer, out var r)) return;
        if (!server) { Shutdown(true); return; }
        // L'événement part au prochain poll (comme ENet), pas au milieu de l'appel de SceneMultiplayer.
        RemoveRemote(r, sendBye: true);
        pendingDisconnects.Enqueue(pPeer);
    }

    public override void _Close() => Shutdown(true);

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete) Shutdown(true, emit: false);   // fermeture du jeu : « bye » plutôt que 10 s de silence
    }

    // ------------------------------------------------------------------ protocole

    void Handle(ulong source, byte[] data, double now)
    {
        byte type = data[0];
        if (server && type == MsgHello) { HandleHello(source, data, now); return; }
        if (!byAddress.TryGetValue(source, out var remote))
        {
            if (!server && source == hostAddress && type == MsgHelloAck && status == ConnectionStatus.Connecting)
            {
                remote = new Remote { Id = 1, Address = hostAddress, LastHeard = now, LastSent = now };
                byAddress[hostAddress] = remote;
                byId[1] = remote;
                status = ConnectionStatus.Connected;
                GD.Print($"[P2P] Connecté à l'hôte (id local {uniqueId})");
                EmitSignal(MultiplayerPeer.SignalName.PeerConnected, 1L);
            }
            else if (!server && source == hostAddress && type == MsgBye && status == ConnectionStatus.Connecting)
            {
                GD.Print("[P2P] Connexion refusée par l'hôte");
                Shutdown(false);
            }
            return;   // inconnu : ignoré (hors hello)
        }
        remote.LastHeard = now;
        switch (type)
        {
            case MsgData:
                if (data.Length < DataHeader) return;
                var mode = (TransferModeEnum)data[1];
                int channel = data[2];
                if (mode == TransferModeEnum.UnreliableOrdered)
                {
                    uint seq = BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(3));
                    var key = (remote.Id, channel);
                    if (lastOrderedSeq.TryGetValue(key, out uint last) && (int)(seq - last) <= 0) return;   // en retard : ignoré
                    lastOrderedSeq[key] = seq;
                }
                incoming.Enqueue(new Packet(remote.Id, data[DataHeader..], channel, mode));
                break;
            case MsgBye:
                GD.Print(server ? $"[P2P] Pair {remote.Id} parti" : "[P2P] L'hôte a fermé la partie");
                if (server) Drop(remote, sendBye: false);
                else Shutdown(false);
                break;
            case MsgHelloAck:
            case MsgKeepAlive:
                break;
        }
    }

    void HandleHello(ulong source, byte[] data, double now)
    {
        if (data.Length < 7) return;
        int id = BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(1));
        ushort version = BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(5));
        if (byAddress.TryGetValue(source, out var known))
        {
            if (known.Id == id) { known.LastHeard = now; SendTo(source, new[] { MsgHelloAck }, true); }   // hello répété : on reconfirme
            return;
        }
        string? refusal = refuseNew ? "connexions fermées"
            : version != ProtocolVersion ? $"protocole {version} au lieu de {ProtocolVersion}"
            : id < 2 || byId.ContainsKey(id) ? $"id {id} invalide ou déjà pris"
            : null;
        if (refusal != null)
        {
            GD.Print($"[P2P] Hello de {source} refusé : {refusal}");
            SendTo(source, new[] { MsgBye }, true);
            return;
        }
        var remote = new Remote { Id = id, Address = source, LastHeard = now, LastSent = now };
        byAddress[source] = remote;
        byId[id] = remote;
        SendTo(source, new[] { MsgHelloAck }, true);
        GD.Print($"[P2P] Pair {id} connecté (adresse {source})");
        EmitSignal(MultiplayerPeer.SignalName.PeerConnected, (long)id);
    }

    void SendHello()
    {
        var hello = new byte[7];
        hello[0] = MsgHello;
        BinaryPrimitives.WriteInt32LittleEndian(hello.AsSpan(1), uniqueId);
        BinaryPrimitives.WriteUInt16LittleEndian(hello.AsSpan(5), ProtocolVersion);
        // Le premier hello est fiable ; les suivants (toutes les 1 s) couvrent un hôte lancé après le client.
        pipe?.Send(hostAddress, hello, lastHelloAt < 0, PipeChannel);
        lastHelloAt = Now;
    }

    void SendTo(ulong address, byte[] data, bool reliable)
    {
        pipe?.Send(address, data, reliable, PipeChannel);
        if (byAddress.TryGetValue(address, out var r)) r.LastSent = Now;
    }

    void RemoveRemote(Remote r, bool sendBye)
    {
        if (sendBye) pipe?.Send(r.Address, new[] { MsgBye }, true, PipeChannel);
        byAddress.Remove(r.Address);
        byId.Remove(r.Id);
        foreach (var key in new List<(int, int)>(lastOrderedSeq.Keys)) if (key.Item1 == r.Id) lastOrderedSeq.Remove(key);
    }

    /// <summary>Hôte : un client part. Client : l'hôte part (fin de la connexion).</summary>
    void Drop(Remote r, bool sendBye)
    {
        if (!server) { Shutdown(sendBye); return; }
        RemoveRemote(r, sendBye);
        EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, (long)r.Id);
    }

    /// <summary>Fermeture du jeu (--quit-after, croix de la fenêtre) : « bye » aux pairs plutôt que 10 s de silence.</summary>
    void HookQuit()
    {
        if (Engine.GetMainLoop() is not SceneTree tree) return;
        quitHook = () => Shutdown(true, emit: false);
        tree.Root.TreeExiting += quitHook;
    }

    void Shutdown(bool sendBye, bool emit = true)
    {
        if (quitHook != null && Engine.GetMainLoop() is SceneTree tree && IsInstanceValid(tree.Root)) tree.Root.TreeExiting -= quitHook;
        quitHook = null;
        if (pipe == null) return;
        bool wasConnected = status == ConnectionStatus.Connected;
        foreach (var r in new List<Remote>(byId.Values))
        {
            RemoveRemote(r, sendBye);
            if (emit && wasConnected && !server) EmitSignal(MultiplayerPeer.SignalName.PeerDisconnected, (long)r.Id);
        }
        if (sendBye && !server && !wasConnected) pipe.Send(hostAddress, new[] { MsgBye }, true, PipeChannel);
        // Un « bye » perdu n'est pas grave : l'autre côté conclut au bout de 10 s sans battement.
        if (sendBye) GD.Print("[P2P] Fermeture du peer");
        pipe.Dispose();
        pipe = null;
        incoming.Clear();
        status = ConnectionStatus.Disconnected;
    }
}
