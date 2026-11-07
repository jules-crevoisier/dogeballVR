using System.Collections.Generic;
using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Maps;

/// <summary>
/// Construit une arène libre (layout « arena », cartes PC) : blocs pleins habillés selon leur rôle, rampes et escaliers,
/// tremplins, piédestaux de balles, liseré blanc au bord du vide, fond lointain et décor sans collision.
/// Les collisions des balles sont les boîtes de la géométrie ; celles des joueurs et des bots passent par
/// <see cref="ArenaGeometry"/> (mêmes chiffres).
/// </summary>
public static class ArenaLevelBuilder
{
    public static ArenaBuilder.Result Build(MapData map, bool pcProfile)
    {
        var arena = map.Arena!;
        var geo = map.Geometry!;
        var root = new Node3D { Name = "Arena" };
        var platforms = new Dictionary<int, Node3D>();
        foreach (var z in map.Zones)
        {
            var platform = new Node3D { Name = $"Platform{z.Id}" };
            root.AddChild(platform);
            platforms[z.Id] = platform;
        }
        ArenaBuilder.AddKcEnvironment(root, pcProfile, map.Ambiance, arena.FogScale);
        AddBelow(root, map, arena);
        foreach (var b in arena.Blocks) AddBlock(root, b);
        foreach (var r in arena.Ramps) AddRamp(root, r);
        foreach (var p in arena.Pads) AddPad(root, p);
        foreach (var d in arena.Decor) AddDecor(root, d);
        AddVoidEdges(root, arena, geo);
        foreach (var def in map.BallSpawns) AddPedestal(root, def, geo);
        foreach (var s in arena.Spawns) AddSpawnMark(root, s, geo);
        ArenaBuilder.AddBoundsPublic(root, map);
        return new ArenaBuilder.Result(root, platforms);
    }

    // ------------------------------------------------------------------ habillage par rôle

    /// <summary>Habillage d'un rôle : matériau du dessus, des flancs, et filet lumineux éventuel.</summary>
    record Look(Material Top, Material Side, Color? Trim);

    static Look LookOf(ArenaBlockDef b)
    {
        Color? custom = b.Color != null ? Color.FromString(b.Color, Colors.Gray) : null;
        ShaderMaterial T(string c1, string c2, int pattern, float scale = 1f) => KcShaders.Toon(custom ?? new Color(c1), custom?.Darkened(0.15f) ?? new Color(c2), pattern, scale);
        switch (b.Kind)
        {
            case "roof":
            case "floor":
                return new Look(T("5E6178", "50536A", 7), KcShaders.Toon(new Color("A8553F"), new Color("86412F"), 1, 0.5f), null);
            case "deck":
                return new Look(T("C9B79A", "B29F80", 3), T("8E95AD", "7A819A", 2), null);
            case "pool":
                return new Look(T("5FB8C8", "4E9FAF", 2), T("8FD3DD", "76BFCB", 2), null);
            case "bridge":
                return new Look(T("A9773F", "8A5C2E", 3), T("A9773F", "8A5C2E", 3), null);
            case "wall":
                return new Look(T("B5533C", "8E3F30", 1, 0.5f), T("B5533C", "8E3F30", 1, 0.5f), null);
            case "parapet":
                return new Look(T("B8BDD0", "A2A8BE", 2), T("8F95AE", "7A809A", 2), null);
            case "crate":
                return new Look(T("B07A45", "8A5A30", 3), T("B07A45", "8A5A30", 3), null);
            case "container":
                return new Look(T("2F6F8F", "245A75", 6), T("2F6F8F", "245A75", 6), new Color("7FE8FF"));
            case "tower":
                return new Look(T("5A6180", "4A5170", 2), T("5A6180", "4A5170", 2), new Color("7FE8FF"));
            case "stall":
                return new Look(T("E36A2E", "C2551F", 0), T("8E95AD", "7A819A", 2), null);
            case "low":
            case "cover":
            default:
                return new Look(T("B4BAD2", "9AA1BE", 2), T("8C93AB", "757D97", 2), new Color("7FE8FF"));
        }
    }

