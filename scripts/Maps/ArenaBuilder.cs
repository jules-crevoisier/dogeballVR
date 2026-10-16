using Godot;

namespace DogeballVR.Maps;

/// <summary>
/// Construit l'arène en greybox à partir d'une <see cref="MapData"/> : sol teinté par équipe, lignes de zone,
/// murs et plafond invisibles, puis les props du level design. Aucune géométrie n'est codée en dur dans les scènes.
/// </summary>
public static class ArenaBuilder
{
    static readonly Color[] TeamColors = { new(0.15f, 0.45f, 0.95f), new(0.95f, 0.3f, 0.2f) };

    public static Node3D Build(MapData map)
    {
        var root = new Node3D { Name = "Arena" };
        AddFloor(root, map);
        AddZones(root, map);
        AddBounds(root, map);
        foreach (var prop in map.Props) AddProp(root, prop);
        return root;
    }

    static void AddFloor(Node3D root, MapData map)
    {
        var size = new Vector3(map.ArenaWidth + 6f, 0.2f, map.ArenaDepth + 6f);
        var floor = new StaticBody3D { Name = "Floor" };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = new Vector3(0, -0.1f, 0) });
        floor.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = new Vector3(0, -0.1f, 0),
            MaterialOverride = Flat(new Color(0.18f, 0.19f, 0.22f)),
        });
        root.AddChild(floor);
    }

    static void AddZones(Node3D root, MapData map)
    {
        foreach (var zone in map.Zones)
        {
            var color = TeamColors[zone.Team % TeamColors.Length];
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
            // Dalle teintée un peu au-dessus du sol (pas de collision : le sol sert de support).
            var tile = new MeshInstance3D
            {
                Name = $"Zone{zone.Id}",
                Mesh = new BoxMesh { Size = new Vector3(map.ZoneWidth - 0.1f, 0.02f, map.ZoneDepth - 0.1f) },
                Basis = basis,
                Position = zone.Center + new Vector3(0, 0.01f, 0),
                MaterialOverride = Flat(color.Darkened(0.55f)),
            };
            root.AddChild(tile);
            // Liseré lumineux sur le contour : on voit où s'arrête sa zone.
            AddEdge(root, zone, basis, color, map.ZoneWidth, 0.06f, new Vector3(0, 0, -map.ZoneDepth * 0.5f));
            AddEdge(root, zone, basis, color, map.ZoneWidth, 0.06f, new Vector3(0, 0, map.ZoneDepth * 0.5f));
            AddEdge(root, zone, basis, color, 0.06f, map.ZoneDepth, new Vector3(-map.ZoneWidth * 0.5f, 0, 0));
            AddEdge(root, zone, basis, color, 0.06f, map.ZoneDepth, new Vector3(map.ZoneWidth * 0.5f, 0, 0));
            root.AddChild(new Label3D
            {
                Text = $"ÉQUIPE {zone.Team + 1}",
                Position = zone.Center + new Vector3(0, 0.03f, 0),
                RotationDegrees = new Vector3(-90, zone.FacingDegrees, 0),
                FontSize = 96,
                PixelSize = 0.006f,
                Modulate = color,
            });
        }
    }

    static void AddEdge(Node3D root, ZoneDef zone, Basis basis, Color color, float sx, float sz, Vector3 offset)
    {
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(sx, 0.03f, sz) },
            Basis = basis,
            Position = zone.Center + basis * offset + new Vector3(0, 0.02f, 0),
            MaterialOverride = Glow(color),
        });
    }

    /// <summary>Murs et plafond invisibles : les balles restent dans l'arène.</summary>
    static void AddBounds(Node3D root, MapData map)
    {
        float w = map.ArenaWidth, d = map.ArenaDepth, h = map.ArenaHeight;
        AddWall(root, new Vector3(0.4f, h, d + 1), new Vector3(-w / 2 - 0.2f, h / 2, 0));
        AddWall(root, new Vector3(0.4f, h, d + 1), new Vector3(w / 2 + 0.2f, h / 2, 0));
        AddWall(root, new Vector3(w + 1, h, 0.4f), new Vector3(0, h / 2, -d / 2 - 0.2f));
        AddWall(root, new Vector3(w + 1, h, 0.4f), new Vector3(0, h / 2, d / 2 + 0.2f));
        AddWall(root, new Vector3(w + 1, 0.4f, d + 1), new Vector3(0, h + 0.2f, 0));
    }

    static void AddWall(Node3D root, Vector3 size, Vector3 position)
    {
        var wall = new StaticBody3D { Position = position };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(wall);
    }

    static void AddProp(Node3D root, PropDef prop)
    {
        var size = new Vector3(prop.SizeX, prop.SizeY, prop.SizeZ);
        var color = Color.FromString(prop.Color, new Color(0.54f, 0.56f, 0.6f));
        var body = new StaticBody3D { Name = "Prop", Position = new Vector3(prop.X, prop.Y, prop.Z) };
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Flat(color) });
        if (prop.Solid) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
    }

    static StandardMaterial3D Flat(Color c) => new() { AlbedoColor = c, Roughness = 1f };

    static StandardMaterial3D Glow(Color c) => new()
    {
        AlbedoColor = c,
        EmissionEnabled = true,
        Emission = c,
        EmissionEnergyMultiplier = 1.5f,
    };
}
