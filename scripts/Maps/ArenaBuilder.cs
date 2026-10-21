using System.Collections.Generic;
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

    /// <summary>L'arène construite et, par zone, le plateau qui monte avec le lift (zone, couverts, distributeur, pad).</summary>
    public record Result(Node3D Root, Dictionary<int, Node3D> Platforms);

    public static Result Build(MapData map, int localZoneId, bool pcProfile = true)
    {
        var root = new Node3D { Name = "Arena" };
        var platforms = new Dictionary<int, Node3D>();
        foreach (var z in map.Zones)
        {
            var platform = new Node3D { Name = $"Platform{z.Id}" };
            root.AddChild(platform);
            platforms[z.Id] = platform;
        }
        Node3D ParentOf(int zone) => platforms.TryGetValue(zone, out var p) ? p : root;

        var decor = map.Decor == null ? null : ModelLibrary.Instance(map.Decor);
        bool kc = decor != null;
        if (kc) AddKcEnvironment(root, pcProfile); else AddEnvironment(root);
        AddFloor(root, map);
        if (kc)
        {
            root.AddChild(decor!);
            var city = ModelLibrary.Instance(map.Decor + "_ville");
            if (city != null) root.AddChild(city);
            HideFloorMeshes(root);   // le toit du décor remplace la grille ; les collisions restent
        }
        AddZones(map, platforms, kc);
        var local = map.Zones.Find(z => z.Id == localZoneId);
        if (local != null) AddLimitWall(ParentOf(local.Id), map, local);
        AddBounds(root, map);
        if (map.IsRing) { AddRingExtras(root, map, kc); if (pcProfile) AddPcExtras(root, map); }
        foreach (var prop in map.Props) AddProp(root, prop);
        foreach (var o in map.Obstacles) AddObstacle(ParentOf(o.Zone), o, kc);
        foreach (var def in map.BallSpawns) AddPedestal(ParentOf(def.Zone), def);
        return new Result(root, platforms);
    }

    /// <summary>Ciel de jour dégradé, brume lointaine, soleil chaud : l'ambiance de la DA Knockout City. Le glow n'est activé qu'au clavier-souris.</summary>
    static void AddKcEnvironment(Node3D root, bool pcProfile)
    {
        var sky = new ProceduralSkyMaterial
        {
            SkyTopColor = new Color("4C8DE8"), SkyHorizonColor = new Color("FFDDBA"), SkyCurve = 0.18f,
            GroundHorizonColor = new Color("EBCDB4"), GroundBottomColor = new Color("8A7BB5"),
            SunAngleMax = 20f, SunCurve = 0.1f,
        };
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Sky,
            Sky = new Sky { SkyMaterial = sky },
            AmbientLightSource = Godot.Environment.AmbientSource.Sky,
            AmbientLightEnergy = 1.0f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
            FogEnabled = true, FogLightColor = new Color("E6D4EA"), FogDensity = 0.0022f,
            GlowEnabled = pcProfile, GlowIntensity = 0.7f, GlowBloom = 0.08f, GlowHdrThreshold = 1.0f,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(new DirectionalLight3D
        {
            RotationDegrees = new Vector3(-48, -35, 0), LightEnergy = 1.4f, LightColor = new Color("FFE6C2"),
            ShadowEnabled = pcProfile, ShadowBias = 0.06f, ShadowNormalBias = 1.5f, ShadowBlur = 1.6f,
            DirectionalShadowMode = DirectionalLight3D.ShadowMode.Parallel2Splits, DirectionalShadowMaxDistance = 70f,   // Quest : pas d'ombres temps réel, faux contact au sol
        });
    }

    static void HideFloorMeshes(Node3D root)
    {
        foreach (var n in root.GetChildren())
            if (n is StaticBody3D body && body.Name.ToString().StartsWith("Floor"))
                foreach (var c in body.GetChildren())
                    if (c is MeshInstance3D m) m.Visible = false;
    }

    static void AddEnvironment(Node3D root)
    {
        // Arène sombre et calme (DA v3), ambiance indigo, aucun post-traitement (pas de glow, pas de SSAO) pour tenir sur Quest.
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.Color,
            BackgroundColor = Palette.Night,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.62f, 0.68f, 0.85f),
            AmbientLightEnergy = 0.8f,
        };
        root.AddChild(new WorldEnvironment { Environment = env });
        root.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-60, 30, 0), LightEnergy = 0.45f, ShadowEnabled = false });
    }

    /// <summary>Sol. Une fosse (obstacle « pit ») coupe le sol en deux dalles épaisses : les balles qui y tombent sont perdues.</summary>
    static void AddFloor(Node3D root, MapData map)
    {
        var b = map.Bounds;
        float x0 = b.MinX - 3f, x1 = b.MaxX + 3f, z0 = b.MinZ - 3f, z1 = b.MaxZ + 3f;
        var pit = map.Obstacles.Find(o => o.Kind == "pit");
        if (pit == null) { AddSlab(root, x0, x1, z0, z1, 0.2f); return; }
        if (map.PitRect is { } hole)
        {
            // Fosse carrée au centre (ring-out) : quatre dalles autour du trou, rebord clair de 0,3 m.
            float px0 = hole.X - hole.Sx / 2f, px1 = hole.X + hole.Sx / 2f, pz0 = hole.Z - hole.Sz / 2f, pz1 = hole.Z + hole.Sz / 2f;
            AddSlab(root, x0, x1, z0, pz0, 4.2f);
            AddSlab(root, x0, x1, pz1, z1, 4.2f);
            AddSlab(root, x0, px0, pz0, pz1, 4.2f);
            AddSlab(root, px1, x1, pz0, pz1, 4.2f);
            AddRim(root, px0, px1, pz0, pz1);
            return;
        }
        AddSlab(root, x0, x1, z0, pit.Z - pit.Sz / 2f, 4.2f);
        AddSlab(root, x0, x1, pit.Z + pit.Sz / 2f, z1, 4.2f);
    }

    /// <summary>Rebord de la fosse : bande claire de 0,3 m de large (visuelle, sans collision : le joueur peut s'y engager et tomber).</summary>
    static void AddRim(Node3D root, float x0, float x1, float z0, float z1)
    {
        const float w = 0.3f;
        void Strip(float cx, float cz, float sx, float sz) => root.AddChild(new MeshInstance3D
        {
            Name = "Rebord",
            Mesh = new BoxMesh { Size = new Vector3(sx, 0.04f, sz) },
            Position = new Vector3(cx, 0.02f, cz),
            MaterialOverride = Materials.Glow(Palette.Lines, 0.8f),
        });
        Strip((x0 + x1) / 2f, z0 + w / 2f, x1 - x0, w);
        Strip((x0 + x1) / 2f, z1 - w / 2f, x1 - x0, w);
        Strip(x0 + w / 2f, (z0 + z1) / 2f, w, z1 - z0 - 2 * w);
        Strip(x1 - w / 2f, (z0 + z1) / 2f, w, z1 - z0 - 2 * w);
    }

    /// <summary>Terrasses et tremplins du profil PC. Terrasse : bloc plein à parapet (les balles ne le traversent pas) ; tremplin : disque lumineux au sol.</summary>
    static void AddPcExtras(Node3D root, MapData map)
    {
        if (map.Pc == null) return;
        foreach (var t in map.Pc.Terraces)
        {
            var zone = map.Zones.Find(z => z.Id == t.Zone);
            var color = Palette.Team(zone?.Team ?? 0);
            var body = new StaticBody3D { Name = t.Id, Position = new Vector3(t.X, t.Sy / 2f, t.Z) };
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(t.Sx, t.Sy, t.Sz) }, MaterialOverride = Materials.Grid(Palette.Indigo, color.Darkened(0.35f)) });
            body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = new Vector3(t.Sx, t.Sy, t.Sz) } });
            root.AddChild(body);
            // Parapet : quatre murets fins sur le bord de la terrasse.
            const float th = 0.12f;
            void Wall(float cx, float cz, float sx, float sz)
            {
                var size = new Vector3(sx, t.ParapetHeight, sz);
                var w = new StaticBody3D { Name = $"{t.Id}_parapet", Position = new Vector3(cx, t.Sy + t.ParapetHeight / 2f, cz) };
                w.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Materials.Flat(color.Darkened(0.2f)) });
                w.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
                root.AddChild(w);
            }
            Wall(t.X, t.Z - t.Sz / 2f + th / 2f, t.Sx, th);
            Wall(t.X, t.Z + t.Sz / 2f - th / 2f, t.Sx, th);
            Wall(t.X - t.Sx / 2f + th / 2f, t.Z, th, t.Sz - 2 * th);
            Wall(t.X + t.Sx / 2f - th / 2f, t.Z, th, t.Sz - 2 * th);
        }
        foreach (var p in map.Pc.JumpPads)
        {
            var zone = map.Zones.Find(z => z.Id == p.Zone);
            root.AddChild(new MeshInstance3D
            {
                Name = p.Id,
                Mesh = new CylinderMesh { TopRadius = p.Radius, BottomRadius = p.Radius, Height = 0.05f },
                Position = new Vector3(p.X, 0.03f, p.Z),
                MaterialOverride = Materials.Glow(Palette.Team(zone?.Team ?? 0), 1.2f),
            });
        }
    }

    static void AddSlab(Node3D root, float x0, float x1, float z0, float z1, float thickness)
    {
        var size = new Vector3(x1 - x0, thickness, z1 - z0);
        var center = new Vector3((x0 + x1) / 2f, -thickness / 2f, (z0 + z1) / 2f);
        var floor = new StaticBody3D { Name = "Floor" };
        floor.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = center });
        floor.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = size },
            Position = center,
            MaterialOverride = Materials.Grid(Palette.Floor, Palette.Lines.Darkened(0.55f)),
        });
        root.AddChild(floor);
    }

    /// <param name="kc">Décor continu (DA Knockout City) : pas de dalles, contours ni textes de zone peints au sol ; les zones physiques ne sont marquées que par les limites en jeu.</param>
    static void AddZones(MapData map, Dictionary<int, Node3D> platforms, bool kc = false)
    {
        foreach (var zone in map.Zones)
        {
            var root = platforms[zone.Id];
            var color = Palette.Team(zone.Team);
            var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
            if (kc)
            {
                if (zone.Lift != null) AddLift(root, map, zone);
                continue;
            }
            // Dalle teintée un peu au-dessus du sol (sans collision : le sol sert de support).
            root.AddChild(new MeshInstance3D
            {
                Name = $"Zone{zone.Id}",
                Mesh = new BoxMesh { Size = new Vector3(map.ZoneWidth - 0.1f, 0.02f, map.ZoneDepth - 0.1f) },
                Basis = basis,
                Position = zone.Center + new Vector3(0, 0.01f, 0),
                MaterialOverride = Materials.Grid(zone.Team == 0 ? Palette.FloorTeamA : Palette.FloorTeamB, color.Darkened(0.4f)),
            });
            // Contour de zone au sol, en couleur d'équipe.
            AddEdge(root, zone, basis, color, map.ZoneWidth, 0.06f, new Vector3(0, 0, -map.ZoneDepth * 0.5f));
            AddEdge(root, zone, basis, color, map.ZoneWidth, 0.06f, new Vector3(0, 0, map.ZoneDepth * 0.5f));
            AddEdge(root, zone, basis, color, 0.06f, map.ZoneDepth, new Vector3(-map.ZoneWidth * 0.5f, 0, 0));
            AddEdge(root, zone, basis, color, 0.06f, map.ZoneDepth, new Vector3(map.ZoneWidth * 0.5f, 0, 0));
            if (zone.Lift != null) AddLift(root, map, zone);
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

    /// <summary>Plateau qui monte avec le lift : dalle sous la zone (visible et solide seulement quand elle est haute) et pad rond.</summary>
    static void AddLift(Node3D platform, MapData map, ZoneDef zone)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        var size = new Vector3(map.ZoneWidth, 0.3f, map.ZoneDepth);
        var slab = new StaticBody3D { Name = "Plateau", Position = zone.Center + new Vector3(0, -0.17f, 0), Basis = basis };
        slab.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = Materials.Flat(Palette.Indigo) });
        slab.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        platform.AddChild(slab);
        // Garde-corps du bord avant (côté adverse) : 0,9 m, il retient les balles et sert d'appui visuel en hauteur.
        float rail = map.LiftRules.RailingHeight;
        if (rail > 0f)
        {
            var railSize = new Vector3(map.ZoneWidth, rail, 0.06f);
            var railing = new StaticBody3D { Name = "GardeCorps", Basis = basis, Position = zone.Center + basis * new Vector3(0, rail * 0.5f, -map.ZoneDepth * 0.5f) };
            railing.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = railSize }, MaterialOverride = Materials.Glow(Palette.Team(zone.Team), 0.6f) });
            railing.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = railSize } });
            platform.AddChild(railing);
        }
        platform.AddChild(new MeshInstance3D
        {
            Name = "LiftPad",
            Mesh = new CylinderMesh { TopRadius = zone.Lift!.Pad.Radius, BottomRadius = zone.Lift.Pad.Radius, Height = 0.03f },
            Position = new Vector3(zone.Lift.Pad.X, 0.03f, zone.Lift.Pad.Z),
            MaterialOverride = Materials.Glow(Palette.UiGrey, 1.2f),
        });
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
        void Wall(float sx, float sz, Vector3 offset)
        {
            var wall = new MeshInstance3D
            {
                Name = "LimitWall",
                Mesh = new BoxMesh { Size = new Vector3(sx, LimitWallHeight, sz) },
                Basis = basis,
                Position = zone.Center + basis * offset + new Vector3(0, LimitWallHeight * 0.5f, 0),
                MaterialOverride = material,
            };
            wall.AddToGroup("limit_wall");   // Godot renomme les doublons : le joueur PC retrouve les 4 murs par ce groupe
            root.AddChild(wall);
        }
        Wall(map.ZoneWidth, 0.02f, new Vector3(0, 0, -hd));
        Wall(map.ZoneWidth, 0.02f, new Vector3(0, 0, hd));
        Wall(0.02f, map.ZoneDepth, new Vector3(-hw, 0, 0));
        Wall(0.02f, map.ZoneDepth, new Vector3(hw, 0, 0));
    }

    /// <summary>Maps en moulinet : joints entre plaques (sol continu), pylône central plein et lanceur lumineux au sommet.</summary>
    static void AddRingExtras(Node3D root, MapData map, bool kc = false)
    {
        if (map.Pc != null && !kc)
            foreach (var j in map.Pc.Joints)
                root.AddChild(new MeshInstance3D
                {
                    Name = j.Id,
                    Mesh = new BoxMesh { Size = new Vector3(j.Sx, 0.02f, j.Sz) },
                    Position = new Vector3(j.X, 0.012f, j.Z),
                    MaterialOverride = Materials.Grid(Palette.Indigo, Palette.Lines.Darkened(0.5f)),
                });
        if (map.Hub == null) return;
        var h = map.Hub;
        if (h.Rotor != null) root.AddChild(Rotor.Create(h, kc));
        // Le pylône est l'obstacle « pylone » du JSON (déjà construit avec les couverts) : ici seulement le lanceur.
        root.AddChild(new MeshInstance3D
        {
            Name = "Lanceur",
            Mesh = new SphereMesh { Radius = 0.35f, Height = 0.7f },
            Position = new Vector3(h.X, h.Launcher.Y, h.Z),
            MaterialOverride = Materials.Glow(Palette.BallCore, 1.4f),
        });
    }

    /// <summary>Murs et plafond invisibles. Mur « rebound » : la balle rebondit fort dessus (jeu en ricochets).</summary>
    static void AddBounds(Node3D root, MapData map)
    {
        var b = map.Bounds;
        float w = b.MaxX - b.MinX, d = b.MaxZ - b.MinZ, cx = (b.MaxX + b.MinX) / 2, cz = (b.MaxZ + b.MinZ) / 2;
        float ch = b.CeilingHeight;
        var mat = b.WallKind == "rebound" ? new PhysicsMaterial { Bounce = 0.8f, Friction = 0.2f } : null;
        AddWall(root, new Vector3(0.4f, ch, d + 1), new Vector3(b.MinX - 0.2f, ch / 2, cz), mat);
        AddWall(root, new Vector3(0.4f, ch, d + 1), new Vector3(b.MaxX + 0.2f, ch / 2, cz), mat);
        AddWall(root, new Vector3(w + 1, ch, 0.4f), new Vector3(cx, ch / 2, b.MinZ - 0.2f), mat);
        AddWall(root, new Vector3(w + 1, ch, 0.4f), new Vector3(cx, ch / 2, b.MaxZ + 0.2f), mat);
        AddWall(root, new Vector3(w + 1, 0.4f, d + 1), new Vector3(cx, ch + 0.2f, cz), mat);
    }

    static void AddWall(Node3D root, Vector3 size, Vector3 position, PhysicsMaterial? material = null)
    {
        var wall = new StaticBody3D { Position = position, PhysicsMaterialOverride = material };
        wall.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        root.AddChild(wall);
    }

    /// <summary>
    /// Obstacles du level design : couverts (gris), plots de rebond (gris clair, balle très rebondissante), sol de fosse.
    /// Le centre est en x et z, la base en y. Les bots ne les contournent pas (greybox).
    /// </summary>
    static void AddObstacle(Node3D root, ObstacleDef o, bool kc = false)
    {
        var size = new Vector3(o.Sx, o.Sy, o.Sz);
        var color = o.Kind switch { "bumper" => new Color("b7bcc8"), "pit" => new Color("14161b"), _ => new Color("7d838f") };
        var body = new StaticBody3D
        {
            Name = o.Id,
            Position = new Vector3(o.X, o.Y + o.Sy / 2f, o.Z),
            RotationDegrees = new Vector3(0, o.RotY, 0),
            PhysicsMaterialOverride = o.Kind == "bumper" ? new PhysicsMaterial { Bounce = 0.95f, Friction = 0.1f } : null,
        };
        Material look = kc
            ? (o.Kind == "bumper" ? KcShaders.Toon(new Color("3F8FB5"), new Color("245A75"), 6) : KcShaders.Toon(new Color("8C93AB"), new Color("757D97"), 2))
            : Materials.Flat(color);
        if (kc && look is ShaderMaterial sm && o.Kind != "pit") sm.NextPass = KcShaders.Outline(0.03f);
        body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = look, Visible = !(kc && o.Kind == "hubfloor") });
        body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        if (kc && (o.Kind == "bumper" || o.Kind == "cover" || o.Kind == null)) AddObstacleTrim(body, size, o.Kind == "bumper");
        root.AddChild(body);
    }

    /// <summary>Détails d'abri façon barrière de chantier : socle sombre, bande de danger, chapeau clair et liseré néon. Visuel seulement.</summary>
    static void AddObstacleTrim(Node3D body, Vector3 size, bool pylon)
    {
        void Piece(Vector3 sz, float y, ShaderMaterial m, bool outline = true)
        {
            if (outline) m.NextPass = KcShaders.Outline(0.02f);
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = sz }, Position = new Vector3(0, y - size.Y / 2f, 0), MaterialOverride = m });
        }
        const float pad = 0.06f;
        var wide = new Vector3(size.X + pad, 0, size.Z + pad);
        Piece(new Vector3(wide.X, 0.16f, wide.Z), 0.08f, KcShaders.Toon(new Color("3D4363"), new Color("2F3550"), 2));
        Piece(new Vector3(wide.X, 0.1f, wide.Z), size.Y - 0.05f, KcShaders.Toon(new Color("B4BAD2"), new Color("9AA1BE"), 2));
        float band = pylon ? 1.0f : Mathf.Min(0.7f, size.Y * 0.55f);
        Piece(new Vector3(size.X + 0.02f, 0.18f, size.Z + 0.02f), band, KcShaders.Toon(new Color("F2C230"), new Color("1A1C26"), 4, 1f), false);
        var glow = KcShaders.Toon(new Color("7FE8FF"), new Color("7FE8FF"), 0, 1f, 2.5f);
        Piece(new Vector3(size.X + 0.02f, 0.035f, size.Z + 0.02f), size.Y - 0.13f, glow, false);
        if (pylon) Piece(new Vector3(size.X + 0.02f, 0.035f, size.Z + 0.02f), 2.3f, KcShaders.Toon(new Color("7FE8FF"), new Color("7FE8FF"), 0, 1f, 2.5f), false);
    }

    /// <summary>Piédestal d'un distributeur de balles : cylindre dont le sommet est à la hauteur indiquée par la map.</summary>
    static void AddPedestal(Node3D root, BallSpawnDef def)
    {
        if (def.Kind != "dispenser") return;
        var body = new StaticBody3D { Name = def.Id, Position = new Vector3(def.X, def.Y / 2f, def.Z) };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.28f, BottomRadius = 0.32f, Height = def.Y },
            MaterialOverride = Materials.Flat(new Color("5b616d")),
        });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = 0.3f, Height = def.Y } });
        body.AddChild(new PickupBeacon { TopY = def.Y, Position = new Vector3(0, -def.Y / 2f, 0) });   // plot hexagonal, faisceau et anneau de recharge
        root.AddChild(body);
    }

    /// <summary>Prop de level design. Couleur avec alpha (« #rrggbbaa ») : voile de lumière additif, traversable si Solid est faux.</summary>
    static void AddProp(Node3D root, PropDef prop)
    {
        if (prop.Model != null)
        {
            var model = ModelLibrary.Instance("props/" + prop.Model);
            if (model == null) return;
            var holder = new StaticBody3D { Name = prop.Model, Position = new Vector3(prop.X, prop.Y, prop.Z), RotationDegrees = new Vector3(0, prop.RotY, 0), Scale = Vector3.One * prop.Scale };
            holder.AddChild(model);
            if (prop.Solid && Bounds(model, Transform3D.Identity) is Aabb box)
                holder.AddChild(new CollisionShape3D { Position = box.GetCenter(), Shape = new BoxShape3D { Size = box.Size } });
            root.AddChild(holder);
            return;
        }
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

    /// <summary>Boîte englobante des maillages d'un modèle, dans le repère du modèle.</summary>
    static Aabb? Bounds(Node node, Transform3D xf)
    {
        Aabb? box = null;
        if (node is Node3D n3 && node.GetParent() != null) xf *= n3.Transform;
        if (node is MeshInstance3D mi && mi.Mesh != null) box = xf * mi.Mesh.GetAabb();
        foreach (var child in node.GetChildren())
            if (Bounds(child, xf) is Aabb b) box = box is Aabb a ? a.Merge(b) : b;
        return box;
    }
}
