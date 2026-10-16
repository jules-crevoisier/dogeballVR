using System;
using System.Collections.Generic;

namespace DogeballVR.Rules;

/// <summary>
/// Règles pures (sans Godot) : vies, manches, score. Exécutées côté serveur, testables à part.
/// 2v2 nerveux : 2 vies par joueur, retour 3 s après élimination (pas de temps mort),
/// partie de 5 min = 5 manches de 1 min, 3 manches gagnantes. Une manche se termine quand une équipe
/// n'a plus personne en jeu ou que la minute est écoulée (victoire à l'équipe avec le plus de vies).
/// </summary>
public class MatchRules
{
    public int LivesPerPlayer { get; init; } = 2;
    public float RespawnSeconds { get; init; } = 3f;
    public float RoundSeconds { get; init; } = 60f;
    public int RoundsToWin { get; init; } = 3;
    public int MaxRounds { get; init; } = 5;

    readonly Dictionary<int, int> lives = new();
    readonly Dictionary<int, int> teamOf = new();
    public int[] RoundsWon { get; } = new int[2];

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
        if (lives[targetId] == 0) PlayerEliminated?.Invoke(targetId);
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

    /// <summary>Fin de manche au temps : l'équipe avec le plus de vies gagne, -1 en cas d'égalité.</summary>
    public int ResolveTimeout()
    {
        int a = TeamLives(0), b = TeamLives(1);
        return a == b ? -1 : (a > b ? 0 : 1);
    }

    public void ResetLives()
    {
        foreach (var id in new List<int>(lives.Keys)) lives[id] = LivesPerPlayer;
    }

    public void EndRound(int winningTeam)
    {
        RoundsWon[winningTeam]++;
        RoundWon?.Invoke(winningTeam);
        if (RoundsWon[winningTeam] >= RoundsToWin) MatchWon?.Invoke(winningTeam);
    }
}