    static void AddBlock(Node3D root, ArenaBlockDef b)
    {
        float h = b.Y1 - b.Y0;
        if (h <= 0.001f) return;
        var size = new Vector3(b.Sx, h, b.Sz);
        var body = new StaticBody3D
        {
            Name = string.IsNullOrEmpty(b.Id) ? $"Bloc_{b.Kind}" : b.Id,
            Position = new Vector3(b.X, (b.Y0 + b.Y1) / 2f, b.Z),
            RotationDegrees = new Vector3(0, b.RotY, 0),
        };
        body.AddToGroup(ArenaGroup);
        if (!b.BallsPass) body.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size } });
        var look = LookOf(b);
        var model = b.Model != null ? ModelLibrary.Instance("props/" + b.Model) ?? ModelLibrary.Instance(b.Model) : null;
        if (model != null)
        {
            FitModel(model, size);
            body.AddChild(model);
        }
        else if (b.Kind is "roof" or "floor" or "deck" && h > 0.6f)
        {
            // Toit : dalle de dessus (asphalte, planches) sur une façade (brique, béton) qu'on voit depuis les ponts.
            const float cap = 0.25f;
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(b.Sx, cap, b.Sz) }, Position = new Vector3(0, h / 2f - cap / 2f, 0), MaterialOverride = look.Top });
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(b.Sx - 0.02f, h - cap, b.Sz - 0.02f) }, Position = new Vector3(0, -cap / 2f, 0), MaterialOverride = look.Side });
        }
        else
        {
            body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = look.Side });
            if (b.Kind is "cover" or "low" or "parapet" or "container" or "tower")
                body.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(b.Sx + 0.06f, 0.1f, b.Sz + 0.06f) }, Position = new Vector3(0, h / 2f - 0.05f, 0), MaterialOverride = look.Top });
        }
        if (look.Trim is Color trim && model == null && h > 0.5f)
            body.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(b.Sx + 0.03f, 0.04f, b.Sz + 0.03f) }, Position = new Vector3(0, h / 2f - 0.16f, 0),
                MaterialOverride = KcShaders.Toon(trim, trim, 0, 1f, 2.5f), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        root.AddChild(body);
    }

    /// <summary>Groupe des solides d'une arène libre (la caméra d'épaule les évite, les bots s'en servent pour la ligne de tir).</summary>
    public const string ArenaGroup = "arena_solid";

    /// <summary>Étire un modèle pour qu'il remplisse la boîte de collision (même encombrement que ce qu'on heurte).</summary>
    static void FitModel(Node3D model, Vector3 size)
    {
        if (ArenaBuilder.ModelBounds(model) is not Aabb box || box.Size.X < 0.01f || box.Size.Y < 0.01f || box.Size.Z < 0.01f) return;
        var scale = new Vector3(size.X / box.Size.X, size.Y / box.Size.Y, size.Z / box.Size.Z);
        model.Scale = scale;
        var c = box.GetCenter() * scale;
        model.Position = -c;
    }

    static void AddRamp(Node3D root, ArenaRampDef r)
    {
        float lo = Mathf.Min(r.H0, r.H1), hi = Mathf.Max(r.H0, r.H1);
        bool alongX = r.Dir is "+x" or "-x";
        float run = alongX ? r.Sx : r.Sz, width = alongX ? r.Sz : r.Sx;
        bool upPositive = (r.Dir is "+x" or "+z") == (r.H1 >= r.H0);
        var mat = KcShaders.Toon(new Color("8E95AD"), new Color("7A819A"), 2);
        var holder = new StaticBody3D { Name = string.IsNullOrEmpty(r.Id) ? "Rampe" : r.Id, Position = new Vector3(r.X, 0, r.Z) };
        holder.AddToGroup(ArenaGroup);
        if (r.Stairs)
        {
            int steps = Mathf.Max(2, Mathf.RoundToInt((hi - lo) / 0.3f));
            float stepRun = run / steps;
            for (int k = 0; k < steps; k++)
            {
                float top = lo + (hi - lo) * (k + 1) / steps;
                float along = -run / 2f + stepRun * (k + 0.5f);
                if (!upPositive) along = -along;
                var size = alongX ? new Vector3(stepRun, top - lo + 0.3f, width) : new Vector3(width, top - lo + 0.3f, stepRun);
                var pos = alongX ? new Vector3(along, (top + lo - 0.3f) / 2f, 0) : new Vector3(0, (top + lo - 0.3f) / 2f, along);
                holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = pos, MaterialOverride = k % 2 == 0 ? mat : KcShaders.Toon(new Color("A2A8BE"), new Color("8E95AD"), 2) });
                holder.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = pos });
            }
        }
        else
        {
            // Plan incliné : une boîte tournée (dessus = la pente), posée sur un socle pour ne pas laisser de jour dessous.
            float len = Mathf.Sqrt(run * run + (hi - lo) * (hi - lo));
            float angle = Mathf.Atan2(hi - lo, run) * (upPositive ? 1f : -1f);
            const float thick = 0.3f;
            var slab = new Node3D { Position = new Vector3(0, (lo + hi) / 2f - thick / 2f * Mathf.Cos(angle), 0) };
            if (alongX) slab.Rotation = new Vector3(0, 0, angle);
            else slab.Rotation = new Vector3(-angle, 0, 0);
            var size = alongX ? new Vector3(len, thick, width) : new Vector3(width, thick, len);
            slab.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, MaterialOverride = mat });
            slab.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = alongX ? new Vector3(len, 0.02f, 0.12f) : new Vector3(0.12f, 0.02f, len) }, Position = new Vector3(0, thick / 2f + 0.01f, 0),
                MaterialOverride = KcShaders.Toon(new Color("F2C230"), new Color("1A1C26"), 4, 1f),
            });
            holder.AddChild(new CollisionShape3D { Shape = new BoxShape3D { Size = size }, Position = slab.Position, Rotation = slab.Rotation });
            holder.AddChild(slab);
        }
        root.AddChild(holder);
    }

    static void AddPad(Node3D root, ArenaPadDef p)
    {
        var holder = new Node3D { Name = string.IsNullOrEmpty(p.Id) ? "Tremplin" : p.Id, Position = new Vector3(p.X, p.Y, p.Z) };
        holder.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = p.Radius, BottomRadius = p.Radius + 0.08f, Height = 0.12f, RadialSegments = 24 },
            Position = new Vector3(0, 0.06f, 0), MaterialOverride = KcShaders.Toon(new Color("2A2C38"), new Color("1A1C26"), 0),
        });
        holder.AddChild(new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = p.Radius * 0.62f, OuterRadius = p.Radius * 0.8f, Rings = 24, RingSegments = 6 },
            Position = new Vector3(0, 0.13f, 0), MaterialOverride = Materials.Glow(new Color("F2F4F8"), 1.6f),
        });
        // Flèche au sol vers la cible : on sait où le tremplin envoie.
        var dir = new Vector2(p.Tx - p.X, p.Tz - p.Z);
        if (dir.LengthSquared() > 0.01f)
        {
            var arrow = new MeshInstance3D
            {
                Mesh = new PrismMesh { Size = new Vector3(0.5f, 0.6f, 0.04f) },
                Position = new Vector3(0, 0.14f, 0),
                Rotation = new Vector3(-Mathf.Pi / 2f, Mathf.Atan2(-dir.X, -dir.Y) + Mathf.Pi, 0),
                MaterialOverride = Materials.Glow(new Color("F2F4F8"), 1.6f),
            };
            holder.AddChild(arrow);
        }
        root.AddChild(holder);
    }

    static void AddDecor(Node3D root, ArenaDecorDef d)
    {
        if (d.Model != null)
        {
            var model = ModelLibrary.Instance(d.Model.Contains('/') ? d.Model : "props/" + d.Model) ?? ModelLibrary.Instance(d.Model);
            if (model == null) return;
            model.Position = new Vector3(d.X, d.Y, d.Z);
            model.RotationDegrees = new Vector3(0, d.RotY, 0);
            model.Scale = Vector3.One * d.Scale;
            root.AddChild(model);
            return;
        }
        var color = Color.FromString(d.Color, Colors.Gray);
        root.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(d.Sx, d.Sy, d.Sz) },
            Position = new Vector3(d.X, d.Y + d.Sy / 2f, d.Z), RotationDegrees = new Vector3(0, d.RotY, 0),
            MaterialOverride = d.Glow > 0f ? KcShaders.Toon(color, color, 0, 1f, d.Glow)
                : KcShaders.Toon(color, d.Color2 != null ? Color.FromString(d.Color2, color) : color.Darkened(0.12f), d.Pattern),
            CastShadow = d.Glow > 0f ? GeometryInstance3D.ShadowCastingSetting.Off : GeometryInstance3D.ShadowCastingSetting.On,
        });
    }

    /// <summary>Fond sous le vide (rue, eau) : grand plan sans collision, loin en dessous.</summary>
    static void AddBelow(Node3D root, MapData map, ArenaDef arena)
    {
        var b = map.Bounds;
        float w = b.MaxX - b.MinX + 120f, d = b.MaxZ - b.MinZ + 120f;
        root.AddChild(new MeshInstance3D
        {
            Name = "Fond",
            Mesh = new PlaneMesh { Size = new Vector2(w, d) },
            Position = new Vector3((b.MinX + b.MaxX) / 2f, arena.FloorBelowY, (b.MinZ + b.MaxZ) / 2f),
            MaterialOverride = KcShaders.Toon(new Color(arena.FloorBelowColor), new Color(arena.FloorBelowColor).Darkened(0.2f), 7, 2f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }

    /// <summary>
    /// Liseré blanc au bord du vide (règle de la bible : blanc = limite) : sur chaque bord de bloc praticable, les tronçons
    /// d'un mètre dont l'extérieur est du vide reçoivent une bande lumineuse. On voit d'un coup d'œil où l'on peut tomber.
    /// </summary>
    static void AddVoidEdges(Node3D root, ArenaDef arena, ArenaGeometry geo)
    {
        var mat = KcShaders.Toon(new Color("F2F4F8"), new Color("F2F4F8"), 0, 1f, 1.4f);
        var holder = new Node3D { Name = "BordsDuVide" };
        foreach (var b in arena.Blocks)
        {
            if (b.Kind is not ("roof" or "floor" or "bridge" or "deck") || b.RotY != 0f) continue;
            float hx = b.Sx / 2f, hz = b.Sz / 2f;
            void Edge(Vector2 a, Vector2 c, Vector2 outward)
            {
                float len = a.DistanceTo(c);
                int n = Mathf.Max(1, Mathf.RoundToInt(len));
                for (int k = 0; k < n; k++)
                {
                    var p0 = a.Lerp(c, k / (float)n); var p1 = a.Lerp(c, (k + 1) / (float)n);
                    var mid = (p0 + p1) / 2f + outward * 0.3f;
                    if (!geo.IsVoid(mid.X, mid.Y, 0f)) continue;
                    if (geo.HighestAt(((p0 + p1) / 2f - outward * 0.1f).X, ((p0 + p1) / 2f - outward * 0.1f).Y, 0f) > b.Y1 + 0.05f) continue;   // un mur plus haut borde déjà
                    var seg = p1 - p0;
                    var size = Mathf.Abs(seg.X) > Mathf.Abs(seg.Y) ? new Vector3(seg.Length(), 0.03f, 0.14f) : new Vector3(0.14f, 0.03f, seg.Length());
                    var at = (p0 + p1) / 2f - outward * 0.09f;
                    holder.AddChild(new MeshInstance3D { Mesh = new BoxMesh { Size = size }, Position = new Vector3(at.X, b.Y1 + 0.015f, at.Y), MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off });
                }
            }
            Edge(new Vector2(b.X - hx, b.Z - hz), new Vector2(b.X + hx, b.Z - hz), new Vector2(0, -1));
            Edge(new Vector2(b.X - hx, b.Z + hz), new Vector2(b.X + hx, b.Z + hz), new Vector2(0, 1));
            Edge(new Vector2(b.X - hx, b.Z - hz), new Vector2(b.X - hx, b.Z + hz), new Vector2(-1, 0));
            Edge(new Vector2(b.X + hx, b.Z - hz), new Vector2(b.X + hx, b.Z + hz), new Vector2(1, 0));
        }
        root.AddChild(holder);
    }

    /// <summary>
    /// Piédestal de balles neutre (« pedestal », ou « dispenser » d'une carte en arène) : socle posé sur le sol réel,
    /// sommet à la hauteur Y de la carte, faisceau turquoise. Socle de balle spéciale (« special ») : plus large et plus bas,
    /// cerclé de rose, faisceau rose quand la balle y attend.
    /// </summary>
    static void AddPedestal(Node3D root, BallSpawnDef def, ArenaGeometry geo)
    {
        if (def.Kind is not ("pedestal" or "dispenser" or "special")) return;
        bool special = def.Kind == "special";
        float ground = geo.HighestAt(def.X, def.Z, 0f);
        if (float.IsNegativeInfinity(ground)) ground = 0f;
        float h = Mathf.Max(0.2f, def.Y - ground);
        float top = special ? 0.5f : 0.28f, bottom = special ? 0.62f : 0.34f;
        var body = new StaticBody3D { Name = def.Id, Position = new Vector3(def.X, ground + h / 2f, def.Z) };
        body.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = top, BottomRadius = bottom, Height = h, RadialSegments = special ? 6 : 16 },
            MaterialOverride = special ? KcShaders.Toon(new Color("4a2f45"), new Color("3a2338"), 2) : KcShaders.Toon(new Color("3D4363"), new Color("2F3550"), 2),
        });
        body.AddChild(new CollisionShape3D { Shape = new CylinderShape3D { Radius = special ? 0.55f : 0.3f, Height = h } });
        if (special)
            body.AddChild(new MeshInstance3D
            {
                Name = "Liseré",
                Mesh = new TorusMesh { InnerRadius = top - 0.05f, OuterRadius = top + 0.02f, Rings = 6, RingSegments = 4 },
                Position = new Vector3(0, h / 2f + 0.01f, 0), Scale = new Vector3(1, 0.3f, 1),
                MaterialOverride = Materials.Flat(Palette.Special), CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            });
        body.AddChild(new PickupBeacon { TopY = h, Position = new Vector3(0, -h / 2f, 0), Tint = special ? Palette.Special : Palette.Beacon });
        root.AddChild(body);
    }

    /// <summary>Marque d'apparition : plaque peinte à la couleur de l'équipe, qui dit « ici c'est chez nous ».</summary>
    static void AddSpawnMark(Node3D root, ArenaSpawnDef s, ArenaGeometry geo)
    {
        float ground = geo.HighestAt(s.X, s.Z, 0f);
        if (float.IsNegativeInfinity(ground)) return;
        var color = Palette.Team(s.Team);
        root.AddChild(new MeshInstance3D
        {
            Name = "Apparition",
            Mesh = new CylinderMesh { TopRadius = 0.7f, BottomRadius = 0.7f, Height = 0.02f, RadialSegments = 6, Rings = 1 },
            Position = new Vector3(s.X, ground + 0.012f, s.Z),
            MaterialOverride = KcShaders.Toon(color.Darkened(0.1f), color.Darkened(0.3f), 0, 1f, 0.4f),
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        });
    }
}
