using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// --cam-test (carte croix_neon) : le joueur est posé tour à tour contre les couverts qui gênaient la caméra d'épaule
/// (dos au muret, muret à droite, coin du L, dos au pylône, face au pilier, accroupi), 2 s par pose, bots au calme.
/// À chaque image (après 0,6 s de mise en place) : tête visible, objectif hors des décors, centre de l'écran bouché à 30 % au plus
/// par un décor opaque à moins de 2 m (rotor compris). Bilan par pose. Fin : « [CamTest] TOUT OK » ou la liste des poses à revoir.
/// </summary>
public partial class DesktopPlayer
{
    bool camTest;
    int camPose = -1;
    double camPoseAt;
    int camFails;

    record struct CamPose(string Name, float X, float Z, float YawDeg, bool Crouch, bool ScreenMayBeBlocked);

    // Couverts de croix_neon : muret « abri_g_z0 » (x -1,8 → 1,4 en z -7,2), son flanc (x 1,3, z -10,1 → -6,9),
    // pilier_z0 (-4,5 ; -9, 2,2 m de haut), pylône (0 ; 0, 1,6 m de côté, 3,4 m de haut). Lacet 0 = regard vers -Z.
    static readonly CamPose[] CamPoses =
    {
        new("dos au muret",         -0.2f, -6.65f, 180f, false, false),
        new("muret à droite",        0.75f, -8.6f,   0f, false, false),
        new("coin du L",             0.7f, -7.75f,  45f, false, false),
        new("dos au pylône",         0f,    1.35f, 180f, false, false),
        new("face au pilier",       -4.5f, -8.15f,   0f, false, true),    // un pilier à 50 cm devant soi bouche forcément le viseur
        new("accroupi au muret",    -0.2f, -6.65f,   0f, true,  false),
        new("dos au muret, de biais", -1.1f, -6.7f, 150f, false, false),
        new("au bord du rotor",      0f,    3.2f, 180f, false, false),   // la barre rayée balaie l'espace entre la caméra et le joueur
        new("rotor sur le côté",     2.6f,  2.6f, 135f, false, false),
    };
    const double CamPoseTime = 2.0;

    void StartCamTest()
    {
        camTest = true;
        autoplay = true;   // pas de capture de la souris
        Bot.Calm = true;
        GD.Print($"[CamTest] {CamPoses.Length} poses de {CamPoseTime:0.#} s");
    }

    PcInput CamTestInput()
    {
        var input = new PcInput();
        int pose = (int)(time / CamPoseTime);
        if (pose >= CamPoses.Length)
        {
            if (camPose < CamPoses.Length) { camPose = CamPoses.Length; GD.Print(camFails == 0 ? "[CamTest] TOUT OK" : $"[CamTest] {camFails} pose(s) à revoir"); GetTree().Quit(camFails == 0 ? 0 : 1); }
            return input;
        }
        var p = CamPoses[pose];
        if (pose != camPose) { camPose = pose; camPoseAt = time; }
        worldPos = new Vector2(p.X, p.Z);
        velocity = Vector2.Zero;
        yaw = Mathf.DegToRad(p.YawDeg);
        pitch = Mathf.DegToRad(-6f);
        input.Crouch = p.Crouch;
        return input;
    }

    int camBadFrames, camFrames, camWorstPct;
    bool camHeadEverHidden, camEverInDecor;

