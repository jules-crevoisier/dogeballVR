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
    const float CardWidth = 520f;
    // Test (CI) : `--tutorial-test` passe seule une étape restée 8 s sans être faite, pour parcourir tout l'entraînement.
    static readonly bool autoTest = Tools.DevFlags.Has("--tutorial-test");
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
        steps.Add(new("DÉPLACE-TOI", () => Pad("Le [color=#ffd23f]stick gauche[/color] fait courir sur le toit.", L.F("{0} {1} {2} {3} pour courir sur le toit.", K("move_forward"), K("move_left"), K("move_back"), K("move_right"))), p => p.WalkTravel - start["walk"] > 8f));
        steps.Add(new("SAUTE", () => L.F("{0} pour sauter par-dessus un muret.", K("jump")), p => p.Jumps > start["jumps"]));
        if (Player.MapHasPads)
            steps.Add(new("TREMPLIN", () => "Marche sur un [color=#d2f53c]tremplin[/color] (le disque lumineux au sol) : il te lance vers une plateforme plus haute, d'où l'on tire en plongée.", p => p.Pads > start["pads"]));
        steps.Add(new("ESQUIVE", () => L.F("{0} : un bond éclair, invulnérable pendant l'élan. Tu as deux charges : enchaîne-en deux.", K("dash")), p => p.Dashes - start["dashes"] >= 2));
        steps.Add(new("RAMASSE UNE BALLE", () => "Cours sur une balle pour la prendre. Les balles réapparaissent sur les plots lumineux.", p => p.HeldBall != null));
        steps.Add(new("LANCER CHARGÉ", () => L.F("Maintiens {0} pour charger (la jauge se remplit), relâche pour lancer.", K("fire")), p => p.ChargedThrows > start["charged"]));
        steps.Add(new("TOUCHE UN ADVERSAIRE", () => L.F("Vise un bleu (le viseur prend sa couleur) et lance avec {0}. L'aide à la visée corrige les presque-touches.", K("fire")), _ => hitsByMe > 0));
        steps.Add(new(CatchTitle, () => L.F("Quand un bleu te vise (liseré bleu au bord de l'écran, chevron près du viseur), appuie sur {0} juste avant l'impact. Approche-toi d'eux pour qu'ils te visent.", K("catch")), p => p.Catches > start["catches"]));
        steps.Add(new("PASSE À UN ALLIÉ", () => L.F("Balle en main, {0} l'envoie à ton coéquipier orange : une passe charge la balle.", K("pass")), p => p.Passes > start["passes"]));
        steps.Add(new("FEINTE", () => L.F("Balle en main, {0} fait mine de lancer : l'adversaire attrape dans le vide.", K("feint")), p => p.Feints > start["feints"]));
        if (Player.FreeRoam)
            steps.Add(new("TACLE", () => L.F("Fonce sur un bleu avec {0} : il est bousculé et lâche sa balle. Au bord du vide, il tombe : [color=#d2f53c]éjecté[/color], c'est un KO pour toi. Attention, ça marche aussi contre toi.", K("dash")), p => p.Tackles > start["tackles"]));
        if (Match != null)
            steps.Add(new("BALLE SPÉCIALE", () => L.F("Une [color=#ffd23f]bombe[/color] tombe près de toi : elle explose à l'impact et touche tout autour. "
                + "Ramasse-la (lance d'abord la balle que tu tiens) et envoie-la sur les bleus avec {0}.", K("fire")), SpecialDone, DropSpecial));

        var root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        // Objectif à gauche, comme dans les jeux d'action en ligne : plaque d'encre du HUD (coins biseautés), titre en Anton,
        // consigne en Barlow, barre de progression lime ; le centre de l'écran reste libre pour viser.
        card = new PanelContainer { CustomMinimumSize = new Vector2(CardWidth, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", DesktopHud.PlateBox(new Color(UiKit.Navy, 0.86f), 22, 16, 14));
        card.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
        card.OffsetLeft = 32; card.OffsetRight = 32 + CardWidth; card.OffsetTop = 190;
        root.AddChild(card);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 6);
        card.AddChild(col);
        var head = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(head);
        counter = UiKit.Text("", 15, UiKit.Yellow, 0);
        counter.AddThemeFontOverride("font", UiKit.Heavy);
        counter.SizeFlagsHorizontal = Control.SizeFlags.ExpandFill;
        head.AddChild(counter);
        skipHint = UiKit.Text("", 15, new Color(1, 1, 1, 0.55f), 0);
        head.AddChild(skipHint);
        title = UiKit.Text("", 36, Colors.White, 0);
        title.AddThemeFontOverride("font", UiKit.Display);
        col.AddChild(title);
        body = new RichTextLabel { BbcodeEnabled = true, FitContent = true, ScrollActive = false, CustomMinimumSize = new Vector2(CardWidth - 44, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        body.AddThemeFontSizeOverride("normal_font_size", 21);
        body.AddThemeConstantOverride("outline_size", 0);
        col.AddChild(body);
        col.AddChild(new Control { CustomMinimumSize = new Vector2(0, 4), MouseFilter = Control.MouseFilterEnum.Ignore });
        var barBack = new ColorRect { Color = new Color(1, 1, 1, 0.14f), CustomMinimumSize = new Vector2(0, 4), MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddChild(barBack);
        progress = new ColorRect { Color = UiKit.Yellow, Size = new Vector2(0, 4), MouseFilter = Control.MouseFilterEnum.Ignore };
        barBack.AddChild(progress);
        check = UiKit.Text("✔", 56, UiKit.Yellow, 0, HorizontalAlignment.Center);
        check.AddThemeColorOverride("font_shadow_color", new Color(0, 0, 0, 0.5f));
        check.AddThemeConstantOverride("shadow_offset_y", 3);
        check.Position = new Vector2(32 + CardWidth - 70, 196);   // coin haut droit de la carte (hors conteneur : posé à la main)
        check.Size = new Vector2(56, 64);
        check.Visible = false;
        root.AddChild(check);
        // Captures : `--tutorial-step=N` commence à l'étape N.
        foreach (var arg in Tools.DevFlags.Args)
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
            ["pads"] = Player.Pads, ["tackles"] = Player.Tackles,
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
        counter.Text = L.F("ENTRAÎNEMENT  ·  {0}/{1}", index + 1, steps.Count);
        title.Text = s.Title;
        s.Enter?.Invoke();
        RefreshTexts();
        progress.Size = new Vector2((CardWidth - 44) * index / steps.Count, 4);
        card.PivotOffset = new Vector2(0, 40);
        UiKit.Punch(card, 1.06f, 0.2);
        Sfx.Play(this, SfxKind.Tier, null, 1.2f, -4f);
    }

    /// <summary>Textes selon l'appareil : on touche la manette, les consignes passent aux boutons (et inversement).</summary>
    void RefreshTexts()
    {
        shownPad = InputSetup.UsingPad;
        skipHint.Text = Pad("Vue : passer", "Entrée : passer");
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
        int koTarget = KcRules.Def.KoTarget > 0 ? KcRules.Def.KoTarget : rules?.KoTarget ?? 3;   // seuil de la carte (6 sur Croix Néon), sinon celui de la partie
        col.AddChild(UiKit.Text(L.F("Première équipe à {0} KO gagne la manche. {1} manches, prolongation en mort subite.\n1 cœur par coup, et attraper ne soigne pas.", koTarget, rules?.MaxRounds ?? 5), 24, Colors.White, 6, HorizontalAlignment.Center));
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
