using System;
using Godot;
using DogeballVR.Online;
using DogeballVR.Online.Steam;

namespace DogeballVR.UI;

/// <summary>
/// Écran Profil du menu : niveau, titre et barre d'XP, statistiques de carrière, et liste des succès (débloqués en jaune,
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
        var panel = new PanelContainer { CustomMinimumSize = new Vector2(1280, 0) };
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
        var stats = new VBoxContainer { CustomMinimumSize = new Vector2(420, 0) };
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

        // Succès : débloqués d'abord.
        var right = new VBoxContainer { SizeFlagsHorizontal = SizeFlags.ExpandFill };
        right.AddThemeConstantOverride("separation", 6);
        int unlocked = 0;
        foreach (var a in AchievementDefs.All) if (Achievements.IsUnlocked(a.Id)) unlocked++;
        right.AddChild(Heading($"SUCCÈS  {unlocked} / {AchievementDefs.All.Count}"));
        var scroll = new ScrollContainer { CustomMinimumSize = new Vector2(0, 470), HorizontalScrollMode = ScrollContainer.ScrollMode.Disabled };
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
        var row = new PanelContainer();
        // Débloqué : encart doré à liseré jaune (comme la barre du menu) ; à faire : encart sombre.
        var box = unlocked ? UiKit.InsetBox(new Color(UiKit.Gold, 0.2f), 10, 2, new Color(UiKit.Yellow, 0.8f)) : UiKit.InsetBox(null, 10);
        box.ContentMarginTop = box.ContentMarginBottom = 6;
        row.AddThemeStyleboxOverride("panel", box);
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
