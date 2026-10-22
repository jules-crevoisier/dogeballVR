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

    // Personnage riggé (assets/models/avatar_<équipe>_rig.glb) : un seul maillage animé, piloté par le déplacement et les événements de jeu.
    Node3D? body;
    AnimationPlayer? anim;
    int playerId = -1;
    Vector3 lastHead;
    float speed;
    double actionLeft;
    readonly System.Collections.Generic.Dictionary<Gameplay.Ball, (bool held, int owner, float speed)> seenBalls = new();
    float climb;   // vitesse verticale de la tête (saut)
    const float HeadHeight = 1.62f;   // centre de la tête du modèle (1,75 m) au-dessus des pieds

    public static Avatar Create(int team, bool bot, int playerId = -1)
    {
        var avatar = new Avatar { Name = "Avatar", playerId = playerId };
        string teamName = team == 0 ? "orange" : "bleu";
        // deux persos par équipe (Riko/Bo, Nova/Kai) : le second joueur de chaque équipe prend la variante
        var rigged = (playerId >= 0 && playerId / 2 % 2 == 1 ? ModelLibrary.Instance($"avatar_{teamName}2_rig") : null)
                     ?? ModelLibrary.Instance($"avatar_{teamName}_rig");
        if (rigged != null)
        {
            avatar.body = rigged;
            avatar.anim = rigged.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
            avatar.AddChild(rigged);
            foreach (var loop in new[] { "Repos", "Course" })
                if (avatar.anim?.HasAnimation(loop) == true) avatar.anim.GetAnimation(loop).LoopMode = Animation.LoopModeEnum.Linear;
            avatar.anim?.Play("Repos");
            // Nœuds attendus par les appelants (tête, mains) : vides, le modèle animé porte le rendu.
            avatar.Head = new Node3D { Name = "Head" };
            avatar.LeftHand = new Node3D { Name = "LeftHand" };
            avatar.RightHand = new Node3D { Name = "RightHand" };
            avatar.torso = new Node3D { Name = "Torso" };
            foreach (var n in new[] { avatar.Head, avatar.LeftHand, avatar.RightHand, avatar.torso }) avatar.AddChild(n);
            avatar.Pose(new Vector3(0, 1.6f, 0), 0f, new Vector3(-0.35f, 1.0f, -0.2f), new Vector3(0.35f, 1.0f, -0.2f));
            return avatar;
        }
        var color = bot ? Palette.BotTeam(team) : Palette.Team(team);
        var suit = Materials.Flat(color);
        var dark = Materials.Flat(Palette.Panel);
        var white = Materials.Flat(Palette.Lines);

        // Modèles Blender (assets/models) pour tout le monde, bots compris ; fichiers absents : formes simples ci-dessous.
        string team_ = team == 0 ? "orange" : "bleu";
        var bodyModel = ModelLibrary.Instance($"avatar_{team_}_corps");
        var headModel = ModelLibrary.Instance($"avatar_{team_}_tete");
        var gloveModel = ModelLibrary.Instance("main");

        avatar.torso = new Node3D { Name = "Torso" };
        if (bodyModel != null) avatar.torso.AddChild(bodyModel);
        else
        {
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
        }
        avatar.AddChild(avatar.torso);

        avatar.Head = new Node3D { Name = "Head" };
        if (headModel != null) avatar.Head.AddChild(headModel);
        else
        {
            avatar.Head.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.13f, Height = 0.26f }, MaterialOverride = suit });
            // Visière sombre, pas de visage.
            avatar.Head.AddChild(new MeshInstance3D
            {
                Mesh = new BoxMesh { Size = new Vector3(0.2f, 0.07f, 0.06f) }, Position = new Vector3(0, 0.01f, -0.1f), MaterialOverride = dark,
            });
        }
        avatar.AddChild(avatar.Head);

        avatar.LeftHand = MakeGlove(white, gloveModel);
        avatar.RightHand = MakeGlove(white, ModelLibrary.Instance("main"));
        avatar.AddChild(avatar.LeftHand);
        avatar.AddChild(avatar.RightHand);
        avatar.Pose(new Vector3(0, 1.6f, 0), 0f, new Vector3(-0.35f, 1.0f, -0.2f), new Vector3(0.35f, 1.0f, -0.2f));
        return avatar;
    }

    static Node3D MakeGlove(Material white, Node3D? model)
    {
        var glove = new Node3D { Name = "Glove" };
        if (model != null)
        {
            model.RotationDegrees = new Vector3(-90, 0, 0);   // doigts vers l'avant (-Z)
            glove.AddChild(model);
        }
        else glove.AddChild(new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.11f, Height = 0.22f }, MaterialOverride = white });
        return glove;
    }

    public override void _Ready()
    {
        if (body != null) Gameplay.MatchController.HitScored += OnHit;
    }

    public override void _ExitTree()
    {
        if (body != null) Gameplay.MatchController.HitScored -= OnHit;
    }

    void OnHit(int shooterId, int targetId, bool kill)
    {
        if (targetId == playerId && playerId >= 0) PlayAction(kill ? "KO" : "Touche", kill ? 3.0 : 0.45);
    }

    /// <summary>Joue une animation ponctuelle (lancer, attrape, esquive, touché, KO) puis revient à la course ou au repos.</summary>
    public void PlayAction(string name, double seconds)
    {
        if (anim == null || !anim.HasAnimation(name)) return;
        anim.Play(name, 0.08);
        anim.SpeedScale = 1f;
        actionLeft = seconds;
    }

    public override void _Process(double delta)
    {
        if (body == null || anim == null || delta <= 0) return;
        // Événements de balle déduits de l'état visible (hôte et clients) : prise puis lâcher par ce joueur.
        if (playerId >= 0)
            foreach (var n in GetTree().GetNodesInGroup("balls"))
            {
                if (n is not Gameplay.Ball ball) continue;
                var cur = (held: ball.IsHeld, owner: ball.OwnerPlayerId, speed: ball.LinearVelocity.Length());
                if (seenBalls.TryGetValue(ball, out var prev))
                {
                    // balle lente = ramassée au sol, balle rapide = attrapée au vol
                    if (!prev.held && cur.held && cur.owner == playerId) PlayAction(prev.speed < 3f ? "Ramasser" : "Attrape", prev.speed < 3f ? 0.4 : 0.35);
                    else if (prev.held && !cur.held && prev.owner == playerId) PlayAction(ball.IsPass ? "Passe" : "Lancer", ball.IsPass ? 0.45 : 0.6);
                }
                seenBalls[ball] = cur;
            }
        if (actionLeft <= 0 && climb > 2.2f) PlayAction("Saut", 0.7);
        if (actionLeft > 0) { actionLeft -= delta; return; }
        string loop = speed > 1.0f ? "Course" : "Repos";
        if (anim.CurrentAnimation != loop) anim.Play(loop, 0.15);
        anim.SpeedScale = loop == "Course" ? Mathf.Clamp(speed / 4.5f, 0.7f, 1.6f) : 1f;
    }

    /// <summary>Place l'avatar dans le monde (positions globales : le parent doit être à l'origine, sans rotation).</summary>
    public void Pose(Vector3 head, float yaw, Vector3 left, Vector3 right)
    {
        if (body != null)
        {
            float dt = (float)GetProcessDeltaTime();
            var flat = new Vector3(head.X - lastHead.X, 0, head.Z - lastHead.Z);
            if (dt > 0 && lastHead != Vector3.Zero)
            {
                speed = Mathf.Lerp(speed, Mathf.Min(flat.Length() / dt, 12f), 0.25f);
                climb = (head.Y - lastHead.Y) / dt;
            }
            lastHead = head;
            // pieds au sol même pour un grand joueur (écart de taille), mais le corps suit un vrai saut
            float y = head.Y - HeadHeight;
            body.Position = new Vector3(head.X, y < 0.25f ? Mathf.Min(0f, y) : y, head.Z);
            body.Rotation = new Vector3(0, yaw, 0);
        }
        Head.Position = head;
        Head.Rotation = new Vector3(0, yaw, 0);
        torso.Position = head - new Vector3(0, 0.5f, 0);
        torso.Rotation = new Vector3(0, yaw, 0);
        LeftHand.Position = left;
        RightHand.Position = right;
    }
}
