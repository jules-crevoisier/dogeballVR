using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Impacts de touche, visibles par tous les joueurs (pas seulement la victime) :
/// touche = gerbe d'étincelles à la couleur de l'équipe du tireur + éclat blanc bref + anneau de choc ;
/// KO = la même chose en plus gros, avec étoiles et confettis.
/// Éclat et anneau ignorent la profondeur (la balle frappe le corps : on ne veut pas que l'avatar les masque à moitié).
/// Rien n'est créé en mode sans affichage (simulations de CI), ni collé à la caméra PC (joueur touché en vue subjective) ;
/// les particules s'effacent en approchant de la caméra.
/// </summary>
public static class ImpactFx
{
    static GradientTexture2D? flashTex, ringTex;
    static ArrayMesh? starMesh;

    /// <summary>
    /// En deçà de cette distance de la caméra (joueur PC touché en vue subjective), pas d'effet 3D : une étincelle devant le plan proche
    /// devient un grand polygone plat qui traverse l'écran, et l'éclat sans test de profondeur blanchit tout. Le HUD montre le coup.
    /// </summary>
    const float MinCameraDistance = 1.8f;

    static bool TooClose(Node context, Vector3 pos)
    {
        var cam = context.GetViewport()?.GetCamera3D();
        return cam != null && cam is not XRCamera3D && cam.GlobalPosition.DistanceTo(pos) < MinCameraDistance;
    }

    public static void Hit(Node context, Vector3 pos, Color shooterColor)
    {
        if (!Usable(context) || TooClose(context, pos)) return;
        float k = DistanceScale(context, pos);
        Sparks(context, pos, shooterColor, 16, 9f, 0.32f, k);
        Flash(context, pos, 0.9f * k, 0.11f);
        Ring(context, pos, shooterColor, 1.6f * k, 0.28f);
    }

    public static void Ko(Node context, Vector3 pos, Color shooterColor)
    {
        if (!Usable(context) || TooClose(context, pos)) return;
        float k = DistanceScale(context, pos);
        Sparks(context, pos, shooterColor, 26, 12f, 0.42f, k);
        Flash(context, pos, 1.6f * k, 0.16f);
        Ring(context, pos, shooterColor, 2.8f * k, 0.4f);
        Ring(context, pos, Colors.White, 1.8f * k, 0.3f);
        Stars(context, pos, k);
        Confetti(context, pos, shooterColor, k);
    }

    /// <summary>Au-delà de 9 m, l'effet grossit avec la distance (jusqu'à ×2,6) : une touche lointaine reste lisible à l'écran.</summary>
    static float DistanceScale(Node context, Vector3 pos)
    {
        var cam = context.GetViewport()?.GetCamera3D();
        if (cam == null || cam is XRCamera3D) return 1f;
        return Mathf.Clamp(cam.GlobalPosition.DistanceTo(pos) / 9f, 1f, 2.6f);
    }

    static bool Usable(Node context) =>
        GodotObject.IsInstanceValid(context) && context.IsInsideTree() && DisplayServer.GetName() != "headless" && context.GetTree().CurrentScene != null;

    static void AddOneShot(Node context, Node3D n, Vector3 pos, double life)
    {
        context.GetTree().CurrentScene.AddChild(n);
        n.GlobalPosition = pos;
        if (n is CpuParticles3D p) p.Restart();   // Godot 4.3 : « Emitting = true » posé hors de l'arbre ne déclenche rien
        n.GetTree().CreateTimer(life, false).Timeout += n.QueueFree;
    }

