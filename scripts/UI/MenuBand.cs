using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Bandeau plein du menu : part du bord gauche, bord droit en biais (plus large en haut), dégradé violet profond vers
/// marine, liseré crème épais, ombre et bande violette en retrait derrière, comme les panneaux de Knockout City.
/// </summary>
public partial class MenuBand : Control
{
    const float TopW = 800f, BottomW = 560f;   // à 1920 px de large : ~40 % de l'écran, la droite reste aux personnages
    static readonly Color VioletTop = new("2e1260"), VioletMid = new("221447"), NavyLow = new("121a3c"), Violet = new("5a1a8a");

    /// <summary>Abscisse du bord droit (intérieur du liseré) à la hauteur y.</summary>
    public float EdgeX(float y) => Mathf.Lerp(TopW, BottomW, Mathf.Clamp(y / Mathf.Max(Size.Y, 1f), 0f, 1f));

    public override void _Draw()
    {
        float h = Size.Y;
        // Bande parallèle au bord, entre deux écarts (négatifs : vers l'intérieur).
        Vector2[] Strip(float a, float b) => new[] { new Vector2(TopW + a, 0), new Vector2(TopW + b, 0), new Vector2(BottomW + b, h), new Vector2(BottomW + a, h) };
        DrawColoredPolygon(Strip(36, 74), new Color(Violet, 0.7f));        // seconde bande violette, en retrait
        DrawColoredPolygon(Strip(14, 30), new Color(UiKit.Navy, 0.5f));    // ombre du liseré
        DrawColoredPolygon(Strip(-2, 16), UiKit.Cream);                     // liseré crème
        DrawPolygon(new[] { Vector2.Zero, new Vector2(TopW, 0), new Vector2(BottomW, h), new Vector2(0, h) },
            new[] { VioletTop, VioletMid, NavyLow, NavyLow });
        // Aplats graphiques en biais à l'intérieur : un reflet clair et une bande plus sombre, pour l'énergie BD.
        DrawColoredPolygon(Strip(-150, -96), new Color(1, 1, 1, 0.035f));
        DrawColoredPolygon(new[] { new Vector2(0, h * 0.78f), new Vector2(BottomW + 40, h * 0.62f), new Vector2(BottomW + 40, h * 0.62f + 34), new Vector2(0, h * 0.78f + 34) }, new Color(0, 0, 0, 0.12f));
    }
}
