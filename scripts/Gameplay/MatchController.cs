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
    const int BallsOnField = 3;
    const float BallSpawnInterval = 4f;   // une nouvelle balle toutes les 4 s tant qu'il en manque
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
        banner = $"MANCHE {round}";
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
            if (n is Ball { IsHeld: false } b && b.GlobalPosition.Y < -5f) b.QueueFree();

        ballTimer += delta;
        if (ballTimer >= BallSpawnInterval && GetTree().GetNodesInGroup("balls").Count < BallsOnField)
        {
            ballTimer = 0;
            SpawnBall();
        }

        roundTimeLeft -= delta;
        int winner = Rules.KoTargetReached();
        if (winner >= 0) FinishRound(winner);
        else if (roundTimeLeft <= 0) FinishRound(Rules.ResolveTimeout());
        UpdateHud();
    }

    bool MatchOver() => Rules.RoundsWon[0] >= Rules.RoundsToWin || Rules.RoundsWon[1] >= Rules.RoundsToWin
                        || round > Rules.MaxRounds;

    void FinishRound(int winnerTeam)
    {
        inRound = false;
        Rules.EndRound(winnerTeam);
        round++;
        bool over = MatchOver();
        banner = winnerTeam < 0 ? "MANCHE NULLE" : $"MANCHE POUR L'ÉQUIPE {winnerTeam + 1}";
        if (over)
        {
            int champion = Rules.RoundsWon[0] == Rules.RoundsWon[1] ? -1 : (Rules.RoundsWon[0] > Rules.RoundsWon[1] ? 0 : 1);
            banner = champion < 0 ? "ÉGALITÉ" : $"VICTOIRE ÉQUIPE {champion + 1} !";
        }
        breakLeft = over ? MatchEndSeconds : RoundBreakSeconds;
    }

    void SpawnBall()
    {
        var ball = Ball.Create();
        AddChild(ball);
        // Lob depuis le milieu de l'arène, vers un côté au hasard : une balle à saisir toutes les quelques secondes.
        ball.GlobalPosition = new Vector3(rng.RandfRange(-1f, 1f), 3.2f, rng.RandfRange(-4f, 4f));
        ball.LinearVelocity = new Vector3(rng.RandfRange(-5f, 5f), 3f, rng.RandfRange(-3f, 3f));
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
