using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.UI;

/// <summary>
/// Verrouillage façon Knockout City : quatre crochets autour de l'adversaire que l'aide à la visée va corriger,
/// qui se resserrent en arrivant et rougissent quand le lancer est chargé à fond. Rien quand on n'a pas de balle.
/// </summary>
public partial class LockOnMarker : Control
{
    public DesktopPlayer Player { get; set; } = null!;

    Hurtbox? last;
    float appear;        // 0 → 1 à l'apparition d'une nouvelle cible
    float alpha;

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Ignore;
    }

    public override void _Process(double delta)
    {
        var target = Player != null && IsInstanceValid(Player) ? Player.LockTarget : null;
        if (target != last && target != null) appear = 0f;
        if (target != null) last = target;
        appear = Mathf.Min(1f, appear + (float)delta * 7f);
        alpha = Mathf.MoveToward(alpha, target != null ? 1f : 0f, (float)delta * 8f);
        QueueRedraw();
    }

    public override void _Draw()
    {
        if (alpha <= 0.01f || last == null || !IsInstanceValid(last) || last.Eliminated) return;
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        var center = last.GlobalPosition + Vector3.Up * 0.15f;
        if (cam.IsPositionBehind(center)) return;
        var top = cam.UnprojectPosition(center + Vector3.Up * 0.95f);
        var bottom = cam.UnprojectPosition(center - Vector3.Up * 0.95f);
        var mid = cam.UnprojectPosition(center);
        float h = Mathf.Clamp(bottom.Y - top.Y, 40f, 420f);
        float w = h * 0.55f;
        float grow = 1f + (1f - Mathf.Ease(appear, 0.4f)) * 0.8f;
        w *= grow; h *= grow;
        float charge = Player.ThrowCharge01;
        var col = charge >= 0.99f ? new Color("ff2a3a") : UiKit.Orange.Lerp(new Color("ff2a3a"), charge);
        col.A = alpha;
        var shadow = new Color(UiKit.Navy, 0.7f * alpha);
        float len = Mathf.Clamp(h * 0.18f, 10f, 34f);
        float thick = 4f;
        foreach (var (sx, sy) in new[] { (-1, -1), (1, -1), (-1, 1), (1, 1) })
        {
            var corner = mid + new Vector2(sx * w / 2, sy * h / 2);
            var a = corner - new Vector2(sx * len, 0);
            var b = corner - new Vector2(0, sy * len);
            DrawPolyline(new[] { a + Vector2.One * 2, corner + Vector2.One * 2, b + Vector2.One * 2 }, shadow, thick + 2, true);
            DrawPolyline(new[] { a, corner, b }, col, thick, true);
        }
        // Losange sous la cible : lisible même quand elle est petite au loin.
        float d = 7f;
        var foot = mid + new Vector2(0, h / 2 + 14f);
        DrawColoredPolygon(new[] { foot + new Vector2(0, -d), foot + new Vector2(d, 0), foot + new Vector2(0, d), foot + new Vector2(-d, 0) }, col);
    }
}
