using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Online;
using DogeballVR.Online.Steam;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Aides contextuelles des premières parties (façon Knockout City) : un rappel court, avec la touche ou le bouton réellement
/// assigné, au moment où le geste sert. Attraper quand un tir arrive, esquiver si on a les mains prises, charger ou passer
/// quand on garde la balle, ce que fait une balle spéciale, où trouver une balle. Chaque aide se retire quand le joueur maîtrise
/// le geste (statistiques de carrière) ; toutes s'arrêtent après quelques parties, ou avec l'option « Aides contextuelles ».
/// Pendant l'entraînement, seule l'aide pour attraper reste (la carte de l'entraînement donne déjà les autres consignes).
/// </summary>
public partial class ContextHints : CanvasLayer
{
    public DesktopPlayer Player { get; set; } = null!;
    public MatchView View { get; set; } = null!;

    /// <summary>Au-delà de ce nombre de parties terminées, plus aucune aide.</summary>
    public const int MatchesWithHints = 8;
    // Captures : `--hints-test` montre les aides au joueur automatique, comme à un débutant.
    static readonly bool test = Array.IndexOf(OS.GetCmdlineUserArgs(), "--hints-test") >= 0;

    /// <summary>Une aide : <c>Wanted</c> dit si elle sert maintenant, <c>Learned</c> si le joueur n'en a plus besoin.
    /// <c>PerMatch</c> limite les rappels d'une même partie, <c>Cooldown</c> espace deux affichages.</summary>
    sealed record Hint(string Id, Func<string> Text, Func<bool> Wanted, Func<bool> Learned, int PerMatch, double Cooldown, bool InTutorial = false);

    readonly List<Hint> hints = new();
    readonly Dictionary<string, int> shown = new();
    readonly Dictionary<string, double> lastShown = new();
    Hint? current;
    double age, currentSince, lostSince = -1, heldSince = -1, emptySince = -1;
    Ball? lastHeld;
    bool shownPad;

    PanelContainer panel = null!;
    RichTextLabel label = null!;

    static string K(string action) => $"[color=#ffd23f]{InputSetup.KeyName(action)}[/color]";
    static int Stat(string api) => Achievements.GetStat(api);
    bool Holding => Player.HeldBall != null;
    bool Incoming => ThreatIndicator.LocalTimeToImpact < ThreatIndicator.WarnSeconds;

