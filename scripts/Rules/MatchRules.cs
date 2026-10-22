using System;
using System.Collections.Generic;

namespace DogeballVR.Rules;

/// <summary>
/// Règles pures (sans Godot) : vies, manches, score. Exécutées côté serveur, testables à part.
/// 2v2 nerveux : 2 vies par joueur, un joueur à 0 vie est un KO et revient 3 s plus tard (pas de temps mort).
/// Partie de 5 min minimum (demande de Jules), visée autour de 10 min : 5 manches de 2 min toutes jouées ; une manche s'arrête dès qu'une équipe atteint 3 KO (manches nerveuses, parfois plus courtes).
/// Une manche revient à l'équipe avec le plus de KO. Égalité à la fin : prolongation de 30 s à mort subite
/// (le premier KO gagne), sinon manche nulle. Si les manches sont à égalité après la 5e, une manche
/// décisive est jouée jusqu'à départage. KoTarget > 0 réactive la fin anticipée.
/// </summary>
public class MatchRules
{
    public int LivesPerPlayer { get; init; } = 2;
    public float RespawnSeconds { get; set; } = 3f;
    public float RoundSeconds { get; set; } = 120f;
    public float OvertimeSeconds { get; init; } = 30f;
    public int MaxRounds { get; set; } = 5;
    public int KoTarget { get; set; } = 3;
    /// <summary>Manches gagnantes qui finissent la partie (0 = les <see cref="MaxRounds"/> manches sont toutes jouées).</summary>
    public int RoundsToWin { get; set; }
    /// <summary>Pause entre deux manches.</summary>
    public float BreakSeconds { get; set; } = 3f;

    /// <summary>KO qui gagnent une manche au format en ligne, selon la taille des équipes (valeurs de Knockout City : 7, 10, 15).</summary>
    public static int OnlineKoTarget(int teamSize) => teamSize <= 2 ? 7 : teamSize == 3 ? 10 : 15;
    public const int OnlineMaxRounds = 3;
    public const int OnlineRoundsToWin = 2;
    public const float OnlineRespawnSeconds = 5f;

    /// <summary>
    /// Format PC en ligne (décision n° 7, façon Knockout City) : premier à 7 / 10 / 15 KO, chrono de 5:00 en filet de sécurité,
    /// 2 manches gagnantes, réapparition 5 s, pause de 12 s entre manches. La VR d'EVA garde 5 manches de 2 min.
    /// </summary>
    public void UseOnlineFormat(int teamSize)
    {
        KoTarget = OnlineKoTarget(teamSize);
        RoundSeconds = 300f;
        MaxRounds = OnlineMaxRounds;
        RoundsToWin = OnlineRoundsToWin;
        RespawnSeconds = OnlineRespawnSeconds;
        BreakSeconds = 12f;
    }

    readonly Dictionary<int, int> lives = new();
    readonly Dictionary<int, int> teamOf = new();
    public int[] RoundsWon { get; } = new int[2];
    public int[] Kos { get; } = new int[2];

    /// <summary>Prolongation à mort subite : le premier KO met fin à la manche.</summary>
    public bool InOvertime
    {
        get => inOvertime;
        set { inOvertime = value; if (value) { overtimeStartKos[0] = Kos[0]; overtimeStartKos[1] = Kos[1]; } }
    }
    bool inOvertime;
    readonly int[] overtimeStartKos = new int[2];

    public event Action<int>? PlayerEliminated;
    public event Action<int>? RoundWon;

    public void AddPlayer(int playerId, int team)
    {
        teamOf[playerId] = team;
        // Un joueur qui change de main (bot remplacé par un humain, reconnexion) garde ses vies.
        if (!lives.ContainsKey(playerId)) lives[playerId] = LivesPerPlayer;
    }

    /// <summary>Vrai si le joueur est inscrit à la manche (un pair parti ou un identifiant inconnu ne l'est pas).</summary>
    public bool HasPlayer(int playerId) => teamOf.ContainsKey(playerId) && lives.ContainsKey(playerId);

    /// <summary>Vies restantes ; 0 pour un joueur inconnu (départ réseau pendant une élimination) au lieu d'une exception chez l'hôte.</summary>
    public int LivesOf(int playerId) => lives.TryGetValue(playerId, out int n) ? n : 0;

