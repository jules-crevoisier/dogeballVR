using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Colonne du menu principal, DA « Affiche » : un aplat d'encre droit sur toute la hauteur à gauche (le décor 3D et les
/// personnages occupent la droite), filet papier épais sur le bord droit doublé d'un filet vert acide, et une trame de
/// points en bas (demi-teinte de sérigraphie) qui s'efface vers le haut.
/// </summary>
public partial class MenuBand : Control
{
    /// <summary>Largeur de la colonne à 1920 px de large (un tiers de l'écran).</summary>
    public const float Width = 680f;

    /// <summary>Abscisse du bord droit (intérieur du filet) à la hauteur y : la colonne est droite.</summary>
    public float EdgeX(float y) => Width;

    public override void _Draw()
    {
        float h = Size.Y;
        DrawRect(new Rect2(Width + 14, 0, 6, h), UiKit.Yellow);             // filet vert acide, en retrait
        DrawRect(new Rect2(0, 0, Width, h), UiKit.VioletMid);                // aplat d'encre
        DrawRect(new Rect2(Width - 2, 0, 10, h), UiKit.Cream);               // filet papier
        // Trame de demi-teinte : points de plus en plus gros vers le bas, sur le tiers inférieur.
        var dot = new Color(UiKit.Cream, 0.06f);
        const float step = 18f;
        for (float y = h * 0.62f; y < h; y += step)
        {
            float k = (y - h * 0.62f) / (h * 0.38f);
            float r = 1f + 4.2f * k;
            for (float x = (y / step) % 2 < 1 ? 9f : 18f; x < Width - 12; x += step) DrawCircle(new Vector2(x, y), r, dot);
        }
    }
}