    public override void _Ready()
    {
        Layer = 31;   // au-dessus du HUD et de la carte de l'entraînement, sous l'écran de fin
        hints.Add(new("catch", () => $"{K("catch")}  ATTRAPE !", () => Incoming && !Holding && Player.CatchState == 0,
            () => Stat(AchievementDefs.StatCatches) >= 5, 3, 3, InTutorial: true));
        hints.Add(new("dodge", () => $"Mains prises : {K("dash")} pour ESQUIVER", () => Incoming && Holding && Player.DashCharges > 0,
            () => Stat(AchievementDefs.StatMatches) >= 4, 2, 8));
        hints.Add(new("special", () => Player.HeldBall?.Kind == Ball.KindBomb
                ? "BOMBE : elle explose à l'impact et touche tout autour"
                : "REBOND : elle ricoche sur les murs, vise à côté d'eux",
            () => Player.HeldBall?.Kind is Ball.KindBomb or Ball.KindBounce, () => Stat(AchievementDefs.StatMatches) >= 5, 2, 20));
        hints.Add(new("charge", () => $"Maintiens {K("fire")} pour charger  ·  {K("pass")} passe à un allié", () => Holding && heldSince >= 0 && age - heldSince > 3.5 && Player.ThrowCharge01 <= 0f,
            () => Stat(AchievementDefs.StatPasses) >= 10 && Stat(AchievementDefs.StatMatches) >= 3, 2, 30));
        hints.Add(new("pickup", () => "Pas de balle ? Cours sur une balle jaune ou sur le plot de ton camp", () => !Holding && emptySince >= 0 && age - emptySince > 9,
            () => Stat(AchievementDefs.StatMatches) >= 2, 1, 60));

        var root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        var box = UiKit.Box(new Color(UiKit.Navy, 0.86f), 16, 0f, 3, new Color(UiKit.Yellow, 0.8f), 4);
        box.ContentMarginLeft = box.ContentMarginRight = 22; box.ContentMarginTop = 6; box.ContentMarginBottom = 6;
        panel.AddThemeStyleboxOverride("panel", box);
        panel.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        panel.GrowHorizontal = Control.GrowDirection.Both;
        panel.GrowVertical = Control.GrowDirection.Begin;
        panel.OffsetLeft = -420; panel.OffsetRight = 420; panel.OffsetTop = -290; panel.OffsetBottom = -236;
        root.AddChild(panel);
        label = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, AutowrapMode = TextServer.AutowrapMode.Off, MouseFilter = Control.MouseFilterEnum.Ignore };
        label.AddThemeFontSizeOverride("normal_font_size", 28);
        label.AddThemeConstantOverride("outline_size", 6);
        label.AddThemeColorOverride("font_outline_color", UiKit.Navy);
        panel.AddChild(label);
    }

    public override void _Process(double delta)
    {
        age += delta;
        if (Player == null || !IsInstanceValid(Player)) return;
        TrackBall();
        bool on = GameSettings.ContextHints && (!Player.Autoplay || test) && View.State.Phase == MatchView.Phase.Playing && !Player.Hurtbox.Eliminated
            && (Tutorial.Running || Stat(AchievementDefs.StatMatches) < MatchesWithHints);
        if (View.State.Phase == MatchView.Phase.MatchOver) shown.Clear();   // partie suivante : les rappels repartent

        if (current != null)
        {
            bool still = on && current.Wanted();
            if (still) lostSince = -1;
            else if (lostSince < 0) lostSince = age;
            // Une aide reste au moins 1 s (lisible), et disparaît 0,25 s après que le moment est passé.
            if (!still && age - currentSince > (test ? 4.0 : 1.0) && age - lostSince > 0.25) Hide();
            else if (shownPad != InputSetup.UsingPad) { shownPad = InputSetup.UsingPad; SetText(current.Text()); }
            return;
        }
        if (!on) return;
        foreach (var h in hints)
        {
            if (Tutorial.Running && !h.InTutorial) continue;
            if (shown.GetValueOrDefault(h.Id) >= h.PerMatch || age - lastShown.GetValueOrDefault(h.Id, -999) < h.Cooldown) continue;
            if (!Tutorial.Running && !test && h.Learned()) continue;
            if (!h.Wanted()) continue;
            Show(h);
            break;
        }
    }

    void TrackBall()
    {
        // Depuis quand la balle est en main, ou depuis quand on joue les mains vides (hors jeu et KO ne comptent pas).
        var held = Player.HeldBall;
        if (held != null) { if (held != lastHeld) heldSince = age; emptySince = -1; }
        else
        {
            heldSince = -1;
            if (emptySince < 0 || View.State.Phase != MatchView.Phase.Playing || Player.Hurtbox.Eliminated) emptySince = age;
        }
        lastHeld = held;
    }

    void Show(Hint h)
    {
        current = h;
        currentSince = age;
        lostSince = -1;
        shown[h.Id] = shown.GetValueOrDefault(h.Id) + 1;
        lastShown[h.Id] = age;
        shownPad = InputSetup.UsingPad;
        SetText(h.Text());
        panel.Visible = true;
        panel.Modulate = Colors.White;
        UiKit.Punch(panel, 1.25f, 0.2);
        GD.Print($"[Aide] {h.Id}");
    }

    void Hide()
    {
        current = null;
        var tween = panel.CreateTween();
        tween.TweenProperty(panel, "modulate:a", 0.0, 0.18);
        tween.TweenCallback(Callable.From(() => { if (current == null) panel.Visible = false; }));
    }

    /// <summary>Texte et largeur de la pastille, ajustée au texte (sans les balises de couleur).</summary>
    void SetText(string text)
    {
        label.Text = $"[center]{text}[/center]";
        string plain = System.Text.RegularExpressions.Regex.Replace(text, @"\[[^\]]*\]", "");
        float w = label.GetThemeFont("normal_font").GetStringSize(plain, HorizontalAlignment.Left, -1, 28).X + 70;
        panel.OffsetLeft = -w / 2; panel.OffsetRight = w / 2;
        panel.PivotOffset = new Vector2(w / 2, 27);
    }
}
