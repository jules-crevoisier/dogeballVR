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
    public float RespawnSeconds { get; init; } = 3f;
    public float RoundSeconds { get; init; } = 120f;
    public float OvertimeSeconds { get; init; } = 30f;
    public int MaxRounds { get; init; } = 5;
    public int KoTarget { get; set; } = 3;

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

    public int LivesOf(int playerId) => lives[playerId];

    /// <summary>Appelée par le serveur quand une balle touche une cible adverse.</summary>
    public void RegisterHit(int shooterId, int targetId)
    {
        if (teamOf[shooterId] == teamOf[targetId]) return; // pas de tir allié
        if (lives[targetId] <= 0) return;
        lives[targetId]--;
        if (lives[targetId] == 0)
        {
            Kos[teamOf[shooterId]]++;
            PlayerEliminated?.Invoke(targetId);
        }
    }

    /// <summary>Chute dans la fosse sans pousseur : un cœur en moins ; à 0 c'est un KO crédité à l'équipe adverse.</summary>
    public void RegisterFall(int playerId)
    {
        if (lives[playerId] <= 0) return;
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
        if (lives[catcherId] > 0 && lives[catcherId] < LivesPerPlayer) lives[catcherId]++;
    }

    public int TeamLives(int team)
    {
        int total = 0;
        foreach (var (id, t) in teamOf)
            if (t == team) total += lives[id];
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

    /// <summary>La partie est finie quand les 5 manches sont jouées et que les équipes sont départagées.</summary>
    public bool IsMatchOver(int roundsPlayed) => roundsPlayed >= MaxRounds && RoundsWon[0] != RoundsWon[1];

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
