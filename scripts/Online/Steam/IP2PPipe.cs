using System;
using System.Collections.Generic;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Tuyau de messages pair à pair sous <see cref="SteamMultiplayerPeer"/>. Adresse = SteamID (Steam) ou port (UDP local).
/// Contrat : un envoi fiable arrive une fois, dans l'ordre des envois fiables vers la même cible ; un envoi non fiable
/// peut se perdre ou doubler un envoi fiable.
/// </summary>
public interface IP2PPipe : IDisposable
{
    ulong LocalAddress { get; }
    /// <summary>Taille maximale d'un message (en-tête du peer compris).</summary>
    int MaxPacketSize { get; }
    void Send(ulong target, byte[] data, bool reliable, int channel);
    /// <summary>Messages reçus depuis le dernier appel (appelé à chaque poll : fait aussi avancer renvois et acquittements).</summary>
    List<(ulong source, byte[] data, int channel)> Receive();
}
