using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;
using DogeballVR.Rules;

namespace DogeballVR.Gameplay;

/// <summary>
/// Chef d'orchestre de la partie (côté hôte) : manches de 2 min, apparition continue des balles, KO et retours,
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

    /// <summary>Texte du tableau de score (envoyé aux clients).</summary>
    public string HudText => hud.Text;
    int nextBallId;

    sealed class Dispenser
    {
        public BallSpawnDef Def = null!;
        public int Team;
        public int Stored;
        public double Timer;
        public Ball? OnPedestal;
    }

    readonly List<Dispenser> dispensers = new();
    readonly List<(double due, int team)> pendingReturns = new();
    BallSpawnDef? neutralDrop;
    double neutralTimer;
    double returnSeconds = 3;
    int maxBallsInPlay;
    double invulnerableSeconds;
    readonly Dictionary<int, double> lastHitAt = new();

    /// <summary>Attraper une balle lancée vite par l'adversaire rend une vie (jusqu'au maximum).</summary>
    void OnCaughtThrow(int catcherId)
    {
        if (inRound && hurtboxes.ContainsKey(catcherId)) Rules.RegisterPerfectCatch(catcherId);
    }
    /// <summary>Altitude actuelle du plateau d'une zone (lift) : le distributeur monte avec elle.</summary>
    public System.Func<int, float> ZoneElevation { get; set; } = _ => 0f;

    int BallsInPlay()
    {
        int n = 0;
        foreach (var b in GetTree().GetNodesInGroup("balls")) if (b is Ball && !b.IsQueuedForDeletion()) n++;
        return n;
    }

    int ballsCached = -1;   // recalculé une fois par image (évite un parcours du groupe par distributeur)
    bool RoomForBall() => maxBallsInPlay <= 0 || (ballsCached < 0 ? ballsCached = BallsInPlay() : ballsCached) < maxBallsInPlay;
    int neutralFlip;

    /// <summary>Lit les zones et les emplacements de balles de la map (distributeurs, lâcher neutre, retour des balles perdues).</summary>
    public void ConfigureMap(MapData map)
    {
        foreach (var zone in map.Zones) SpawnZones.Add(zone.Center);
        returnSeconds = map.BallReturn.Seconds;
        maxBallsInPlay = map.BallRules.MaxBallsInPlay;
        Ball.GravityFactor = map.BallRules.BallGravityFactor;
        Ball.MaxFlightSeconds = map.BallRules.MaxFlightSeconds;
        invulnerableSeconds = map.Rules.InvulnerableAfterHitSeconds;
        HandThrower.MinLaunchSpeed = map.BallRules.ThrowAssist.MinLaunchSpeed;
        foreach (var def in map.BallSpawns)
        {
            if (def.Kind == "neutral_drop") neutralDrop = def;
            else if (def.Kind == "dispenser")
            {
                var zone = map.Zones.Find(z => z.Id == def.Zone);
                if (zone != null) dispensers.Add(new Dispenser { Def = def, Team = zone.Team });
            }
        }
    }

    bool UsesDispensers => dispensers.Count > 0;

    public bool IsEliminated(int playerId) => hurtboxes.TryGetValue(playerId, out var h) && h.Eliminated;

    /// <summary>Retire une hurtbox (joueur remplacé par un bot ou l'inverse). Ses vies restent à jour au prochain Register.</summary>
    public void Unregister(int playerId)
    {
        hurtboxes.Remove(playerId);
    }

    public void Register(Hurtbox hurtbox)
    {
        hurtboxes[hurtbox.PlayerId] = hurtbox;
        Rules.AddPlayer(hurtbox.PlayerId, hurtbox.Team);
        hurtbox.Hit += OnHit;
        hurtbox.Vulnerable = () => !(invulnerableSeconds > 0 && lastHitAt.TryGetValue(hurtbox.PlayerId, out double t) && Time.GetTicksMsec() / 1000.0 - t < invulnerableSeconds);
        // Un joueur KO remplacé (départ, arrivée) reste KO jusqu'à son retour : pas d'invulnérabilité à 0 vie.
        if (Rules.LivesOf(hurtbox.PlayerId) <= 0)
        {
            hurtbox.Eliminated = true;
            if (!respawnAt.ContainsKey(hurtbox.PlayerId)) respawnAt[hurtbox.PlayerId] = Time.GetTicksMsec() / 1000.0 + Rules.RespawnSeconds;
        }
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
        Ball.Released += OnBallReleased;
        Ball.CaughtThrow += OnCaughtThrow;
        TreeExiting += () => { Ball.Released -= OnBallReleased; Ball.CaughtThrow -= OnCaughtThrow; };
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
        pendingReturns.Clear();
        neutralTimer = 0;
        foreach (var d in dispensers) { d.Stored = d.Def.StartBalls; d.Timer = 0; d.OnPedestal = null; }
        if (!UsesDispensers) for (int i = 0; i < BallsOnField; i++) SpawnBall();
        roundTimeLeft = Rules.RoundSeconds;
        ballTimer = 0;
        banner = round > Rules.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {round}/{Rules.MaxRounds}";
        inRound = true;
    }

    // Mesure de portée : lancers et touches par tranche de distance au plus proche adversaire.
    static readonly float[] RangeEdges = { 6f, 12f, 18f };
    readonly int[] throwsByRange = new int[4];
    readonly int[] hitsByRange = new int[4];
    static int RangeBucket(float d) { int i = 0; while (i < RangeEdges.Length && d >= RangeEdges[i]) i++; return i; }

    void OnBallReleased(Ball ball)
    {
        float best = float.MaxValue;
        foreach (var h in hurtboxes.Values)
            if (h.Team != ball.OwnerTeam && !h.Eliminated && IsInstanceValid(h)) best = Mathf.Min(best, ball.GlobalPosition.DistanceTo(h.GlobalPosition));
        if (best == float.MaxValue || ball.OwnerTeam < 0 || ball.LinearVelocity.Length() < Ball.HitMinSpeed) return;   // lâcher mou (KO, départ) : pas un tir
        ball.ThrowDistance = best;
        throwsByRange[RangeBucket(best)]++;
    }

    string RangeReport()
    {
        string[] names = { "0-6 m", "6-12 m", "12-18 m", "18 m et +" };
        var parts = new List<string>();
        for (int i = 0; i < names.Length; i++)
            parts.Add($"{names[i]} : {hitsByRange[i]}/{throwsByRange[i]}");
        return string.Join(" | ", parts);
    }

    void OnHit(int targetId, int shooterId, int charge, float throwDistance)
    {
        if (!inRound) return;
        double clock = Time.GetTicksMsec() / 1000.0;
        // Invulnérabilité après une touche : pas d'enchaînement de KO sur une même rafale.
        if (invulnerableSeconds > 0 && lastHitAt.TryGetValue(targetId, out double prev) && clock - prev < invulnerableSeconds) return;
        lastHitAt[targetId] = clock;
        if (throwDistance >= 0f) hitsByRange[RangeBucket(throwDistance)]++;
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

        if (UsesDispensers) UpdateDispensers(delta, now);
        else
        {
            foreach (var n in GetTree().GetNodesInGroup("balls"))
                if (n is Ball { IsHeld: false } b && (b.GlobalPosition.Y < -5f || b.RestTime > BallRecycleSeconds)) b.QueueFree();

            ballTimer += delta;
            if (ballTimer >= BallSpawnInterval && GetTree().GetNodesInGroup("balls").Count < BallsOnField)
            {
                ballTimer = 0;
                SpawnBall();
            }
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
        if (!UsesDispensers)
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
        GD.Print($"[Portée] touches/lancers par distance : {RangeReport()}");
        GD.Print($"[Match] {banner} | manches {Rules.RoundsWon[0]}-{Rules.RoundsWon[1]} | KO {Rules.Kos[0]}-{Rules.Kos[1]}");
        breakLeft = over ? MatchEndSeconds : RoundBreakSeconds;
    }

    /// <summary>Centres des zones : les balles tombent à tour de rôle dans chaque zone, chacun a toujours de quoi jouer.</summary>
    public List<Vector3> SpawnZones { get; } = new();
    int nextZone;

    /// <summary>
    /// Distributeurs : chacun garde une balle sur son piédestal tant qu'il en a en stock et en regagne une à intervalle fixe.
    /// Une balle perdue (immobile trop longtemps ou hors de l'arène) revient, après quelques secondes, au distributeur de
    /// l'équipe adverse de celle qui l'a lancée en dernier. Lâcher neutre au centre : lob vers l'équipe menée.
    /// </summary>
    void UpdateDispensers(double delta, double now)
    {
        ballsCached = -1;
        foreach (var d in dispensers)
        {
            var pedestal = new Vector3(d.Def.X, d.Def.Y + ZoneElevation(d.Def.Zone), d.Def.Z);
            if (d.OnPedestal != null && (!IsInstanceValid(d.OnPedestal) || d.OnPedestal.IsHeld || d.OnPedestal.GlobalPosition.DistanceTo(pedestal) > 0.6f))
                d.OnPedestal = null;
            if (d.OnPedestal == null && d.Stored > 0 && RoomForBall())
            {
                d.OnPedestal = SpawnBallAt(pedestal + new Vector3(0, 0.2f, 0));
                d.Stored--;
                ballsCached++;
            }
            if (d.Stored < d.Def.MaxStored)
            {
                d.Timer += delta;
                if (d.Timer >= d.Def.IntervalSeconds) { d.Timer = 0; d.Stored++; }
            }
        }

        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball { IsHeld: false } b || b.IsQueuedForDeletion()) continue;
            if (dispensers.Exists(d => d.OnPedestal == b)) continue;
            if (b.GlobalPosition.Y > -2f && b.RestTime <= BallRecycleSeconds) continue;   // sous -2 m : tombée dans la fosse
            // Balle perdue : elle revient à l'autre camp que celui qui l'a lancée en dernier.
            int team = b.LastTeam >= 0 ? 1 - b.LastTeam : (neutralFlip++ % 2);
            pendingReturns.Add((now + returnSeconds, team));
            b.QueueFree();
        }
        for (int i = pendingReturns.Count - 1; i >= 0; i--)
        {
            if (now < pendingReturns[i].due) continue;
            Dispenser? target = null;
            foreach (var d in dispensers)
                if (d.Team == pendingReturns[i].team && (target == null || d.Stored < target.Stored)) target = d;
            // Distributeur plein : la balle retournée attend en file jusqu'à ce qu'il y ait de la place.
            if (target == null) { pendingReturns.RemoveAt(i); continue; }
            if (target.Stored >= target.Def.MaxStored) continue;
            target.Stored++;
            pendingReturns.RemoveAt(i);
        }

        if (neutralDrop != null)
        {
            neutralTimer += delta;
            if (neutralTimer >= neutralDrop.IntervalSeconds && RoomForBall())
            {
                neutralTimer = 0;
                DropNeutral(neutralDrop);
            }
        }
    }

    /// <summary>Balle neutre au centre : lob vers l'équipe qui perd (alterné à égalité).</summary>
    void DropNeutral(BallSpawnDef def)
    {
        int trailing = Rules.Kos[0] == Rules.Kos[1] ? neutralFlip++ % 2 : (Rules.Kos[0] < Rules.Kos[1] ? 0 : 1);
        // Équipe 0 côté z négatif, équipe 1 côté z positif (repère des maps).
        float dirZ = trailing == 0 ? -1f : 1f;
        var ball = SpawnBallAt(new Vector3(def.X, Mathf.Max(def.Y, 3.5f), def.Z));
        ball.LinearVelocity = new Vector3(0, 2f, dirZ * 3f);
    }

    Ball SpawnBallAt(Vector3 position)
    {
        var ball = Ball.Create();
        ball.NetId = ++nextBallId;
        AddChild(ball);
        ball.GlobalPosition = position;
        return ball;
    }

    void SpawnBall()
    {
        var ball = Ball.Create();
        ball.NetId = ++nextBallId;
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
