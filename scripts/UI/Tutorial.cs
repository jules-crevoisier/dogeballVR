using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;

namespace DogeballVR.UI;

/// <summary>
/// Entraînement guidé du joueur PC (clavier-souris ou manette), joué dans une vraie partie hors ligne contre des bots doux (<see cref="Bot.Gentle"/>).
/// Une carte en haut de l'écran donne une consigne à la fois (avec les touches ou boutons réellement assignés, selon le dernier
/// appareil touché) ; chaque consigne se valide toute seule en la faisant (compteurs de <see cref="DesktopPlayer"/>), avec un tampon
/// vert et un son. Entrée (ou Vue à la manette) passe l'étape. Dernière étape : une balle spéciale tombe aux pieds du joueur.
/// </summary>
public partial class Tutorial : CanvasLayer
{
    public DesktopPlayer Player { get; set; } = null!;
    /// <summary>Pour l'étape de la balle spéciale (et le rappel des règles à la fin).</summary>
    public MatchController? Match { get; set; }
    /// <summary>Fin de l'entraînement : jouer une vraie partie ou revenir au menu.</summary>
    public Action? PlayForReal { get; set; }
    public Action? BackToMenu { get; set; }
    /// <summary>Entraînement en cours : le HUD cache le score, le chrono et les annonces de manche.</summary>
    public static bool Running { get; private set; }

    sealed record Step(string Title, Func<string> Text, Func<DesktopPlayer, bool> Done, Action? Enter = null);

    readonly List<Step> steps = new();
    int index = -1;
    double doneAt = -1, age;
    int hitsByMe;
    Dictionary<string, float> start = new();

    PanelContainer card = null!;
    Label counter = null!, title = null!, check = null!, skipHint = null!;
    bool shownPad;
    Ball? special;
    double specialAge;
    bool heldSpecial;
    int throwsWithSpecial;
    RichTextLabel body = null!;
    ColorRect progress = null!;

    const string CatchTitle = "ATTRAPE UN TIR";
    // Test (CI) : `--tutorial-test` passe seule une étape restée 8 s sans être faite, pour parcourir tout l'entraînement.
    static readonly bool autoTest = Array.IndexOf(OS.GetCmdlineUserArgs(), "--tutorial-test") >= 0;
    double stepStart;

    static string K(string action) => $"[color=#ffd23f]{InputSetup.KeyName(action)}[/color]";
    static string Pad(string pad, string keyboard) => InputSetup.UsingPad ? pad : keyboard;