    /// <summary>Appelée par le serveur quand une balle touche une cible adverse.</summary>
    public void RegisterHit(int shooterId, int targetId)
    {
        if (!teamOf.TryGetValue(shooterId, out int shooterTeam) || !HasPlayer(targetId)) return;   // joueur parti ou inconnu
        if (shooterTeam == teamOf[targetId]) return; // pas de tir allié
        if (lives[targetId] <= 0) return;
        lives[targetId]--;
        if (lives[targetId] == 0)
        {
            Kos[shooterTeam]++;
            PlayerEliminated?.Invoke(targetId);
        }
    }

    /// <summary>
    /// Ring-out : le joueur poussé dans la fosse perd toutes ses vies d'un coup, KO crédité à l'équipe du pousseur.
    /// Rien si les deux sont alliés, inconnus ou si la cible est déjà éliminée. Vrai si le KO est compté.
    /// </summary>
    public bool Eliminate(int byId, int targetId)
    {
        if (!teamOf.TryGetValue(byId, out int byTeam) || !HasPlayer(targetId)) return false;
        if (byTeam == teamOf[targetId] || lives[targetId] <= 0) return false;
        lives[targetId] = 0;
        Kos[byTeam]++;
        PlayerEliminated?.Invoke(targetId);
        return true;
    }

    /// <summary>Chute dans la fosse sans pousseur : un cœur en moins ; à 0 c'est un KO crédité à l'équipe adverse.</summary>
    public void RegisterFall(int playerId)
    {
        if (!HasPlayer(playerId) || lives[playerId] <= 0) return;
        lives[playerId]--;
        if (lives[playerId] == 0)
        {
            Kos[1 - teamOf[playerId]]++;
            PlayerEliminated?.Invoke(playerId);
        }
    }

    /// <summary>Attrapé parfait : le receveur gagne une vie (plafonnée).</summary>
    public void RegisterPerfectCatch(int catcherId)
    {
        if (lives.TryGetValue(catcherId, out int n) && n > 0 && n < LivesPerPlayer) lives[catcherId] = n + 1;
    }

    public int TeamLives(int team)
    {
        int total = 0;
        foreach (var (id, t) in teamOf)
            if (t == team) total += LivesOf(id);
        return total;
    }

    /// <summary>Équipe qui a atteint le nombre de KO cible, ou -1.</summary>
    public int KoTargetReached()
    {
        if (InOvertime)
        {
            // Mort subite : le premier KO depuis le début de la prolongation (les KO de la manche restent comptés).
            if (Kos[0] > overtimeStartKos[0]) return 0;
            if (Kos[1] > overtimeStartKos[1]) return 1;
            return -1;
        }
        if (KoTarget <= 0) return -1;
        if (Kos[0] >= KoTarget) return 0;
        if (Kos[1] >= KoTarget) return 1;
        return -1;
    }

    /// <summary>Fin de manche au temps : l'équipe avec le plus de KO gagne, -1 en cas d'égalité.</summary>
    public int ResolveTimeout()
    {
        return Kos[0] == Kos[1] ? -1 : (Kos[0] > Kos[1] ? 0 : 1);
    }

    /// <summary>Un KO revient en jeu avec toutes ses vies.</summary>
    public void Respawn(int playerId) => lives[playerId] = LivesPerPlayer;

    /// <summary>Nouvelle manche : vies et KO remis à zéro.</summary>
    public void StartRound()
    {
        foreach (var id in new List<int>(lives.Keys)) lives[id] = LivesPerPlayer;
        Kos[0] = 0;
        Kos[1] = 0;
        InOvertime = false;
    }

    /// <summary>La partie est finie quand les 5 manches sont jouées et que les équipes sont départagées (format en ligne : 2 manches gagnées).</summary>
    public bool IsMatchOver(int roundsPlayed) => RoundsToWin > 0
        ? Math.Max(RoundsWon[0], RoundsWon[1]) >= RoundsToWin
        : roundsPlayed >= MaxRounds && RoundsWon[0] != RoundsWon[1];

    public void ResetMatch()
    {
        RoundsWon[0] = 0;
        RoundsWon[1] = 0;
        StartRound();
    }

    /// <summary>Fin de manche. winningTeam = -1 : manche nulle, aucun point.</summary>
    public void EndRound(int winningTeam)
    {
        if (winningTeam < 0) return;
        RoundsWon[winningTeam]++;
        RoundWon?.Invoke(winningTeam);
    }
}
