using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using Steamworks;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Tuyau Steam (ISteamNetworkingMessages) : adresse = SteamID, trafic relayé par Valve (SDR) par défaut, sans ouverture
/// de port ni IP exposée. Les sessions entrantes ne sont acceptées que si <see cref="AcceptFrom"/> le permet
/// (par défaut : membres du salon courant, ou tout le monde hors salon).
/// </summary>
public sealed class SteamMessagesPipe : IP2PPipe
{
    const int BatchSize = 64;

    readonly int channels;
    readonly IntPtr[] batch = new IntPtr[BatchSize];
    Callback<SteamNetworkingMessagesSessionRequest_t>? sessionRequestCb;
    Callback<SteamNetworkingMessagesSessionFailed_t>? sessionFailedCb;
    bool disposed;
    // Demande de session arrivée avant que le salon nous ait montré ce membre (LobbyChatUpdate en retard) : réessayée 10 s.
    readonly Dictionary<ulong, (SteamNetworkingIdentity identity, ulong since)> pendingSessions = new();

    public ulong LocalAddress { get; }
    public int MaxPacketSize => Constants.k_cbMaxSteamNetworkingSocketsMessageSizeSend;
    public Func<ulong, bool> AcceptFrom { get; set; } = id => SteamLobbies.Current == null || SteamLobbies.IsMemberOfCurrent(id);

    /// <param name="channels">Canaux Steam relevés à chaque Receive (le peer n'utilise que le canal 0).</param>
    public SteamMessagesPipe(int channels = 1)
    {
        if (!SteamPlatform.Available) throw new InvalidOperationException("Steam indisponible : utiliser UdpPipe ou ENet");
        this.channels = Math.Max(1, channels);
        LocalAddress = SteamPlatform.LocalSteamId;
        SteamNetworkingUtils.InitRelayNetworkAccess();   // prépare le relais SDR dès maintenant (sinon au premier envoi)
        sessionRequestCb = Callback<SteamNetworkingMessagesSessionRequest_t>.Create(r =>
        {
            var identity = r.m_identityRemote;
            ulong id = identity.GetSteamID().m_SteamID;
            if (AcceptFrom(id)) SteamNetworkingMessages.AcceptSessionWithUser(ref identity);
            else pendingSessions[id] = (identity, Time.GetTicksMsec());
        });
        sessionFailedCb = Callback<SteamNetworkingMessagesSessionFailed_t>.Create(r =>
            GD.Print($"[P2P] Session Steam perdue avec {r.m_info.m_identityRemote.GetSteamID().m_SteamID} ({r.m_info.m_eEndReason}) {r.m_info.m_szEndDebug}"));
    }

    void RetryPendingSessions()
    {
        if (pendingSessions.Count == 0) return;
        foreach (var (id, (identity, since)) in new Dictionary<ulong, (SteamNetworkingIdentity, ulong)>(pendingSessions))
        {
            var who = identity;
            if (AcceptFrom(id)) { pendingSessions.Remove(id); SteamNetworkingMessages.AcceptSessionWithUser(ref who); }
            else if (Time.GetTicksMsec() - since > 10_000) { pendingSessions.Remove(id); GD.Print($"[P2P] Session refusée : {id} n'est pas dans le salon"); }
        }
    }

    public void Send(ulong target, byte[] data, bool reliable, int channel)
    {
        if (disposed || !SteamPlatform.Available) return;   // Steam déjà éteint (fermeture du jeu)
        var identity = new SteamNetworkingIdentity();
        identity.SetSteamID(new CSteamID(target));
        int flags = (reliable ? Constants.k_nSteamNetworkingSend_ReliableNoNagle : Constants.k_nSteamNetworkingSend_UnreliableNoNagle)
                    | Constants.k_nSteamNetworkingSend_AutoRestartBrokenSession;
        var handle = GCHandle.Alloc(data, GCHandleType.Pinned);
        try
        {
            var result = SteamNetworkingMessages.SendMessageToUser(ref identity, handle.AddrOfPinnedObject(), (uint)data.Length, flags, channel);
            if (result != EResult.k_EResultOK && reliable) GD.Print($"[P2P] Envoi fiable à {target} refusé ({result})");
        }
        finally { handle.Free(); }
    }

    public List<(ulong source, byte[] data, int channel)> Receive()
    {
        var received = new List<(ulong, byte[], int)>();
        if (disposed) return received;
        RetryPendingSessions();
        for (int ch = 0; ch < channels; ch++)
        {
            int n;
            while ((n = SteamNetworkingMessages.ReceiveMessagesOnChannel(ch, batch, BatchSize)) > 0)
            {
                for (int i = 0; i < n; i++)
                {
                    var msg = SteamNetworkingMessage_t.FromIntPtr(batch[i]);
                    var data = new byte[msg.m_cbSize];
                    Marshal.Copy(msg.m_pData, data, 0, msg.m_cbSize);
                    received.Add((msg.m_identityPeer.GetSteamID().m_SteamID, data, msg.m_nChannel));
                    SteamNetworkingMessage_t.Release(batch[i]);
                }
                if (n < BatchSize) break;
            }
        }
        return received;
    }

    public void Dispose()
    {
        if (disposed) return;
        disposed = true;
        sessionRequestCb?.Dispose();
        sessionFailedCb?.Dispose();
        // Pas de CloseSessionWithUser ici : il jetterait le « bye » encore en file. Steam ferme les sessions inactives seul.
    }
}
