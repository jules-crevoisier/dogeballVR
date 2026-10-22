using System.Collections.Generic;
using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// « Autocollant » de texte façon « GOTCHA! » (Knockout City) : « BAM ! », « PRISE ! », « PASSE ! », « KO ! ».
/// Lettres blanches grasses, contour épais bleu nuit, liseré extérieur à la couleur de l'équipe, légère inclinaison.
/// Taille stable à l'écran, toujours devant le décor (pas de test de profondeur), face à la caméra.
/// Animation : pop d'échelle 1,3 → 1 en 0,15 s, tenue, puis montée et fondu. Placé à hauteur de tête, décalé sur le côté
/// (vers l'extérieur de l'écran) pour ne cacher ni le visage ni le réticule, à 40 px au moins de l'étiquette nom/cœurs du joueur
/// concerné, et remonté au-dessus des autres étiquettes qu'il couvrirait (<see cref="PlayerTag"/>).
/// Un seul mot à la fois : un nouvel autocollant efface le précédent.
/// </summary>
public partial class SpeechBubble : Node3D
{
    public enum Size { Small, Normal, Big }

    const float Pop = 0.15f, Hold = 0.55f, Fade = 0.4f;
    const float Life = Pop + Hold + Fade;
    static readonly Color Ink = new("101830");
    static Font? font;
    static readonly List<SpeechBubble> active = new();

    Label3D front = null!, back = null!;
    Node3D? follow;
    Vector3 anchor;
    float emPx, tilt, side, widthEm;
    double age;

    /// <summary>Compatibilité : autocollant posé à un point fixe du monde.</summary>
    public static void Spawn(Node context, string text, Vector3 position, Color color) => Spawn(context, text, position, color, Size.Normal, null);

    /// <param name="position">Point d'ancrage (dessus de la tête) ; si <paramref name="follow"/> est donné, décalage par rapport à lui.</param>
    /// <param name="color">Couleur du liseré (équipe concernée).</param>
    public static SpeechBubble? Spawn(Node context, string text, Vector3 position, Color color, Size size, Node3D? follow)
    {
        if (!GodotObject.IsInstanceValid(context) || !context.IsInsideTree()) return null;
        // Un seul autocollant à l'écran : le nouveau remplace l'ancien, quel que soit le joueur concerné (pas de pile illisible
        // « BAM ! » / « PRISE ! » / « WHAM ! » : les mots posés devant la caméra n'ont pas de joueur à suivre et s'empilaient).
        foreach (var old in active.ToArray())
            if (GodotObject.IsInstanceValid(old)) { old.Visible = false; old.QueueFree(); }
        active.Clear();
        font ??= GD.Load<Font>("res://assets/fonts/LilitaOne-Regular.ttf");
        var b = new SpeechBubble
        {
            Name = "Sticker", TopLevel = true, follow = follow,
            emPx = size switch { Size.Small => 45f, Size.Big => 93f, _ => 66f },
        };
        b.widthEm = font.GetStringSize(text, HorizontalAlignment.Left, -1, 96).X / 96f;
        b.anchor = follow != null ? position - follow.GlobalPosition : position;
        b.tilt = Mathf.DegToRad((float)GD.RandRange(5.0, 9.0)) * (GD.Randf() < 0.5f ? -1f : 1f);
        // Liseré à la couleur de l'équipe, éclairci pour rester vif sur fond sombre.
        var rim = color.Lerp(Colors.White, 0.1f);
        b.back = MakeLabel(text, rim, rim, 84, 5, 4);
        b.front = MakeLabel(text, Colors.White, Ink, 50, 7, 6);
        b.AddChild(b.back);
        b.AddChild(b.front);
        context.GetTree().CurrentScene.AddChild(b);
        b.Visible = false;   // placé à la première image (taille et côté dépendent de la caméra)
        active.Add(b);
        return b;
    }

    static Label3D MakeLabel(string text, Color fill, Color outline, int outlineSize, int priority, int outlinePriority) => new()
    {
        Text = text, Font = font, FontSize = 96, PixelSize = 1f / 96f, OutlineSize = outlineSize, Modulate = fill, OutlineModulate = outline,
        NoDepthTest = true, Shaded = false, DoubleSided = true, FixedSize = false, Billboard = BaseMaterial3D.BillboardModeEnum.Disabled,
        RenderPriority = priority, OutlineRenderPriority = outlinePriority, AlphaCut = Label3D.AlphaCutMode.Disabled,
        TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps,
    };

    public override void _ExitTree() => active.Remove(this);

