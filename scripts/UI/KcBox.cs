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
    /// <summary>Rayon des coins arrondis (px) lorsque Slant == 0.</summary>
    [Export] public float Radius { get; set; } = 14f;

    public KcBox Copy() => (KcBox)Duplicate();

    static Vector2[] Quad(Rect2 r, float s) => new[]
    {
        r.Position + new Vector2(s, 0), r.Position + new Vector2(r.Size.X, 0),
        r.Position + new Vector2(r.Size.X - s, r.Size.Y), r.Position + new Vector2(0, r.Size.Y),
    };

    static Vector2[] RoundedRect(Rect2 r, float radius)
    {
        float rad = Mathf.Clamp(radius, 0f, Mathf.Min(r.Size.X, r.Size.Y) * 0.48f);
        if (rad < 1f) return Quad(r, 0f);
        const int seg = 5;
        var pts = new Vector2[(seg + 1) * 4];
        int idx = 0;
        var corners = new (Vector2 center, float start)[]
        {
            (new Vector2(r.Position.X + r.Size.X - rad, r.Position.Y + rad), -Mathf.Pi * 0.5f),
            (new Vector2(r.Position.X + r.Size.X - rad, r.Position.Y + r.Size.Y - rad), 0f),
            (new Vector2(r.Position.X + rad, r.Position.Y + r.Size.Y - rad), Mathf.Pi * 0.5f),
            (new Vector2(r.Position.X + rad, r.Position.Y + rad), Mathf.Pi),
        };
        foreach (var (center, start) in corners)
            for (int i = 0; i <= seg; i++)
            {
                float a = start + (Mathf.Pi * 0.5f) * (i / (float)seg);
                pts[idx++] = center + new Vector2(Mathf.Cos(a) * rad, Mathf.Sin(a) * rad);
            }
        return pts;
    }

    public override void _Draw(Rid ci, Rect2 rect)
    {
        var rs = RenderingServer.Singleton;
        float s = Mathf.Clamp(Slant, -rect.Size.X * 0.2f, rect.Size.X * 0.2f);
        bool rounded = Mathf.Abs(s) < 0.5f && Radius > 0.5f;
        void Poly(Vector2[] pts, Color c) { if (c.A > 0.001f) rs.CanvasItemAddPolygon(ci, pts, new[] { c }); }

        if (rounded)
        {
            if (ShadowColor.A > 0f) Poly(RoundedRect(new Rect2(rect.Position + ShadowOffset, rect.Size), Radius), ShadowColor);
            if (BorderWidth > 0f && BorderColor.A > 0.001f) Poly(RoundedRect(rect, Radius), BorderColor);
            var inner = rect.Grow(-Mathf.Max(BorderWidth, 0f));
            if (inner.Size.X <= 1f || inner.Size.Y <= 1f) return;
            float inRad = Mathf.Max(0f, Radius - BorderWidth);
            if (Top.A > 0.001f || Bottom.A > 0.001f)
            {
                var pts = RoundedRect(inner, inRad);
                var cols = new Color[pts.Length];
                float h = Mathf.Max(inner.Size.Y, 1f);
                for (int i = 0; i < pts.Length; i++)
                {
                    float t = Mathf.Clamp((pts[i].Y - inner.Position.Y) / h, 0f, 1f);
                    cols[i] = Top.Lerp(Bottom, t);
                }
                rs.CanvasItemAddPolygon(ci, pts, cols);
            }
            return;
        }

        if (StripColor.A > 0f) Poly(Quad(new Rect2(rect.Position + StripOffset, rect.Size), s), StripColor);
        if (ShadowColor.A > 0f) Poly(Quad(new Rect2(rect.Position + ShadowOffset, rect.Size), s), ShadowColor);
        var innerRect = rect.Grow(-Mathf.Max(BorderWidth, 0f));
        if (innerRect.Size.X <= 1f || innerRect.Size.Y <= 1f) return;
        float k = s * innerRect.Size.Y / Mathf.Max(rect.Size.Y, 1f);
        var q = Quad(innerRect, k);
        if (BorderWidth > 0f)
        {
            var o = Quad(rect, s);
            for (int i = 0; i < 4; i++) Poly(new[] { o[i], o[(i + 1) % 4], q[(i + 1) % 4], q[i] }, BorderColor);
        }
        if (Top.A > 0.001f || Bottom.A > 0.001f)
            rs.CanvasItemAddPolygon(ci, q, new[] { Top, Top, Bottom, Bottom });
    }
}
