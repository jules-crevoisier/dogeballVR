using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Net;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Écoute les événements statiques du jeu (MatchController.HitScored, MatchController.TackleRequested, Ball.Released,
/// Ball.CaughtThrow) et la fin de match, et fait avancer succès et stats du joueur local. Ne modifie rien au jeu.
/// Les événements ne sont émis que là où le match est simulé (hors ligne ou hôte) : sur un client, seuls VR,
/// « en ligne avec un ami » et <see cref="ReportMatchEnd"/> s'appliquent (TODO : relayer les touches de l'hôte).
///
/// Câblage dans Main (après la création du MatchController, hôte ou hors ligne) :
/// <code>
/// AddChild(new AchievementTracker
/// {
///     Name = "AchievementTracker", LocalPlayerId = localZone.Id, LocalTeam = localZone.Team,
///     Match = match, TeamOf = id => map.Zones.Find(z => z.Id == id)?.Team ?? -1,
///     Vr = xrOrigin != null, Online = net != null,
/// });
/// </code>
/// Sur un client (BuildClient) : même chose avec Match = null.
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

    public override void _Ready()
    {
        SteamPlatform.Init();
        MatchController.HitScored += OnHitScored;
        MatchController.TackleRequested += OnTackle;
        Ball.Released += OnReleased;
        Ball.CaughtThrow += OnCaughtThrow;
        if (Vr) Achievements.Unlock(AchievementDefs.VrPlayer);
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
            Achievements.AddStat(AchievementDefs.StatKos, 1);
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
        Achievements.AddStat(AchievementDefs.StatCatches, 1);
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
        if (Match == null) return;
        // Fin de match : le bandeau « VICTOIRE ÉQUIPE n » du MatchController reste affiché pendant la pause de fin.
        string hud = Match.HudText;
        bool victory = hud.StartsWith("VICTOIRE ÉQUIPE ");
        if (victory && !victoryShown)
        {
            int champion = hud.Length > 16 && char.IsDigit(hud[16]) ? hud[16] - '1' : -1;
            ReportMatchEnd(champion, Match.Rules.RoundsWon);
        }
        victoryShown = victory;
    }

    /// <summary>Fin de match (appelée seule sur l'hôte et hors ligne ; à appeler à la main sur un client).</summary>
    public void ReportMatchEnd(int winningTeam, int[] roundsWon)
    {
        Achievements.AddStat(AchievementDefs.StatMatches, 1);
        if (winningTeam == LocalTeam)
        {
            Achievements.AddStat(AchievementDefs.StatWins, 1);
            if (roundsWon.Length == 2 && roundsWon[1 - LocalTeam] == 0) Achievements.Unlock(AchievementDefs.Flawless);
        }
        if (Online && FriendInGame()) Achievements.Unlock(AchievementDefs.OnlineFriend);
        GD.Print($"[Succès] Fin de match : {(winningTeam == LocalTeam ? "victoire" : "défaite")}, manches {string.Join("-", roundsWon)}");
        Achievements.StoreStats();
    }
}
