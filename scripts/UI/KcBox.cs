using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Fond d'interface dans la DA du menu principal (bandeau de Knockout City) : parallélogramme penché, dégradé vertical,
/// liseré épais, ombre portée décalée et bande de couleur en retrait derrière. Sert aux fenêtres (UiKit.PanelBox),
/// aux boutons (UiKit.Button : barre jaune au focus, comme les items du menu) et aux champs.
/// </summary>
public partial class KcBox : StyleBox
{
    /// <summary>Dégradé de remplissage, du haut vers le bas.</summary>
    [Export] public Color Top { get; set; } = new("2e1260");
    [Export] public Color Bottom { get; set; } = new("121a3c");
    /// <summary>Liseré (0 : aucun).</summary>
    [Export] public float BorderWidth { get; set; }
    [Export] public Color BorderColor { get; set; } = UiKit.Cream;
    /// <summary>Décalage horizontal du haut par rapport au bas (px) : positif, le haut part vers la droite comme le texte penché.</summary>
    [Export] public float Slant { get; set; } = 14f;
    /// <summary>Ombre portée (décalage et couleur ; alpha 0 : aucune).</summary>
    [Export] public Vector2 ShadowOffset { get; set; } = new(7, 7);
    [Export] public Color ShadowColor { get; set; } = new(0.02f, 0.03f, 0.1f, 0f);
    /// <summary>Bande en retrait derrière (comme la seconde bande violette du menu ; alpha 0 : aucune).</summary>
    [Export] public Vector2 StripOffset { get; set; } = new(22, 22);
    [Export] public Color StripColor { get; set; } = new(0.35f, 0.1f, 0.54f, 0f);
    /// <summary>Accent à gauche (barre penchée de la couleur du bouton ; alpha 0 : aucun).</summary>
    [Export] public float AccentWidth { get; set; } = 8f;
    [Export] public Color AccentColor { get; set; } = new(1, 1, 1, 0);
    /// <summary>Étoile marine devant le texte (« ✦ JOUER »), comme sur la barre jaune du menu.</summary>
    [Export] public bool Star { get; set; }

    public KcBox Copy() => (KcBox)Duplicate();

    static Vector2[] Quad(Rect2 r, float s) => new[]
    {
        r.Position + new Vector2(s, 0), r.Position + new Vector2(r.Size.X, 0),
        r.Position + new Vector2(r.Size.X - s, r.Size.Y), r.Position + new Vector2(0, r.Size.Y),
    };

    public override void _Draw(Rid ci, Rect2 rect)
    {
        var rs = RenderingServer.Singleton;
        // Le biais ne doit pas dépasser la moitié de la largeur (petits boutons, cases).
        float s = Mathf.Clamp(Slant, -rect.Size.X * 0.25f, rect.Size.X * 0.25f);
        void Poly(Vector2[] pts, Color c) { if (c.A > 0.001f) rs.CanvasItemAddPolygon(ci, pts, new[] { c }); }
        if (StripColor.A > 0f) Poly(Quad(new Rect2(rect.Position + StripOffset, rect.Size), s), StripColor);
        if (ShadowColor.A > 0f) Poly(Quad(new Rect2(rect.Position + ShadowOffset, rect.Size), s), ShadowColor);
        var inner = rect.Grow(-Mathf.Max(BorderWidth, 0f));
        if (inner.Size.X <= 1f || inner.Size.Y <= 1f) return;
        var q = Quad(inner, s * inner.Size.Y / Mathf.Max(rect.Size.Y, 1f));
        if (BorderWidth > 0f)
        {
            // Liseré en anneau (4 trapèzes) : un remplissage translucide ne doit pas laisser voir du crème dessous.
            var o = Quad(rect, s);
            for (int k = 0; k < 4; k++) Poly(new[] { o[k], o[(k + 1) % 4], q[(k + 1) % 4], q[k] }, BorderColor);
        }
        if (Top.A > 0.001f || Bottom.A > 0.001f) rs.CanvasItemAddPolygon(ci, q, new[] { Top, Top, Bottom, Bottom });
        if (AccentColor.A > 0f)
        {
            float k = s * inner.Size.Y / Mathf.Max(rect.Size.Y, 1f);
            var a = inner.Position;
            float w = AccentWidth, h = inner.Size.Y;
            Poly(new[] { a + new Vector2(k, 0), a + new Vector2(k + w, 0), a + new Vector2(w, h), a + new Vector2(0, h) }, AccentColor);
        }
        if (Star && inner.Size.X > 60f)
        {
            var c = inner.Position + new Vector2(Mathf.Abs(s) + 20f, inner.Size.Y / 2f);
            float r = Mathf.Min(inner.Size.Y * 0.24f, 13f), m = r * 0.3f;
            Poly(new[] { c + new Vector2(0, -r), c + new Vector2(m, -m), c + new Vector2(r, 0), c + new Vector2(m, m), c + new Vector2(0, r), c + new Vector2(-m, m), c + new Vector2(-r, 0), c + new Vector2(-m, -m) }, UiKit.Navy);
        }
    }
}
