using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>Échange de plaques : un point de la plaque de départ se retrouve au même endroit relatif de la plaque d'arrivée (tourné de l'écart de cap).</summary>
public static class PlateSwap
{
    public static float YawDelta(ZoneDef from, ZoneDef to) => Mathf.DegToRad(to.FacingDegrees - from.FacingDegrees);

    public static Vector3 Remap(Vector3 p, ZoneDef from, ZoneDef to)
    {
        var offset = new Basis(Vector3.Up, YawDelta(from, to)) * new Vector3(p.X - from.X, 0, p.Z - from.Z);
        return new Vector3(to.X + offset.X, p.Y, to.Z + offset.Z);
    }
}
