using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Globalization;
using System.Net;
using System.Net.Sockets;
using Godot;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Tuyau UDP sur 127.0.0.1, pour tester <see cref="SteamMultiplayerPeer"/> sans Steam (CI, deux instances sur une machine).
/// Adresse = port. Fiabilité simple : numéro de séquence, acquittement, renvoi toutes les 100 ms, remise dans l'ordre.
/// Perte simulée à l'envoi (acquittements et renvois compris) : <c>--p2p-loss=0.1</c>.
/// Chaque instance tire un numéro de session : un pair relancé sur le même port repart de zéro.
/// </summary>
public sealed class UdpPipe : IP2PPipe
{
    const byte KindUnreliable = 0, KindReliable = 1, KindAck = 2;
    const double ResendSeconds = 0.1;
    const int MaxTries = 200;          // 20 s : au-delà, le pair est mort (le peer l'aura déconnecté à 10 s)
    const int MaxOutOfOrder = 4096;
    const int ReliableHeader = 14;     // genre, session, session visée, canal, séquence

    sealed class Link
    {
        public uint RemoteSession;
        public uint NextSendSeq, NextExpected;
        public readonly Dictionary<uint, (byte[] datagram, double lastSent, int tries)> Unacked = new();
        public readonly Dictionary<uint, (byte[] data, int channel)> OutOfOrder = new();
    }

    readonly Socket socket;
    readonly uint session;
    readonly float loss;
    readonly Random rng = new();
    readonly Dictionary<ulong, Link> links = new();
    readonly byte[] buffer = new byte[65536];
    bool disposed;

    public ulong LocalAddress { get; }
    public int MaxPacketSize => 60000;
    /// <summary>Compteurs pour les journaux de test : datagrammes perdus exprès, renvois.</summary>
    public int Dropped { get; private set; }
    public int Resent { get; private set; }

    public static ulong AddressOf(int port) => (ulong)port;

