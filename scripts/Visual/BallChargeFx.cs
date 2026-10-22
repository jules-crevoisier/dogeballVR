using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Balle chargée (paliers gagnés en enchaînant les attrapes), lisible de loin comme dans Knockout City :
/// palier 1 : un anneau d'énergie à la couleur de l'équipe tourne autour de la balle, quelques étincelles ;
/// palier 2 : deux anneaux croisés, plus d'étincelles, la lueur pulse ;
/// palier 3 (max) : trois anneaux rose vif presque blancs, la lueur bat vite, l'ensemble tremble, gerbe d'étincelles continue.
/// Nœud indépendant de la balle (TopLevel) : il ne tourne pas avec elle et ne touche ni à son aspect ni à sa traînée.
/// </summary>
public partial class BallChargeFx : Node3D
{
    public Gameplay.Ball Ball { get; init; } = null!;

    static readonly Color MaxColor = new(1f, 0.35f, 0.75f);   // même rose que la traînée d'une balle très chargée
    static GradientTexture2D? glowTex;

    readonly MeshInstance3D[] rings = new MeshInstance3D[3];
    readonly StandardMaterial3D ringMat = new()
    {
        ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
        BlendMode = BaseMaterial3D.BlendModeEnum.Add, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
    };
    StandardMaterial3D glowMat = null!, sparkMat = null!;
    MeshInstance3D glow = null!;
    CpuParticles3D sparks = null!;
    int shownCharge = -1, shownTeam = -2;
    /// <summary>--charge-test : chaque balle affiche un palier fixe (1, 2 ou 3) pour les captures, sans toucher aux règles.</summary>
    static readonly bool ChargeTest = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--charge-test") >= 0;
    double t;
    readonly RandomNumberGenerator rng = new();

    public override void _Ready()
    {
        TopLevel = true;
        // simulations de CI : rien à dessiner ; --sans-charge-fx : images « avant » de la vitrine
        if (DisplayServer.GetName() == "headless" || System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--sans-charge-fx") >= 0) { SetProcess(false); return; }
        for (int i = 0; i < rings.Length; i++)
        {
            rings[i] = new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.205f, OuterRadius = 0.232f, Rings = 32, RingSegments = 6 },
                MaterialOverride = ringMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false,
            };
            AddChild(rings[i]);
        }
        glowTex ??= new GradientTexture2D
        {
            Gradient = new Gradient { Offsets = new[] { 0f, 0.45f, 1f }, Colors = new[] { new Color(1, 1, 1, 0.9f), new Color(1, 1, 1, 0.35f), new Color(1, 1, 1, 0) } },
            Fill = GradientTexture2D.FillEnum.Radial, FillFrom = new Vector2(0.5f, 0.5f), FillTo = new Vector2(0.5f, 0f), Width = 64, Height = 64,
        };
        glowMat = new StandardMaterial3D
        {
            AlbedoTexture = glowTex, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            BlendMode = BaseMaterial3D.BlendModeEnum.Add, BillboardMode = BaseMaterial3D.BillboardModeEnum.Enabled, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
        };
        glow = new MeshInstance3D { Mesh = new QuadMesh { Size = Vector2.One * 0.9f }, MaterialOverride = glowMat, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off, Visible = false };
        AddChild(glow);
        sparkMat = new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, VertexColorUseAsAlbedo = true, CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            DistanceFadeMode = BaseMaterial3D.DistanceFadeModeEnum.PixelDither, DistanceFadeMinDistance = 0.6f, DistanceFadeMaxDistance = 1.6f,
        };
        var fade = new Curve();
        fade.AddPoint(new Vector2(0, 1f));
        fade.AddPoint(new Vector2(1, 0f));
        sparks = new CpuParticles3D
        {
            Amount = 24, Lifetime = 0.35, Emitting = false, LocalCoords = false,
            EmissionShape = CpuParticles3D.EmissionShapeEnum.Sphere, EmissionSphereRadius = 0.2f,
            Direction = Vector3.Up, Spread = 180f, InitialVelocityMin = 1.2f, InitialVelocityMax = 3f, Gravity = new Vector3(0, -2f, 0),
            ScaleAmountCurve = fade, ParticleFlagAlignY = true, CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            Mesh = new BoxMesh { Size = new Vector3(0.02f, 0.11f, 0.02f) }, MaterialOverride = sparkMat,
        };
        AddChild(sparks);
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Ball)) { QueueFree(); return; }
        t += delta;
        int charge = !Ball.Visible ? 0 : ChargeTest ? 1 + (int)(Ball.GetInstanceId() % 3) : Ball.Charge;
        GlobalPosition = Ball.GlobalPosition;
        if (charge != shownCharge || Ball.OwnerTeam != shownTeam) Restyle(charge);
        if (charge <= 0) return;

        float k = charge / (float)Gameplay.Ball.MaxCharge;
        // anneaux : chacun sur son axe, vitesses différentes ; au palier max l'ensemble tremble
        for (int i = 0; i < charge && i < rings.Length; i++)
        {
            float spin = (float)t * (5f + 2.5f * i) * (i % 2 == 0 ? 1f : -1f);
            rings[i].Basis = new Basis(new Vector3(1, 0, 0), 0.5f + i * 1.05f).Rotated(new Vector3(0, 1, 0), i * 2.1f) * new Basis(Vector3.Up, spin);
        }
        float beat = 0.5f + 0.5f * Mathf.Sin((float)t * (4f + 8f * k));
        glow.Scale = Vector3.One * (0.75f + 0.35f * k + 0.18f * beat * k);
        glowMat.AlbedoColor = new Color(glowMat.AlbedoColor, 0.2f + 0.22f * k * (0.6f + 0.4f * beat));
        Position = charge >= Gameplay.Ball.MaxCharge
            ? GlobalPosition + new Vector3(rng.RandfRange(-1, 1), rng.RandfRange(-1, 1), rng.RandfRange(-1, 1)) * 0.012f
            : GlobalPosition;
    }

    /// <summary>Couleurs et quantités du palier : équipe du porteur, rose vif au palier max.</summary>
    void Restyle(int charge)
    {
        bool up = charge > shownCharge && shownCharge >= 0 && !ChargeTest;
        shownCharge = charge;
        shownTeam = Ball.OwnerTeam;
        bool on = charge > 0;
        var team = Palette.Team(Ball.OwnerTeam);
        var c = charge >= Gameplay.Ball.MaxCharge ? MaxColor : Color.FromHsv(team.H, Mathf.Min(1f, team.S * 1.3f), 1f);
        ringMat.AlbedoColor = new Color(c.Lerp(Colors.White, 0.06f * charge), 0.9f);   // reste saturé : les halos de la balle blanchissent déjà le centre
        glowMat.AlbedoColor = new Color(c, 0.4f);
        for (int i = 0; i < rings.Length; i++) rings[i].Visible = i < charge;
        glow.Visible = on;
        var ramp = new Gradient { Offsets = new[] { 0f, 1f }, Colors = new[] { Colors.White, c } };
        sparks.ColorRamp = ramp;
        sparks.Amount = charge >= Gameplay.Ball.MaxCharge ? 24 : charge == 2 ? 12 : 6;
        sparks.Emitting = on;
        if (up) BallFx.ChargeUp(this, Ball.GlobalPosition, c, charge);   // palier gagné : onde et gerbe
    }
}
