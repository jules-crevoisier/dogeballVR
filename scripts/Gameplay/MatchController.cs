using System.Collections.Generic;
using Godot;
using DogeballVR.Rules;

namespace DogeballVR.Gameplay;

/// <summary>
/// Chef d'orchestre de la partie (côté hôte) : manches de 1 min, apparition continue des balles, KO et retours,
/// affichage du score. Les règles pures sont dans <see cref="MatchRules"/>.
/// </summary>
public partial class MatchController : Node3D
{
    const int BallsOnField = 4;
    const float BallSpawnInterval = 3f;   // une nouvelle balle toutes les 3 s tant qu'il en manque
    const float BallRecycleSeconds = 6f;  // balle immobile trop longtemps : recyclée
    const float RoundBreakSeconds = 3f;
    const float MatchEndSeconds = 8f;

    public MatchRules Rules { get; } = new();
    readonly Dictionary<int, Hurtbox> hurtboxes = new();
    readonly Dictionary<int, double> respawnAt = new();
    readonly RandomNumberGenerator rng = new();

    Label3D hud = null!;
    double roundTimeLeft;
    double breakLeft;
    double ballTimer;
    int round = 1;
    bool inRound;
    string banner = "";

    public void Register(Hurtbox hurtbox)
    {
        hurtboxes[hurtbox.PlayerId] = hurtbox;
        Rules.AddPlayer(hurtbox.PlayerId, hurtbox.Team);
        hurtbox.Hit += OnHit;
    }

    public override void _Ready()
    {
        rng.Randomize();
        hud = new Label3D
        {
            Name = "Hud",
            Position = new Vector3(0, 4f, 0),
            Billboard = BaseMaterial3D.BillboardModeEnum.Enabled,
            FontSize = 96,
            PixelSize = 0.004f,
            NoDepthTest = true,
        };
        AddChild(hud);
        Rules.PlayerEliminated += OnEliminated;
    }

    /// <summary>À appeler quand tous les joueurs sont enregistrés.</summary>
    public void StartMatch()
    {
        Rules.ResetMatch();
        round = 1;
        StartRound();
    }

    void StartRound()
    {
        Rules.StartRound();
        foreach (var h in hurtboxes.Values) h.Eliminated = false;
        respawnAt.Clear();
        ClearBalls();
        for (int i = 0; i < BallsOnField; i++) SpawnBall();
        roundTimeLeft = Rules.RoundSeconds;
        ballTimer = 0;
        banner = round > Rules.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {round}/{Rules.MaxRounds}";
        inRound = true;
    }

    void OnHit(int targetId, int shooterId, int charge)
    {
        if (!inRound) return;
        // Une balle chargée fait plus mal : charge 2 ou plus = 2 vies, sinon 1.
        Rules.RegisterHit(shooterId, targetId);
        if (charge >= 2 && Rules.LivesOf(targetId) > 0) Rules.RegisterHit(shooterId, targetId);
    }

    void OnEliminated(int playerId)
    {
        hurtboxes[playerId].Eliminated = true;
        respawnAt[playerId] = Time.GetTicksMsec() / 1000.0 + Rules.RespawnSeconds;
    }

    public override void _Process(double delta)
    {
        if (!inRound)
        {
            breakLeft -= delta;
            if (breakLeft <= 0 && hurtboxes.Count > 0) { if (MatchOver()) StartMatch(); else StartRound(); }
            UpdateHud();
            return;
        }

        double now = Time.GetTicksMsec() / 1000.0;
        foreach (var id in new List<int>(respawnAt.Keys))
        {
            if (now < respawnAt[id]) continue;
            Rules.Respawn(id);
            hurtboxes[id].Eliminated = false;
            respawnAt.Remove(id);
        }

        foreach (var n in GetTree().GetNodesInGroup("balls"))
            if (n is Ball { IsHeld: false } b && (b.GlobalPosition.Y < -5f || b.RestTime > BallRecycleSeconds)) b.QueueFree();

        ballTimer += delta;
        if (ballTimer >= BallSpawnInterval && GetTree().GetNodesInGroup("balls").Count < BallsOnField)
        {
            ballTimer = 0;
            SpawnBall();
        }

        roundTimeLeft -= delta;
        int winner = Rules.KoTargetReached();
        if (winner >= 0) FinishRound(winner);
        else if (roundTimeLeft <= 0)
        {
            int result = Rules.ResolveTimeout();
            if (result < 0 && !Rules.InOvertime) StartOvertime();
            else FinishRound(result);
        }
        UpdateHud();
    }

    // `round` est le numéro de la prochaine manche : manches jouées = round - 1.
    void StartOvertime()
    {
        Rules.InOvertime = true;
        roundTimeLeft = Rules.OvertimeSeconds;
        banner = "PROLONGATION : PREMIER KO";
        for (int i = GetTree().GetNodesInGroup("balls").Count; i < BallsOnField; i++) SpawnBall();
    }

    bool MatchOver() => Rules.IsMatchOver(round - 1);

    void FinishRound(int winnerTeam)
    {
        inRound = false;
        Rules.EndRound(winnerTeam);
        round++;
        bool over = MatchOver();
        banner = winnerTeam < 0 ? "MANCHE NULLE" : $"MANCHE POUR L'ÉQUIPE {winnerTeam + 1}";
        if (over)
        {
            int champion = Rules.RoundsWon[0] > Rules.RoundsWon[1] ? 0 : 1;
            banner = $"VICTOIRE ÉQUIPE {champion + 1} !";
        }
        GD.Print($"[Match] {banner} | manches {Rules.RoundsWon[0]}-{Rules.RoundsWon[1]} | KO {Rules.Kos[0]}-{Rules.Kos[1]}");
        breakLeft = over ? MatchEndSeconds : RoundBreakSeconds;
    }

    /// <summary>Centres des zones : les balles tombent à tour de rôle dans chaque zone, chacun a toujours de quoi jouer.</summary>
    public List<Vector3> SpawnZones { get; } = new();
    int nextZone;

    void SpawnBall()
    {
        var ball = Ball.Create();
        AddChild(ball);
        var center = SpawnZones.Count > 0 ? SpawnZones[nextZone++ % SpawnZones.Count] : Vector3.Zero;
        ball.GlobalPosition = center + new Vector3(rng.RandfRange(-3f, 3f), 3f, rng.RandfRange(-2f, 2f));
    }

    void ClearBalls()
    {
        foreach (var n in GetTree().GetNodesInGroup("balls")) n.QueueFree();
    }

    void UpdateHud()
    {
        int seconds = inRound ? Mathf.Max(0, Mathf.CeilToInt((float)roundTimeLeft)) : 0;
        hud.Text = $"{banner}\nÉquipe 1 : {Rules.RoundsWon[0]}  -  {Rules.RoundsWon[1]} : Équipe 2\n" +
                   $"KO {Rules.Kos[0]} - {Rules.Kos[1]}   |   {seconds}s";
    }
}
