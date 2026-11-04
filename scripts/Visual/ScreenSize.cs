using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Conversion pixels d'écran ↔ mètres à une profondeur donnée, pour les textes du monde qui gardent une taille stable à l'écran
/// (étiquettes de nom, autocollants « BAM ! »). Les tailles sont exprimées en pixels d'une image de 720 lignes et suivent la résolution.
/// En VR, pas d'écran : on garde une taille en mètres (bornée) pour ne pas coller des textes géants dans le casque.
/// </summary>
public static class ScreenSize
{
    /// <summary>Mètres par pixel « 720p » au point donné, ou -1 si le point est derrière la caméra (ou sans caméra).</summary>
    public static float MetersPerPixel(Camera3D? cam, Vector3 world)
    {
        if (cam == null || !GodotObject.IsInstanceValid(cam)) return -1f;
        float depth = -(cam.GlobalTransform.AffineInverse() * world).Z;
        if (depth < 0.15f) return -1f;
        if (cam is XRCamera3D) return Mathf.Clamp(depth, 1.5f, 12f) * 0.0011f;   // VR : taille angulaire constante jusqu'à 12 m, puis fixe
        var size = cam.GetViewport().GetVisibleRect().Size;
        if (size.Y < 1f) return -1f;
        float fovSpan = cam.KeepAspect == Camera3D.KeepAspectEnum.Width ? size.X : size.Y;
        float worldSpan = 2f * depth * Mathf.Tan(Mathf.DegToRad(cam.Fov) * 0.5f);
        return worldSpan / fovSpan * (size.Y / 720f);
    }

    /// <summary>Le point est-il dans le cadre de la caméra (avec une marge en fraction d'écran) ?</summary>
    public static bool OnScreen(Camera3D? cam, Vector3 world, float margin = 0.05f)
    {
        if (cam == null || !GodotObject.IsInstanceValid(cam) || cam.IsPositionBehind(world)) return false;
        var size = cam.GetViewport().GetVisibleRect().Size;
        var p = cam.UnprojectPosition(world);
        return p.X > -margin * size.X && p.X < size.X * (1f + margin) && p.Y > -margin * size.Y && p.Y < size.Y * (1f + margin);
    }
}
