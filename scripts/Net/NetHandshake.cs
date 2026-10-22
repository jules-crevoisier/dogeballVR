using System;
using System.Linq;
using System.Reflection;
using System.Text;
using Godot;

namespace DogeballVR.Net;

/// <summary>
/// Poignée de main avant tout RPC (authentification de SceneMultiplayer) : chaque côté envoie sa version du protocole et
/// une empreinte de tous les RPC du jeu (noms, paramètres, modes). Les identifiants de RPC de Godot dépendent de cet
/// ensemble : deux versions différentes ne doivent jamais s'échanger un seul RPC. Le client qui voit une autre empreinte
/// se déconnecte avec « version différente » au lieu d'attendre une réponse qui ne viendra pas.
/// </summary>
public static class NetHandshake
{
    /// <summary>Empreinte de la version : protocole + RPC (calculée une fois).</summary>
    public static string Fingerprint { get; } = Compute();

    /// <summary>Appelé quand la poignée de main échoue côté client (texte pour le joueur).</summary>
    public static event Action<string>? Rejected;

    static SceneMultiplayer? installed;

    /// <summary>Branche la poignée de main sur l'API multijoueur (une seule fois par arbre).</summary>
    public static void Install(MultiplayerApi api)
    {
        if (api is not SceneMultiplayer sm || ReferenceEquals(installed, sm)) return;
        installed = sm;
        sm.AuthTimeout = 5.0;
        sm.ServerRelay = false;   // les clients ne se parlent jamais entre eux : tout passe par l'hôte
        sm.AuthCallback = new Callable(HandshakeNode.Get(sm), HandshakeNode.MethodName.OnAuth);
        sm.PeerAuthenticating += id => sm.SendAuth((int)id, Encoding.UTF8.GetBytes(Fingerprint));
    }

    /// <summary>Fermeture du jeu : plus de référence statique vers l'API multijoueur.</summary>
    public static void Release()
    {
        installed = null;
        HandshakeNode.Release();
    }

    internal static void Fail(string reason) => Rejected?.Invoke(reason);

    static string Compute()
    {
        var sb = new StringBuilder($"dogeball/{NetSession.ProtocolVersion}|");
        var rpcs = typeof(NetHandshake).Assembly.GetTypes()
            .SelectMany(t => t.GetMethods(BindingFlags.Instance | BindingFlags.Static | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly)
                .Where(m => m.GetCustomAttribute<RpcAttribute>() != null)
                .Select(m =>
                {
                    var a = m.GetCustomAttribute<RpcAttribute>()!;
                    return $"{t.FullName}.{m.Name}({string.Join(",", m.GetParameters().Select(p => p.ParameterType.Name))}){a.Mode}{a.TransferMode}{a.CallLocal}{a.TransferChannel}";
                }))
            .OrderBy(s => s, StringComparer.Ordinal);
        foreach (var r in rpcs) sb.Append(r).Append(';');
        var hash = System.Security.Cryptography.SHA256.HashData(Encoding.UTF8.GetBytes(sb.ToString()));
        string fake = Array.IndexOf(OS.GetCmdlineUserArgs(), "--fake-version") >= 0 ? "-test" : "";   // test CI d'une version différente
        return $"dogeball/{NetSession.ProtocolVersion}/{Convert.ToHexString(hash)[..16]}{fake}";
    }
}

/// <summary>Porteur du rappel d'authentification (un Callable Godot doit viser un objet Godot).</summary>
public partial class HandshakeNode : RefCounted
{
    static HandshakeNode? instance;
    SceneMultiplayer api = null!;

    public static void Release() => instance = null;

    public static HandshakeNode Get(SceneMultiplayer api)
    {
        instance ??= new HandshakeNode();
        instance.api = api;
        return instance;
    }

    public void OnAuth(int id, byte[] data)
    {
        string theirs = data.Length <= 128 ? Encoding.UTF8.GetString(data) : "?";
        if (theirs == NetHandshake.Fingerprint)
        {
            api.CompleteAuth(id);
            return;
        }
        GD.Print($"[Net] Version différente : la nôtre {NetHandshake.Fingerprint}, celle du pair {id} {theirs}");
        if (id == 1) { NetHandshake.Fail("Version différente de celle de l'hôte : mettez le jeu à jour"); return; }
        // Hôte : on laisse une seconde à notre empreinte pour arriver, le client affiche alors « mettez le jeu à jour ».
        if (Engine.GetMainLoop() is SceneTree tree) tree.CreateTimer(1.0).Timeout += () => { if (System.Array.IndexOf(api.GetAuthenticatingPeers(), id) >= 0 || System.Array.IndexOf(api.GetPeers(), id) >= 0) api.DisconnectPeer(id); };   // seulement s'il est encore là
        else api.DisconnectPeer(id);
    }
}
