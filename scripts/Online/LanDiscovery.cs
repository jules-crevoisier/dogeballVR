using System.Collections.Generic;
using Godot;

namespace DogeballVR.Online;

/// <summary>
/// Salles visibles sur le réseau local (arènes EVA, LAN) : l'hôte annonce sa salle en UDP chaque seconde (diffusion
/// et 127.0.0.1 pour les tests sur une seule machine), les joueurs qui cherchent écoutent le port <see cref="Port"/>.
/// </summary>
public partial class LanDiscovery : Node
{
    public const int Port = 7778;
    const double AnnounceEvery = 1.0, ForgetAfter = 3.5;

    public record Room(string Address, int GamePort, string Name, string Map, int Players, int MaxPlayers, int Protocol, bool InMatch, double SeenAt, string Id);

    /// <summary>Hôte : contenu de l'annonce (null = ne rien annoncer).</summary>
    public System.Func<Godot.Collections.Dictionary?>? Announcement { get; set; }
    public bool Listening => listener != null;
    public IReadOnlyDictionary<string, Room> Rooms => rooms;

    PacketPeerUdp? sender, listener;
    readonly Dictionary<string, Room> rooms = new();
    double announceTimer;

    public Error Listen()
    {
        if (listener != null) return Error.Ok;
        listener = new PacketPeerUdp();
        var err = listener.Bind(Port);
        if (err != Error.Ok) { GD.Print($"[LAN] Écoute impossible sur le port {Port} : {err}"); listener = null; }
        return err;
    }

    public void StopListening() { listener?.Close(); listener = null; rooms.Clear(); }

    public override void _Process(double delta)
    {
        double now = Time.GetTicksMsec() / 1000.0;
        if (Announcement != null)
        {
            announceTimer -= delta;
            if (announceTimer <= 0)
            {
                announceTimer = AnnounceEvery;
                var data = Announcement();
                if (data != null) Announce(Json.Stringify(data).ToUtf8Buffer());
            }
        }
        if (listener == null) return;
        while (listener.GetAvailablePacketCount() > 0)
        {
            var bytes = listener.GetPacket();
            string from = listener.GetPacketIP();
            if (bytes.Length > 1024) continue;
            var parsed = Json.ParseString(bytes.GetStringFromUtf8());
            if (parsed.VariantType != Variant.Type.Dictionary) continue;
            var d = parsed.AsGodotDictionary();
            if (!d.ContainsKey("game") || (string)d["game"] != "dogeball") continue;
            int gamePort = d.ContainsKey("port") ? (int)d["port"] : 0;
            if (gamePort <= 0 || gamePort > 65535) continue;
            var room = new Room(from, gamePort, Identity.Clean(d.ContainsKey("name") ? (string)d["name"] : "?"), d.ContainsKey("map") ? (string)d["map"] : "",
                d.ContainsKey("players") ? (int)d["players"] : 0, d.ContainsKey("max") ? (int)d["max"] : 0,
                d.ContainsKey("protocol") ? (int)d["protocol"] : 0, d.ContainsKey("inMatch") && (bool)d["inMatch"], now, d.ContainsKey("id") ? (string)d["id"] : "");
            rooms[$"{from}:{gamePort}"] = room;
        }
        foreach (var key in new List<string>(rooms.Keys))
            if (now - rooms[key].SeenAt > ForgetAfter) rooms.Remove(key);
    }

    void Announce(byte[] payload)
    {
        if (sender == null)
        {
            sender = new PacketPeerUdp();
            sender.SetBroadcastEnabled(true);
        }
        sender.SetDestAddress("255.255.255.255", Port);
        sender.PutPacket(payload);
        sender.SetDestAddress("127.0.0.1", Port);
        sender.PutPacket(payload);
    }

    public override void _ExitTree()
    {
        sender?.Close();
        listener?.Close();
    }
}
