using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Effets de la balle entre deux impacts, dans l'esprit de Knockout City :
/// lancer = onde d'air perpendiculaire au tir + cône de traits qui partent devant (plus gros et plus nombreux selon la charge,
/// éclat blanc et seconde onde au palier max) ; cloche = bouffée en anneau ; passe = anneau doux et paillettes à la couleur alliée ;
/// réception d'un tir adverse = choc dans les mains (onde et étincelles renvoyées vers le tireur) ;
/// attrape parfaite = éclat doré, double onde, gerbe d'or et paillettes qui montent ;
/// passe reçue = anneau allié et paillettes ; ramassage au sol = anneau au sol qui s'ouvre et spirale d'étincelles qui monte ;
/// montée de palier de charge = onde et gerbe autour de la balle.
/// Mêmes garde-fous qu'ImpactFx : rien sans affichage, rien collé à la caméra PC. --sans-balle-fx coupe tout (images « avant »).
/// </summary>
public static class BallFx
{
    public enum Catch { Pickup, Pass, Steal }

    static readonly bool Off = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--sans-balle-fx") >= 0;
    static readonly Color Gold = new(1f, 0.82f, 0.25f), MaxColor = new(1f, 0.35f, 0.75f);
    static GradientTexture2D? flashTex, softTex;
    const float MinCameraDistance = 1.2f;

    /// <summary>Couleur d'un tir : équipe du lanceur, rose vif au palier max (comme la charge et la traînée).</summary>
    static Color ShotColor(int team, int charge)
    {
        if (charge >= Gameplay.Ball.MaxCharge) return MaxColor;
        var c = Palette.Team(team);
        return Color.FromHsv(c.H, Mathf.Min(1f, c.S * 1.2f), Mathf.Max(c.V, 0.95f));
    }

    public static void Thrown(Node context, Vector3 pos, Vector3 velocity, int team, int charge, int mode)
    {
        if (!Usable(context, pos) || velocity.LengthSquared() < 1f) return;
        var dir = velocity.Normalized();
        var c = ShotColor(team, charge);
        if (mode == Gameplay.Ball.ModePass)
        {
            Ring(context, pos, dir, c.Lerp(Colors.White, 0.3f), 0.12f, 0.4f, 0.3f, 0.6f);
            Glitter(context, pos, c, 14, 0.2f, 1.2f, 0.55f);
            return;
        }
        if (mode == Gameplay.Ball.ModeLob)
        {
            Ring(context, pos, Vector3.Up, c.Lerp(Colors.White, 0.2f), 0.12f, 0.5f, 0.25f, 0.7f);
            Cone(context, pos, dir, c, 6, 5f, 0.8f);
            return;
        }
        float k = 1f + 0.35f * charge;
        Ring(context, pos + dir * 0.2f, dir, c, 0.1f, 0.42f * k, 0.2f, 0.9f);
        Cone(context, pos, dir, c, 8 + 4 * charge, 7f + 2f * charge, k);
        if (charge >= Gameplay.Ball.MaxCharge)
        {
            Ring(context, pos + dir * 0.6f, dir, Colors.White, 0.08f, 0.6f * k, 0.26f, 0.8f);
            ImpactExtras.Burst(context, pos, c, 0.7f, 0.12f, 0f);
        }
    }

