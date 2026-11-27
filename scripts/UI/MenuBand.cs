using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Bandeau plein du menu : part du bord gauche, bord droit en biais (plus large en haut), dégradé violet profond vers
/// marine, liseré crème épais, ombre et bande violette en retrait derrière, comme les panneaux de Knockout City.
/// </summary>
public partial class MenuBand : Control
{
    const float TopW = 740f, BottomW = 540f;   // laisse une grande fenêtre sur l'arène 3D à droite
    static readonly Color CarbonTop = new("0b1222", 0.94f), CarbonMid = new("080d1a", 0.94f), CarbonLow = new("060a14", 0.96f);

    /// <summary>Abscisse du bord droit (intérieur du filet lumineux) à la hauteur y.</summary>
    public float EdgeX(float y) => Mathf.Lerp(TopW, BottomW, Mathf.Clamp(y / Mathf.Max(Size.Y, 1f), 0f, 1f));

    public override void _Draw()
    {
        float h = Size.Y;
        Vector2[] Strip(float a, float b) => new[] { new Vector2(TopW + a, 0), new Vector2(TopW + b, 0), new Vector2(BottomW + b, h), new Vector2(BottomW + a, h) };
        // Halo subtil bleu-cyan derrière la coupe diagonale
        DrawColoredPolygon(Strip(4, 28), new Color(UiKit.Blue, 0.14f));
        DrawColoredPolygon(Strip(0, 12), new Color(0f, 0f, 0f, 0.55f));
        // Panneau carbone principal
        DrawPolygon(new[] { Vector2.Zero, new Vector2(TopW, 0), new Vector2(BottomW, h), new Vector2(0, h) },
            new[] { CarbonTop, CarbonMid, CarbonLow, CarbonLow });
        // Filet lumineux rasoir Orange → Volt sur l'arête diagonale
        DrawPolygon(Strip(-4, 1), new[] { UiKit.Orange, UiKit.Orange, UiKit.Yellow, UiKit.Yellow });
        // Subtils reflets architecturaux internes
        DrawColoredPolygon(Strip(-110, -76), new Color(1, 1, 1, 0.025f));
        DrawColoredPolygon(new[] { new Vector2(0, h - 88f), new Vector2(BottomW - 4, h - 88f), new Vector2(BottomW - 4, h - 86f), new Vector2(0, h - 86f) }, new Color(1, 1, 1, 0.08f));
    }
}
