using System.Collections.Generic;
using Godot;
using DogeballVR.UI;

namespace DogeballVR.Xr;

/// <summary>
/// Terrain calé sur la zone réelle, comme à EVA : le joueur se déplace en vrai (pas de stick), l'origine VR est posée au centre
/// de sa zone de 15 × 8 m et tournée vers l'adversaire (Main.BuildLocalPlayer).
/// <list type="bullet">
/// <item>Recalage : debout au centre de sa zone réelle, regard vers l'adversaire, les deux gâchettes tenues 2 s sans balle.
/// Le jeu se recale sur la tête (le sol reste au sol) ; vibration et message de confirmation.</item>
/// <item>Seulement entre deux manches (ni pendant le décompte ni en pleine manche, chez l'hôte comme chez le client), et seulement
/// comme retouche : l'écart <b>total</b> au repère de la salle (« Stage », calé par l'opérateur) reste sous 0,5 m et 5°, quel que soit
/// le nombre de recalages. Au-delà, refusé : 5° décalent déjà le coin d'une zone 15 × 8 de 0,75 m.
/// Le recentrage système (bouton Meta maintenu) ne touche pas au terrain : à EVA, l'espace « Stage » est la salle.</item>
/// <item>Au lancement, le contour de la zone de jeu Meta (Guardian) est lu si le runtime le donne : si un coin du terrain en
/// sort, un message prévient que les bords du jeu sont plus loin que les murs réels. Le recalage refuse aussi une tête penchée
/// de plus de 60° (cap indéfini).</item>
/// </list>
/// </summary>
public partial class ZoneCalibration : Node
{
    /// <summary>Temps de gâchettes tenues pour recaler (s).</summary>
    public const double HoldSeconds = 2.0;

    public Node3D Head { get; init; } = null!;
    public IReadOnlyList<Gameplay.HandThrower> Hands { get; init; } = System.Array.Empty<Gameplay.HandThrower>();
    public float ZoneWidth { get; init; } = 15f;
    public float ZoneDepth { get; init; } = 8f;
    /// <summary>Vrai quand le recalage est permis : entre deux manches seulement (ni décompte ni manche en cours). Nul : toujours.</summary>
    public System.Func<bool>? Allowed { get; init; }

    OpenXRInterface? xr;
    static readonly StringName Trigger = "trigger_click";
    double heldFor;
    int lastStep, areaTries;
    bool fired;

    public override void _Ready()
    {
        xr = XRServer.FindInterface("OpenXR") as OpenXRInterface;
        if (xr == null) return;
        xr.SessionFocussed += CheckPlayAreaLater;
    }

    public override void _ExitTree()
    {
        if (xr == null) return;
        xr.SessionFocussed -= CheckPlayAreaLater;
    }

    public override void _Process(double delta)
    {
        if (Hands.Count < 2) return;
        bool both = true;
        for (int i = 0; i < Hands.Count; i++) { var h = Hands[i]; both &= h.IsButtonPressed(Trigger) && h.HeldBall == null && h.Hurtbox.HeldHand < 0; }
        both = both && Allowed?.Invoke() != false;
        if (!both) { heldFor = 0; lastStep = 0; fired = false; return; }
        if (fired) return;   // une fois par appui : relâcher pour recommencer
        heldFor += delta;
        int step = (int)(heldFor / (HoldSeconds / 4));
        if (step > lastStep && step < 4)
        {
            lastStep = step;
            for (int i = 0; i < Hands.Count; i++) Hands[i].Pulse(0.2f, 0.03f);
        }
        if (heldFor < HoldSeconds) return;
        fired = true;
        var (shift, yaw) = TotalAfterRecenter(XRServer.GetReferenceFrame(), Head.Transform, (float)XRServer.WorldScale);
        if (!CanRecenter(shift, yaw))
        {
            // Retouche seulement, bornée par rapport à la salle : des recalages successifs ne peuvent pas faire dériver le terrain.
            GD.Print($"[VR] Recalage refusé : le terrain bougerait de {shift:0.00} m et {yaw:0} ° par rapport à la salle");
            for (int i = 0; i < Hands.Count; i++) Hands[i].Pulse(0.3f, 0.2f);
            VrToast.InFront(Head, L.T("RECALAGE REFUSÉ : VA AU CENTRE DE TA ZONE, FACE À L'ADVERSAIRE"), new Color(1f, 0.55f, 0.2f), 3.0);
            return;
        }
        Recenter(L.T("ZONE RECALÉE"));
    }

