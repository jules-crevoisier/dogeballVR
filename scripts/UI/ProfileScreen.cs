using System;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Online;
using DogeballVR.Online.Steam;

namespace DogeballVR.UI;

/// <summary>
/// Écran Profil du menu : niveau, titre et barre d'XP, statistiques de carrière, vestiaire (couvre-chefs débloqués par niveau,
/// essayés sur un aperçu 3D tournant), et liste des succès (débloqués en jaune,
/// les autres grisés avec leur progression). Tout vient de progress.cfg, le même fichier que Steam recopie (Auto-Cloud).
/// </summary>
public partial class ProfileScreen : Control
{
    /// <summary>Retour au menu (bouton RETOUR ; Échap est géré par le menu principal).</summary>
    public Action? Back { get; set; }

    public override void _Ready()
    {
        Theme = UiKit.Theme;
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(UiKit.Dim(0.82f));

        var center = new CenterContainer { MouseFilter = MouseFilterEnum.Ignore };
        center.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1720, 0) };
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelBox(22, 34));   // la fenêtre du menu principal
        center.AddChild(panel);
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        panel.AddChild(col);

        // En-tête : nom, niveau, titre, barre.
        int xp = Progression.TotalXp;
        var (level, into, need) = Progression.LevelOf(xp);
        var head = new HBoxContainer();
        head.AddThemeConstantOverride("separation", 22);
        col.AddChild(head);
        var badge = new PanelContainer { CustomMinimumSize = new Vector2(104, 104) };
        badge.AddThemeStyleboxOverride("panel", Badge());
        var badgeCol = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        badgeCol.AddThemeConstantOverride("separation", -10);
        badgeCol.AddChild(UiKit.Text("NIVEAU", 18, UiKit.Navy, 0, HorizontalAlignment.Center));
        badgeCol.AddChild(UiKit.Text(level.ToString(), 52, UiKit.Navy, 0, HorizontalAlignment.Center));
        badge.AddChild(badgeCol);
        head.AddChild(badge);
        var who = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill, Alignment = BoxContainer.AlignmentMode.Center };
        who.AddThemeConstantOverride("separation", 4);
        var nameRow = new HBoxContainer();
        var name = UiKit.Title(Identity.Name.ToUpper(), 50, HorizontalAlignment.Left);
        name.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        nameRow.AddChild(name);
        nameRow.AddChild(UiKit.Text(need > 0 ? $"{XpBanner.Spaced(into)} / {XpBanner.Spaced(need)} XP" : "NIVEAU MAX", 22, UiKit.Sky, 6, HorizontalAlignment.Right));
        who.AddChild(nameRow);
        who.AddChild(UiKit.Text($"{Progression.TitleOf(level)}  ·  {XpBanner.Spaced(xp)} XP au total", 24, UiKit.Cream, 6));
        float fill = need > 0 ? (float)into / need : 1f;
        var bar = new Control { CustomMinimumSize = new Vector2(0, 18) };
        bar.Draw += () =>
        {
            bar.DrawStyleBox(Gauge(false), new Rect2(Vector2.Zero, bar.Size));
            if (fill > 0) bar.DrawStyleBox(Gauge(true), new Rect2(Vector2.Zero, new Vector2(Mathf.Max(bar.Size.Y * 2, bar.Size.X * fill), bar.Size.Y)));
        };
        who.AddChild(bar);
        head.AddChild(who);

        var body = new HBoxContainer();
        body.AddThemeConstantOverride("separation", 28);
        col.AddChild(body);

        // Statistiques de carrière.
        var stats = new VBoxContainer { CustomMinimumSize = new Vector2(380, 0) };
        stats.AddThemeConstantOverride("separation", 6);
        stats.AddChild(Heading("STATISTIQUES"));
        int matches = Achievements.GetStat(AchievementDefs.StatMatches), wins = Achievements.GetStat(AchievementDefs.StatWins);
        StatRow(stats, "Matchs joués", matches.ToString());
        StatRow(stats, "Victoires", matches > 0 ? $"{wins}  ({100 * wins / matches} %)" : wins.ToString());
        StatRow(stats, "KO infligés", Achievements.GetStat(AchievementDefs.StatKos).ToString());
        StatRow(stats, "Balles attrapées", Achievements.GetStat(AchievementDefs.StatCatches).ToString());
        StatRow(stats, "Arrêts parfaits", Achievements.GetStat(AchievementDefs.StatPerfects).ToString());
        StatRow(stats, "Passes", Achievements.GetStat(AchievementDefs.StatPasses).ToString());
        StatRow(stats, "Tacles", Achievements.GetStat(AchievementDefs.StatTackles).ToString());
        body.AddChild(stats);
        body.AddChild(Wardrobe(level));

        // Succès : débloqués d'abord.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 6);
        int unlocked = 0;
        foreach (var a in AchievementDefs.All) if (Achievements.IsUnlocked(a.Id)) unlocked++;
        right.AddChild(Heading($"SUCCÈS  {unlocked} / {AchievementDefs.All.Count}"));
        // Manette et Deck : chaque succès prend le focus (croix haut/bas) et la liste défile pour le suivre.
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 470), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled, FollowFocus = true };
        var list = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        list.AddThemeConstantOverride("separation", 6);
        scroll.AddChild(list);
        right.AddChild(scroll);
        foreach (bool wanted in new[] { true, false })
            foreach (var a in AchievementDefs.All)
                if (Achievements.IsUnlocked(a.Id) == wanted) list.AddChild(AchievementRow(a, wanted));
        body.AddChild(right);

        var back = UiKit.Button("RETOUR", UiKit.Blue, () => Back?.Invoke(), 28, new Vector2(300, 64));
        back.SizeFlagsHorizontal = SizeFlags.ShrinkCenter;
        col.AddChild(back);
        back.CallDeferred(Control.MethodName.GrabFocus);
        // Fondu (pas de glissé : le conteneur centré replace le panneau à chaque mise en page).
        panel.Modulate = new Color(1, 1, 1, 0);
        panel.CreateTween().TweenProperty(panel, "modulate:a", 1.0, 0.2);
    }

    // ------------------------------------------------------------------ vestiaire

    const float PreviewW = 520f, PreviewH = 286f;
    Node3D? turntable;
    Visual.Avatar? model;
    Label wearName = null!, wearState = null!;
    string shown = "";
    readonly System.Collections.Generic.List<(Button tile, HeadwearDef def)> tiles = new();

    /// <summary>
    /// Colonne du vestiaire : aperçu 3D du personnage (tourne doucement), nom et état du couvre-chef montré, grille des 9 couvre-chefs.
    /// Survol ou focus : on l'essaie sur l'aperçu, même verrouillé (on voit ce qu'on gagnera). Clic ou Entrée : on le porte s'il est débloqué.
    /// </summary>
    Control Wardrobe(int level)
    {
        var col = new VBoxContainer { CustomMinimumSize = new Vector2(PreviewW, 0) };
        col.AddThemeConstantOverride("separation", 6);
        int owned = 0;
        foreach (var h in Cosmetics.Headwear) if (Cosmetics.IsUnlocked(h, level)) owned++;
        col.AddChild(Heading($"VESTIAIRE  {owned} / {Cosmetics.Headwear.Count}"));

        var frame = new PanelContainer { CustomMinimumSize = new Vector2(PreviewW, PreviewH) };
        var frameBox = UiKit.InsetBox(UiKit.NavySoft, 0);
        frameBox.ContentMarginLeft = frameBox.ContentMarginRight = frameBox.ContentMarginTop = frameBox.ContentMarginBottom = 0;
        frame.AddThemeStyleboxOverride("panel", frameBox);
        var view = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        frame.AddChild(view);
        var world = new SubViewport { OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, HandleInputLocally = false };
        view.AddChild(world);
        BuildPreview(world);
        // Nom et état par-dessus l'aperçu, en bas à gauche.
        var caption = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, SizeFlagsVertical = SizeFlags.ShrinkEnd };
        caption.AddThemeConstantOverride("separation", -4);
        var captionPad = new MarginContainer { MouseFilter = MouseFilterEnum.Ignore };
        captionPad.AddThemeConstantOverride("margin_left", 16);
        captionPad.AddThemeConstantOverride("margin_bottom", 10);
        wearName = UiKit.Title("", 34, HorizontalAlignment.Left);
        wearState = UiKit.Text("", 18, UiKit.Yellow, 4);
        caption.AddChild(wearName);
        caption.AddChild(wearState);
        captionPad.AddChild(caption);
        frame.AddChild(captionPad);
        col.AddChild(frame);

        var grid = new GridContainer { Columns = 3 };
        grid.AddThemeConstantOverride("h_separation", 6);
        grid.AddThemeConstantOverride("v_separation", 6);
        foreach (var h in Cosmetics.Headwear) grid.AddChild(Tile(h, level));
        grid.MouseExited += () => Show(Cosmetics.Equipped);
        col.AddChild(grid);
        Show(Cosmetics.Equipped);
        return col;
    }

    void BuildPreview(SubViewport world)
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.86f, 0.88f, 1f), AmbientLightEnergy = 0.6f,
        };
        world.AddChild(new WorldEnvironment { Environment = env });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-30, -30, 0), LightEnergy = 1.1f, LightColor = new Color("fff0dc") });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-10, 150, 0), LightEnergy = 0.5f, LightColor = new Color("d2f53c") });   // liseré lime dans le dos
        world.AddChild(new Camera3D { Position = new Vector3(0, 1.58f, 1.8f), RotationDegrees = new Vector3(-4, 0, 0), Fov = 30f, Current = true });   // buste et tête
        turntable = new Node3D { Name = "Plateau", RotationDegrees = new Vector3(0, 180 - 25, 0) };   // de trois quarts, face caméra (+Z)
        world.AddChild(turntable);
        model = Visual.Avatar.Create(0, false);
        model.ProcessMode = ProcessModeEnum.Disabled;   // hors jeu : seule l'animation tourne
        turntable.AddChild(model);
        model.Pose(new Vector3(0, 1.62f, 0), 0f, new Vector3(-0.35f, 1.0f, -0.2f), new Vector3(0.35f, 1.0f, -0.2f));
        if (model.FindChild("AnimationPlayer", true, false) is AnimationPlayer ap && ap.HasAnimation("Repos"))
        {
            ap.ProcessMode = ProcessModeEnum.Pausable;
            ap.Play("Repos");
        }
    }

    Button Tile(HeadwearDef h, int level)
    {
        bool open = Cosmetics.IsUnlocked(h, level);
        var b = new Button { CustomMinimumSize = new Vector2(169, 62), FocusMode = FocusModeEnum.All, ClipText = true };
        var label = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        label.AddThemeConstantOverride("separation", -4);
        label.AddChild(UiKit.Text(h.Name, 18, open ? Colors.White : new Color(1, 1, 1, 0.45f), 0, HorizontalAlignment.Center));
        label.AddChild(UiKit.Text(open ? (h.Level > 1 ? $"niveau {h.Level}" : "de base") : $"verrouillé · niveau {h.Level}", 14,
            open ? UiKit.Sky : new Color(UiKit.Sky, 0.6f), 0, HorizontalAlignment.Center));
        b.AddChild(label);
        b.FocusEntered += () => { Show(h.Id); Sfx.Play(b, SfxKind.UiHover); };
        b.MouseEntered += () => Show(h.Id);
        b.Pressed += () =>
        {
            if (Cosmetics.Equip(h.Id)) { Sfx.Play(b, SfxKind.UiClick); RefreshTiles(); Show(h.Id); }
            else Sfx.Play(b, SfxKind.UiBack);
        };
        tiles.Add((b, h));
        StyleTile(b, h);
        return b;
    }

    void RefreshTiles() { foreach (var (b, h) in tiles) StyleTile(b, h); }

    /// <summary>Case du vestiaire : encart sombre ; porté : fond lime pâle et liseré lime ; focus : liseré épais.</summary>
    static void StyleTile(Button b, HeadwearDef h)
    {
        bool worn = Cosmetics.Equipped == h.Id;
        var normal = worn ? UiKit.InsetBox(new Color(UiKit.Yellow, 0.16f), 0, 2, UiKit.Yellow) : UiKit.InsetBox(null, 0);
        var lit = normal.Copy();
        lit.BorderWidth = 4;
        lit.BorderColor = UiKit.Yellow;
        b.AddThemeStyleboxOverride("normal", normal);
        b.AddThemeStyleboxOverride("disabled", normal);
        b.AddThemeStyleboxOverride("hover", lit);
        b.AddThemeStyleboxOverride("pressed", lit);
        b.AddThemeStyleboxOverride("focus", lit);
    }

    /// <summary>Essaie un couvre-chef sur l'aperçu et met à jour le nom et l'état affichés dessous.</summary>
    void Show(string id)
    {
        if (Cosmetics.Find(id) is not { } h) return;
        if (shown != id) { shown = id; model?.SetHeadwear(id); }
        wearName.Text = h.Name.ToUpper();
        bool open = Cosmetics.IsUnlocked(h, Cosmetics.Level);
        wearState.Text = Cosmetics.Equipped == id ? "PORTÉ" : open ? "Entrée ou clic : porter" : $"Se débloque au niveau {h.Level}";
        wearState.AddThemeColorOverride("font_color", Cosmetics.Equipped == id ? UiKit.Yellow : open ? UiKit.Cream : UiKit.Sky);
    }

    public override void _Process(double delta)
    {
        if (turntable != null) turntable.RotateY((float)delta * 0.35f);
    }

    static Label Heading(string text) => UiKit.Title(text, 32, HorizontalAlignment.Left);

    static void StatRow(VBoxContainer parent, string label, string value)
    {
        var row = new PanelContainer();
        var box = UiKit.InsetBox(null, 10);
        box.ContentMarginTop = box.ContentMarginBottom = 6;
        row.AddThemeStyleboxOverride("panel", box);
        var h = new HBoxContainer();
        var l = UiKit.Text(label, 24, UiKit.Cream, 4);
        l.SizeFlagsHorizontal = SizeFlags.ExpandFill;
        h.AddChild(l);
        h.AddChild(UiKit.Text(value, 26, Colors.White, 6, HorizontalAlignment.Right));
        row.AddChild(h);
        parent.AddChild(row);
    }

    static Control AchievementRow(AchievementDef a, bool unlocked)
    {
        var row = new PanelContainer { FocusMode = FocusModeEnum.All, MouseFilter = MouseFilterEnum.Pass };
        // Débloqué : encart doré à liseré jaune (comme la barre du menu) ; à faire : encart sombre. Sélectionné : liseré jaune épais.
        var box = unlocked ? UiKit.InsetBox(new Color(UiKit.Gold, 0.2f), 10, 2, new Color(UiKit.Yellow, 0.8f)) : UiKit.InsetBox(null, 10);
        box.ContentMarginTop = box.ContentMarginBottom = 6;
        var lit = box.Copy();
        lit.BorderWidth = 4;
        lit.BorderColor = UiKit.Yellow;
        row.AddThemeStyleboxOverride("panel", box);
        row.FocusEntered += () => { row.AddThemeStyleboxOverride("panel", lit); Sfx.Play(row, SfxKind.UiHover); };
        row.FocusExited += () => row.AddThemeStyleboxOverride("panel", box);
        var h = new HBoxContainer();
        h.AddThemeConstantOverride("separation", 14);
        h.AddChild(UiKit.Text(unlocked ? "★" : "·", 30, unlocked ? UiKit.Yellow : UiKit.Grey, 4, HorizontalAlignment.Center));
        var text = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        text.AddThemeConstantOverride("separation", -2);
        bool secret = a.Hidden && !unlocked;
        text.AddChild(UiKit.Text(secret ? "Succès caché" : a.NameFr, 24, unlocked ? Colors.White : new Color(1, 1, 1, 0.6f), 4));
        text.AddChild(UiKit.Text(secret ? "À découvrir en jouant." : a.DescFr, 18, unlocked ? UiKit.Cream : new Color(UiKit.Cream, 0.5f), 3));
        h.AddChild(text);
        if (!unlocked && a.Stat != null && a.Threshold > 1)
            h.AddChild(UiKit.Text($"{Mathf.Min(Achievements.GetStat(a.Stat), a.Threshold)} / {a.Threshold}", 20, UiKit.Sky, 4, HorizontalAlignment.Right));
        row.AddChild(h);
        return row;
    }

    /// <summary>Pastille du niveau : la barre jaune du menu (jaune vers or, penchée, ombre encre).</summary>
    public static KcBox Badge(float slant = 12f)
    {
        var b = new KcBox { Top = UiKit.Yellow, Bottom = UiKit.Gold, Slant = slant, ShadowOffset = new Vector2(6, 6), ShadowColor = new Color("0a0f22", 0.55f) };
        b.ContentMarginLeft = b.ContentMarginRight = b.ContentMarginTop = b.ContentMarginBottom = 0;
        return b;
    }

    /// <summary>Jauge d'XP penchée : rail sombre, remplissage jaune vers or.</summary>
    public static KcBox Gauge(bool filled, float slant = 8f)
    {
        var b = filled ? new KcBox { Top = UiKit.Yellow, Bottom = UiKit.Gold, Slant = slant } : UiKit.InsetBox(null, slant);
        b.ContentMarginLeft = b.ContentMarginRight = b.ContentMarginTop = b.ContentMarginBottom = 0;
        return b;
    }
}
