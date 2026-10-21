using Godot;

namespace DogeballVR.Visual;

/// <summary>Petits effets de « game feel » partagés : éclaboussure de peinture, traînée de balle, arrêt sur image à l'impact.</summary>
public static class Juice
{
    static ulong hitStopUntil;
    static bool hitStopActive;

    /// <summary>Gerbe de gouttes colorées (one-shot, se libère seule).</summary>
    public static void Burst(Node context, Vector3 pos, Color color, int amount = 28, float speed = 5f)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;
        var mat = Materials.Additive(new Color(color.R, color.G, color.B, 0.9f));
        var p = new CpuParticles3D
        {
            Amount = amount, Lifetime = 0.55, OneShot = true, Explosiveness = 1f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.08f, Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = speed * 0.4f, InitialVelocityMax = speed,
            Gravity = new Vector3(0, -9f, 0), ScaleAmountMin = 0.6f, ScaleAmountMax = 1.4f, Emitting = true,
            Mesh = new SphereMesh { Radius = 0.035f, Height = 0.07f, RadialSegments = 6, Rings = 3 }, MaterialOverride = mat,
        };
        context.GetTree().CurrentScene.AddChild(p);
        p.GlobalPosition = pos;
        p.GetTree().CreateTimer(1.0).Timeout += p.QueueFree;
    }

    /// <summary>Onde plate qui s'ouvre à l'impact (0,3 s).</summary>
    public static void Shock(Node context, Vector3 pos, Color color, float radius = 1.1f)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;
        var mat = Materials.Additive(new Color(color.R, color.G, color.B, 0.8f));
        var ring = new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.9f, OuterRadius = 1f, Rings = 32, RingSegments = 4 }, MaterialOverride = mat, Scale = Vector3.One * 0.15f };
        var scene = context.GetTree().CurrentScene;
        scene.AddChild(ring);
        ring.GlobalPosition = pos;
        var tw = ring.CreateTween().SetParallel();
        tw.TweenProperty(ring, "scale", Vector3.One * radius, 0.3).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, 0.3);
        tw.Chain().TweenCallback(Callable.From(ring.QueueFree));
    }

    /// <summary>Fantôme additif laissé derrière une balle rapide : fond en 0,18 s.</summary>
    public static void TrailGhost(Node context, Vector3 pos, Color color, float radius)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;
        var mat = Materials.Additive(new Color(color.R, color.G, color.B, 0.45f));
        var g = new MeshInstance3D { Mesh = new SphereMesh { Radius = radius, Height = radius * 2f, RadialSegments = 8, Rings = 4 }, MaterialOverride = mat, TopLevel = true };
        context.GetTree().CurrentScene.AddChild(g);
        g.GlobalPosition = pos;
        var tw = g.CreateTween().SetParallel();
        tw.TweenProperty(g, "scale", Vector3.One * 0.3f, 0.18);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, 0.18);
        tw.Chain().TweenCallback(Callable.From(g.QueueFree));
    }

    /// <summary>Petit nuage de poussière au sol (départ de dash, réception de saut) : sphères grises qui s'étalent et s'effacent.</summary>
    public static void Dust(Node context, Vector3 pos, int amount = 10, float spread = 1.6f)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree() || DisplayServer.GetName() == "headless") return;
        var mat = new StandardMaterial3D
        {
            AlbedoColor = new Color(0.92f, 0.9f, 0.96f, 0.55f), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha, VertexColorUseAsAlbedo = false,
        };
        var p = new CpuParticles3D
        {
            Amount = amount, Lifetime = 0.45, OneShot = true, Explosiveness = 0.95f, EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere,
            EmissionSphereRadius = 0.15f, Direction = Vector3.Up, Spread = 80f, InitialVelocityMin = spread * 0.5f, InitialVelocityMax = spread,
            Gravity = new Vector3(0, -1.5f, 0), DampingMin = 3f, DampingMax = 5f, ScaleAmountMin = 0.8f, ScaleAmountMax = 1.6f, Emitting = true,
            ScaleAmountCurve = new Curve(), Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f, RadialSegments = 6, Rings = 3 }, MaterialOverride = mat,
        };
        p.ScaleAmountCurve.AddPoint(new Vector2(0, 0.6f));
        p.ScaleAmountCurve.AddPoint(new Vector2(0.4f, 1f));
        p.ScaleAmountCurve.AddPoint(new Vector2(1, 0f));
        context.GetTree().CurrentScene.AddChild(p);
        p.GlobalPosition = pos + Vector3.Up * 0.08f;
        p.GetTree().CreateTimer(0.8).Timeout += p.QueueFree;
    }

    /// <summary>Silhouette fantôme (capsule additive) laissée pendant un dash : s'efface en 0,22 s.</summary>
    public static void Afterimage(Node context, Vector3 feet, Color color)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree() || DisplayServer.GetName() == "headless") return;
        var mat = Materials.Additive(new Color(color.R, color.G, color.B, 0.35f));
        var g = new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.3f, Height = 1.7f }, MaterialOverride = mat };
        context.GetTree().CurrentScene.AddChild(g);
        g.GlobalPosition = feet + Vector3.Up * 0.85f;
        var tw = g.CreateTween().SetParallel();
        tw.TweenProperty(g, "scale", new Vector3(0.7f, 1f, 0.7f), 0.22);
        tw.TweenProperty(mat, "albedo_color:a", 0.0, 0.22);
        tw.Chain().TweenCallback(Callable.From(g.QueueFree));
    }

    /// <summary>Ralentit le monde 60-90 ms à l'impact (temps réel, indépendant du ralenti). Aucun effet sur la caméra VR.</summary>
    public static void HitStop(Node context, float seconds = 0.07f, float scale = 0.12f)
    {
        if (DisplayServer.GetName() == "headless" || !UI.GameSettings.HitStop || !GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return;   // pas de ralenti dans les simulations de CI
        ulong until = Time.GetTicksMsec() + (ulong)(seconds * 1000f);
        if (until > hitStopUntil) hitStopUntil = until;
        if (hitStopActive) return;
        hitStopActive = true;
        Engine.TimeScale = scale;
        var t = context.GetTree().CreateTimer(seconds, true, false, true);
        t.Timeout += () => Release(context);
    }

    static void Release(Node context)
    {
        if (Time.GetTicksMsec() < hitStopUntil && GodotObject.IsInstanceValid(context) && context.IsInsideTree())
        {
            float rest = (hitStopUntil - Time.GetTicksMsec()) / 1000f;
            context.GetTree().CreateTimer(Mathf.Max(rest, 0.01f), true, false, true).Timeout += () => Release(context);
            return;
        }
        Engine.TimeScale = 1.0;
        hitStopActive = false;
    }
}
