using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Couche « bande dessinée » posée sur les impacts d'ImpactFx, dans l'esprit de Knockout City :
/// touche = étoile d'impact dentelée (couleur du tireur, cœur blanc) + traits de vitesse qui partent du point d'impact ;
/// KO = étoile double plus grande, traits plus longs, onde de choc au sol, bouffée de poussière et colonne de lumière qui monte.
/// Appelé à la fin d'ImpactFx.Hit et Ko (mêmes garde-fous : pas en sans affichage, pas collé à la caméra PC).
/// --sans-impact-bd coupe cette couche (images « avant » de la vitrine).
/// </summary>
public static class ImpactExtras
{
    static readonly bool Off = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--sans-impact-bd") >= 0;
    static ArrayMesh? burstMesh, streakMesh;
    static GradientTexture2D? dustTex, pillarTex;

    public static void Hit(Node context, Vector3 pos, Color shooter)
    {
        if (Off) return;
        float k = Scale(context, pos);
        Burst(context, pos, shooter, 0.55f * k, 0.14f, 0f);
        Streaks(context, pos, shooter, 1.3f * k, 0.2f);
    }

    public static void Ko(Node context, Vector3 pos, Color shooter)
    {
        if (Off) return;
        float k = Scale(context, pos);
        Burst(context, pos, Colors.White, 1.0f * k, 0.18f, 0.3f);
        Burst(context, pos, shooter, 0.8f * k, 0.18f, 0f);
        Streaks(context, pos, shooter, 2.2f * k, 0.28f);
        var ground = Ground(context, pos);
        Shockwave(context, ground, shooter, 1.9f * k);
        Dust(context, ground, k);
        Pillar(context, ground, shooter, k);
    }

    /// <summary>Même grossissement qu'ImpactFx au-delà de 9 m, pour que les deux couches restent calées.</summary>
    static float Scale(Node context, Vector3 pos)
    {
        var cam = context.GetViewport()?.GetCamera3D();
        if (cam == null || cam is XRCamera3D) return 1f;
        return Mathf.Clamp(cam.GlobalPosition.DistanceTo(pos) / 9f, 1f, 2.6f);
    }

    /// <summary>Sol sous le point d'impact (rayon de 3 m vers le bas), sinon 1,1 m plus bas.</summary>
    static Vector3 Ground(Node context, Vector3 pos)
    {
        var space = context.GetViewport()?.World3D?.DirectSpaceState;
        var hit = space?.IntersectRay(PhysicsRayQueryParameters3D.Create(pos, pos + Vector3.Down * 3f));
        return hit != null && hit.Count > 0 ? (Vector3)hit["position"] + Vector3.Up * 0.03f : pos + Vector3.Down * 1.1f;
    }

    static void Add(Node context, Node3D n, Vector3 pos, double life)
    {
        context.GetTree().CurrentScene.AddChild(n);
        n.GlobalPosition = pos;
        if (n is CpuParticles3D p) p.Restart();
        n.GetTree().CreateTimer(life, false).Timeout += n.QueueFree;
    }

    static StandardMaterial3D Unlit(Color c, bool billboard, bool additive = false, Texture2D? tex = null) => new()
    {
        AlbedoColor = c, AlbedoTexture = tex, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        BlendMode = additive ? BaseMaterial3D.BlendModeEnum.Add : BaseMaterial3D.BlendModeEnum.Mix,
        BillboardMode = billboard ? BaseMaterial3D.BillboardModeEnum.Enabled : BaseMaterial3D.BillboardModeEnum.Disabled, BillboardKeepScale = true,
    };

