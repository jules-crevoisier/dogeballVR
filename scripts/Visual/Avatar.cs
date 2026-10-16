using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Avatar de la DA v3 : combinaison unie à la couleur de l'équipe, jambes sombres, épaules marquées, tête sphérique avec
/// visière sombre, grandes mains blanches, disque blanc (équipe 1) ou losange blanc (équipe 2) sur le torse.
/// Sert pour les bots et (réseau) pour les joueurs distants.
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
        var suit = Materials.Flat(color);
        var dark = Materials.Flat(Palette.Panel);
        var white = Materials.Flat(Palette.Lines);

        avatar.torso = new Node3D { Name = "Torso" };
        avatar.torso.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.22f, Height = 0.75f }, MaterialOverride = suit });
        // Épaules marquées.
        foreach (float side in new[] { -1f, 1f })
            avatar.torso.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.12f, Height = 0.24f }, Position = new Vector3(0.24f * side, 0.2f, 0), MaterialOverride = suit });
        // Jambes sombres sous le torse.
        foreach (float side in new[] { -1f, 1f })
            avatar.torso.AddChild(new MeshInstance3D { Mesh = new CapsuleMesh { Radius = 0.09f, Height = 0.7f }, Position = new Vector3(0.11f * side, -0.6f, 0), MaterialOverride = dark });
        // Emblème blanc sur le torse (face avant = -Z) : disque pour l'équipe 1, losange pour l'équipe 2.
        avatar.torso.AddChild(team == 0
            ? new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.085f, BottomRadius = 0.085f, Height = 0.02f }, Position = new Vector3(0, 0.1f, -0.215f), RotationDegrees = new Vector3(90, 0, 0), MaterialOverride = white }
            : new MeshInstance3D { Mesh = new BoxMesh { Size = new Vector3(0.12f, 0.12f, 0.02f) }, Position = new Vector3(0, 0.1f, -0.215f), RotationDegrees = new Vector3(0, 0, 45), MaterialOverride = white });
        avatar.AddChild(avatar.torso);

        avatar.Head = new Node3D { Name = "Head" };
        avatar.Head.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.13f, Height = 0.26f }, MaterialOverride = suit });
        // Visière sombre, pas de visage.
        avatar.Head.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.07f, 0.06f) }, Position = new Vector3(0, 0.01f, -0.1f), MaterialOverride = dark,
        });
        avatar.AddChild(avatar.Head);

        avatar.LeftHand = MakeGlove(white);
        avatar.RightHand = MakeGlove(white);
        avatar.AddChild(avatar.LeftHand);
        avatar.AddChild(avatar.RightHand);
        avatar.Pose(new Vector3(0, 1.6f, 0), 0f, new Vector3(-0.35f, 1.0f, -0.2f), new Vector3(0.35f, 1.0f, -0.2f));
        return avatar;
    }

    static Node3D MakeGlove(Material white)
    {
        var glove = new Node3D { Name = "Glove" };
        glove.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f }, MaterialOverride = white });
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
