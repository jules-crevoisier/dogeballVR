using Godot;

namespace DogeballVR.Xr;

/// <summary>
/// Essai maison (<c>--essai-maison</c>) : recale le jeu sur la tête du joueur. OpenXR démarre en espace « Stage » : sans
/// recentrage, le point de départ est le centre du Guardian et l'avant du jeu suit l'orientation du Guardian, pas le regard.
/// Recentre 1 s après le lancement et au premier focus de la session, puis seulement quand le joueur le demande (bouton
/// Meta maintenu) : rouvrir le menu Meta en plein test ne déplace pas le pad sous ses pieds.
/// </summary>
public partial class HomeTestRecenter : Node
{
    OpenXRInterface? xr;
    bool focused;

    public override void _Ready()
    {
        xr = XRServer.FindInterface("OpenXR") as OpenXRInterface;
        if (xr == null) return;
        xr.SessionFocussed += FirstFocus;
        xr.PoseRecentered += Center;
        Later();
    }

    public override void _ExitTree()
    {
        if (xr == null) return;
        xr.SessionFocussed -= FirstFocus;
        xr.PoseRecentered -= Center;
    }

    void FirstFocus()
    {
        if (focused) return;
        focused = true;
        Later();
    }

    void Later() => GetTree().CreateTimer(1.0).Timeout += () => { if (IsInstanceValid(this) && IsInsideTree()) Center(); };

    static void Center()
    {
        XRServer.CenterOnHmd(XRServer.RotationMode.ResetButKeepTilt, true);   // garde la hauteur : le sol reste à 0
        GD.Print("[Essai maison] Jeu recentré sur la tête du joueur");
    }
}