    /// <summary>pos : position de la balle juste avant la prise ; incoming : sa vitesse à ce moment (choc renvoyé vers le tireur).</summary>
    public static void Caught(Node context, Vector3 pos, Vector3 incoming, int team, Catch how, bool perfect)
    {
        if (!Usable(context, pos)) return;
        var c = ShotColor(team, 0);
        switch (how)
        {
            case Catch.Steal when perfect:
            {
                var back = incoming.LengthSquared() > 0.01f ? -incoming.Normalized() : Vector3.Up;
                Flash(context, pos, Gold, 1.4f, 0.16f);
                ImpactExtras.Burst(context, pos, Gold, 0.95f, 0.2f, 0.25f);
                ImpactExtras.Streaks(context, pos, Gold, 1.6f, 0.24f);
                Ring(context, pos, back, Gold, 0.12f, 0.85f, 0.3f, 1f);
                Ring(context, pos, Vector3.Up, Colors.White, 0.1f, 0.65f, 0.26f, 0.9f);
                Sparks(context, pos, back, Gold, 22, 7f, 70f);
                Glitter(context, pos, Gold, 18, 0.3f, 1.6f, 0.9f);
                break;
            }
            case Catch.Steal:
            {
                var back = incoming.LengthSquared() > 0.01f ? -incoming.Normalized() : Vector3.Up;
                ImpactExtras.Burst(context, pos, c, 0.45f, 0.1f, 0f);
                Ring(context, pos, back, c.Lerp(Colors.White, 0.35f), 0.1f, 0.5f, 0.2f, 0.85f);
                Sparks(context, pos, back, c.Lerp(Colors.White, 0.3f), 12, 5f, 55f);
                break;
            }
            case Catch.Pass:
                Ring(context, pos, Vector3.Up, c.Lerp(Colors.White, 0.25f), 0.1f, 0.45f, 0.25f, 0.8f);
                Glitter(context, pos, c, 14, 0.22f, 1.3f, 0.6f);
                break;
            default:
            {
                var ground = Ground(context, pos);
                Ring(context, ground, Vector3.Up, c.Lerp(Colors.White, 0.2f), 0.1f, 0.6f, 0.35f, 0.75f);
                Spiral(context, ground, c);
                break;
            }
        }
    }

    /// <summary>La balle gagne un palier de charge : onde autour d'elle et gerbe courte, plus forte à chaque palier.</summary>
    public static void ChargeUp(Node context, Vector3 pos, Color color, int charge)
    {
        if (!Usable(context, pos)) return;
        float k = 0.8f + 0.25f * charge;
        Ring(context, pos, Vector3.Up, color, 0.18f, 0.5f * k, 0.25f, 0.9f);
        Ring(context, pos, new Vector3(0.6f, 0.2f, 0.75f).Normalized(), color.Lerp(Colors.White, 0.4f), 0.18f, 0.42f * k, 0.22f, 0.7f);
        Sparks(context, pos, Vector3.Up, color, 6 + 6 * charge, 3.5f + charge, 180f);
    }

    static bool Usable(Node context, Vector3 pos)
    {
        if (Off || !GodotObject.IsInstanceValid(context) || !context.IsInsideTree() || DisplayServer.GetName() == "headless" || context.GetTree().CurrentScene == null) return false;
        var cam = context.GetViewport()?.GetCamera3D();
        return cam == null || cam is XRCamera3D || cam.GlobalPosition.DistanceTo(pos) >= MinCameraDistance;
    }

    static Vector3 Ground(Node context, Vector3 pos)
    {
        var space = context.GetViewport()?.World3D?.DirectSpaceState;
        var q = PhysicsRayQueryParameters3D.Create(pos + Vector3.Up * 0.2f, pos + Vector3.Down * 2.5f);
        q.CollideWithAreas = false;
        var hit = space?.IntersectRay(q);
        return hit != null && hit.Count > 0 ? (Vector3)hit["position"] + Vector3.Up * 0.03f : pos + Vector3.Down * 0.12f;
    }

    static void Add(Node context, Node3D n, Vector3 pos, double life)
    {
        context.GetTree().CurrentScene.AddChild(n);
        n.GlobalPosition = pos;
        if (n is CpuParticles3D p) p.Restart();   // Godot 4.3 : « Emitting = true » posé hors de l'arbre ne déclenche rien
        n.GetTree().CreateTimer(life, false).Timeout += n.QueueFree;
    }

    static StandardMaterial3D Additive(Color c, Texture2D? tex = null) => new()
    {
        AlbedoColor = c, AlbedoTexture = tex, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
        Transparency = BaseMaterial3D.TransparencyEnum.Alpha, BlendMode = BaseMaterial3D.BlendModeEnum.Add,
        CullMode = BaseMaterial3D.CullModeEnum.Disabled, VertexColorUseAsAlbedo = true,
        DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelAlpha, DistanceFadeMinDistance = 0.5f, DistanceFadeMaxDistance = 1.4f,
    };

    static Basis Facing(Vector3 axis)
    {
        axis = axis.Normalized();
        var up = Mathf.Abs(axis.Dot(Vector3.Up)) > 0.95f ? Vector3.Forward : Vector3.Up;
        var x = up.Cross(axis).Normalized();
        return new Basis(x, axis, axis.Cross(x));   // Y local = axe : le tore est couché dans le plan perpendiculaire
    }

