using Godot;
using DogeballVR.Maps;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Plan vu de dessus d'une carte, tracé depuis son JSON (choix de carte) : sol encre, zones aux couleurs d'équipe,
/// couverts en crème, fosse hachurée, moyeu et bumpers en jaune, points de balle en vert balle. Aucun asset dessiné :
/// la carte se dessine elle-même, et un nouveau JSON a son plan sans rien faire.
/// </summary>
public partial class MapPlan : Control
{
    MapData? map;
    /// <summary>Carte à tracer (null : rien).</summary>
    public MapData? Map { get => map; set { map = value; QueueRedraw(); } }

    static readonly Color Floor = new("23202e"), Edge = new("3a3550");

    public override void _Draw()
    {
        if (map == null || map.Zones.Count == 0) return;
        // Emprise : les zones (tournées de 90° : largeur et profondeur échangées) et ce qui est posé dessus.
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        void Grow(float x, float z) { minX = Mathf.Min(minX, x); maxX = Mathf.Max(maxX, x); minZ = Mathf.Min(minZ, z); maxZ = Mathf.Max(maxZ, z); }
        foreach (var z in map.Zones)
        {
            var h = Half(z);
            Grow(z.X - h.X, z.Z - h.Y); Grow(z.X + h.X, z.Z + h.Y);
        }
        float margin = 1.2f;
        minX -= margin; maxX += margin; minZ -= margin; maxZ += margin;
        float scale = Mathf.Min(Size.X / (maxX - minX), Size.Y / (maxZ - minZ));
        var offset = (Size - new Vector2(maxX - minX, maxZ - minZ) * scale) / 2;
        Vector2 P(float x, float z) => offset + new Vector2(x - minX, z - minZ) * scale;
        Vector2[] Box(float x, float z, float sx, float sz, float rotDeg)
        {
            var b = new Basis(Vector3.Up, Mathf.DegToRad(rotDeg));
            var pts = new Vector2[4];
            var c = new[] { new Vector2(-1, -1), new Vector2(1, -1), new Vector2(1, 1), new Vector2(-1, 1) };
            for (int i = 0; i < 4; i++)
            {
                var v = b * new Vector3(c[i].X * sx / 2, 0, c[i].Y * sz / 2);
                pts[i] = P(x + v.X, z + v.Z);
            }
            return pts;
        }

        // Fond : dalle encre à liseré, ombre franche.
        var all = new Rect2(P(minX, minZ), new Vector2(maxX - minX, maxZ - minZ) * scale);
        DrawRect(new Rect2(all.Position + new Vector2(8, 10), all.Size), new Color(0, 0, 0, 0.45f));
        DrawRect(all, Floor);
        DrawRect(all, Edge, false, 3f);

        foreach (var z in map.Zones)
        {
            var h = Half(z);
            var team = Palette.Team(z.Team);
            DrawColoredPolygon(Box(z.X, z.Z, h.X * 2, h.Y * 2, 0), new Color(team, 0.55f));
            DrawPolyline(Close(Box(z.X, z.Z, h.X * 2, h.Y * 2, 0)), team.Lightened(0.2f), 3f);
        }
        foreach (var o in map.Obstacles)
        {
            var pts = Box(o.X, o.Z, o.Sx, o.Sz, o.RotY);
            switch (o.Kind)
            {
                case "pit":
                    DrawColoredPolygon(pts, new Color("0a0910"));
                    DrawPolyline(Close(pts), UiKit.Yellow, 3f);
                    break;
                case "bumper": case "hubfloor":
                    DrawColoredPolygon(pts, new Color(UiKit.Yellow, o.Kind == "hubfloor" ? 0.35f : 0.9f));
                    break;
                default:
                    DrawColoredPolygon(pts, UiKit.Cream);
                    break;
            }
        }
        float r = Mathf.Max(4f, scale * 0.35f);
        foreach (var s in map.BallSpawns)
        {
            var c = P(s.X, s.Z);
            DrawCircle(c, r + 2, new Color("0a0910"));
            DrawCircle(c, r, Palette.BallCore);
        }
    }

    Vector2 Half(ZoneDef z)
    {
        bool turned = Mathf.PosMod(Mathf.RoundToInt(z.FacingDegrees), 180) == 90;
        return (turned ? new Vector2(map!.ZoneDepth, map.ZoneWidth) : new Vector2(map!.ZoneWidth, map.ZoneDepth)) / 2;
    }

    static Vector2[] Close(Vector2[] p) => new[] { p[0], p[1], p[2], p[3], p[0] };
}