    void Recenter(string? message)
    {
        XRServer.CenterOnHmd(XRServer.RotationMode.ResetButKeepTilt, true);   // garde la hauteur : le sol reste à 0
        GD.Print("[VR] Zone recalée sur la tête du joueur");
        for (int i = 0; i < Hands.Count; i++) Hands[i].Pulse(0.8f, 0.15f, safety: true);
        if (message != null) GetTree().CreateTimer(0.15).Timeout += () => { if (IsInstanceValid(Head)) VrToast.InFront(Head, message, new Color(0.83f, 1f, 0.2f), 2.0); };
    }

    void CheckPlayAreaLater()
    {
        if (areaTries > 0) return;
        areaTries = 1;
        GetTree().CreateTimer(1.5).Timeout += CheckPlayArea;
    }

    /// <summary>
    /// Zone de jeu du système (Guardian) : les quatre coins du terrain (repère de la salle) doivent être dans son contour, à
    /// <see cref="CornerTolerance"/> près ; sinon un message prévient. Contour pas encore prêt : jusqu'à 4 essais espacés de 2 s.
    /// </summary>
    void CheckPlayArea()
    {
        if (xr == null || !IsInstanceValid(this) || !IsInsideTree()) return;
        var area = xr.GetPlayArea();
        if (area == null || area.Length < 3)
        {
            if (++areaTries <= 4) { GetTree().CreateTimer(2.0).Timeout += CheckPlayArea; return; }
            GD.Print("[VR] Zone de jeu du casque inconnue (le runtime ne la donne pas)");
            return;
        }
        var size = PlayAreaSize(area);
        bool inside = ZoneInside(area, ZoneWidth, ZoneDepth, XRServer.GetReferenceFrame());
        GD.Print($"[VR] Zone de jeu du casque : {size.X:0.0} × {size.Y:0.0} m (zone du jeu {ZoneWidth:0} × {ZoneDepth:0} m) : {(inside ? "le terrain tient dedans" : "le terrain dépasse")}");
        if (!inside && IsInstanceValid(Head))
            VrToast.InFront(Head, L.F("ZONE DE JEU {0} × {1} M : LE TERRAIN FAIT {2} × {3} M. RESTE LOIN DES MURS.", size.X.ToString("0.0"), size.Y.ToString("0.0"), ZoneWidth.ToString("0"), ZoneDepth.ToString("0")), new Color(1f, 0.55f, 0.2f), 6.0);
    }

    /// <summary>Écart total maximal (m, degrés) entre le terrain recalé et le repère de la salle (« Stage »).</summary>
    public const float MaxShift = 0.5f, MaxYawDegrees = 5f;

    /// <summary>Le recalage n'est qu'une retouche : refusé si le terrain s'écarterait trop du repère de la salle.</summary>
    public static bool CanRecenter(float shift, float yawDegrees) => shift <= MaxShift && yawDegrees <= MaxYawDegrees;

