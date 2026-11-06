using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Fond plein du menu principal (DA v3) : page turquoise rayée en éventail depuis le héros, grand panneau violet penché à
/// liseré crème derrière lui, comme les pages de Knockout City (le personnage en pied sur un aplat de couleur, pas sur un
/// décor 3D). Dessiné une fois par taille d'écran.
/// </summary>
public partial class MenuStage : Control
{
    /// <summary>Centre du héros, en fraction de l'écran : l'éventail de rayons part de là.</summary>
    public static readonly Vector2 HeroAt = new(0.6f, 0.55f);

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        DrawRect(new Rect2(Vector2.Zero, Size), UiKit.Teal);
        // Éventail de rayons plus sombres, très larges : l'énergie BD sans bruit.
        var c = new Vector2(w * HeroAt.X, h * HeroAt.Y);
        float far = w * 1.5f;
        var ray = UiKit.Teal.Darkened(0.12f);
        for (int i = 0; i < 18; i++)
        {
            float a0 = i * Mathf.Tau / 18f, a1 = a0 + Mathf.Tau / 36f;
            DrawColoredPolygon(new[] { c, c + Vector2.FromAngle(a0) * far, c + Vector2.FromAngle(a1) * far }, ray);
        }
        // Grand panneau violet penché derrière le héros : ombre franche, liseré crème épais, dégradé.
        float x0 = w * 0.47f, x1 = w * 0.73f, y0 = h * 0.12f, y1 = h * 0.94f, sl = w * 0.05f;
        Vector2[] Slab(float g, Vector2 o) => new[]
        {
            new Vector2(x0 + sl - g, y0 - g) + o, new Vector2(x1 + g, y0 - g) + o, new Vector2(x1 - sl + g, y1 + g) + o, new Vector2(x0 - g, y1 + g) + o,
        };
        DrawColoredPolygon(Slab(12, new Vector2(18, 20)), new Color(0, 0, 0, 0.3f));
        DrawColoredPolygon(Slab(12, Vector2.Zero), UiKit.Cream);
        DrawPolygon(Slab(0, Vector2.Zero), new[] { UiKit.VioletTop, UiKit.VioletTop, UiKit.NavyLow, UiKit.NavyLow });
        // Halo jaune et orange derrière le buste (couleur de l'équipe du héros).
        DrawCircle(new Vector2((x0 + x1) / 2, h * 0.42f), h * 0.2f, new Color(UiKit.Yellow, 0.16f));
        DrawCircle(new Vector2((x0 + x1) / 2, h * 0.42f), h * 0.13f, new Color(UiKit.Orange, 0.3f));
    }
}
