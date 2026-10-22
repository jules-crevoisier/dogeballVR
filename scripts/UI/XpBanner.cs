using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;
using DogeballVR.Online;

namespace DogeballVR.UI;

/// <summary>
/// Bandeau d'XP en bas de l'écran de fin : niveau, titre, barre qui se remplit avec l'XP du match (passage de niveau
/// marqué d'un coup de tampon), gain total et détail. Se montre une fois l'écran de fin ouvert, se retire à la reprise.
/// </summary>
public partial class XpBanner : CanvasLayer
{
    public MatchView View { get; set; } = null!;
    /// <summary>Faut-il attendre encore (podium de l'écran de fin) ? Sans lui, le bandeau suit le délai seul.</summary>
    public System.Func<bool>? Ready { get; set; }

    const double ShowDelay = 0.4, FillTime = 1.6;   // après l'arrivée du tableau

    MatchAward? seen, award;
    double overFor = -1, fillAge;
    int shownLevel;
    Control? root;
    Control bar = null!, badge = null!;
    Label levelLabel = null!, titleLabel = null!, progressLabel = null!, gainLabel = null!;
    float fill;

    public override void _Ready()
    {
        Layer = 56;   // au-dessus de l'écran de fin (55), sous le menu pause (60)
        seen = Progression.LastAward;
    }

    public override void _Process(double delta)
    {
        if (View.State.Phase != MatchView.Phase.MatchOver)
        {
            overFor = -1;
            if (root != null) { root.QueueFree(); root = null; }
            seen = Progression.LastAward;
            return;
        }
        if (Ready?.Invoke() == false) return;
        overFor = overFor < 0 ? 0 : overFor + delta;
        if (root == null && overFor >= ShowDelay && Progression.LastAward is { } a && a != seen) Open(a);
        if (root != null && award != null) Animate(delta);
    }

    void Open(MatchAward a)
    {
        award = a;
        seen = a;
        fillAge = 0;
        shownLevel = a.LevelBefore;
        root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.PanelBox(14, 14);
        box.StripColor = new Color(0, 0, 0, 0);   // bandeau bas : pas de bande violette qui déborde de l'écran
        box.ContentMarginTop = 8; box.ContentMarginBottom = 8; box.ContentMarginLeft = 22; box.ContentMarginRight = 28;
        panel.AddThemeStyleboxOverride("panel", box);
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.OffsetLeft = -500; panel.OffsetRight = 500; panel.OffsetTop = -98; panel.OffsetBottom = -20;   // marge : l'ombre penchée reste dans l'écran
        root.AddChild(panel);

        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        row.AddThemeConstantOverride("separation", 16);
        panel.AddChild(row);

        // Pastille du niveau.
        badge = new PanelContainer { CustomMinimumSize = new Vector2(62, 62), PivotOffset = new Vector2(31, 31), MouseFilter = Control.MouseFilterEnum.Ignore };
        badge.AddThemeStyleboxOverride("panel", ProfileScreen.Badge(8f));
        var badgeCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        badgeCol.AddThemeConstantOverride("separation", -6);
        badgeCol.AddChild(UiKit.Text("NIV.", 14, UiKit.Navy, 0, HorizontalAlignment.Center));
        levelLabel = UiKit.Text("", 30, UiKit.Navy, 0, HorizontalAlignment.Center);
        badgeCol.AddChild(levelLabel);
        badge.AddChild(badgeCol);
        row.AddChild(badge);

        // Titre, barre et détail.
        var mid = new VBoxContainer { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = Control.MouseFilterEnum.Ignore };
        mid.AddThemeConstantOverride("separation", 3);
        var top = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        titleLabel = UiKit.Title("", 26, HorizontalAlignment.Left);
        titleLabel.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        top.AddChild(titleLabel);
        progressLabel = UiKit.Text("", 18, UiKit.Sky, 5, HorizontalAlignment.Right);
        top.AddChild(progressLabel);
        mid.AddChild(top);
        bar = new Control { CustomMinimumSize = new Vector2(0, 14), MouseFilter = Control.MouseFilterEnum.Ignore };
        bar.Draw += DrawBar;
        mid.AddChild(bar);
        var detail = UiKit.Text(string.Join("  ·  ", System.Linq.Enumerable.Select(a.Lines, l => $"{l.Label} {l.Xp:+0;-0}")), 15, new Color(UiKit.Cream, 0.8f), 4);
        detail.ClipText = true;
        detail.AutowrapMode = TextServer.AutowrapMode.Off;
        mid.AddChild(detail);
        row.AddChild(mid);

        gainLabel = UiKit.Title("+0 XP", 40, HorizontalAlignment.Right);
        gainLabel.CustomMinimumSize = new Vector2(150, 0);
        gainLabel.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(gainLabel);

        UiKit.SlideIn(panel, 0f, 80f);
        Refresh(a.TotalBefore);
    }

    void Animate(double delta)
    {
        var a = award!;
        fillAge += delta;
        float k = (float)Mathf.Clamp((fillAge - 0.35) / FillTime, 0, 1);   // petite pause, puis la barre se remplit
        k = 1f - Mathf.Pow(1f - k, 3f);
        int shown = a.TotalBefore + Mathf.RoundToInt(a.Xp * k);
        gainLabel.Text = $"+{Mathf.RoundToInt(a.Xp * k)} XP";
        Refresh(shown);
    }

    void Refresh(int totalXp)
    {
        var (level, into, need) = Progression.LevelOf(totalXp);
        if (level > shownLevel)
        {
            shownLevel = level;
            UiKit.Punch(badge, 1.5f, 0.35);
            Sfx.Play(this, SfxKind.Tier);
            titleLabel.Text = $"NIVEAU {level} !";
        }
        else if (level == award?.LevelBefore)
            titleLabel.Text = Progression.TitleOf(level).ToUpper();
        levelLabel.Text = level.ToString();
        progressLabel.Text = need > 0 ? $"{Spaced(into)} / {Spaced(need)} XP" : "NIVEAU MAX";
        fill = need > 0 ? (float)into / need : 1f;
        bar.QueueRedraw();
    }

    void DrawBar()
    {
        var size = bar.Size;
        bar.DrawStyleBox(ProfileScreen.Gauge(false, 6f), new Rect2(Vector2.Zero, size));
        if (fill > 0) bar.DrawStyleBox(ProfileScreen.Gauge(true, 6f), new Rect2(Vector2.Zero, new Vector2(Mathf.Max(size.Y * 2, size.X * fill), size.Y)));
    }

    /// <summary>1 240 plutôt que 1240 (espace simple : la police n'a pas l'espace fine insécable).</summary>
    public static string Spaced(int n) => n.ToString("#,0", System.Globalization.CultureInfo.InvariantCulture).Replace(',', ' ');
}