    /// <summary>Onde d'air : tore fin perpendiculaire à l'axe, qui s'ouvre vite et s'efface.</summary>
    static void Ring(Node context, Vector3 pos, Vector3 axis, Color color, float from, float to, float duration, float alpha)
    {
        if (UI.GameSettings.ReduceFlashes) alpha *= 0.6f;
        var mat = Additive(new Color(color, alpha));
        mat.VertexColorUseAsAlbedo = false;
        var ring = new MeshInstance3D
        {
            Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 40, RingSegments = 4 },
            MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        var holder = new Node3D();
        holder.AddChild(ring);
        Add(context, holder, pos, duration + 0.1);
        holder.Basis = Facing(axis);
        ring.Scale = new Vector3(from, from * 0.25f, from);
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "scale", new Vector3(to, to * 0.05f, to), duration).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Expo);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, duration).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Quad);
    }

    /// <summary>Lancer : traits étirés qui jaillissent devant la balle dans un cône serré.</summary>
    static void Cone(Node context, Vector3 pos, Vector3 dir, Color color, int amount, float speed, float k)
    {
        var fade = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { new Color(1.4f, 1.4f, 1.4f, 1f), new Color(color, 0f) } };
        var thin = new Curve();
        thin.AddPoint(new Vector2(0, 1f));
        thin.AddPoint(new Vector2(1, 0.2f));
        var p = new CpuParticles3D
        {
            Amount = amount, Lifetime = 0.26, OneShot = true, Explosiveness = 1f, Emitting = true, LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.18f * k,
            Direction = Vector3.Forward, Spread = 16f, InitialVelocityMin = speed * 0.7f, InitialVelocityMax = speed * 1.2f,
            DampingMin = speed * 1.2f, DampingMax = speed * 2f, Gravity = Vector3.Zero, ParticleFlagAlignY = true,
            ScaleAmountCurve = thin, ColorRamp = fade, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new BoxMesh { Size = new Vector3(0.035f, 0.9f * k, 0.035f) }, MaterialOverride = Additive(Colors.White),
        };
        Add(context, p, pos, 0.5);
        p.Basis = Basis.LookingAt(dir, Mathf.Abs(dir.Y) > 0.95f ? Vector3.Forward : Vector3.Up);
    }

    /// <summary>Étincelles nettes (gerbe) autour d'un axe ; spread 180 = dans toutes les directions.</summary>
    static void Sparks(Node context, Vector3 pos, Vector3 axis, Color color, int amount, float speed, float spread)
    {
        var fade = new Gradient { Offsets = new[] { 0f, 0.4f, 1f }, Colors = new[] { new Color(1.6f, 1.6f, 1.6f, 1f), color, new Color(color, 0f) } };
        var thin = new Curve();
        thin.AddPoint(new Vector2(0, 1f));
        thin.AddPoint(new Vector2(1, 0f));
        var p = new CpuParticles3D
        {
            Amount = amount, Lifetime = 0.35, OneShot = true, Explosiveness = 1f, Emitting = true, LocalCoords = false,
            Direction = Vector3.Forward, Spread = spread, InitialVelocityMin = speed * 0.5f, InitialVelocityMax = speed,
            DampingMin = speed * 1.5f, DampingMax = speed * 2.5f, Gravity = new Vector3(0, -3f, 0), ParticleFlagAlignY = true,
            ScaleAmountCurve = thin, ColorRamp = fade, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.16f, 0.02f) }, MaterialOverride = Additive(Colors.White),
        };
        Add(context, p, pos, 0.6);
        var a = axis.Normalized();
        p.Basis = Basis.LookingAt(a, Mathf.Abs(a.Y) > 0.95f ? Vector3.Forward : Vector3.Up);
    }

    /// <summary>Paillettes : petits points brillants qui scintillent en flottant vers le haut.</summary>
    static void Glitter(Node context, Vector3 pos, Color color, int amount, float radius, float rise, float life)
    {
        softTex ??= Radial(new[] { 0f, 0.25f, 1f }, new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.6f), new Color(1, 1, 1, 0) });
        var mat = Additive(Colors.White, softTex);
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Particles;
        var fade = new Gradient { Offsets = new[] { 0f, 0.2f, 1f }, Colors = new[] { new Color(1, 1, 1, 0f), new Color(color.Lerp(Colors.White, 0.5f), 1f), new Color(color, 0f) } };
        var twinkle = new Curve();
        twinkle.AddPoint(new Vector2(0, 0.3f));
        twinkle.AddPoint(new Vector2(0.25f, 1f));
        twinkle.AddPoint(new Vector2(0.5f, 0.45f));
        twinkle.AddPoint(new Vector2(0.75f, 0.9f));
        twinkle.AddPoint(new Vector2(1, 0f));
        var p = new CpuParticles3D
        {
            Amount = amount, Lifetime = life, OneShot = true, Explosiveness = 0.85f, Emitting = true, LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = radius,
            Direction = Vector3.Up, Spread = 50f, InitialVelocityMin = rise * 0.5f, InitialVelocityMax = rise, DampingMin = rise, DampingMax = rise * 1.5f,
            Gravity = Vector3.Zero, ScaleAmountMin = 0.6f, ScaleAmountMax = 1.2f, ScaleAmountCurve = twinkle, ColorRamp = fade,
            Mesh = new QuadMesh { Size = Vector2.One * 0.11f }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
        };
        Add(context, p, pos, life + 0.3);
    }

    /// <summary>Ramassage : six traînées de lumière qui montent en hélice autour de la balle depuis le sol et se resserrent.</summary>
    static void Spiral(Node context, Vector3 ground, Color color)
    {
        softTex ??= Radial(new[] { 0f, 0.25f, 1f }, new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.6f), new Color(1, 1, 1, 0) });
        var holder = new Node3D();
        Add(context, holder, ground, 0.75);
        const int n = 5, tail = 9;
        for (int s = 0; s < n; s++)
        {
            float a0 = s * Mathf.Tau / n;
            for (int j = 0; j < tail; j++)
            {
                var mat = Additive(new Color(color.Lerp(Colors.White, 0.6f - 0.05f * j), 1f - 0.09f * j), softTex);
                mat.VertexColorUseAsAlbedo = false;
                mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
                var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * (0.15f - 0.011f * j) }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
                holder.AddChild(q);
                float lag = j * 0.022f;
                var tw = q.CreateTween();
                tw.TweenMethod(Callable.From<float>(t =>
                {
                    float u = Mathf.Clamp(t - lag, 0f, 1f);
                    float r = Mathf.Lerp(0.5f, 0.12f, u);
                    float a = a0 + u * 5.5f;
                    q.Position = new Vector3(Mathf.Cos(a) * r, 0.05f + u * 0.8f, Mathf.Sin(a) * r);
                    mat.AlbedoColor = new Color(mat.AlbedoColor, (1f - 0.09f * j) * (1f - u * u));
                }), 0f, 1f + lag, 0.55).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Quad);
            }
        }
    }

    /// <summary>Éclat rond face caméra, bref, sans test de profondeur (les mains ne le coupent pas).</summary>
    static void Flash(Node context, Vector3 pos, Color color, float size, float duration)
    {
        if (UI.GameSettings.ReduceFlashes) size *= 0.6f;
        flashTex ??= Radial(new[] { 0f, 0.3f, 1f }, new[] { new Color(1, 1, 1, 1), new Color(1, 1, 1, 0.55f), new Color(1, 1, 1, 0) });
        var mat = Additive(new Color(color, 0.9f), flashTex);
        mat.VertexColorUseAsAlbedo = false;
        mat.BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled;
        mat.NoDepthTest = true;
        var q = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * size }, MaterialOverride = mat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off };
        Add(context, q, pos, duration + 0.05);
        var tw = q.CreateTween().SetParallel();
        tw.TweenProperty(q, "scale", Vector3.One * 1.3f, duration).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, duration).SetEase(Tween.EaseType.In);
    }

    static GradientTexture2D Radial(float[] offsets, Color[] colors) => new()
    {
        Gradient = new Gradient { Offsets = offsets, Colors = colors }, Fill = GradientTexture2D.FillEnum.Radial,
        FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
    };
}