    public override void _Process(double delta)
    {
        age += delta;
        if (age >= Life) { QueueFree(); return; }
        if (follow != null && !IsInstanceValid(follow)) follow = null;
        var basePos = follow != null ? follow.GlobalPosition + anchor : anchor;
        var cam = GetViewport().GetCamera3D();
        float mpp = ScreenSize.MetersPerPixel(cam, basePos);
        if (mpp <= 0f) { Visible = false; return; }
        var camBasis = cam!.GlobalTransform.Basis.Orthonormalized();

        float t = (float)age;
        float scale = t < Pop ? Mathf.Lerp(1.3f, 1f, EaseOutBack(t / Pop)) : 1f;
        float fadeT = Mathf.Clamp((t - Pop - Hold) / Fade, 0f, 1f);
        float rise = 34f * fadeT * fadeT;                       // pixels : la montée accélère pendant le fondu
        float alpha = 1f - fadeT * fadeT;
        float em = emPx * mpp * scale;

        Vector3 offset;
        if (cam is XRCamera3D)
        {
            if (side == 0f) ChooseSide(cam, basePos, 0f);
            offset = camBasis.X * (side * 40f * mpp) + camBasis.Y * ((14f + rise) * mpp);
        }
        else
        {
            var view = cam.GetViewport().GetVisibleRect().Size;
            float f = view.Y / 720f;
            // Écart horizontal : la moitié de l'étiquette du joueur (s'il en a une) + 40 px, + le liseré gauche du texte.
            float tagHalf = PlayerTag.Of(follow)?.ScreenRect(cam) is Rect2 own ? own.Size.X * 0.5f / f : 0f;
            float gap = tagHalf + MinGap + RimEm * emPx * scale;
            if (side == 0f) ChooseSide(cam, basePos, gap + (widthEm + RimEm) * emPx * scale);
            float upPx = 14f + rise;
            // Autres étiquettes couvertes : on monte l'autocollant juste au-dessus (deux passes, elles peuvent s'empiler).
            for (int pass = 0; pass < 2; pass++)
            {
                var r = ScreenRectAt(cam, basePos + Offset(camBasis, gap, upPx, mpp), emPx * scale * f);
                foreach (var tag in PlayerTag.Shown())
                {
                    if (tag.Target == follow || tag.ScreenRect(cam) is not Rect2 tr) continue;
                    tr = tr.Grow(10f * f);
                    if (r.Intersects(tr)) { float need = (r.End.Y - tr.Position.Y) / f; upPx += need; r.Position -= new Vector2(0, need * f); }
                }
            }
            offset = Offset(camBasis, gap, upPx, mpp);
        }
        offset += KeepOnScreen(cam, basePos + offset, emPx * scale, camBasis, mpp);
        GlobalTransform = new Transform3D(camBasis * new Basis(Vector3.Back, tilt * -side) * Basis.FromScale(Vector3.One * em), basePos + offset);
        SetAlpha(front, alpha);
        SetAlpha(back, alpha);
        Visible = true;
    }

    const float MinGap = 40f;    // pixels (image de 720 lignes) entre l'étiquette nom/cœurs et le mot
    const float RimEm = 0.45f;   // liseré extérieur (contour 84 sur une police de 96) qui déborde des lettres

    Vector3 Offset(Basis camBasis, float sidePx, float upPx, float mpp) => camBasis.X * (side * sidePx * mpp) + camBasis.Y * (upPx * mpp);

    /// <summary>Côté choisi une fois : à l'opposé du centre (le réticule), sauf si le mot n'y tient pas en entier.</summary>
    void ChooseSide(Camera3D cam, Vector3 basePos, float reach720)
    {
        var view = cam.GetViewport().GetVisibleRect().Size;
        float x = cam.UnprojectPosition(basePos).X;
        float reach = reach720 * view.Y / 720f + 8f;
        side = x < view.X * 0.5f ? -1f : 1f;
        bool fits = side > 0f ? x + reach < view.X : x - reach > 0f;
        bool otherFits = side > 0f ? x - reach > 0f : x + reach < view.X;
        if (!fits && otherFits) side = -side;
        var align = side < 0f ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        front.HorizontalAlignment = back.HorizontalAlignment = align;
        front.VerticalAlignment = back.VerticalAlignment = VerticalAlignment.Bottom;
    }

    /// <summary>Rectangle approximatif du mot à l'écran (liseré et inclinaison compris), l'origine étant le coin bas côté tête.</summary>
    Rect2 ScreenRectAt(Camera3D cam, Vector3 origin, float emScreenPx)
    {
        var p = cam.UnprojectPosition(origin);
        float w = (widthEm + 2f * RimEm) * emScreenPx;
        float h = (1.2f + 2f * RimEm) * emScreenPx + 0.16f * w;   // inclinaison de 5 à 9° : le bout du mot monte ou descend
        float x = side > 0f ? p.X - RimEm * emScreenPx : p.X - w + RimEm * emScreenPx;
        return new Rect2(x, p.Y - h + RimEm * emScreenPx + 0.08f * w, w, h);
    }

    /// <summary>Décalage (en mètres) qui ramène l'autocollant entier dans l'écran, marge de 8 px, s'il dépasse d'un bord.</summary>
    Vector3 KeepOnScreen(Camera3D cam, Vector3 origin, float emPx720, Basis camBasis, float mpp)
    {
        if (cam is XRCamera3D) return Vector3.Zero;
        var size = cam.GetViewport().GetVisibleRect().Size;
        float f = size.Y / 720f;
        var p = cam.UnprojectPosition(origin);
        float width = (widthEm + RimEm) * emPx720 * f, height = 1.15f * emPx720 * f, pad = 8f * f;
        float dx = 0f, dy = 0f;
        // Bords gauche et droit, quel que soit le côté (la cible peut sortir du cadre pendant que le mot est affiché).
        float left = side > 0f ? p.X - RimEm * emPx720 * f : p.X - width, right = left + width + RimEm * emPx720 * f;
        if (right > size.X - pad) dx = size.X - pad - right;
        if (left + dx < pad) dx = pad - left;
        float top = 96f * f;   // sous le bandeau de score du HUD
        if (p.Y - height < top) dy = -(top - (p.Y - height));   // trop haut : on descend (y écran vers le bas)
        return (camBasis.X * dx + camBasis.Y * dy) * (mpp / f);
    }

    static void SetAlpha(Label3D l, float a)
    {
        var m = l.Modulate; m.A = a; l.Modulate = m;
        var o = l.OutlineModulate; o.A = a; l.OutlineModulate = o;
    }

    static float EaseOutBack(float x)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(x - 1f, 3f) + c1 * Mathf.Pow(x - 1f, 2f);
    }
}
