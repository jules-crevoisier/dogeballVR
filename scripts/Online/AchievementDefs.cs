using System.Collections.Generic;

namespace DogeballVR.Online;

/// <summary>Un succès Steam : id API (identique dans Steamworks), textes FR/EN, stat de progression et seuil éventuels.</summary>
public sealed record AchievementDef(string Id, string NameFr, string NameEn, string DescFr, string DescEn, string? Stat = null, int Threshold = 0, bool Hidden = false);

/// <summary>Une stat Steam entière (type INT dans Steamworks, « incrément seulement »).</summary>
public sealed record StatDef(string Id, string NameFr, string NameEn);

/// <summary>
/// Liste des succès et des stats, à recopier dans Steamworks (docs/steam/achievements.csv est tenu à la main d'après ce fichier).
/// Un succès avec stat se débloque tout seul quand la stat atteint le seuil (voir Achievements.AddStat).
/// </summary>
public static class AchievementDefs
{
    public const string StatWins = "STAT_WINS";
    public const string StatKos = "STAT_KOS";
    public const string StatCatches = "STAT_CATCHES";
    public const string StatTackles = "STAT_TACKLES";
    public const string StatMatches = "STAT_MATCHES";

    public const string FirstWin = "ACH_FIRST_WIN";
    public const string Ko10 = "ACH_KO_10";
    public const string Ko100 = "ACH_KO_100";
    public const string PerfectCatch = "ACH_PERFECT_CATCH";
    public const string Catch50 = "ACH_CATCH_50";
    public const string Assist = "ACH_ASSIST";
    public const string ChargedKo = "ACH_CHARGED_KO";
    public const string RingOut = "ACH_RING_OUT";
    public const string Tackle = "ACH_TACKLE";
    public const string Flawless = "ACH_FLAWLESS";
    public const string OnlineFriend = "ACH_ONLINE_FRIEND";
    public const string VrPlayer = "ACH_VR_PLAYER";

    public static readonly IReadOnlyList<StatDef> Stats = new StatDef[]
    {
        new(StatWins, "Victoires", "Wins"),
        new(StatKos, "KO infligés", "Knockouts"),
        new(StatCatches, "Attrapes", "Catches"),
        new(StatTackles, "Tacles", "Tackles"),
        new(StatMatches, "Matchs joués", "Matches played"),
    };

    public static readonly IReadOnlyList<AchievementDef> All = new AchievementDef[]
    {
        new(FirstWin, "Première victoire", "First Victory", "Remporter un match.", "Win a match.", StatWins, 1),
        new(Ko10, "Frappeur", "Heavy Hitter", "Mettre 10 adversaires KO.", "Knock out 10 opponents.", StatKos, 10),
        new(Ko100, "Légende du dodgeball", "Dodgeball Legend", "Mettre 100 adversaires KO.", "Knock out 100 opponents.", StatKos, 100),
        new(PerfectCatch, "Main de fer", "Iron Grip", "Réussir une attrape parfaite sur un tir adverse.", "Land a perfect catch on an enemy throw."),
        new(Catch50, "Gardien", "Goalkeeper", "Attraper 50 tirs adverses.", "Catch 50 enemy throws.", StatCatches, 50),
        new(Assist, "Passe décisive", "Assist", "Faire une passe à un coéquipier qui met KO dans la foulée.", "Pass to a teammate who scores a knockout right after."),
        new(ChargedKo, "Pleine charge", "Fully Charged", "Mettre KO avec une balle chargée au palier 2 ou plus.", "Score a knockout with a ball charged to level 2 or more."),
        new(RingOut, "Sortie de piste", "Ring Out", "Pousser un adversaire dans la fosse.", "Push an opponent into the pit."),
        new(Tackle, "Plaquage", "Tackle", "Tacler un adversaire en plein dash.", "Tackle an opponent with a dash.", StatTackles, 1),
        new(Flawless, "Sans bavure", "Flawless", "Remporter un match sans perdre une manche.", "Win a match without losing a round."),
        new(OnlineFriend, "Entre potes", "Squad Up", "Jouer un match en ligne avec un ami Steam.", "Play an online match with a Steam friend."),
        new(VrPlayer, "Immersion totale", "Full Immersion", "Jouer une partie en réalité virtuelle.", "Play a match in virtual reality."),
    };

    public static AchievementDef? Find(string id)
    {
        foreach (var a in All) if (a.Id == id) return a;
        return null;
    }
}