    public override void _Ready()
    {
        Layer = 30;
        Running = true;
        Bot.Gentle = true;
        Bot.Calm = true;   // dès la première image : pas de tir pendant « REGARDE AUTOUR DE TOI » (Next arrive en différé)
        MatchController.HitScored += OnHit;
        steps.Add(new("REGARDE AUTOUR DE TOI", () => Pad("Le [color=#ffd23f]stick droit[/color] tourne la caméra.", "Bouge la souris pour tourner la caméra."), p => p.LookTravel - start["look"] > 3.5f));
        steps.Add(new("DÉPLACE-TOI", () => Pad("Le [color=#ffd23f]stick gauche[/color] fait courir sur le toit.", $"{K("move_forward")} {K("move_left")} {K("move_back")} {K("move_right")} pour courir sur le toit."), p => p.WalkTravel - start["walk"] > 8f));
        steps.Add(new("SAUTE", () => $"{K("jump")} pour sauter par-dessus un muret.", p => p.Jumps > start["jumps"]));
        steps.Add(new("ESQUIVE", () => $"{K("dash")} : un bond éclair, invulnérable pendant l'élan. Tu as deux charges : enchaîne-en deux.", p => p.Dashes - start["dashes"] >= 2));
        steps.Add(new("RAMASSE UNE BALLE", () => "Cours sur une balle jaune (ou sur le plot de ton camp) pour la prendre.", p => p.HeldBall != null));
        steps.Add(new("LANCER CHARGÉ", () => $"Maintiens {K("fire")} pour charger (la jauge se remplit), relâche pour lancer.", p => p.ChargedThrows > start["charged"]));
        steps.Add(new("TOUCHE UN ADVERSAIRE", () => $"Vise un bleu (le viseur prend sa couleur) et lance avec {K("fire")}. L'aide à la visée corrige les presque-touches.", _ => hitsByMe > 0));
        steps.Add(new(CatchTitle, () => $"Quand un bleu te vise (alerte rouge), appuie sur {K("catch")} juste avant l'impact. Approche-toi d'eux pour qu'ils te visent.", p => p.Catches > start["catches"]));
        steps.Add(new("PASSE À UN ALLIÉ", () => $"Balle en main, {K("pass")} l'envoie à ton coéquipier orange : une passe charge la balle.", p => p.Passes > start["passes"]));
        steps.Add(new("FEINTE", () => $"Balle en main, {K("feint")} fait mine de lancer : l'adversaire attrape dans le vide.", p => p.Feints > start["feints"]));
        if (Match != null)
            steps.Add(new("BALLE SPÉCIALE", () => "Une [color=#ffd23f]bombe[/color] tombe près de toi : elle explose à l'impact et touche tout autour. "
                + $"Ramasse-la (lance d'abord la balle que tu tiens) et envoie-la sur les bleus avec {K("fire")}.", SpecialDone, DropSpecial));

        var root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        card = new PanelContainer { CustomMinimumSize = new Vector2(760, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        var cardBox = UiKit.PanelBox(16, 26);   // la fenêtre du menu, liseré jaune : la consigne se détache du jeu
        cardBox.BorderColor = UiKit.Yellow;
        card.AddThemeStyleboxOverride("panel", cardBox);
        card.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        card.OffsetLeft = -380; card.OffsetRight = 380; card.OffsetTop = 150;
        root.AddChild(card);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 6);
        card.AddChild(col);
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(head);
        counter = UiKit.Text("", 22, UiKit.Sky, 6);
        counter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(counter);
        skipHint = UiKit.Text("", 22, UiKit.Cream, 6);
        head.AddChild(skipHint);
        title = UiKit.Title("", 46, HorizontalAlignment.Left);
        col.AddChild(title);
        body = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(720, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddThemeFontSizeOverride("normal_font_size", 26);
        body.AddThemeConstantOverride("outline_size", 6);
        body.AddThemeColorOverride("font_outline_color", UiKit.Navy);
        col.AddChild(body);
        var barBack = new ColorRect { Color = new Color(1, 1, 1, 0.15f), CustomMinimumSize = new Vector2(0, 8), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(barBack);
        progress = new ColorRect { Color = UiKit.Orange, Size = new Vector2(0, 8), MouseFilter = Control.MouseFilterEnum.Ignore };
        barBack.AddChild(progress);
        check = UiKit.Text("✔", 90, UiKit.Green, 14, HorizontalAlignment.Center);
        check.SetAnchorsPreset(Control.LayoutPreset.CenterRight);
        check.Position = new Vector2(card.OffsetRight - 70, 0);
        check.Visible = false;
        card.AddChild(check);
        // Captures : `--tutorial-step=N` commence à l'étape N.
        foreach (var arg in OS.GetCmdlineUserArgs())
            if (arg.StartsWith("--tutorial-step=") && int.TryParse(arg["--tutorial-step=".Length..], out int first)) index = Mathf.Clamp(first, 1, steps.Count) - 2;
        CallDeferred(nameof(Next));
    }

    public override void _ExitTree()
    {
        Running = false;
        Bot.Gentle = false;
        Bot.Calm = false;
        MatchController.HitScored -= OnHit;
    }

    void OnHit(int shooter, int target, bool kill)
    {
        if (shooter == Player.Hurtbox.PlayerId) hitsByMe++;
    }

    void Snapshot()
    {
        start = new()
        {
            ["look"] = Player.LookTravel, ["walk"] = Player.WalkTravel, ["jumps"] = Player.Jumps, ["dashes"] = Player.Dashes,
            ["charged"] = Player.ChargedThrows, ["catches"] = Player.Catches, ["passes"] = Player.Passes, ["feints"] = Player.Feints,
        };
    }

    void Next()
    {
        index++;
        doneAt = -1;
        stepStart = age;
        check.Visible = false;
        // Bots calmes (ils ramassent et bougent, sans lancer) jusqu'à l'étape où il faut attraper un tir : on apprend à viser en paix.
        Bot.Calm = index < steps.Count && index < steps.FindIndex(st => st.Title == CatchTitle);
        if (index >= steps.Count) { Finish(); return; }
        Snapshot();
        var s = steps[index];
        counter.Text = $"ENTRAÎNEMENT  {index + 1}/{steps.Count}";
        title.Text = s.Title;
        s.Enter?.Invoke();
        RefreshTexts();
        progress.Size = new Vector2(card.Size.X > 0 ? (card.Size.X - 52) * index / steps.Count : 0, 8);
        card.PivotOffset = new Vector2(380, 60);
        UiKit.Punch(card, 1.12f, 0.25);
        Sfx.Play(this, SfxKind.Tier, null, 1.2f, -4f);
    }

    /// <summary>Textes selon l'appareil : on touche la manette, les consignes passent aux boutons (et inversement).</summary>
    void RefreshTexts()
    {
        shownPad = InputSetup.UsingPad;
        skipHint.Text = Pad("Vue : passer l'étape", "Entrée : passer l'étape");
        if (index >= 0 && index < steps.Count) body.Text = steps[index].Text();
    }

    void DropSpecial()
    {
        heldSpecial = false;
        specialAge = 0;
        if (Match == null) return;
        var cam = GetViewport().GetCamera3D();
        var ahead = cam != null ? -cam.GlobalTransform.Basis.Z with { Y = 0 } : Vector3.Zero;
        special = Match.DropSpecial(Player.GlobalPosition + (ahead.LengthSquared() > 0.01f ? ahead.Normalized() : Vector3.Forward) * 1.6f, Ball.KindBomb);
        GD.Print("[Tuto] Bombe lâchée près du joueur");
    }

    bool SpecialDone(DesktopPlayer p)
    {
        if (p.HeldBall is { } b && b == special) { heldSpecial = true; throwsWithSpecial = p.Throws; }
        return heldSpecial && p.HeldBall != special && p.Throws > throwsWithSpecial;
    }

    public override void _Process(double delta)
    {
        age += delta;
        if (shownPad != InputSetup.UsingPad && index >= 0) RefreshTexts();
        if (index < 0 || index >= steps.Count || Player == null || !IsInstanceValid(Player)) return;
        // La bombe a explosé sans avoir été lancée par le joueur, ou un bot l'a prise : une autre tombe.
        if (steps[index].Enter != null && doneAt < 0 && !heldSpecial && (specialAge += delta) > 4
            && (special == null || !IsInstanceValid(special) || special.HasExploded || special.IsHeld && Player.HeldBall != special))
            DropSpecial();
        if (doneAt < 0 && steps[index].Done(Player)) Complete();
        else if (doneAt < 0 && autoTest && age - stepStart > 8) Complete(skipped: true);
        else if (doneAt >= 0 && age - doneAt > 1.1) Next();
    }

    void Complete(bool skipped = false)
    {
        GD.Print($"[Tuto] {index + 1}/{steps.Count} {steps[index].Title} : {(skipped ? "passée" : "réussie")}");
        doneAt = age;
        check.Visible = true;
        check.PivotOffset = check.Size / 2;
        UiKit.Punch(check, 2.2f, 0.3);
        Sfx.Play(this, SfxKind.Perfect, null, 1.1f);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        bool skip = e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Enter or Key.KpEnter } || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.Back };
        if (skip && doneAt < 0 && index >= 0 && index < steps.Count)
        {
            GetViewport().SetInputAsHandled();
            Complete(skipped: true);
        }
    }

    void Finish()
    {
        GD.Print("[Tuto] Fin de l'entraînement");
        GameSettings.TutorialDone = true;
        GameSettings.Save();
        card.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var layer = new Control { Theme = UiKit.Theme };
        layer.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(layer);
        layer.AddChild(UiKit.Dim(0.55f));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        panel.AddChild(col);
        col.AddChild(UiKit.Title("TU ES PRÊT !", 76));
        var rules = Match?.Rules;
        col.AddChild(UiKit.Text($"Première équipe à {rules?.KoTarget ?? 3} KO gagne la manche. {rules?.MaxRounds ?? 5} manches, prolongation en mort subite.", 24, Colors.White, 6, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text("En ligne, PARTIE RAPIDE te trouve des joueurs, et complète avec des bots.", 22, UiKit.Sky, 6, HorizontalAlignment.Center));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        col.AddChild(row);
        var play = UiKit.Button("VRAIE PARTIE", UiKit.Orange, () => PlayForReal?.Invoke(), 30, new Vector2(340, 70));
        row.AddChild(play);
        row.AddChild(UiKit.Button("CONTINUER ICI", UiKit.Blue, () => { layer.QueueFree(); Input.MouseMode = Input.MouseModeEnum.Captured; }, 30, new Vector2(340, 70)));
        row.AddChild(UiKit.Button("MENU", UiKit.Grey, () => BackToMenu?.Invoke(), 30, new Vector2(200, 70)));
        UiKit.Punch(panel, 0.8f, 0.3);
        Sfx.Play(this, SfxKind.Victory);
        play.CallDeferred(Control.MethodName.GrabFocus);
    }
}