    /// <summary>Étoile d'impact dentelée face caméra : jaillit en 0,05 s, tient, puis s'efface ; derrière l'éclat blanc d'ImpactFx.</summary>
    internal static void Burst(Node context, Vector3 pos, Color color, float size, float duration, float spin)
    {
        if (UI.GameSettings.ReduceFlashes) size *= 0.6f;
        burstMesh ??= BurstMesh();
        var mat = Unlit(Color.FromHsv(color.H, Mathf.Min(1f, color.S * 1.2f), Mathf.Max(color.V, 0.95f)), true);
        mat.NoDepthTest = true;
        mat.RenderPriority = 1;
        mat.VertexColorUseAsAlbedo = true;
        var q = new MeshInstance3D { Mesh = burstMesh, MaterialOverride = mat, Scale = Vector3.One * size * 0.3f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Add(context, q, pos, duration + 0.1);
        q.RotateObjectLocal(Vector3.Back, GD.Randf() * Mathf.Tau);
        var tw = q.CreateTween();
        tw.TweenProperty(q, "scale", Vector3.One * size, 0.05).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Back);
        tw.Parallel().TweenMethod(Callable.From<float>(a => q.RotateObjectLocal(Vector3.Back, a)), 0f, spin, duration);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, duration - 0.05).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
        tw.Parallel().TweenProperty(q, "scale", Vector3.One * size * 1.15f, duration - 0.05);
    }

    /// <summary>Traits de vitesse rayonnants (plan face caméra) qui s'écartent et s'amincissent.</summary>
    internal static void Streaks(Node context, Vector3 pos, Color color, float size, float duration)
    {
        streakMesh ??= StreakMesh();
        var mat = Unlit(color.Lerp(Colors.White, 0.55f), true);
        mat.NoDepthTest = true;
        mat.RenderPriority = 1;
        var q = new MeshInstance3D { Mesh = streakMesh, MaterialOverride = mat, Scale = Vector3.One * size * 0.45f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Add(context, q, pos, duration + 0.1);
        q.RotateObjectLocal(Vector3.Back, GD.Randf() * Mathf.Tau);
        var tw = q.CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * size, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
    }

    /// <summary>KO : anneau plat posé au sol qui s'ouvre vite.</summary>
    static void Shockwave(Node context, Vector3 ground, Color color, float size)
    {
        var mat = Unlit(new Color(color.Lerp(Colors.White, 0.2f), 0.75f), false, true);
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.86f, OuterRadius = 1f, Rings = 48, RingSegments = 4 },
            MaterialOverride = mat, Scale = new Vector3(0.15f, 0.04f, 0.15f) * size, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Add(context, ring, ground, 0.6);
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "scale", new Vector3(1f, 0.02f, 1f) * size, 0.45).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, 0.45).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
    }

    /// <summary>KO : bouffée de poussière claire au ras du sol, qui s'étale et retombe lentement.</summary>
    static void Dust(Node context, Vector3 ground, float k)
    {
        dustTex ??= Radial(new[] { 0f, 0.5f, 1f }, new[] { new Color(1, 1, 1, 0.75f), new Color(1, 1, 1, 0.4f), new Color(1, 1, 1, 0) });
        var mat = Unlit(new Color("e8e2d8"), false, false, dustTex);
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        mat.VertexColorUseAsAlbedo = true;
        var grow = new Curve();
        grow.AddPoint(new Vector2(0, 0.4f));
        grow.AddPoint(new Vector2(1, 1.4f));
        var fade = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(1, 1, 1, 0.85f), new Color(1, 1, 1, 0) } };
        var p = new CpuParticles3D
        {
            Amount = 14, Lifetime = 0.8, OneShot = true, Explosiveness = 1f, Emitting = true,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Ring, EmissionRingAxis = Vector3.Up, EmissionRingRadius = 0.35f * k, EmissionRingInnerRadius = 0.2f * k, EmissionRingHeight = 0.05f,
            Direction = Vector3.Up, Spread = 85f, Flatness = 0.85f, InitialVelocityMin = 2f * k, InitialVelocityMax = 3.5f * k, DampingMin = 4f * k, DampingMax = 6f * k,
            Gravity = new Vector3(0, 0.3f, 0), ScaleAmountMin = 0.9f * k, ScaleAmountMax = 1.5f * k, ScaleAmountCurve = grow, ColorRamp = fade,
            Mesh = new QuadMesh { Size = Vector2.One * 0.5f }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Add(context, p, ground, 1.2);
    }

    /// <summary>KO : colonne de lumière à la couleur du tireur qui jaillit du sol et s'efface en s'amincissant.</summary>
    static void Pillar(Node context, Vector3 ground, Color color, float k)
    {
        pillarTex ??= new GradientTexture2D
        {
            Gradient = new Gradient { Offsets = new[] { 0f, 0.15f, 1f }, Colors = new[] { new Color(1, 1, 1, 0), new Color(1, 1, 1, 1), new Color(1, 1, 1, 0) } },
            FillFrom = new Vector2(0, 1), FillTo = new Vector2(0, 0), Width = 4, Height = 64,
        };
        var mat = Unlit(new Color(color.Lerp(Colors.White, 0.15f), 0.6f), false, true, pillarTex);
        float h = 4.5f * k;
        var col = new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.5f, BottomRadius = 0.5f, Height = h, CapTop = false, CapBottom = false, RadialSegments = 20, Rings = 1 },
            MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Scale = new Vector3(0.9f * k, 0.05f, 0.9f * k), Position = Vector3.Up * h * 0.025f,   // part écrasée au sol
        };
        var holder = new Node3D();
        holder.AddChild(col);
        Add(context, holder, ground, 0.8);
        var tw = col.CreateTween();
        tw.TweenProperty(col, "scale", new Vector3(0.5f * k, 1f, 0.5f * k), 0.12).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        tw.Parallel().TweenProperty(col, "position", Vector3.Up * h * 0.5f, 0.12);
        tw.TweenProperty(col, "scale", new Vector3(0.05f, 1.1f, 0.05f) * new Vector3(k, 1, k), 0.5).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Cubic);
        tw.Parallel().TweenProperty(mat, "albedo_color:a", 0.0, 0.5).SetEase(Tween.EaseType.In);
    }

    /// <summary>Étoile de BD à 12 pointes irrégulières ; couleur de sommet blanche surexposée au centre : cœur blanc, bord à la couleur du matériau.</summary>
    static ArrayMesh BurstMesh()
    {
        const int n = 24;
        var rng = new RandomNumberGenerator { Seed = 7 };
        var verts = new Vector3[n + 1];
        var cols = new Color[n + 1];
        cols[0] = new Color(2.2f, 2.2f, 2.2f, 1);
        for (int i = 0; i < n; i++)
        {
            float a = i * Mathf.Tau / n + rng.RandfRange(-0.08f, 0.08f);
            float r = i % 2 == 0 ? rng.RandfRange(0.42f, 0.5f) : rng.RandfRange(0.2f, 0.26f);
            verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
            cols[i + 1] = Colors.White;
        }
        var idx = new int[n * 3];
        for (int i = 0; i < n; i++) { idx[i * 3] = 0; idx[i * 3 + 1] = i + 1; idx[i * 3 + 2] = (i + 1) % n + 1; }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Color] = cols;
        arrays[(int)Mesh.ArrayType.Index] = idx;
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return m;
    }

    /// <summary>Dix traits effilés rayonnants, longueurs et angles un peu irréguliers, qui laissent un trou au centre.</summary>
    static ArrayMesh StreakMesh()
    {
        var rng = new RandomNumberGenerator { Seed = 11 };
        var st = new SurfaceTool();
        st.Begin(Mesh.PrimitiveType.Triangles);
        for (int i = 0; i < 10; i++)
        {
            float a = i * Mathf.Tau / 10 + rng.RandfRange(-0.15f, 0.15f);
            var dir = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
            var side = new Vector3(-dir.Y, dir.X, 0);
            float r0 = rng.RandfRange(0.32f, 0.4f), r1 = rng.RandfRange(0.62f, 0.75f), w = rng.RandfRange(0.018f, 0.03f);
            st.AddVertex(dir * r0);
            st.AddVertex(dir * (r0 + 0.08f) + side * w);
            st.AddVertex(dir * r1);
            st.AddVertex(dir * r0);
            st.AddVertex(dir * r1);
            st.AddVertex(dir * (r0 + 0.08f) - side * w);
        }
        return st.Commit();
    }

    static GradientTexture2D Radial(float[] offsets, Color[] colors) => new()
    {
        Gradient = new Gradient { Offsets = offsets, Colors = colors }, Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
    };
}
