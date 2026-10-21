using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;

namespace DogeballVR.UI;

/// <summary>
/// Entraînement guidé du joueur clavier-souris, joué dans une vraie partie hors ligne contre des bots doux (<see cref="Bot.Gentle"/>).
/// Une carte en haut de l'écran donne une consigne à la fois (avec les touches réellement assignées) ; chaque consigne
/// se valide toute seule en la faisant (compteurs de <see cref="DesktopPlayer"/>), avec un tampon vert et un son. Entrée passe l'étape.
/// </summary>
public partial class Tutorial : CanvasLayer
{
    public DesktopPlayer Player { get; set; } = null!;
    /// <summary>Fin de l'entraînement : jouer une vraie partie ou revenir au menu.</summary>
    public Action? PlayForReal { get; set; }
    public Action? BackToMenu { get; set; }

    sealed record Step(string Title, Func<string> Text, Func<DesktopPlayer, bool> Done);

    readonly List<Step> steps = new();
    int index = -1;
    double doneAt = -1, age;
    int hitsByMe;
    Dictionary<string, float> start = new();

    PanelContainer card = null!;
    Label counter = null!, title = null!, check = null!;
    RichTextLabel body = null!;
    ColorRect progress = null!;

    static string K(string action) => $"[color=#ffd23f]{InputSetup.KeyName(action)}[/color]";

    public override void _Ready()
    {
        Layer = 30;
        Bot.Gentle = true;
        MatchController.HitScored += OnHit;
        steps.Add(new("REGARDE AUTOUR DE TOI", () => "Bouge la souris pour tourner la caméra.", p => p.LookTravel - start["look"] > 3.5f));
        steps.Add(new("DÉPLACE-TOI", () => $"{K("move_forward")} {K("move_left")} {K("move_back")} {K("move_right")} pour courir sur le toit.", p => p.WalkTravel - start["walk"] > 8f));
        steps.Add(new("SAUTE", () => $"{K("jump")} pour sauter par-dessus un muret.", p => p.Jumps > start["jumps"]));
        steps.Add(new("DASH", () => $"{K("dash")} : un dash rapide, invulnérable. Tu as deux charges : enchaîne-en deux.", p => p.Dashes - start["dashes"] >= 2));
        steps.Add(new("RAMASSE UNE BALLE", () => "Cours sur une balle jaune (ou sur le plot de ton camp) pour la prendre.", p => p.HeldBall != null));
        steps.Add(new("LANCER CHARGÉ", () => $"Maintiens {K("fire")} pour charger (la jauge se remplit), relâche pour lancer.", p => p.ChargedThrows > start["charged"]));
        steps.Add(new("TOUCHE UN ADVERSAIRE", () => $"Vise un bleu (le viseur devient rouge) et lance avec {K("fire")}. L'aide à la visée corrige les presque-touches.", _ => hitsByMe > 0));
        steps.Add(new("ATTRAPE UN TIR", () => $"Quand un bleu te vise (alerte rouge), appuie sur {K("catch")} juste avant l'impact. Approche-toi d'eux pour qu'ils te visent.", p => p.Catches > start["catches"]));
        steps.Add(new("PASSE À UN ALLIÉ", () => $"Balle en main, {K("pass")} l'envoie à ton coéquipier orange : une passe charge la balle.", p => p.Passes > start["passes"]));
        steps.Add(new("FEINTE", () => $"Balle en main, {K("feint")} fait mine de lancer : l'adversaire attrape dans le vide.", p => p.Feints > start["feints"]));

        var root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        card = new PanelContainer { CustomMinimumSize = new Vector2(760, 0), MouseFilter = Control.MouseFilterEnum.Ignore };
        card.AddThemeStyleboxOverride("panel", UiKit.Box(new Color(UiKit.Navy, 0.88f), 20, 0f, 3, UiKit.Yellow, 8));
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
        head.AddChild(UiKit.Text("Entrée : passer", 18, new Color(1, 1, 1, 0.6f), 4));
        title = UiKit.Text("", 44, UiKit.Yellow, 10);
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
        CallDeferred(nameof(Next));
    }

    public override void _ExitTree()
    {
        Bot.Gentle = false;
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
        check.Visible = false;
        if (index >= steps.Count) { Finish(); return; }
        Snapshot();
        var s = steps[index];
        counter.Text = $"ENTRAÎNEMENT  {index + 1}/{steps.Count}";
        title.Text = s.Title;
        body.Text = s.Text();
        progress.Size = new Vector2(card.Size.X > 0 ? (card.Size.X - 52) * index / steps.Count : 0, 8);
        card.PivotOffset = new Vector2(380, 60);
        UiKit.Punch(card, 1.12f, 0.25);
        Sfx.Play(this, SfxKind.Tier, null, 1.2f, -4f);
    }

    public override void _Process(double delta)
    {
        age += delta;
        if (index < 0 || index >= steps.Count || Player == null || !IsInstanceValid(Player)) return;
        if (doneAt < 0 && steps[index].Done(Player)) Complete();
        else if (doneAt >= 0 && age - doneAt > 1.1) Next();
    }

    void Complete()
    {
        doneAt = age;
        check.Visible = true;
        check.PivotOffset = check.Size / 2;
        UiKit.Punch(check, 2.2f, 0.3);
        Sfx.Play(this, SfxKind.Perfect, null, 1.1f);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, PhysicalKeycode: Key.Enter or Key.KpEnter } && doneAt < 0 && index < steps.Count)
        {
            GetViewport().SetInputAsHandled();
            Complete();
        }
    }

    void Finish()
    {
        GameSettings.TutorialDone = true;
        GameSettings.Save();
        card.Visible = false;
        Input.MouseMode = Input.MouseModeEnum.Visible;
        var layer = new Control { Theme = UiKit.Theme };
        layer.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(layer);
        layer.AddChild(UiKit.Dim(0.55f));
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        layer.AddChild(center);
        var panel = new PanelContainer();
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 16);
        panel.AddChild(col);
        col.AddChild(UiKit.Text("TU ES PRÊT !", 72, UiKit.Yellow, 16, HorizontalAlignment.Center));
        col.AddChild(UiKit.Text("Première équipe à 3 KO gagne la manche. 5 manches, prolongation en mort subite.", 24, Colors.White, 6, HorizontalAlignment.Center));
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
