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

    public static void Catch(Ball ball, Node3D hand, int handIndex, int playerId, int team, bool perfect = false)
    {
        if (IsClient) Net!.RequestCatch(ball.NetId, handIndex, perfect);
        else ball.Hold(hand, playerId, team, perfect);
    }

    public static void Throw(Ball ball, int handIndex, Vector3 velocity, float spin, int mode = 0)
    {
        if (IsClient) Net!.RequestThrow(handIndex, velocity, spin, mode);
        else ball.Release(Assist.PlanFor(ball, velocity, mode), spin, mode);
    }

    /// <summary>Parade de la paume : le client la demande, l'hôte la juge (balle de palier 0, proche de la main).</summary>
    public static void Parry(Ball ball, int handIndex, Vector3 normal)
    {
        if (IsClient) Net!.RequestParry(ball.NetId, handIndex, normal);
        else ball.Deflect(normal);
    }

    /// <summary>Un dash touche un adversaire : le client le signale à l'hôte, l'hôte (ou le mode hors ligne) le juge.</summary>
    public static void Tackle(int tacklerId, int victimId, Vector2 dir)
    {
        if (IsClient) Net!.RequestTackle(victimId, dir);
        else MatchController.RequestTackle(tacklerId, victimId, dir);
    }

    /// <summary>Côté client : la balle (marionnette) que l'hôte dit tenue par ce joueur.</summary>
    public static Ball? FindHeld(SceneTree tree, int playerId)
    {
        foreach (var n in tree.GetNodesInGroup("balls"))
            if (n is Ball { IsHeld: true } b && b.OwnerPlayerId == playerId) return b;
        return null;
    }
}
