using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Maps;

/// <summary>
/// Construit l'arène en greybox à partir d'une <see cref="MapData"/> : sol à grille, zones teintées par équipe,
/// mur de limite autour de la zone locale, murs et plafond invisibles, puis les props du level design.
/// Les couleurs viennent de <see cref="Palette"/> (provisoire). Aucune géométrie n'est codée en dur dans les scènes.
/// </summary>
public static class ArenaBuilder
{
    const float LimitWallHeight = 3f;

    public static Node3D Build(MapData map, int localZoneId)
    {
        var root = new Node3D { Name = "Arena" };
        AddEnvironment(root);
        AddFloor(root, map);
        AddZones(root, map);
        var local = map.Zones.Find(z => z.Id == localZoneId);
        if (local != null) AddLimitWall(root, map, local);
        AddBounds(root, map);
        foreach (var prop in map.Props) AddProp(root, prop);
        return root;
    }

    static void AddEnvironment(Node3D root)
    {
        // Nuit bleutée, ambiance indigo, aucun post-traitement (pas de glow, pas de SSAO) pour tenir sur Quest.
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Night,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.55f, 0.6f, 0.85f),
            AmbientLightEnergy = 0.7f,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 30, 0), LightEnergy = 0.5f });
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
            MaterialOverride = Materials.Grid(Palette.Night, Palette.Indigo.Lightened(0.15f)),
        });
        root.AddChild(floor);
    }

    static void AddZones(Node3D root, MapData map)
    {
        foreach (var zone in map.Zones)
        {
            var color = Palette.Team(zone.Team);
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
            // Dalle teintée un peu au-dessus du sol (sans collision : le sol sert de support).
            root.AddChild(new MeshInstance3D
            {
                Name = $"Zone{zone.Id}",
                Mesh = new BoxMesh { Size = new Vector3(map.ZoneWidth - 0.1f, 0.02f, map.ZoneDepth - 0.1f) },
                Basis = basis,
                Position = zone.Center + new Vector3(0, 0.01f, 0),
                MaterialOverride = Materials.Grid(Palette.Night.Lerp(color, 0.14f), color.Darkened(0.4f)),
            });
            // Contour de zone au sol, en couleur d'équipe.
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
            MaterialOverride = Materials.Glow(color),
        });
    }

    /// <summary>
    /// Mur de lumière magenta sur le pourtour de la zone du joueur : invisible de loin, il apparaît à l'approche
    /// du bord (le fondu par distance est dans le shader, qui lit la position de la caméra).
    /// </summary>
    static void AddLimitWall(Node3D root, MapData map, ZoneDef zone)
    {
        var material = Materials.LimitWall(Palette.Danger);
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        float hw = map.ZoneWidth * 0.5f, hd = map.ZoneDepth * 0.5f;
        void Wall(float sx, float sz, Vector3 offset) => root.AddChild(new MeshInstance3D
        {
            Name = "LimitWall",
            Mesh = new BoxMesh { Size = new Vector3(sx, LimitWallHeight, sz) },
            Basis = basis,
            Position = zone.Center + basis * offset + new Vector3(0, LimitWallHeight * 0.5f, 0),
            MaterialOverride = material,
        });
        Wall(map.ZoneWidth, 0.02f, new Vector3(0, 0, -hd));
        Wall(map.ZoneWidth, 0.02f, new Vector3(0, 0, hd));
        Wall(0.02f, map.ZoneDepth, new Vector3(-hw, 0, 0));
        Wall(0.02f, map.ZoneDepth, new Vector3(hw, 0, 0));
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

    /// <summary>Prop de level design. Couleur avec alpha (« #rrggbbaa ») : voile de lumière additif, traversable si Solid est faux.</summary>
    static void AddProp(Node3D root, PropDef prop)
    {
        var size = new Vector3(prop.SizeX, prop.SizeY, prop.SizeZ);
        var color = Color.FromString(prop.Color, new Color(0.54f, 0.56f, 0.6f));
        var body = new StaticBody3D { Name = "Prop", Position = new Vector3(prop.X, prop.Y, prop.Z) };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            MaterialOverride = color.A < 1f ? Materials.Additive(color) : Materials.Flat(color),
        });
        if (prop.Solid) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(body);
    }
}