    /// <param name="loss">Probabilité de perte à l'envoi ; null : lue sur la ligne de commande (<c>--p2p-loss=</c>), 0 par défaut.</param>
    public UdpPipe(int port, float? loss = null)
    {
        socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp) { Blocking = false, ReceiveBufferSize = 1 << 20, SendBufferSize = 1 << 20 };
        socket.Bind(new IPEndPoint(IPAddress.Loopback, port));
        LocalAddress = AddressOf(port);
        session = (uint)rng.Next(1, int.MaxValue);
        this.loss = Mathf.Clamp(loss ?? LossFromCommandLine(), 0f, 0.9f);
        GD.Print($"[P2P] Tuyau UDP local sur le port {port}" + (this.loss > 0 ? $", perte simulée {this.loss * 100:0} %" : ""));
    }

    static float LossFromCommandLine()
    {
        if (!Tools.DevFlags.Allowed) return 0f;
        var args = new List<string>(OS.GetCmdlineArgs());
        args.AddRange(OS.GetCmdlineUserArgs());
        foreach (var a in args)
            if (a.StartsWith("--p2p-loss=") && float.TryParse(a["--p2p-loss=".Length..], NumberStyles.Float, CultureInfo.InvariantCulture, out float l)) return l;
        return 0f;
    }

    static double Now => Time.GetTicksMsec() / 1000.0;

    Link LinkOf(ulong address)
    {
        if (!links.TryGetValue(address, out var link)) links[address] = link = new Link();
        return link;
    }

    public void Send(ulong target, byte[] data, bool reliable, int channel)
    {
        if (disposed) return;
        if (!reliable)
        {
            var d = new byte[6 + data.Length];
            d[0] = KindUnreliable;
            BinaryPrimitives.WriteUInt32LittleEndian(d.AsSpan(1), session);
            d[5] = (byte)channel;
            data.CopyTo(d, 6);
            Transmit(target, d);
            return;
        }
        var link = LinkOf(target);
        uint seq = link.NextSendSeq++;
        var r = new byte[ReliableHeader + data.Length];
        r[0] = KindReliable;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(1), session);
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(5), link.RemoteSession);   // 0 tant que le pair ne s'est pas présenté
        r[9] = (byte)channel;
        BinaryPrimitives.WriteUInt32LittleEndian(r.AsSpan(10), seq);
        data.CopyTo(r, ReliableHeader);
        link.Unacked[seq] = (r, Now, 1);
        Transmit(target, r);
    }

    public List<(ulong source, byte[] data, int channel)> Receive()
    {
        var received = new List<(ulong, byte[], int)>();
        if (disposed) return received;
        for (int guard = 0; guard < 10000; guard++)
        {
            int n;
            EndPoint from = new IPEndPoint(IPAddress.Any, 0);
            try
            {
                if (socket.Available <= 0) break;
                n = socket.ReceiveFrom(buffer, ref from);
            }
            catch (SocketException e) when (e.SocketErrorCode is SocketError.WouldBlock) { break; }
            catch (SocketException) { continue; }   // ICMP « port fermé » renvoyé par un envoi précédent (Windows)
            if (n < 6 || from is not IPEndPoint ep) continue;
            Handle(AddressOf(ep.Port), new ReadOnlySpan<byte>(buffer, 0, n), received);
        }
        ResendDue();
        return received;
    }

    void Handle(ulong source, ReadOnlySpan<byte> d, List<(ulong, byte[], int)> received)
    {
        uint remoteSession = BinaryPrimitives.ReadUInt32LittleEndian(d[1..]);
        var link = LinkOf(source);
        if (link.RemoteSession != remoteSession)
        {
            if (link.RemoteSession != 0)
            {
                // Pair relancé sur le même port : on repart de zéro dans les deux sens.
                GD.Print($"[P2P] Le pair {source} a redémarré : liaison remise à zéro");
                link.NextExpected = 0;
                link.OutOfOrder.Clear();
                link.NextSendSeq = 0;
                link.Unacked.Clear();
            }
            link.RemoteSession = remoteSession;
        }
        switch (d[0])
        {
            case KindUnreliable:
                received.Add((source, d[6..].ToArray(), d[5]));
                break;
            case KindReliable:
            {
                if (d.Length < ReliableHeader) return;
                uint target = BinaryPrimitives.ReadUInt32LittleEndian(d[5..]);
                if (target != 0 && target != session) return;   // destiné à une instance précédente de ce port
                uint seq = BinaryPrimitives.ReadUInt32LittleEndian(d[10..]);
                var ack = new byte[13];
                ack[0] = KindAck;
                BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(1), session);
                BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(5), remoteSession);
                BinaryPrimitives.WriteUInt32LittleEndian(ack.AsSpan(9), seq);
                Transmit(source, ack);   // toujours acquitter, même un doublon (l'acquittement précédent a pu se perdre)
                int ahead = (int)(seq - link.NextExpected);
                if (ahead < 0) return;   // doublon déjà remis
                if (ahead > 0)
                {
                    if (link.OutOfOrder.Count < MaxOutOfOrder) link.OutOfOrder.TryAdd(seq, (d[ReliableHeader..].ToArray(), d[9]));
                    return;
                }
                received.Add((source, d[ReliableHeader..].ToArray(), d[9]));
                link.NextExpected++;
                while (link.OutOfOrder.Remove(link.NextExpected, out var next))
                {
                    received.Add((source, next.data, next.channel));
                    link.NextExpected++;
                }
                break;
            }
            case KindAck:
                if (d.Length < 13 || BinaryPrimitives.ReadUInt32LittleEndian(d[5..]) != session) return;
                link.Unacked.Remove(BinaryPrimitives.ReadUInt32LittleEndian(d[9..]));
                break;
        }
    }

    void ResendDue()
    {
        double now = Now;
        foreach (var (address, link) in links)
        {
            if (link.Unacked.Count == 0) continue;
            List<uint>? dead = null;
            foreach (var seq in new List<uint>(link.Unacked.Keys))
            {
                var (datagram, lastSent, tries) = link.Unacked[seq];
                if (now - lastSent < ResendSeconds) continue;
                if (tries >= MaxTries) { (dead ??= new()).Add(seq); continue; }
                link.Unacked[seq] = (datagram, now, tries + 1);
                Resent++;
                Transmit(address, datagram);
            }
            if (dead != null) foreach (var s in dead) link.Unacked.Remove(s);
        }
    }

    void Transmit(ulong target, byte[] datagram)
    {
        if (loss > 0 && rng.NextDouble() < loss) { Dropped++; return; }
        try { socket.SendTo(datagram, new IPEndPoint(IPAddress.Loopback, (int)target)); }
        catch (SocketException) { /* tampon plein ou port fermé : le renvoi s'en charge pour le fiable */ }
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        socket.Dispose();
    }
}
