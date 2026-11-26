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
/// <item>Seulement hors du jeu (avant GO, entre deux manches) : un recalage en pleine manche déplacerait le terrain sous les pieds.
/// Le recentrage système (bouton Meta maintenu) ne touche pas au terrain : à EVA, l'espace « Stage » est la salle.</item>
/// <item>Au lancement, la taille de la zone de jeu Meta (Guardian) est lue si le runtime la donne : plus petite que la zone
/// du jeu, un message prévient que les bords du jeu sont plus loin que les murs réels.</item>
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
    /// <summary>Vrai quand le recalage est permis (hors manche ou pendant le décompte). Nul : toujours.</summary>
    public System.Func<bool>? Allowed { get; init; }

    OpenXRInterface? xr;
    double heldFor;
    int lastStep;
    bool areaChecked;

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
        bool both = Allowed?.Invoke() != false;
        foreach (var h in Hands) both &= h.IsButtonPressed("trigger_click") && h.HeldBall == null && h.Hurtbox.HeldHand < 0;
        if (!both) { heldFor = 0; lastStep = 0; return; }
        heldFor += delta;
        int step = (int)(heldFor / (HoldSeconds / 4));
        if (step > lastStep && step < 4)
        {
            lastStep = step;
            foreach (var h in Hands) h.Pulse(0.2f, 0.03f);
        }
        if (heldFor < HoldSeconds) return;
        heldFor = -1e9;   // une fois par appui : relâcher pour recommencer
        Recenter(L.T("ZONE RECALÉE"));
    }

    void Recenter(string? message)
    {
        XRServer.CenterOnHmd(XRServer.RotationMode.ResetButKeepTilt, true);   // garde la hauteur : le sol reste à 0
        GD.Print("[VR] Zone recalée sur la tête du joueur");
        foreach (var h in Hands) h.Pulse(0.8f, 0.15f, safety: true);
        if (message != null) GetTree().CreateTimer(0.15).Timeout += () => { if (IsInstanceValid(Head)) VrToast.InFront(Head, message, new Color(0.83f, 1f, 0.2f), 2.0); };
    }

    void CheckPlayAreaLater()
    {
        if (areaChecked) return;
        areaChecked = true;
        GetTree().CreateTimer(1.5).Timeout += CheckPlayArea;
    }

    /// <summary>Zone de jeu du système (Guardian) : comparée à la zone du jeu, prévient si elle est plus petite.</summary>
    void CheckPlayArea()
    {
        if (xr == null || !IsInstanceValid(this) || !IsInsideTree()) return;
        var area = xr.GetPlayArea();
        if (area == null || area.Length < 3) { GD.Print("[VR] Zone de jeu du casque inconnue (le runtime ne la donne pas)"); return; }
        var size = PlayAreaSize(area);
        GD.Print($"[VR] Zone de jeu du casque : {size.X:0.0} × {size.Y:0.0} m (zone du jeu {ZoneWidth:0} × {ZoneDepth:0} m)");
        if (!Fits(size, ZoneWidth, ZoneDepth) && IsInstanceValid(Head))
            VrToast.InFront(Head, L.F("ZONE DE JEU {0} × {1} M : LE TERRAIN FAIT {2} × {3} M. RESTE LOIN DES MURS.", size.X.ToString("0.0"), size.Y.ToString("0.0"), ZoneWidth.ToString("0"), ZoneDepth.ToString("0")), new Color(1f, 0.55f, 0.2f), 6.0);
    }

    /// <summary>Encombrement (largeur, profondeur) d'un contour de zone de jeu, sur le plan du sol.</summary>
    public static Vector2 PlayAreaSize(IReadOnlyList<Vector3> points)
    {
        float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        foreach (var p in points) { minX = Mathf.Min(minX, p.X); maxX = Mathf.Max(maxX, p.X); minZ = Mathf.Min(minZ, p.Z); maxZ = Mathf.Max(maxZ, p.Z); }
        return new Vector2(maxX - minX, maxZ - minZ);
    }

    /// <summary>La zone du jeu tient-elle dans la zone de jeu réelle (dans un sens ou dans l'autre) ?</summary>
    public static bool Fits(Vector2 area, float width, float depth) =>
        (area.X >= width - 0.05f && area.Y >= depth - 0.05f) || (area.X >= depth - 0.05f && area.Y >= width - 0.05f);
}
