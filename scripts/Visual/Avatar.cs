using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Avatar de la DA : casque lisse sans visage, buste sombre avec bandes lumineuses à la couleur de l'équipe,
/// deux gants, pas de jambes. Sert pour les bots et (réseau) pour les joueurs distants.
/// </summary>
public partial class Avatar : Node3D
{
    public Node3D Head { get; private set; } = null!;
    public Node3D LeftHand { get; private set; } = null!;
    public Node3D RightHand { get; private set; } = null!;
    Node3D torso = null!;

    public static Avatar Create(int team, bool bot)
    {
        var avatar = new Avatar { Name = "Avatar" };
        var color = bot ? Palette.BotTeam(team) : Palette.Team(team);
        var armor = Materials.Flat(Palette.Indigo);
        var glow = Materials.Glow(color, 1.8f);

        avatar.torso = new Node3D { Name = "Torso" };
        avatar.torso.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.22f, Height = 0.75f }, MaterialOverride = armor });
        avatar.torso.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.225f, BottomRadius = 0.225f, Height = 0.07f }, Position = new Vector3(0, 0.12f, 0), MaterialOverride = glow,
        });
        avatar.torso.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.2f, BottomRadius = 0.2f, Height = 0.05f }, Position = new Vector3(0, -0.12f, 0), MaterialOverride = glow,
        });
        avatar.AddChild(avatar.torso);

        avatar.Head = new Node3D { Name = "Head" };
        avatar.Head.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.13f, Height = 0.26f }, MaterialOverride = armor });
        // Visière : fine bande lumineuse, pas de visage.
        avatar.Head.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.22f, 0.04f, 0.06f) }, Position = new Vector3(0, 0.01f, -0.1f), MaterialOverride = glow,
        });
        avatar.AddChild(avatar.Head);

        avatar.LeftHand = MakeGlove(armor, glow);
        avatar.RightHand = MakeGlove(armor, glow);
        avatar.AddChild(avatar.LeftHand);
        avatar.AddChild(avatar.RightHand);
        avatar.Pose(new Vector3(0, 1.6f, 0), 0f, new Vector3(-0.35f, 1.0f, -0.2f), new Vector3(0.35f, 1.0f, -0.2f));
        return avatar;
    }

    static Node3D MakeGlove(Material armor, Material glow)
    {
        var glove = new Node3D { Name = "Glove" };
        glove.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.09f, Height = 0.18f }, MaterialOverride = armor });
        glove.AddChild(new MeshInstance3D
        {
            Mesh = new CylinderMesh { TopRadius = 0.075f, BottomRadius = 0.075f, Height = 0.03f }, Position = new Vector3(0, 0.05f, 0.05f), MaterialOverride = glow,
        });
        return glove;
    }

    /// <summary>Place l'avatar dans le monde (positions globales : le parent doit être à l'origine, sans rotation).</summary>
    public void Pose(Vector3 head, float yaw, Vector3 left, Vector3 right)
    {
        Head.Position = head;
        Head.Rotation = new Vector3(0, yaw, 0);
        torso.Position = head - new Vector3(0, 0.5f, 0);
        torso.Rotation = new Vector3(0, yaw, 0);
        LeftHand.Position = left;
        RightHand.Position = right;
    }
}