    /// <summary>Étincelles allongées dans le sens de leur vitesse, freinées, qui rétrécissent.</summary>
    static void Sparks(Node context, Vector3 pos, Color color, int amount, float speed, float length, float k)
    {
        var mat = new StandardMaterial3D
        {
            // Plus saturé que la couleur d'équipe : le bleu clair se perdait sur le ciel.
            AlbedoColor = Color.FromHsv(color.H, Mathf.Min(1f, color.S * 1.35f), color.V), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            VertexColorUseAsAlbedo = false, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        NearFade(mat);
        var scale = new Curve();
        scale.AddPoint(new Vector2(0, 1f));
        scale.AddPoint(new Vector2(0.6f, 0.75f));
        scale.AddPoint(new Vector2(1, 0f));
        var p = new CpuParticles3D
        {
            Amount = amount, Lifetime = 0.38, OneShot = true, Explosiveness = 1f, Emitting = true,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.1f * k,
            Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = speed * 0.55f * k, InitialVelocityMax = speed * k,
            Gravity = new Vector3(0, -7f * k, 0), DampingMin = 6f * k, DampingMax = 10f * k, ScaleAmountMin = 0.7f * k, ScaleAmountMax = 1.25f * k,
            ScaleAmountCurve = scale, ParticleFlagAlignY = true,
            Mesh = new BoxMesh { Size = new Vector3(0.05f, length, 0.05f) }, MaterialOverride = mat,
        };
        AddOneShot(context, p, pos, 0.8);
    }

    /// <summary>Éclat blanc : disque doux face caméra, 0,1 à 0,16 s.</summary>
    static void Flash(Node context, Vector3 pos, float size, float duration)
    {
        flashTex ??= Radial(new[] { 0f, 0.35f, 1f }, new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.85f), new Color(1, 1, 1, 0) });
        var mat = Billboard(flashTex, Colors.White);
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = mat, Scale = Vector3.One * size * 0.45f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddOneShot(context, q, pos, duration + 0.1);
        var tw = q.CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * size, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
    }

    /// <summary>Anneau de choc face caméra qui s'ouvre et s'efface.</summary>
    static void Ring(Node context, Vector3 pos, Color color, float size, float duration)
    {
        ringTex ??= Radial(new[] { 0f, 0.72f, 0.82f, 0.9f, 1f },
            new[] { new Color(1, 1, 1, 0), new Color(1, 1, 1, 0), new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0) });
        var mat = Billboard(ringTex, color.Lerp(Colors.White, 0.25f));
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One }, MaterialOverride = mat, Scale = Vector3.One * size * 0.2f, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        AddOneShot(context, q, pos, duration + 0.1);
        var tw = q.CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * size, duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
    }

    /// <summary>KO : étoiles jaunes et blanches qui jaillissent, tournent et retombent.</summary>
    static void Stars(Node context, Vector3 pos, float k)
    {
        starMesh ??= Star(0.16f, 0.07f);
        var colors = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Constant };
        colors.SetColor(0, new Color("fff35a"));
        colors.SetColor(1, Colors.White);
        colors.SetOffset(1, 0.6f);
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled, NoDepthTest = false,
        };
        NearFade(mat);
        var p = new CpuParticles3D
        {
            Amount = 9, Lifetime = 0.9, OneShot = true, Explosiveness = 1f, Emitting = true,
            Direction = Vector3.Up, Spread = 70f, InitialVelocityMin = 4f * k, InitialVelocityMax = 7f * k, Gravity = new Vector3(0, -9f * k, 0),
            AngleMin = 0f, AngleMax = 360f, AngularVelocityMin = -540f, AngularVelocityMax = 540f, ScaleAmountMin = 0.8f * k, ScaleAmountMax = 1.4f * k,
            ColorInitialRamp = colors, Mesh = starMesh, MaterialOverride = mat,
        };
        AddOneShot(context, p, pos, 1.3);
    }

    /// <summary>KO : confettis multicolores (équipe du tireur, blanc, jaune, rose) qui flottent en retombant.</summary>
    static void Confetti(Node context, Vector3 pos, Color team, float k)
    {
        var colors = new Gradient { InterpolationMode = Gradient.InterpolationModeEnum.Constant };
        colors.SetColor(0, team);
        colors.SetColor(1, Colors.White);
        colors.SetOffset(1, 0.35f);
        colors.AddPoint(0.6f, new Color("ffe04a"));
        colors.AddPoint(0.8f, new Color("ff5fa8"));
        var mat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        NearFade(mat);
        var scale = new Curve();
        scale.AddPoint(new Vector2(0, 1f));
        scale.AddPoint(new Vector2(0.75f, 1f));
        scale.AddPoint(new Vector2(1, 0f));
        var p = new CpuParticles3D
        {
            Amount = 44, Lifetime = 1.5, OneShot = true, Explosiveness = 1f, Emitting = true,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.2f * k,
            Direction = Vector3.Up, Spread = 80f, InitialVelocityMin = 3.5f * k, InitialVelocityMax = 7.5f * k, Gravity = new Vector3(0, -4f * k, 0),
            DampingMin = 2.5f * k, DampingMax = 4f * k, AngleMin = 0f, AngleMax = 360f, AngularVelocityMin = -720f, AngularVelocityMax = 720f,
            ScaleAmountMin = 0.7f * k, ScaleAmountMax = 1.3f * k, ScaleAmountCurve = scale, ColorInitialRamp = colors,
            Mesh = new QuadMesh { Size = new Vector2(0.13f, 0.07f) }, MaterialOverride = mat,
        };
        AddOneShot(context, p, pos, 1.9);
    }

    /// <summary>Particule qui s'efface (tramée, sans tri de transparence) à moins de 2,5 m de la caméra : rien ne vient boucher l'écran.</summary>
    static void NearFade(StandardMaterial3D mat)
    {
        mat.DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelDither;
        mat.DistanceFadeMinDistance = 1.0f;
        mat.DistanceFadeMaxDistance = 2.5f;
    }

    static StandardMaterial3D Billboard(Texture2D tex, Color color) => new()
    {
        AlbedoTexture = tex, AlbedoColor = color, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, BillboardKeepScale = true,
        NoDepthTest = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled, RenderPriority = 2,
    };

    static GradientTexture2D Radial(float[] offsets, Color[] colors)
    {
        var g = new Gradient { Offsets = offsets, Colors = colors };
        return new GradientTexture2D
        {
            Gradient = g, Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 128, Height = 128,
        };
    }

    /// <summary>Étoile à cinq branches plate (plan XY), pour des particules face caméra.</summary>
    static ArrayMesh Star(float outer, float inner)
    {
        var verts = new Vector3[11];
        verts[0] = Vector3.Zero;
        for (int i = 0; i < 10; i++)
        {
            float a = Mathf.Pi / 2f + i * Mathf.Pi / 5f;
            float r = i % 2 == 0 ? outer : inner;
            verts[i + 1] = new Vector3(Mathf.Cos(a) * r, Mathf.Sin(a) * r, 0);
        }
        var idx = new int[30];
        for (int i = 0; i < 10; i++) { idx[i * 3] = 0; idx[i * 3 + 1] = i + 1; idx[i * 3 + 2] = (i + 1) % 10 + 1; }
        var arrays = new Godot.Collections.Array();
        arrays.Resize((int)Mesh.ArrayType.Max);
        arrays[(int)Mesh.ArrayType.Vertex] = verts;
        arrays[(int)Mesh.ArrayType.Index] = idx;
        var m = new ArrayMesh();
        m.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, arrays);
        return m;
    }
}
