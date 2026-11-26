using Godot;

namespace DogeballVR.Xr;

/// <summary>
/// Petits mots en VR (pas d'écran ni de HUD au casque) : nom du coup qui part de la main (« COURBE ! », « CLOCHE ! »…), pour
/// apprendre les gestes, et message face au joueur (recalage de la zone, zone de jeu trop petite). Typo Anton de l'interface Affiche.
/// </summary>
public static class VrToast
{
    /// <summary>Mot qui monte de la main et s'efface en 0,9 s.</summary>
    public static void AtHand(Node3D hand, string text, Color color)
    {
        if (!hand.IsInsideTree()) return;
        var l = Make(text, color, 0.0011f, 64);
        hand.GetTree().CurrentScene.AddChild(l);
        l.GlobalPosition = hand.GlobalPosition + Vector3.Up * 0.18f;
        var tw = l.CreateTween();
        tw.SetParallel();
        tw.TweenProperty(l, "global_position", l.GlobalPosition + Vector3.Up * 0.35f, 0.9).SetEase(Tween.EaseType.Out).SetTrans(Tween.TransitionType.Cubic);
        tw.TweenProperty(l, "modulate:a", 0f, 0.5).SetDelay(0.4);
        tw.Chain().TweenCallback(Callable.From(l.QueueFree));
    }

    /// <summary>Message posé à 1,4 m devant le regard (il ne suit pas la tête : on peut le lire en bougeant), effacé après « seconds ».</summary>
    public static void InFront(Node3D head, string text, Color color, double seconds = 3.0)
    {
        if (!head.IsInsideTree()) return;
        var l = Make(text, color, 0.0012f, 72);
        head.GetTree().CurrentScene.AddChild(l);
        var fwd = -head.GlobalBasis.Z;
        fwd = new Vector3(fwd.X, 0, fwd.Z);
        if (fwd.LengthSquared() < 0.01f) fwd = Vector3.Forward;
        l.GlobalPosition = head.GlobalPosition + fwd.Normalized() * 1.4f - Vector3.Up * 0.1f;
        var tw = l.CreateTween();
        tw.TweenProperty(l, "modulate:a", 0f, 0.6).SetDelay(seconds);
        tw.TweenCallback(Callable.From(l.QueueFree));
    }

    static Label3D Make(string text, Color color, float pixel, int size)
    {
        var l = new Label3D
        {
            Text = text, PixelSize = pixel, FontSize = size, Modulate = color, OutlineModulate = new Color(0.06f, 0.07f, 0.1f), OutlineSize = size / 6,
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled, Shaded = false, NoDepthTest = true, RenderPriority = 10,
            HorizontalAlignment = HorizontalAlignment.Center, Width = 900f, AutowrapMode = TextServer.AutowrapMode.WordSmart,
        };
        if (UI.UiKit.Display != null) l.Font = UI.UiKit.Display;
        return l;
    }
}