    /// <summary>
    /// Écart au repère de la salle (m au sol, degrés de cap) qu'aurait le terrain si on recalait maintenant. Godot place la tête en
    /// <c>referenceFrame * brut</c> (position brute × échelle) et CenterOnHmd pose <c>referenceFrame = inverse(tête brute à plat)</c> :
    /// l'écart total est donc la position et le cap de la tête dans l'espace brut, quel que soit le nombre de recalages déjà faits.
    /// </summary>
    public static (float shift, float yawDegrees) TotalAfterRecenter(Transform3D referenceFrame, Transform3D headLocal, float worldScale = 1f)
    {
        var raw = referenceFrame.AffineInverse() * headLocal;
        float s = Mathf.Max(worldScale, 1e-3f);
        float shift = new Vector2(raw.Origin.X, raw.Origin.Z).Length() / s;
        var fwd = -raw.Basis.Z;
        // Tête penchée de plus de 60° (regard au sol ou au plafond) : le cap n'a plus de sens, CenterOnHmd tournerait le terrain au hasard.
        if (fwd.X * fwd.X + fwd.Z * fwd.Z < 0.25f) return (shift, 180f);
        float yaw = Mathf.RadToDeg(Mathf.Abs(Mathf.Atan2(-fwd.X, -fwd.Z)));
        return (shift, yaw);
    }

    /// <summary>Encombrement (largeur, profondeur) d'un contour de zone de jeu, sur le plan du sol.</summary>
    public static Vector2 PlayAreaSize(IReadOnlyList<Vector3> points)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in points) { minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X); minZ = Mathf.Min(minZ, p.Z); maxZ = Mathf.Max(maxZ, p.Z); }
        return new Vector2(maxX - minX, maxZ - minZ);
    }

    /// <summary>La zone du jeu tient-elle dans la zone de jeu réelle (dans un sens ou dans l'autre, à <see cref="FitTolerance"/> près) ?</summary>
    public static bool Fits(Vector2 area, float width, float depth) =>
        (area.X >= width - FitTolerance && area.Y >= depth - FitTolerance) || (area.X >= depth - FitTolerance && area.Y >= width - FitTolerance);

    /// <summary>Le Quest donne le plus grand rectangle inscrit dans le Guardian, un peu plus petit que la zone tracée : 0,5 m de marge.</summary>
    public const float FitTolerance = 0.5f;

    /// <summary>Un coin du terrain peut dépasser le contour Guardian de cette distance (m) : rectangle inscrit, tracé à la main.</summary>
    public const float CornerTolerance = 0.25f;

    /// <summary>
    /// Les quatre coins du terrain (largeur en X, profondeur en Z, centré sur l'origine : repère de la zone) sont-ils dans le contour
    /// de la zone de jeu (espace de suivi brut) ? Les coins passent par l'inverse du recalage (<paramref name="referenceFrame"/>).
    /// </summary>
    public static bool ZoneInside(IReadOnlyList<Vector3> polygon, float width, float depth, Transform3D referenceFrame)
    {
        var inv = referenceFrame.AffineInverse();
        for (int i = 0; i < 4; i++)
        {
            var c = inv * new Vector3((i & 1) == 0 ? -width / 2 : width / 2, 0, (i & 2) == 0 ? -depth / 2 : depth / 2);
            var p = new Vector2(c.X, c.Z);
            if (!InPolygon(polygon, p) && DistanceToPolygon(polygon, p) > CornerTolerance) return false;
        }
        return true;
    }

    static bool InPolygon(IReadOnlyList<Vector3> poly, Vector2 p)
    {
        bool inside = false;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = new(poly[i].X, poly[i].Z), b = new(poly[j].X, poly[j].Z);
            if ((a.Y > p.Y) != (b.Y > p.Y) && p.X < (b.X - a.X) * (p.Y - a.Y) / (b.Y - a.Y) + a.X) inside = !inside;
        }
        return inside;
    }

    static float DistanceToPolygon(IReadOnlyList<Vector3> poly, Vector2 p)
    {
        float best = float.MaxValue;
        for (int i = 0, j = poly.Count - 1; i < poly.Count; j = i++)
        {
            Vector2 a = new(poly[j].X, poly[j].Z), b = new(poly[i].X, poly[i].Z);
            var ab = b - a;
            float t = ab.LengthSquared() < 1e-8f ? 0f : Mathf.Clamp((p - a).Dot(ab) / ab.LengthSquared(), 0f, 1f);
            best = Mathf.Min(best, p.DistanceTo(a + ab * t));
        }
        return best;
    }
}
