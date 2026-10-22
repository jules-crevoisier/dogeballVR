using Godot;

namespace DogeballVR.Xr;

/// <summary>
/// Gant du joueur VR local, posé sur la pose « grip » de la manette (la paume) plutôt que sur la pose par défaut
/// (pointe de la manette, que suivent les HandThrower). Le nom de la pose dépend de l'interface XR (« grip » ou
/// « grip_pose ») : il est choisi dès que le tracker le publie ; d'ici là, le gant suit la pose par défaut.
/// </summary>
public partial class GripGlove : XRController3D
{
    bool resolved;

    public override void _Process(double delta)
    {
        if (resolved) return;
        if (XRServer.GetTracker(Tracker) is not XRPositionalTracker tracker) return;
        // Tant que la pose grip n'est pas publiée, le gant reste sur la pose par défaut du nœud.
        foreach (var name in new[] { "grip", "grip_pose" })
            if (tracker.HasPose(name)) { Pose = name; resolved = true; return; }
    }
}
