using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Zones;

/// <summary>
/// Logique des zones. Le jeu raisonne en « zone » (centre, taille, orientation), jamais en piste entière.
/// Démo : 4 zones de 15 x 8 m. Sur une arène EVA, seules les définitions de zones changent.
/// </summary>
public partial class ZoneManager : Node
{
    [Signal] public delegate void EdgeWarningEventHandler(float distanceToEdge);

    public MapData Map { get; private set; } = null!;
    public int LocalZoneId { get; set; }

    public void Setup(MapData map, int localZoneId)
    {
        Map = map;
        LocalZoneId = localZoneId;
    }

    ZoneDef LocalZone => Map.Zones.Find(z => z.Id == LocalZoneId)!;

    /// <summary>Position de la tête dans le repère de la zone (x = largeur, z = profondeur), centre = 0.</summary>
    public Vector2 ToZoneLocal(Vector3 worldHeadPos)
    {
        var zone = LocalZone;
        var rel = worldHeadPos - zone.Center;
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees)).Inverse();
        var local = basis * rel;
        return new Vector2(local.X, local.Z);
    }

    /// <summary>Distance au bord le plus proche de la zone (négative si on est sorti).</summary>
    public float DistanceToEdge(Vector3 worldHeadPos)
    {
        var p = ToZoneLocal(worldHeadPos);
        float dx = Map.ZoneWidth * 0.5f - Mathf.Abs(p.X);
        float dz = Map.ZoneDepth * 0.5f - Mathf.Abs(p.Y);
        return Mathf.Min(dx, dz);
    }

    public override void _Process(double delta)
    {
        if (Map == null) return;
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        float d = DistanceToEdge(cam.GlobalPosition);
        if (d < Map.EdgeWarningDistance)
            EmitSignal(SignalName.EdgeWarning, d);
    }
}
