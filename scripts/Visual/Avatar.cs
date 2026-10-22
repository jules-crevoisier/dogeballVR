using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Avatar de la DA v3 : combinaison unie à la couleur de l'équipe, jambes sombres, épaules marquées, tête sphérique avec
/// visière sombre, grandes mains blanches, disque blanc (équipe 1) ou losange blanc (équipe 2) sur le torse.
/// Sert pour les bots et (réseau) pour les joueurs distants.
/// Personnage riggé : touché = animation « Recul » (tirée de « Touche », bras baissés, voir AddRecoil) + recul court à l'opposé du tireur + éclat blanc de 0,1 s ; KO = animation « KO »,
/// jouée par une copie du modèle laissée sur place quand le joueur disparaît aussitôt (bot ou joueur distant masqué au KO).
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

    static readonly System.Collections.Generic.List<Avatar> all = new();
    string modelName = "";
    Vector3 bodyBase;            // position du corps posée par Pose (sans le recul)
    ulong lastPoseFrame;         // les bots n'appellent pas Pose : leur vitesse se déduit alors du déplacement du corps
    Vector3 lastBodyGlobal;
    Vector3 kickDir;             // recul de touche (monde, horizontal), amorti par kickAge
    double kickAge = 99;
    ulong flashUntil;            // éclat blanc (temps réel : il reste visible pendant l'arrêt sur image)
    StandardMaterial3D? flashMat;
    MeshInstance3D[] meshes = System.Array.Empty<MeshInstance3D>();
    double ghostWindow;          // KO à l'instant : si l'avatar est masqué dans ce délai, une copie joue la chute
    Transform3D koTransform;

    public static Avatar Create(int team, bool bot, int playerId = -1)
    {
        var avatar = new Avatar { Name = "Avatar", playerId = playerId };
        string teamName = team == 0 ? "orange" : "bleu";
        var rigged = ModelLibrary.Instance($"avatar_{teamName}_rig");
        if (rigged != null)
        {
            avatar.body = rigged;
            avatar.modelName = $"avatar_{teamName}_rig";
            avatar.anim = rigged.FindChild("AnimationPlayer", true, false) as AnimationPlayer;
            avatar.AddChild(rigged);
            foreach (var loop in new[] { "Repos", "Course" })
                if (avatar.anim?.HasAnimation(loop) == true) avatar.anim.GetAnimation(loop).LoopMode = Animation.LoopModeEnum.Linear;
            if (avatar.anim != null) AddRecoil(avatar.anim);
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

    /// <summary>
    /// Réaction de touche « Recul », fabriquée à l'exécution (le modèle n'est pas modifié) : le clip « Touche » du modèle lève les deux
    /// bras à l'horizontale pendant 0,45 s, ce qui se lit comme une pose en T. On garde son buste et sa tête rejetés en arrière et son
    /// recul de bassin, avec les bras du repos (le long du corps). Ajouté une fois à la bibliothèque partagée du modèle.
    /// </summary>
    static void AddRecoil(AnimationPlayer ap)
    {
        if (ap.HasAnimation(RecoilName) || !ap.HasAnimation("Touche") || !ap.HasAnimation("Repos")) return;
        var hit = (Animation)ap.GetAnimation("Touche").Duplicate();
        var idle = ap.GetAnimation("Repos");
        for (int t = 0; t < hit.GetTrackCount(); t++)
        {
            string path = hit.TrackGetPath(t).ToString();
            if (hit.TrackGetType(t) != Animation.TrackType.Rotation3D || !(path.EndsWith(":Bras.D") || path.EndsWith(":Bras.G") || path.EndsWith(":AvantBras.D") || path.EndsWith(":AvantBras.G"))) continue;
            int i = idle.FindTrack(hit.TrackGetPath(t), Animation.TrackType.Rotation3D);
            if (i < 0 || idle.TrackGetKeyCount(i) == 0) continue;
            var down = (Quaternion)idle.TrackGetKeyValue(i, 0);
            while (hit.TrackGetKeyCount(t) > 0) hit.TrackRemoveKey(t, 0);
            hit.RotationTrackInsertKey(t, 0, down);
        }
        if (ap.HasAnimationLibrary("")) ap.GetAnimationLibrary("").AddAnimation(RecoilName, hit);
    }

    const string RecoilName = "Recul";

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

    public override void _EnterTree()
    {
        if (body == null) return;
        Gameplay.MatchController.HitScored += OnHit;
        all.Add(this);
    }

    public override void _ExitTree()
    {
        if (body == null) return;
        Gameplay.MatchController.HitScored -= OnHit;
        all.Remove(this);
    }

    /// <summary>Fait réagir l'avatar de la cible comme sur une touche validée (utilisé par --impact-test).</summary>
    public static void React(int shooterId, int targetId, bool kill)
    {
        foreach (var a in all.ToArray()) if (IsInstanceValid(a)) a.OnHit(shooterId, targetId, kill);
    }

    void OnHit(int shooterId, int targetId, bool kill)
    {
        if (targetId != playerId || playerId < 0 || body == null) return;
        PlayAction(kill ? "KO" : anim?.HasAnimation(RecoilName) == true ? RecoilName : "Repos", kill ? 3.0 : 0.45);
        Flash();
        if (kill) { ghostWindow = 0.25; koTransform = body.GlobalTransform; return; }
        // Recul : à l'opposé du tireur (sinon vers l'arrière du personnage, +Z : il regarde vers -Z).
        Vector3 dir = body.GlobalTransform.Basis.Z;
        foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
            if (n is Gameplay.Hurtbox h && h.PlayerId == shooterId) dir = body.GlobalPosition - h.GlobalPosition;
        dir.Y = 0;
        kickDir = dir.LengthSquared() > 1e-4f ? dir.Normalized() : Vector3.Back;
        kickAge = 0;
    }

    /// <summary>Éclat blanc sur tout le modèle (calque par-dessus les matériaux, le modèle n'est pas modifié).</summary>
    void Flash()
    {
        if (body == null || DisplayServer.GetName() == "headless") return;
        if (meshes.Length == 0)
        {
            var found = new System.Collections.Generic.List<MeshInstance3D>();
            foreach (var n in body.FindChildren("*", "MeshInstance3D", true, false)) if (n is MeshInstance3D mi) found.Add(mi);
            meshes = found.ToArray();
        }
        flashMat ??= new StandardMaterial3D
        {
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded, Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            AlbedoColor = new Color(1, 1, 1, 0.85f),
        };
        foreach (var mi in meshes) if (IsInstanceValid(mi)) mi.MaterialOverlay = flashMat;
        flashUntil = Time.GetTicksMsec() + FlashMs;
    }

    const ulong FlashMs = 110;
    const double KickTime = 0.3;
    const float KickDistance = 0.28f;

    /// <summary>Recul (mètres) à l'instant t : sortie vive en 0,05 s puis retour amorti.</summary>
    static float KickAmount(double t) =>
        t >= KickTime ? 0f : t < 0.05 ? KickDistance * (float)(t / 0.05) : KickDistance * Mathf.Pow(1f - (float)((t - 0.05) / (KickTime - 0.05)), 2f);

    Vector3 KickLocal()
    {
        float k = KickAmount(kickAge);
        if (k <= 0f) return Vector3.Zero;
        return GlobalTransform.Basis.Inverse() * (kickDir * k);
    }

    /// <summary>Copie du modèle laissée sur place, qui joue « KO » puis rétrécit : le joueur, lui, est déjà masqué.</summary>
    void SpawnKoGhost()
    {
        var scene = GetTree()?.CurrentScene;
        if (scene == null || modelName == "" || DisplayServer.GetName() == "headless") return;
        var ghost = ModelLibrary.Instance(modelName);
        if (ghost == null) return;
        scene.AddChild(ghost);
        ghost.GlobalTransform = koTransform;
        GD.Print($"[Avatar] KO du joueur {playerId} : chute jouée par une copie (joueur masqué)");
        if (ghost.FindChild("AnimationPlayer", true, false) is AnimationPlayer ap && ap.HasAnimation("KO")) ap.Play("KO");
        var tw = ghost.CreateTween();
        tw.TweenInterval(0.95);
        tw.TweenProperty(ghost, "scale", new Vector3(0.05f, 0.05f, 0.05f), 0.22).SetEase(Tween.EaseType.In).SetTrans(Tween.TransitionType.Back);
        tw.TweenCallback(Callable.From(ghost.QueueFree));
    }

    /// <summary>Joue une animation ponctuelle (lancer, attrape, esquive, touché, KO) puis revient à la course ou au repos.</summary>
    public void PlayAction(string name, double seconds)
    {
        if (anim == null || !anim.HasAnimation(name)) return;
        // Même geste déjà en cours (élan du bot puis lâcher de la balle) : on le prolonge au lieu de le reprendre au début.
        if (actionLeft > 0 && anim.CurrentAnimation == name) { actionLeft = Mathf.Max(actionLeft, seconds); return; }
        anim.Play(name, 0.08);
        anim.SpeedScale = 1f;
        actionLeft = seconds;
    }

    public override void _Process(double delta)
    {
        if (body == null || anim == null || delta <= 0) return;
        if (flashUntil != 0 && Time.GetTicksMsec() >= flashUntil)
        {
            flashUntil = 0;
            foreach (var mi in meshes) if (IsInstanceValid(mi)) mi.MaterialOverlay = null;
        }
        else if (flashUntil != 0 && flashMat != null)
            flashMat.AlbedoColor = new Color(1, 1, 1, 0.85f * (flashUntil - Time.GetTicksMsec()) / FlashMs);
        if (ghostWindow > 0)
        {
            ghostWindow -= delta;
            if (!IsVisibleInTree()) { ghostWindow = 0; SpawnKoGhost(); }
        }
        if (kickAge < KickTime + 0.1)
        {
            kickAge += delta;
            body.Position = bodyBase + KickLocal();
        }
        // Bot : pas d'appel à Pose, la vitesse (course ou repos) se lit sur le déplacement du corps.
        var bodyGlobal = body.GlobalPosition;
        if (Engine.GetProcessFrames() - lastPoseFrame > 2 && lastBodyGlobal != Vector3.Zero)
        {
            var flat = bodyGlobal - lastBodyGlobal;
            flat.Y = 0;
            speed = Mathf.Lerp(speed, Mathf.Min(flat.Length() / (float)delta, 12f), 0.25f);
        }
        lastBodyGlobal = bodyGlobal;
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
            lastPoseFrame = Engine.GetProcessFrames();
            // pieds au sol même pour un grand joueur (écart de taille), mais le corps suit un vrai saut
            float y = head.Y - HeadHeight;
            bodyBase = new Vector3(head.X, y < 0.25f ? Mathf.Min(0f, y) : y, head.Z);
            body.Position = bodyBase + (IsInsideTree() ? KickLocal() : Vector3.Zero);
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
