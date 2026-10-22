using Godot;
using DogeballVR.Maps;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Décor 3D derrière le menu principal : la carte Toit, deux joueurs posés comme sur une jaquette (l'orange arme un tir,
/// le bleu se prépare à attraper), une balle en vol, et une caméra qui dérive lentement. Libéré quand la partie commence.
/// </summary>
public partial class MenuBackdrop : Node3D
{
    Camera3D camera = null!;
    Node3D? ball, held;
    double t;
    Vector3 ballFrom, ballTo;
    Avatar? hero, rival;

    static readonly Vector3 CamPos = new(-0.6f, 1.5f, -13.0f);
    static readonly Vector3 LookTarget = new(-2.0f, 1.3f, -5.0f);

    public override void _Ready()
    {
        var path = "res://maps/croix_neon.json";
        if (FileAccess.FileExists(path))
        {
            var map = MapData.Load(path);
            var arena = ArenaBuilder.Build(map, -1, true);
            AddChild(arena.Root);
            ClearShot(arena.Root, CamPos, new Vector3(-2.8f, 1.2f, -9.9f));
            // Distributeur juste devant la caméra : son plot hexagonal faisait une grosse dalle grise plate au premier plan.
            foreach (var n in arena.Root.FindChildren("*", "", true, false))
                if (n is PickupBeacon pb && pb.GetParent() is Node3D stand && stand.GlobalPosition.DistanceTo(CamPos) < 7f) stand.Visible = false;
        }

        // Héros orange au premier plan, de trois quarts, balle levée derrière la tête.
        hero = Avatar.Create(0, false);
        AddChild(hero);
        // Rival bleu plus loin, mains en avant pour attraper.
        rival = Avatar.Create(1, false);
        AddChild(rival);
        // Coéquipiers flous au fond.
        var mate = Avatar.Create(0, false);
        AddChild(mate);
        mate.Pose(new Vector3(-5.4f, 1.6f, 1.6f), Mathf.DegToRad(165), new Vector3(-5.8f, 1.0f, 1.3f), new Vector3(-5.0f, 1.1f, 1.2f));
        var other = Avatar.Create(1, false);
        AddChild(other);
        other.Pose(new Vector3(5.0f, 1.6f, 4.5f), Mathf.DegToRad(150), new Vector3(4.6f, 1.0f, 4.3f), new Vector3(5.4f, 1.0f, 4.4f));

        ball = ModelLibrary.Instance("balle_normale");
        if (ball == null)
        {
            var mesh = new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f }, MaterialOverride = Materials.Flat(Palette.BallCore) };
            ball = mesh;
        }
        AddChild(ball);
        held = ModelLibrary.Instance("balle_normale") ?? new MeshInstance3D { Mesh = new SphereMesh { Radius = 0.15f, Height = 0.3f }, MaterialOverride = Materials.Flat(Palette.BallCore) };
        AddChild(held);
        // Au fond, le rival et un coéquipier orange s'échangent des tirs.
        ballFrom = new Vector3(-5.2f, 1.5f, 1.0f);
        ballTo = new Vector3(-6.3f, 1.4f, -3.4f);

        // Personnages animés (modèles riggés) : le héros célèbre en boucle ; la balle jonglée ne sert qu'aux anciens modèles sans squelette.
        if (hero.FindChild("AnimationPlayer", true, false) is AnimationPlayer ap && ap.HasAnimation("Celebration"))
        {
            ap.GetAnimation("Celebration").LoopMode = Animation.LoopModeEnum.Linear;
            hero.PlayAction("Celebration", double.MaxValue);
            held.Visible = false;
        }
        // Au fond, plus personne au repos (le « Repos » du modèle est presque une pose en A, figée de loin) : chacun enchaîne
        // des gestes calés sur la balle. Le coéquipier lance quand elle part, le rival attrape quand elle arrive, et entre deux,
        // ils restent fléchis, prêts (milieu de l'esquive, tenu). Le troisième esquive puis attrape à contretemps.
        Choreograph(mate, (Cycle - ThrowRelease, "Lancer", 0.6), (1.0, "Esquive", 0.36));
        Choreograph(rival, (Flight - CatchAt, "Attrape", 0.4), (0.5, "Esquive", 0.36));
        Choreograph(other, (0.6, "Esquive", 0.4), (2.0, "Attrape", 0.3));

        camera = new Camera3D { Fov = 62f, Current = true, Near = 0.05f };
        AddChild(camera);
        PoseAll(0);
    }

    const double Cycle = 3.2, Flight = 2.4;          // un tir toutes les 3,2 s, 2,4 s de vol
    const double ThrowRelease = 0.48, CatchAt = 0.35;  // instant du lâcher dans « Lancer », de la prise dans « Attrape »

    readonly System.Collections.Generic.List<(AnimationPlayer ap, (double at, string anim, double hold)[] cues)> loops = new();

    /// <summary>
    /// Gestes joués à des instants fixes du cycle de la balle, chacun figé à l'instant « hold » jusqu'au suivant : les
    /// animations du modèle finissent toutes droites, bras le long du corps, et l'on veut des joueurs en action.
    /// </summary>
    void Choreograph(Avatar a, params (double at, string anim, double hold)[] cues)
    {
        if (a.FindChild("AnimationPlayer", true, false) is not AnimationPlayer ap) return;
        foreach (var (_, anim, _) in cues) if (!ap.HasAnimation(anim)) return;
        a.PlayAction(cues[^1].anim, double.MaxValue);   // l'Avatar ne reprend plus la main (repos ou course)
        ap.SpeedScale = 0f;                              // le temps de l'animation est fixé ici, image par image
        System.Array.Sort(cues, (x, y) => x.at.CompareTo(y.at));
        loops.Add((ap, cues));
    }

    void RunLoops(double time)
    {
        double p = time % Cycle;
        foreach (var (ap, cues) in loops)
        {
            var (at, anim, hold) = cues[^1];   // avant le premier repère : le dernier geste du cycle précédent continue
            foreach (var c in cues) if (c.at <= p) (at, anim, hold) = c;
            double since = Mathf.PosMod(p - at, Cycle);
            if (ap.AssignedAnimation != anim) ap.Play(anim);
            ap.Seek(Mathf.Min(since, hold), true);
        }
    }

    void PoseAll(double time)
    {
        float s = Mathf.Sin((float)time * 1.3f);
        // Héros de trois quarts face caméra, à droite de l'écran : il sautille et jongle avec sa balle, prêt à tirer.
        float hop = Mathf.Abs(Mathf.Sin((float)time * 3.4f)) * 0.05f;
        var heroHead = new Vector3(-2.8f, 1.6f + hop + 0.02f * s, -9.9f);
        var yaw = Mathf.DegToRad(-45 + 6 * Mathf.Sin((float)time * 0.7f));
        var basis = new Basis(Vector3.Up, yaw);
        float u = (float)(time % 1.3 / 1.3);                     // un lancer-rattraper par cycle
        float dip = u < 0.15f ? Mathf.Sin(u / 0.15f * Mathf.Pi) : 0f;   // la main plonge puis pousse la balle
        var rightHand = heroHead + basis * new Vector3(0.38f, 0.05f - 0.12f * dip, 0.28f);
        var leftHand = heroHead + basis * new Vector3(-0.42f, -0.28f + 0.04f * s, 0.32f);   // garde haute, poing vers l'adversaire
        hero!.Pose(heroHead, yaw, leftHand, rightHand);
        if (held != null && held.Visible)
        {
            float air = u > 0.12f ? Mathf.Sin((u - 0.12f) / 0.88f * Mathf.Pi) : 0f;
            held.GlobalPosition = rightHand + Vector3.Up * (0.18f + 0.55f * air);
            held.Rotation = new Vector3((float)time * 5f, (float)time * 2f, 0);
        }
        var rivalHead = new Vector3(-6.3f, 1.5f + 0.03f * Mathf.Sin((float)time * 1.7f + 1f), -3.6f);
        rival!.Pose(rivalHead, Mathf.DegToRad(-20), rivalHead + new Vector3(-0.3f, -0.35f, 0.45f), rivalHead + new Vector3(0.3f, -0.35f, 0.45f));
    }

    /// <summary>Cache le décor posé entre la caméra et le héros (borne, muret) : l'image du menu doit rester dégagée.</summary>
    static void ClearShot(Node root, Vector3 eye, Vector3 subject)
    {
        foreach (var n in root.FindChildren("*", "GeometryInstance3D", true, false))
        {
            if (n is not GeometryInstance3D g || !g.Visible) continue;
            var box = g.GlobalTransform * g.GetAabb();
            if (box.Size.Length() > 8f) continue;   // sol, murs d'enceinte, immeubles : jamais
            var c = box.GetCenter();
            var seg = subject - eye;
            float k = Mathf.Clamp((c - eye).Dot(seg) / seg.LengthSquared(), 0f, 1f);
            if (k < 0.98f && (eye + seg * k).DistanceTo(c) < 1.2f + box.Size.Length() * 0.3f) g.Visible = false;
        }
    }

    public override void _Process(double delta)
    {
        t += delta;
        RunLoops(t);
        PoseAll(t);
        // La balle part du coéquipier vers le rival, en boucle (2,4 s de vol toutes les 3,2 s), avec une légère cloche.
        double cycle = (t % Cycle) / Flight;
        if (ball != null)
        {
            if (cycle < 1.0)
            {
                float u = (float)cycle;
                ball.Visible = true;
                ball.GlobalPosition = ballFrom.Lerp(ballTo, u) + Vector3.Up * (Mathf.Sin(u * Mathf.Pi) * 0.6f);
                ball.Rotation += new Vector3(9f, 4f, 0) * (float)delta;
                if (u > 0.08f && Mathf.PosMod((float)t, 0.05f) < delta) Juice.TrailGhost(this, ball.GlobalPosition, new Color(1f, 0.85f, 0.3f), 0.12f);
            }
            else ball.Visible = false;
        }
        // Caméra : lente dérive en huit, jamais immobile.
        var drift = new Vector3(Mathf.Sin((float)t * 0.11f) * 0.9f, Mathf.Sin((float)t * 0.17f) * 0.25f, Mathf.Cos((float)t * 0.09f) * 0.5f);
        camera.GlobalPosition = CamPos + drift;
        camera.LookAt(LookTarget + drift * 0.3f, Vector3.Up);
    }
}
