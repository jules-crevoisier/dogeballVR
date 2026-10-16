using System;
using System.Collections.Generic;

namespace DogeballVR.Rules;

/// <summary>
/// Règles pures (sans Godot) : vies, manches, score. Exécutées côté serveur, testables à part.
/// 2v2 nerveux : 2 vies par joueur, un joueur à 0 vie est un KO et revient 3 s plus tard (pas de temps mort).
/// Partie de 5 min = 5 manches de 1 min, 3 manches gagnantes. Une manche se termine quand une équipe
/// atteint 5 KO ou à la minute (victoire à l'équipe avec le plus de KO, égalité = manche nulle).
/// </summary>
public class MatchRules
{
    public int LivesPerPlayer { get; init; } = 2;
    public float RespawnSeconds { get; init; } = 3f;
    public float RoundSeconds { get; init; } = 60f;
    public int RoundsToWin { get; init; } = 3;
    public int MaxRounds { get; init; } = 5;
    public int KoTarget { get; init; } = 5;

    readonly Dictionary<int, int> lives = new();
    readonly Dictionary<int, int> teamOf = new();
    public int[] RoundsWon { get; } = new int[2];
    public int[] Kos { get; } = new int[2];

    public event Action<int>? PlayerEliminated;
    public event Action<int>? RoundWon;
    public event Action<int>? MatchWon;

    public void AddPlayer(int playerId, int team)
    {
        teamOf[playerId] = team;
        lives[playerId] = LivesPerPlayer;
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
    }

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
        if (RoundsWon[winningTeam] >= RoundsToWin) MatchWon?.Invoke(winningTeam);
    }
}
