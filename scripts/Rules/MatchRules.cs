using System;
using System.Collections.Generic;

namespace DogeballVR.Rules;

/// <summary>
/// Règles pures (sans Godot) : vies, manches, score. Exécutées côté serveur, testables à part.
/// 2v2 : 3 vies par joueur, élimination 5 s puis retour, 3 manches de 3 min, 2 manches gagnantes.
/// </summary>
public class MatchRules
{
    public int LivesPerPlayer { get; init; } = 3;
    public float RespawnSeconds { get; init; } = 5f;
    public float RoundSeconds { get; init; } = 180f;
    public int RoundsToWin { get; init; } = 2;

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

    public void EndRound(int winningTeam)
    {
        RoundsWon[winningTeam]++;
        RoundWon?.Invoke(winningTeam);
        if (RoundsWon[winningTeam] >= RoundsToWin) MatchWon?.Invoke(winningTeam);
    }
}
