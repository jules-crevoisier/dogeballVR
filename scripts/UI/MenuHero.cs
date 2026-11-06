using Godot;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Personnage en héros du menu (DA v3, comme le perso en pied à droite des menus de Knockout City) : le joueur orange,
/// rendu dans son propre monde 3D sur fond transparent, éclairé de face avec un contre-jour turquoise, qui célèbre en
/// boucle. Posé sur la page d'interface (MenuStage), il remplace la rue grise qu'on voyait derrière le menu.
/// </summary>
public partial class MenuHero : SubViewportContainer
{
    Avatar? hero;
    double t;

    public override void _Ready()
    {
        MouseFilter = MouseFilterEnum.Ignore;
        Stretch = true;
        var vp = new SubViewport { TransparentBg = true, OwnWorld3D = true, Msaa3D = Viewport.Msaa.Msaa4X, RenderTargetUpdateMode = SubViewport.UpdateMode.Always };
        AddChild(vp);

        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color("c8c0ff"), AmbientLightEnergy = 0.55f,
            TonemapMode = Godot.Environment.ToneMapper.Filmic,
        };
        vp.AddChild(new WorldEnvironment { Environment = env });
        // Lumière principale chaude de trois quarts, contre-jour turquoise qui découpe la silhouette sur le violet.
        var key = new DirectionalLight3D { LightEnergy = 1.5f, LightColor = new Color("fff1dc") };
        vp.AddChild(key);
        key.RotationDegrees = new Vector3(-30, 150, 0);
        var rim = new DirectionalLight3D { LightEnergy = 2.2f, LightColor = UiKit.Teal };
        vp.AddChild(rim);
        rim.RotationDegrees = new Vector3(-10, -20, 0);

        hero = Avatar.Create(0, false);
        vp.AddChild(hero);
        if (hero.FindChild("AnimationPlayer", true, false) is AnimationPlayer ap && ap.HasAnimation("Celebration"))
        {
            ap.GetAnimation("Celebration").LoopMode = Animation.LoopModeEnum.Linear;
            hero.PlayAction("Celebration", double.MaxValue);
        }

        var cam = new Camera3D { Fov = 30f, Current = true };
        vp.AddChild(cam);
        cam.Position = new Vector3(0.15f, 1.05f, -4.4f);
        cam.LookAt(new Vector3(0, 0.95f, 0), Vector3.Up);
        Pose(0);
    }

    void Pose(double time)
    {
        if (hero == null) return;
        float s = Mathf.Sin((float)time * 1.3f);
        var head = new Vector3(0, 1.6f + 0.02f * s, 0);
        float yaw = Mathf.DegToRad(-28 + 5 * Mathf.Sin((float)time * 0.6f));   // de trois quarts, face au joueur
        var b = new Basis(Vector3.Up, yaw);
        hero.Pose(head, yaw, head + b * new Vector3(-0.42f, -0.3f, -0.3f), head + b * new Vector3(0.4f, 0.1f, -0.25f));
    }

    public override void _Process(double delta)
    {
        t += delta;
        Pose(t);
    }
}
