using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Net;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Succès, stats et XP du joueur local. Les gestes rares (attrape parfaite, KO chargé, sortie de piste, passe décisive, tacle)
/// viennent des événements statiques du jeu (MatchController.HitScored, TackleRequested, Ball.Released, Ball.CaughtThrow),
/// émis là où le match est simulé (hors ligne ou hôte ; les KO sont relayés aux clients). Les totaux (KO, attrapes, arrêts
/// parfaits, passes, victoires) et l'XP sont comptés à la fin du match d'après le tableau de l'hôte, qui arrive aussi chez
/// les clients : la même partie compte pareil pour tout le monde. Câblé par Main.AddAchievementTracker. Ne modifie rien au jeu.
/// </summary>
public partial class AchievementTracker : Node
{
    const double RingOutCredit = 3.0;     // = PushCreditSeconds du MatchController
    const double ChargedKoWindow = 3.0;   // durée de vol max d'un tir qui met KO
    const double AssistPassWindow = 10.0; // passe -> lancer du coéquipier
    const double AssistThrowWindow = 3.0; // lancer du coéquipier -> KO

    public int LocalPlayerId { get; set; }
    public int LocalTeam { get; set; }
    /// <summary>Équipe d'un joueur (passes décisives) ; -1 si inconnue.</summary>
    public Func<int, int> TeamOf { get; set; } = _ => -1;
    /// <summary>Match simulé localement (hôte, hors ligne) : sert à détecter la victoire. Null sur un client.</summary>
    public MatchController? Match { get; set; }
    /// <summary>Vue du match du joueur PC (hôte ou client) : fin de match et tableau des stats, aussi sur un client.</summary>
    public MatchView? View { get; set; }
    /// <summary>Passes faites par le joueur local depuis son arrivée (compteur du joueur PC).</summary>
    public Func<int>? LocalPasses { get; set; }
    public bool Vr { get; set; }
    public bool Online { get; set; }
    /// <summary>Un ami Steam est-il dans la partie ? Par défaut : un ami dans le salon Steam courant.</summary>
    public Func<bool> FriendInGame { get; set; } = SteamLobbies.FriendInCurrent;

    readonly Dictionary<int, double> tackledAt = new();          // victime -> instant du tacle local
    readonly Dictionary<ulong, double> passedBalls = new();      // balle passée par le joueur local -> instant
    double lastChargedThrowAt = -100, assistedThrowAt = -100;
    int assistedShooter = -1;
    bool checkPerfectCatch;
    bool victoryShown;
    int passesAtStart;

    public override void _Ready()
    {
        SteamPlatform.Init();
        MatchController.HitScored += OnHitScored;
        MatchController.TackleRequested += OnTackle;
        Ball.Released += OnReleased;
        Ball.CaughtThrow += OnCaughtThrow;
        if (Vr) Achievements.Unlock(AchievementDefs.VrPlayer);
        passesAtStart = LocalPasses?.Invoke() ?? 0;
    }

    public override void _ExitTree()
    {
        MatchController.HitScored -= OnHitScored;
        MatchController.TackleRequested -= OnTackle;
        Ball.Released -= OnReleased;
        Ball.CaughtThrow -= OnCaughtThrow;
        Achievements.StoreStats();
    }

    static double Now => Time.GetTicksMsec() / 1000.0;

    void OnHitScored(int shooterId, int targetId, bool kill)
    {
        if (!kill) return;
        double now = Now;
        if (shooterId == LocalPlayerId)
        {
            if (tackledAt.TryGetValue(targetId, out double t) && now - t <= RingOutCredit) Achievements.Unlock(AchievementDefs.RingOut);
            else if (now - lastChargedThrowAt <= ChargedKoWindow) Achievements.Unlock(AchievementDefs.ChargedKo);
        }
        else if (shooterId == assistedShooter && now - assistedThrowAt <= AssistThrowWindow)
            Achievements.Unlock(AchievementDefs.Assist);
    }

    void OnTackle(int tacklerId, int victimId, Vector2 dir)
    {
        if (tacklerId != LocalPlayerId) return;
        tackledAt[victimId] = Now;
        Achievements.AddStat(AchievementDefs.StatTackles, 1);
    }

    void OnReleased(Ball ball)
    {
        double now = Now;
        ulong key = ball.GetInstanceId();
        if (ball.OwnerPlayerId == LocalPlayerId)
        {
            if (ball.IsPass) passedBalls[key] = now;
            else if (ball.Charge >= 2 && ball.LinearVelocity.Length() >= Ball.HitMinSpeed) lastChargedThrowAt = now;
            return;
        }
        // Lancer d'un coéquipier avec une balle que le joueur local lui a passée.
        if (passedBalls.Remove(key, out double passedAt) && now - passedAt <= AssistPassWindow
            && TeamOf(ball.OwnerPlayerId) == LocalTeam && !ball.IsPass)
        {
            assistedShooter = ball.OwnerPlayerId;
            assistedThrowAt = now;
        }
    }

