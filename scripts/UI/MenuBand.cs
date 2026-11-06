using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Bandeau plein du menu, dans la DA franche de Knockout City (DA v3, 06/11) : grand panneau violet saturé qui part du bord
/// gauche, bord droit en biais, liseré crème épais, large bande turquoise en retrait, trame de points (BD) dans le bas,
/// et cadre turquoise de l'écran (bandeau du haut à droite, bande du bas) : le menu n'est plus un calque sombre posé sur
/// la rue, c'est une vraie page d'interface, comme celles de KC.
/// </summary>
public partial class MenuBand : Control
{
    const float TopW = 820f, BottomW = 580f;   // à 1920 px de large : ~40 % de l'écran, la droite reste aux personnages
    /// <summary>Hauteur de la bande turquoise du bas de l'écran (les aides et la version y sont posées).</summary>
    public const float FooterH = 64f;

    /// <summary>Abscisse du bord droit (intérieur du liseré) à la hauteur y.</summary>
    public float EdgeX(float y) => Mathf.Lerp(TopW, BottomW, Mathf.Clamp(y / Mathf.Max(Size.Y, 1f), 0f, 1f));

    public override void _Draw()
    {
        float w = Size.X, h = Size.Y;
        var tealDark = UiKit.Teal.Darkened(0.35f);

        // Cadre turquoise de l'écran : bande du bas sur toute la largeur, bandeau penché en haut à droite.
        DrawColoredPolygon(new[] { new Vector2(0, h - FooterH - 10), new Vector2(w, h - FooterH + 6), new Vector2(w, h), new Vector2(0, h) }, tealDark);
        DrawColoredPolygon(new[] { new Vector2(0, h - FooterH), new Vector2(w, h - FooterH + 16), new Vector2(w, h), new Vector2(0, h) }, UiKit.Teal);
        float tx = TopW + 140f;
        DrawColoredPolygon(new[] { new Vector2(tx, 0), new Vector2(w, 0), new Vector2(w, 86), new Vector2(tx + 40, 70) }, tealDark);
        DrawColoredPolygon(new[] { new Vector2(tx + 14, 0), new Vector2(w, 0), new Vector2(w, 72), new Vector2(tx + 50, 58) }, UiKit.Teal);
        DrawColoredPolygon(new[] { new Vector2(tx + 70, 0), new Vector2(w, 0), new Vector2(w, 46), new Vector2(tx + 92, 36) }, UiKit.Violet);

        // Bande parallèle au bord, entre deux écarts (négatifs : vers l'intérieur).
        Vector2[] Strip(float a, float b) => new[] { new Vector2(TopW + a, 0), new Vector2(TopW + b, 0), new Vector2(BottomW + b, h), new Vector2(BottomW + a, h) };
        DrawColoredPolygon(Strip(40, 96), new Color(0, 0, 0, 0.35f));      // ombre franche
        DrawColoredPolygon(Strip(22, 84), UiKit.Teal);                       // large bande turquoise en retrait, comme les menus de KC
        DrawColoredPolygon(Strip(22, 34), tealDark);
        DrawColoredPolygon(Strip(-4, 22), UiKit.Cream);                      // liseré crème épais
        DrawPolygon(new[] { Vector2.Zero, new Vector2(TopW, 0), new Vector2(BottomW, h), new Vector2(0, h) },
            new[] { UiKit.VioletTop, UiKit.VioletMid, UiKit.NavyLow, UiKit.NavyLow });

        // Grand aplat en biais plus clair (reflet) et trame de points qui grossit vers le bas : l'énergie BD de KC.
        DrawColoredPolygon(new[] { new Vector2(0, h * 0.18f), new Vector2(TopW - 60, 0), new Vector2(TopW - 10, 0), new Vector2(0, h * 0.3f) }, new Color(1, 1, 1, 0.05f));
        const float step = 22f;
        for (float y = h * 0.55f; y < h; y += step)
        {
            float t = (y - h * 0.55f) / (h * 0.45f);
            float r = Mathf.Lerp(0.6f, 5.2f, t);
            float edge = EdgeX(y) - 30f;
            for (float x = ((int)(y / step) % 2) * step / 2; x < edge; x += step)
                DrawCircle(new Vector2(x, y), r, new Color(0, 0, 0, 0.16f));
        }
    }
}
