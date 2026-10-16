using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Net;

/// <summary>
/// Point de passage des actions sur les balles. Hors ligne ou côté hôte : on agit directement sur la balle.
/// Côté client : on envoie une demande à l'hôte (seul maître des balles) et on lit le résultat dans les snapshots.
/// </summary>
public static class BallActions
{
    public static NetSession? Net { get; set; }
    public static bool IsClient => Net is { IsClient: true };

    public static void Catch(Ball ball, Node3D hand, int handIndex, int playerId, int team)
    {
        if (IsClient) Net!.RequestCatch(ball.NetId, handIndex);
        else ball.Hold(hand, playerId, team);
    }

    public static void Throw(Ball ball, int handIndex, Vector3 velocity, float spin)
    {
        if (IsClient) Net!.RequestThrow(handIndex, velocity, spin);
        else ball.Release(velocity, spin);
    }

    /// <summary>Côté client : la balle (marionnette) que l'hôte dit tenue par ce joueur.</summary>
    public static Ball? FindHeld(SceneTree tree, int playerId)
    {
        foreach (var n in tree.GetNodesInGroup("balls"))
            if (n is Ball { IsHeld: true } b && b.OwnerPlayerId == playerId) return b;
        return null;
    }
}