    /// <summary>
    /// Mesure à chaque image de la pose, après 0,6 s de mise en place (appelée après PlaceCamera) ; bilan en fin de pose.
    /// Image ratée : tête invisible, objectif dans un décor, ou centre de l'écran bouché à plus de 30 % (sauf pose où c'est inévitable).
    /// </summary>
    void CamTestMeasure()
    {
        if (camPose < 0 || camPose >= CamPoses.Length || camPoseAt < 0) return;
        var p = CamPoses[camPose];
        double into = time - camPoseAt;
        if (into < 0.6) { camBadFrames = camFrames = camWorstPct = 0; camHeadEverHidden = camEverInDecor = false; return; }
        var cam = camera.GlobalPosition;
        var space = GetWorld3D().DirectSpaceState;
        var bnd = Zones.Map.Bounds;
        var h = head.GlobalPosition;
        float eye = h.Y - Elevation;
        Vector3[] body = { h, h + Vector3.Down * (0.45f * eye), h + Vector3.Down * (0.65f * eye) };
        // Avatar caché (caméra collée au joueur) = invisible. Derrière un muret bas, buste et hanches peuvent l'être : la tête doit se voir.
        int seen = 0;
        bool headSeen = false;
        if (selfAvatar == null || selfAvatar.Visible)
            for (int k = 0; k < body.Length; k++)
                if (CamTestFirstHit(space, cam, body[k], bnd) < 0f) { seen++; if (k == 0) headSeen = true; }
        // Centre de l'écran (60 % x 60 %), grille 7 x 5 : rayons qui touchent un décor opaque à moins de 2 m de l'objectif.
        var basis = camera.GlobalTransform.Basis;
        float ty = Mathf.Tan(Mathf.DegToRad(camera.Fov) * 0.5f), tx = ty * 16f / 9f;
        int blocked = 0, total = 0;
        for (int i = 0; i < 7; i++)
            for (int j = 0; j < 5; j++)
            {
                var dir = basis * new Vector3(tx * 0.6f * (i / 3f - 1f), ty * 0.6f * (j / 2f - 1f), -1f).Normalized();
                total++;
                if (CamTestFirstHit(space, cam, cam + dir * 2.0f, bnd) >= 0f) blocked++;
            }
        int pct = Mathf.RoundToInt(100f * blocked / total);
        bool inDecor = OutsideDecorFraction(cam, cam) < 1f;   // objectif dans le maillage d'un couvert (écran d'une seule couleur)
        bool bad = !headSeen || inDecor || (!p.ScreenMayBeBlocked && pct > 30);
        camFrames++;
        if (bad) camBadFrames++;
        if (bad && OS.HasEnvironment("CAMTEST_DEBUG")) GD.Print($"  raté : avatar {(selfAvatar?.Visible ?? true)}, vu {seen}, dist {cam.DistanceTo(h):0.00}, rotor fantôme {time < rotorGhostUntil}, couverts {coversThrough.Count}, centre {pct} %, dans décor {inDecor}");
        camWorstPct = Mathf.Max(camWorstPct, pct);
        camHeadEverHidden |= !headSeen;
        camEverInDecor |= inDecor;
        if (into < CamPoseTime - 0.05) return;
        camPoseAt = -1;   // bilan une seule fois
        bool ok = camBadFrames == 0;
        if (!ok) camFails++;
        GD.Print($"[CamTest] {p.Name} : tête ({h.X:0.0} ; {h.Z:0.0}), caméra ({cam.X:0.0} ; {cam.Y:0.0} ; {cam.Z:0.0}) à {cam.DistanceTo(h):0.00} m de la tête, épaule {(sideTarget > 0 ? "droite" : "gauche")}{(overTarget ? ", vue haute" : "")}{(raise > 0.5f ? ", par-dessus" : "")}{(coversThrough.Count > 0 ? $", {coversThrough.Count} couvert(s) en transparence" : "")}, personnage visible {seen}/3 ; sur {camFrames} images : {camBadFrames} ratée(s){(camHeadEverHidden ? ", tête cachée" : "")}{(camEverInDecor ? ", OBJECTIF DANS UN DÉCOR" : "")}, centre bouché au pire à {camWorstPct} % → {(ok ? "OK" : "À REVOIR")}");
    }

    /// <summary>Distance du premier décor plein et opaque (au sens de la caméra) sur le segment a → b, ou -1.</summary>
    float CamTestFirstHit(PhysicsDirectSpaceState3D space, Vector3 a, Vector3 b, Maps.BoundsDef bnd)
    {
        var exclude = new Godot.Collections.Array<Rid>();
        for (int i = 0; i < 8; i++)
        {
            var hit = space.IntersectRay(PhysicsRayQueryParameters3D.Create(a, b, 0xFFFFFFFF, exclude));
            if (hit.Count == 0) break;
            var col = hit["collider"].AsGodotObject();
            bool ghost = col is StaticBody3D sb && coversThrough.Contains(sb);   // couvert rendu translucide : on voit à travers
            if (!ghost && BlocksCamera(col, hit["shape"].AsInt32(), bnd)) return a.DistanceTo(hit["position"].AsVector3());
            exclude.Add(hit["rid"].AsRid());
        }
        // Rotor opaque (pas rendu translucide à cette image) : la caméra l'ignore, le joueur le voit.
        if (time >= rotorGhostUntil && RotorCrosses(a, b, 0f)) return 0f;
        return -1f;
    }
}