    void OnCaughtThrow(int catcherId, bool perfect)
    {
        if (catcherId != LocalPlayerId) return;
        checkPerfectCatch = true;   // la charge de la balle n'est posée qu'après l'événement : vérifié à l'image suivante
    }

    public override void _Process(double delta)
    {
        if (checkPerfectCatch)
        {
            checkPerfectCatch = false;
            // Balle volée : charge remise à 0, sauf attrape parfaite (+1 palier, voir Ball.Hold).
            if (BallActions.FindHeld(GetTree(), LocalPlayerId) is { Charge: >= 1 }) Achievements.Unlock(AchievementDefs.PerfectCatch);
        }
        bool over;
        if (View != null)
        {
            var st = View.State;
            over = st.Phase == MatchView.Phase.MatchOver;
            if (over && !victoryShown) ReportMatchEnd(st.Winner, new[] { st.Rounds(0), st.Rounds(1) });
        }
        else if (Match != null)
        {
            // Sans vue (VR) : l'état du MatchController (le texte du bandeau, « VICTOIRE ÉQUIPE ORANGE ! », ne sert qu'à l'affichage).
            over = Match.IsMatchOver;
            if (over && !victoryShown) ReportMatchEnd(Match.Champion, Match.Rules.RoundsWon);
        }
        else return;
        if (!over && victoryShown) passesAtStart = LocalPasses?.Invoke() ?? 0;   // nouvelle partie
        victoryShown = over;
    }

    /// <summary>Fin de match (appelée seule sur l'hôte et hors ligne ; à appeler à la main sur un client).</summary>
    public void ReportMatchEnd(int winningTeam, int[] roundsWon)
    {
        bool won = winningTeam == LocalTeam;
        Achievements.AddStat(AchievementDefs.StatMatches, 1);
        if (won)
        {
            Achievements.AddStat(AchievementDefs.StatWins, 1);
            if (roundsWon.Length == 2 && roundsWon[1 - LocalTeam] == 0) Achievements.Unlock(AchievementDefs.Flawless);
            if (!Online && Bot.Level >= 2) Achievements.Unlock(AchievementDefs.HardBots);
        }
        if (Online && FriendInGame()) Achievements.Unlock(AchievementDefs.OnlineFriend);

        // Stats du match : tableau tenu par l'hôte (reçu par le réseau sur un client), le même que celui de l'écran de fin.
        var table = View?.Stats ?? Match?.Stats;
        var me = table != null && table.TryGetValue(LocalPlayerId, out var mine) ? mine : new MatchController.PlayerStats();
        int passes = Math.Max(0, (LocalPasses?.Invoke() ?? 0) - passesAtStart);
        Achievements.AddStat(AchievementDefs.StatKos, me.Kos);
        Achievements.AddStat(AchievementDefs.StatCatches, me.Catches);
        Achievements.AddStat(AchievementDefs.StatPerfects, me.PerfectCatches);
        Achievements.AddStat(AchievementDefs.StatPasses, passes);
        if (me.Kos >= 3) Achievements.Unlock(AchievementDefs.HatTrick);
        if (me.Catches >= 5) Achievements.Unlock(AchievementDefs.Wall);
        bool mvp = table != null && MvpOf(table, winningTeam) == LocalPlayerId;
        if (mvp) Achievements.Unlock(AchievementDefs.Mvp);

        GD.Print($"[Succès] Fin de match : {(won ? "victoire" : "défaite")}, manches {string.Join("-", roundsWon)}, {me.Kos} KO, {me.Catches} attrapes ({me.PerfectCatches} parfaites), {passes} passes{(mvp ? ", meilleur joueur" : "")}");
        Progression.Award(new MatchResult(won, me.Kos, me.Catches, me.PerfectCatches, passes, mvp, Online, Bot.Level));
        Achievements.StoreStats();
    }

    /// <summary>Meilleur joueur, choisi comme l'écran de fin : meilleure note de l'équipe gagnante (KO pour départager).</summary>
    int MvpOf(IReadOnlyDictionary<int, MatchController.PlayerStats> table, int winningTeam)
    {
        int best = -1, bestScore = -1, bestKos = -1;
        foreach (var (id, st) in table)
        {
            if (winningTeam >= 0 && TeamOf(id) != winningTeam) continue;
            if (st.Score > bestScore || (st.Score == bestScore && st.Kos > bestKos)) { best = id; bestScore = st.Score; bestKos = st.Kos; }
        }
        return bestScore > 0 ? best : -1;
    }
}
