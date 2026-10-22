using System.Collections.Generic;
using Godot;
using DogeballVR.Online.Steam;

namespace DogeballVR.Online;

/// <summary>Une ligne du détail de l'XP gagnée (« KO ×3 : +90 »).</summary>
public sealed record XpLine(string Label, int Xp);

/// <summary>XP d'un match et niveaux avant et après, pour l'écran de fin.</summary>
public sealed record MatchAward(int Xp, int TotalBefore, IReadOnlyList<XpLine> Lines)
{
    public int LevelBefore => Progression.LevelOf(TotalBefore).Level;
    public int LevelAfter => Progression.LevelOf(TotalBefore + Xp).Level;
}

/// <summary>Ce que le joueur local a fait pendant le match (tableau de l'hôte, valable aussi sur un client).</summary>
public readonly record struct MatchResult(bool Won, int Kos, int Catches, int Perfects, int Passes, bool Mvp, bool Online, int BotLevel);

/// <summary>
/// Niveau du joueur, façon rang de Knockout City : de l'XP à chaque fin de match (jouer, gagner, KO, attrapes, passes),
/// un niveau qui demande un peu plus d'XP à chaque palier, et un titre tous les 10 niveaux. L'XP totale est une stat
/// comme les autres (STAT_XP) : dans progress.cfg (Auto-Cloud) et dans les stats Steam, fusionnées au lancement.
/// </summary>
public static class Progression
{
    public const int MaxLevel = 100;

    static readonly string[] Titles =
    {
        "Recrue", "Lanceur de rue", "Esquiveur", "Attrapeur", "Cador",
        "Pro du quartier", "Vedette", "Champion", "Légende", "Icône du Dogeball",
    };

    /// <summary>Gain du dernier match terminé (null avant le premier) : l'écran de fin l'affiche.</summary>
    public static MatchAward? LastAward { get; private set; }

    public static int TotalXp => Achievements.GetStat(AchievementDefs.StatXp);

    /// <summary>XP pour passer du niveau <paramref name="level"/> au suivant : 350 au début, 800 au niveau 10, 2 800 au niveau 50.</summary>
    public static int XpToNext(int level) => 300 + 50 * level;

    /// <summary>Niveau atteint avec cette XP totale, XP déjà faite dans ce niveau et XP qu'il demande.</summary>
    public static (int Level, int Into, int Need) LevelOf(int totalXp)
    {
        int level = 1, left = Mathf.Max(0, totalXp);
        while (level < MaxLevel && left >= XpToNext(level)) { left -= XpToNext(level); level++; }
        return (level, level >= MaxLevel ? 0 : left, level >= MaxLevel ? 0 : XpToNext(level));
    }

    public static string TitleOf(int level) => Titles[Mathf.Clamp((level - 1) / 10, 0, Titles.Length - 1)];

    /// <summary>Calcule l'XP du match, l'ajoute au profil et débloque les succès de niveau.</summary>
    public static MatchAward Award(MatchResult r)
    {
        var lines = new List<XpLine> { new("Match joué", 100) };
        if (r.Won) lines.Add(new("Victoire", 150));
        if (r.Kos > 0) lines.Add(new($"KO ×{r.Kos}", 30 * r.Kos));
        if (r.Catches > 0) lines.Add(new($"Attrapes ×{r.Catches}", 10 * r.Catches));
        if (r.Perfects > 0) lines.Add(new($"Arrêts parfaits ×{r.Perfects}", 20 * r.Perfects));
        if (r.Passes > 0) lines.Add(new($"Passes ×{Mathf.Min(r.Passes, 20)}", 5 * Mathf.Min(r.Passes, 20)));   // plafond : pas de farm de passes
        if (r.Mvp) lines.Add(new("MVP", 50));
        int xp = 0;
        foreach (var l in lines) xp += l.Xp;
        // Hors ligne, contre des bots faciles ou normaux, l'XP est réduite (le jeu en ligne et le niveau difficile rapportent le plus).
        if (!r.Online && r.BotLevel < 2)
        {
            float k = r.BotLevel <= 0 ? 0.5f : 0.8f;
            int cut = Mathf.RoundToInt(xp * k) - xp;
            lines.Add(new($"Bots {(r.BotLevel <= 0 ? "faciles" : "normaux")} ×{k.ToString(System.Globalization.CultureInfo.GetCultureInfo("fr-FR"))}", cut));
            xp += cut;
        }
        int before = TotalXp;
        Achievements.AddStat(AchievementDefs.StatXp, xp);
        var award = new MatchAward(xp, before, lines);
        LastAward = award;
        GD.Print($"[Profil] +{xp} XP, niveau {award.LevelBefore} -> {award.LevelAfter} ({string.Join(", ", lines.ConvertAll(l => $"{l.Label} {l.Xp:+0;-0}"))})");
        if (award.LevelAfter >= 10) Achievements.Unlock(AchievementDefs.Level10);
        if (award.LevelAfter >= 25) Achievements.Unlock(AchievementDefs.Level25);
        return award;
    }
}
