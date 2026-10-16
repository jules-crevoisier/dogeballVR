using Godot;

namespace DogeballVR.Net;

/// <summary>Position d'un joueur dans le monde : tête, cap (yaw), les deux mains et l'altitude de son plateau (lift). C'est tout ce qui voyage sur le réseau.</summary>
public readonly record struct PoseData(Vector3 Head, float Yaw, Vector3 Left, Vector3 Right, float Elevation = 0f, bool Dash = false)
{
    public PoseData Lerp(PoseData to, float t) => new(
        Head.Lerp(to.Head, t), Mathf.LerpAngle(Yaw, to.Yaw, t), Left.Lerp(to.Left, t), Right.Lerp(to.Right, t),
        Mathf.Lerp(Elevation, to.Elevation, t), to.Dash);
}
