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
    [Export] public Color Top { get; set; } = new("0d1426");
    [Export] public Color Bottom { get; set; } = new("070b16");
    /// <summary>Liseré (0 : aucun).</summary>
    [Export] public float BorderWidth { get; set; }
    [Export] public Color BorderColor { get; set; } = new(1f, 1f, 1f, 0.16f);
    /// <summary>Décalage horizontal du haut par rapport au bas (px) : positif, le haut part vers la droite.</summary>
    [Export] public float Slant { get; set; } = 8f;
    /// <summary>Ombre portée (décalage et couleur ; alpha 0 : aucune).</summary>
    [Export] public Vector2 ShadowOffset { get; set; } = new(0, 6);
    [Export] public Color ShadowColor { get; set; } = new(0.01f, 0.02f, 0.06f, 0f);
    /// <summary>Bande en retrait derrière (alpha 0 : aucune).</summary>
    [Export] public Vector2 StripOffset { get; set; } = new(10, 10);
    [Export] public Color StripColor { get; set; } = new(0.12f, 0.65f, 1f, 0f);
    /// <summary>Accent à gauche (barre penchée de la couleur du bouton ; alpha 0 : aucun).</summary>
    [Export] public float AccentWidth { get; set; } = 5f;
    [Export] public Color AccentColor { get; set; } = new(1, 1, 1, 0);
    /// <summary>Chevron athlétique devant le texte sur les éléments sélectionnés.</summary>
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
        // Le biais ne doit pas dépasser un quart de la largeur (petits boutons, cases).
        float s = Mathf.Clamp(Slant, -rect.Size.X * 0.2f, rect.Size.X * 0.2f);
        void Poly(Vector2[] pts, Color c) { if (c.A > 0.001f) rs.CanvasItemAddPolygon(ci, pts, new[] { c }); }
        if (StripColor.A > 0f) Poly(Quad(new Rect2(rect.Position + StripOffset, rect.Size), s), StripColor);
        if (ShadowColor.A > 0f) Poly(Quad(new Rect2(rect.Position + ShadowOffset, rect.Size), s), ShadowColor);
        var inner = rect.Grow(-Mathf.Max(BorderWidth, 0f));
        if (inner.Size.X <= 1f || inner.Size.Y <= 1f) return;
        float k = s * inner.Size.Y / Mathf.Max(rect.Size.Y, 1f);
        var q = Quad(inner, k);
        if (BorderWidth > 0f)
        {
            var o = Quad(rect, s);
            for (int i = 0; i < 4; i++) Poly(new[] { o[i], o[(i + 1) % 4], q[(i + 1) % 4], q[i] }, BorderColor);
        }
        if (Top.A > 0.001f || Bottom.A > 0.001f)
        {
            rs.CanvasItemAddPolygon(ci, q, new[] { Top, Top, Bottom, Bottom });
            // Fin filet spéculaire en haut pour un rendu verre fumé / broadcast propre.
            if (inner.Size.Y > 12f)
            {
                float hi = 1.5f;
                float kh = s * hi / Mathf.Max(rect.Size.Y, 1f);
                Poly(new[]
                {
                    q[0], q[1],
                    q[1] + new Vector2(-kh, hi), q[0] + new Vector2(-kh, hi)
                }, new Color(1f, 1f, 1f, 0.12f));
            }
        }
        if (AccentColor.A > 0f)
        {
            var a = inner.Position;
            float w = AccentWidth, h = inner.Size.Y;
            Poly(new[] { a + new Vector2(k, 0), a + new Vector2(k + w, 0), a + new Vector2(w, h), a + new Vector2(0, h) }, AccentColor);
        }
        if (Star && inner.Size.X > 60f)
        {
            // Chevron e-sport affûté (remplace l'étoile cartoon).
            var c = inner.Position + new Vector2(Mathf.Abs(s) + 18f, inner.Size.Y / 2f);
            float h = Mathf.Min(inner.Size.Y * 0.24f, 11f), w = h * 0.72f, t = Mathf.Max(3f, h * 0.32f);
            Poly(new[]
            {
                c + new Vector2(-w * 0.5f, -h),
                c + new Vector2(-w * 0.5f + t, -h),
                c + new Vector2(w * 0.7f, 0),
                c + new Vector2(-w * 0.5f + t, h),
                c + new Vector2(-w * 0.5f, h),
                c + new Vector2(w * 0.7f - t, 0),
            }, UiKit.Navy);
        }
    }
}
