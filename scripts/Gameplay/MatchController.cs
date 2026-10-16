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
    /// <summary>Le tableau flottant est caché pour le joueur PC, qui a son propre score à l'écran.</summary>
    public bool FloatingHudVisible { set => hud.Visible = value; }
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
    readonly Dictionary<int, double> lastDamageAt = new();   // dernier cœur perdu ou regagné : repart le délai de régénération
    float heartRegenSeconds;

    // Fosse (ring-out), tacle et terrasses : cartes en moulinet du profil PC.
    MapData? ringMap;
    bool terraceBonus;
    readonly Dictionary<int, (int by, double at)> lastPush = new();
    readonly Dictionary<int, double> tackleReadyAt = new();
    readonly Dictionary<int, double> highSince = new();
    readonly HashSet<int> fallReturn = new();   // joueurs tombés avec des cœurs restants : ils reviennent vite, sans retrouver toutes leurs vies
    readonly Dictionary<int, double> overPitSince = new();
    readonly Dictionary<int, double> ringGraceUntil = new();   // juste après un retour, la copie de l'hôte n'a pas encore rejoint le point d'apparition
    const double PushCreditSeconds = 3.0;       // une chute dans les 3 s après une poussée compte pour le pousseur
    const float TackleReach = 3.2f;             // distance maximale tacleur/victime vue par l'hôte (marge de latence comprise)

    /// <summary>Demande de tacle (dash qui touche un adversaire) : tacleur, victime, direction du dash. Jugée ici, par l'hôte ou hors ligne.</summary>
    public static event System.Action<int, int, Vector2>? TackleRequested;
    public static void RequestTackle(int tacklerId, int victimId, Vector2 dir) => TackleRequested?.Invoke(tacklerId, victimId, dir);
    /// <summary>Tacle validé : la victime est poussée de 2 m dans la direction donnée (le joueur local l'applique, un joueur distant reçoit un message).</summary>
    public System.Action<int, Vector2>? PushVictim { get; set; }
    public System.Func<int, bool>? IsBot { get; set; }

    void OnTackle(int tacklerId, int victimId, Vector2 dir)
    {
        if (!inRound || !hurtboxes.TryGetValue(tacklerId, out var a) || !hurtboxes.TryGetValue(victimId, out var v)) return;
        double now = Time.GetTicksMsec() / 1000.0;
        if (a.Team == v.Team || a.Eliminated || v.Eliminated || v.DashInvulnerable || !dir.IsFinite() || dir.LengthSquared() < 0.01f) return;
        if (v.Vulnerable != null && !v.Vulnerable()) return;   // invulnérable (après une touche, un retour de KO) : le tacle ne fait rien
        if (tackleReadyAt.TryGetValue(tacklerId, out double ready) && now < ready) return;
        var pa = a.GlobalPosition; var pv = v.GlobalPosition;
        var toVictim = new Vector2(pv.X - pa.X, pv.Z - pa.Z);
        if (toVictim.Length() > TackleReach || Mathf.Abs(pv.Y - pa.Y) > 1.5f) return;
        if (toVictim.Length() > 0.4f && Mathf.Abs(Mathf.RadToDeg(toVictim.AngleTo(dir))) > 70f) return;   // on pousse dans le sens du dash, pas où l'on veut
        if (a.Head == null || v.Head == null || IsBot?.Invoke(victimId) == true) return;        // un bot ne se déplace pas : pas de tacle sans poussée
        tackleReadyAt[tacklerId] = now + 0.8;
        lastPush[victimId] = (tacklerId, now);
        foreach (var n in GetTree().GetNodesInGroup("balls"))
            if (n is Ball { IsHeld: true } held && held.OwnerPlayerId == victimId) held.Release(Vector3.Zero, 0f);   // la victime lâche sa balle
        PushVictim?.Invoke(victimId, dir.Normalized());
        GD.Print($"[Match] Tacle du joueur {tacklerId} sur le joueur {victimId}");
    }

    /// <summary>Un joueur dont la tête passe sous le bord de la fosse est tombé : poussé récemment, KO pour le pousseur ; seul, un cœur en moins.</summary>
    void CheckRingOut(double now)
    {
        if (ringMap == null) return;
        foreach (var (id, h) in hurtboxes)
        {
            if (h.Eliminated || h.Head == null || !IsInstanceValid(h)) continue;
            if (ringGraceUntil.TryGetValue(id, out double grace) && now < grace) continue;
            var hp = h.Head.GlobalPosition;
            // Suspendu au-dessus de la fosse sans arc de saut (client qui garde la tête haute) : au-delà de 1,8 s c'est une chute.
            bool above = hp.Y > 0.5f && ringMap.OverPit(hp.X, hp.Z, -0.2f);
            if (!above) overPitSince.Remove(id);
            else if (!overPitSince.TryAdd(id, now) && now - overPitSince[id] > 0.9) { overPitSince.Remove(id); hp.Y = 0f; }
            if (hp.Y > 0.5f || !ringMap.OverPit(hp.X, hp.Z, 0.1f)) continue;
            if (lastPush.TryGetValue(id, out var push) && now - push.at <= PushCreditSeconds && hurtboxes.TryGetValue(push.by, out var pusher) && pusher.Team != h.Team)
            {
                while (Rules.LivesOf(id) > 0) Rules.RegisterHit(push.by, id);
                HitScored?.Invoke(push.by, id, true);
                GD.Print($"[Match] Ring-out : le joueur {id} est poussé dans la fosse par le joueur {push.by}");
            }
            else
            {
                Rules.RegisterFall(id);
                lastDamageAt[id] = now;
                if (Rules.LivesOf(id) > 0)
                {
                    h.Eliminated = true;
                    respawnAt[id] = now + 1.5;
                    fallReturn.Add(id);
                }
                GD.Print($"[Match] Chute dans la fosse : le joueur {id} perd un cœur ({Rules.LivesOf(id)} restant) en {hp.X:0.0} / {hp.Z:0.0} à t={Engine.GetPhysicsFrames() / 60.0:0.0} s");
            }
            lastPush.Remove(id);
        }
    }

    /// <summary>Attraper une balle lancée vite par l'adversaire rend une vie (jusqu'au maximum).</summary>
    void OnCaughtThrow(int catcherId)
    {
        if (!inRound || !hurtboxes.ContainsKey(catcherId)) return;
        Rules.RegisterPerfectCatch(catcherId);
        lastDamageAt[catcherId] = Time.GetTicksMsec() / 1000.0;   // le cœur rendu repousse aussi la régénération
    }
    /// <summary>Altitude actuelle du plateau d'une zone (lift) : le distributeur monte avec elle.</summary>
    public System.Func<int, float> ZoneElevation { get; set; } = _ => 0f;
    /// <summary>Faux au clavier-souris en moulinet : l'altitude (terrasse) n'est pas celle d'un plateau de lift, les distributeurs restent au sol.</summary>
    public bool PlatformsMove { get; set; } = true;

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
    public bool PcProfile { get; set; } = true;

    public void ConfigureMap(MapData map)
    {
        foreach (var zone in map.Zones) SpawnZones.Add(zone.Center);
        returnSeconds = map.BallReturn.Seconds;
        maxBallsInPlay = map.BallRules.MaxBallsInPlay;
        Ball.GravityFactor = map.BallRules.BallGravityFactor;
        Ball.MaxFlightSeconds = map.BallRules.MaxFlightSeconds;
        invulnerableSeconds = map.Rules.InvulnerableAfterHitSeconds;
        heartRegenSeconds = map.Rules.HeartRegenSeconds;
        ringMap = PcProfile && map.PitRect != null ? map : null;
        terraceBonus = PcProfile && map.IsRing && map.Pc?.Terraces.Count > 0;
        if (map.Rules.KoTarget > 0) Rules.KoTarget = map.Rules.KoTarget;
        HandThrower.MinLaunchSpeed = map.BallRules.ThrowAssist.MinLaunchSpeed;
        foreach (var def in map.BallSpawns)
        {
            if (def.Kind == "neutral_drop") neutralDrop = def;
            else if (def.Kind == "neutral_launcher") { hubLauncher = def; hubZones.AddRange(map.Zones); hubObstacles = map.Obstacles; }
            else if (def.Kind == "dispenser")
            {
                var zone = map.Zones.Find(z => z.Id == def.Zone);
                if (zone != null) dispensers.Add(new Dispenser { Def = def, Team = zone.Team });
            }
        }
    }

    BallSpawnDef? hubLauncher;
    readonly List<ZoneDef> hubZones = new();
    IEnumerable<ObstacleDef> hubObstacles = System.Array.Empty<ObstacleDef>();
    double hubTimer;

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
        Ball.Exploded += OnBombExploded;
        TackleRequested += OnTackle;
        TreeExiting += () => { Ball.Released -= OnBallReleased; Ball.CaughtThrow -= OnCaughtThrow; Ball.Exploded -= OnBombExploded; TackleRequested -= OnTackle; };
    }

    /// <summary>À appeler quand tous les joueurs sont enregistrés.</summary>
    public void StartMatch()
    {
        Rules.ResetMatch();
        round = 1;
        specialKind = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--special-test") >= 0 ? Ball.KindBomb : rng.RandiRange(Ball.KindBomb, Ball.KindBounce);   // une famille tirée au sort par match (sonar et cage : non implémentées)
        GD.Print($"[Match] Balle spéciale du match : {SpecialName(specialKind)}");
        StartRound();
    }

    void StartRound()
    {
        Rules.StartRound();
        foreach (var h in hurtboxes.Values) h.Eliminated = false;
        respawnAt.Clear();
        fallReturn.Clear();
        lastPush.Clear();
        overPitSince.Clear();
        tackleReadyAt.Clear();
        highSince.Clear();
        foreach (var rid in hurtboxes.Keys) ringGraceUntil[rid] = Time.GetTicksMsec() / 1000.0 + 1.5;
        ClearBalls();
        pendingReturns.Clear();
        neutralTimer = 0;
        foreach (var d in dispensers) { d.Stored = d.Def.StartBalls; d.Timer = 0; d.OnPedestal = null; }
        if (!UsesDispensers) for (int i = 0; i < BallsOnField; i++) SpawnBall();
        roundTimeLeft = Rules.RoundSeconds;
        specialClock = 0;
        specialAnnounced = false;
        specialLaunched = 0;
        ballTimer = 0;
        banner = round > Rules.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {round}/{Rules.MaxRounds}";
        inRound = true;
    }

    // Mesure de portée : lancers et touches par tranche de distance au plus proche adversaire.
    static readonly float[] RangeEdges = { 6f, 12f, 18f };
    readonly int[] throwsByRange = new int[4];
    readonly int[] hitsByRange = new int[4];
    static int RangeBucket(float d) { int i = 0; while (i < RangeEdges.Length && d >= RangeEdges[i]) i++; return i; }

    readonly HashSet<int> highBonusUsed = new();

    void OnBallReleased(Ball ball)
    {
        // Tir plongeant : la première balle lancée depuis le plateau haut est chargée +1 (une fois par montée).
        int thrower = ball.OwnerPlayerId;
        if (thrower >= 0 && ball.OwnerTeam >= 0 && ball.LinearVelocity.Length() >= Ball.HitMinSpeed && ZoneElevation(thrower) > 0.1f && highBonusUsed.Add(thrower))
        {
            if (!terraceBonus) { ball.AddCharge(); ball.AddCharge(); }   // lift VR : charge 2, dégâts doubles dès le premier tir
            else if (ball.Charge == 0 && highSince.TryGetValue(thrower, out double since) && Time.GetTicksMsec() / 1000.0 - since <= 3.0) ball.AddCharge();   // terrasse PC : +1 palier dans les 3 s après l'arrivée
        }
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

    /// <summary>Touche validée par l'hôte (ou hors ligne) : tireur, cible, vrai si la cible est KO. Pour le marqueur de touche du HUD.</summary>
    public static event System.Action<int, int, bool>? HitScored;

    void OnHit(int targetId, int shooterId, int charge, float throwDistance)
    {
        if (!inRound) return;
        double clock = Time.GetTicksMsec() / 1000.0;
        // Invulnérabilité après une touche : pas d'enchaînement de KO sur une même rafale.
        if (invulnerableSeconds > 0 && lastHitAt.TryGetValue(targetId, out double prev) && clock - prev < invulnerableSeconds) return;
        lastHitAt[targetId] = clock;
        lastDamageAt[targetId] = clock;
        if (throwDistance >= 0f) hitsByRange[RangeBucket(throwDistance)]++;
        // Une balle chargée fait plus mal : charge 2 ou plus = 2 vies, sinon 1.
        Rules.RegisterHit(shooterId, targetId);
        if (charge >= 2 && Rules.LivesOf(targetId) > 0) Rules.RegisterHit(shooterId, targetId);
        HitScored?.Invoke(shooterId, targetId, Rules.LivesOf(targetId) <= 0);
    }

    void OnEliminated(int playerId)
    {
        hurtboxes[playerId].Eliminated = true;
        respawnAt[playerId] = Time.GetTicksMsec() / 1000.0 + Rules.RespawnSeconds;
    }

    public override void _Process(double delta)
    {
        // Le tableau flottant garde une taille apparente raisonnable : énorme quand on passe dessous, lisible de loin.
        var cam = GetViewport().GetCamera3D();
        if (cam != null) hud.PixelSize = 0.004f * Mathf.Clamp(cam.GlobalPosition.DistanceTo(hud.GlobalPosition) / 12f, 0.35f, 1.5f);
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
            if (!fallReturn.Remove(id)) Rules.Respawn(id);   // retour d'une chute : les cœurs restants ne sont pas rendus
            else lastHitAt[id] = now;                          // et 1,5 s d'invulnérabilité au point d'apparition
            hurtboxes[id].Eliminated = false;
            ringGraceUntil[id] = now + 1.6;
            respawnAt.Remove(id);
        }
        CheckRingOut(now);
        if (terraceBonus)
            foreach (var id in hurtboxes.Keys)
            {
                if (ZoneElevation(id) > 0.1f) highSince.TryAdd(id, now); else highSince.Remove(id);
            }
        if (heartRegenSeconds > 0)
            foreach (var id in hurtboxes.Keys)
            {
                if (hurtboxes[id].Eliminated || Rules.LivesOf(id) >= Rules.LivesPerPlayer) continue;
                lastDamageAt.TryGetValue(id, out double since);
                if (now - since < heartRegenSeconds) continue;
                Rules.RegisterPerfectCatch(id);   // +1 cœur (plafonné)
                GD.Print($"[Match] Cœur regagné par le joueur {id} ({heartRegenSeconds:0.#} s sans être touché)");
                lastDamageAt[id] = now;
            }

        if (UsesDispensers) UpdateDispensers(delta, now);
        else
        {
            foreach (var n in GetTree().GetNodesInGroup("balls"))
                if (n is Ball { IsHeld: false } b && (b.GlobalPosition.Y < -3f || b.RestTime > BallRecycleSeconds)) b.QueueFree();

            ballTimer += delta;
            if (ballTimer >= BallSpawnInterval && GetTree().GetNodesInGroup("balls").Count < BallsOnField)
            {
                ballTimer = 0;
                SpawnBall();
            }
        }

        UpdateSpecialBalls(delta);
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

    // Balles spéciales : une famille par match, lancée par le moyeu toutes les 45 s vers l'équipe menée, annoncée 5 s avant.
    const double SpecialIntervalSeconds = 45, SpecialAnnounceSeconds = 5;
    int specialKind;
    double specialClock;
    bool specialAnnounced;
    int specialLaunched;
    string specialBanner = "", bannerBeforeSpecial = "";

    bool bombTestDone;

    /// <summary>Test headless (--special-test) : une bombe de l'équipe 0 tombe à 1,2 m d'un joueur de l'équipe 1 et doit le toucher par le souffle.</summary>
    void BombTest()
    {
        int shooter = -1;
        Hurtbox? victim = null;
        foreach (var (id, h) in hurtboxes)
        {
            if (h.Team == 0 && shooter < 0) shooter = id;
            if (h.Team == 1 && victim == null && !h.Eliminated) victim = h;
        }
        if (victim == null || shooter < 0) { GD.Print("[Match] Test bombe : pas de cible"); return; }
        var ball = SpawnBallAt(new Vector3(victim.GlobalPosition.X + 1.2f, 0.6f, victim.GlobalPosition.Z));
        ball.Kind = Ball.KindBomb;
        ball.MarkThrown(0, shooter);
        ball.LinearVelocity = new Vector3(0, -14f, 0);
        GD.Print("[Match] Test bombe : lancée");
    }

    static string SpecialName(int kind) => kind == Ball.KindBomb ? "bombe" : "rebond";

    void UpdateSpecialBalls(double delta)
    {
        if (hubLauncher == null || specialKind == 0 || Rules.InOvertime) return;   // pas de balle spéciale en mort subite
        specialClock += delta;
        if (!bombTestDone && specialClock >= 8 && System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--special-test") >= 0) { bombTestDone = true; BombTest(); }
        double due = SpecialIntervalSeconds * (specialLaunched + 1);
        if (!specialAnnounced && specialClock >= due - SpecialAnnounceSeconds)
        {
            specialAnnounced = true;
            bannerBeforeSpecial = banner;
            specialBanner = $"BALLE SPÉCIALE : {SpecialName(specialKind).ToUpper()} DANS {SpecialAnnounceSeconds:0} s";
            banner = specialBanner;
            GD.Print($"[Match] Balle spéciale annoncée : {SpecialName(specialKind)} (manche {round}, t={specialClock:0.0} s)");
        }
        if (specialAnnounced && specialClock >= due)
        {
            specialAnnounced = false;
            specialLaunched++;
            if (banner == specialBanner) banner = bannerBeforeSpecial;
            if (!LaunchFromHub(hubLauncher, specialKind)) { GD.Print("[Match] Balle spéciale : aucune plaque de l'équipe menée, lancer annulé"); return; }
            GD.Print($"[Match] Balle spéciale lancée : {SpecialName(specialKind)} (manche {round}, t={specialClock:0.0} s)");
        }
    }

    /// <summary>Souffle de la bombe : 1 cœur à chaque adversaire à moins de BombRadius (pas de tir allié), la cible déjà touchée par la balle est exclue.</summary>
    void OnBombExploded(Vector3 position, int team, int shooterId, int exceptId)
    {
        DogeballVR.Visual.Explosion.Spawn(this, position, Ball.BombRadius);
        if (!inRound) return;
        int touched = 0;
        foreach (var (id, h) in new List<KeyValuePair<int, Hurtbox>>(hurtboxes))
        {
            if (id == exceptId || h.Team == team || h.Eliminated || h.DashInvulnerable) continue;
            if (h.Vulnerable != null && !h.Vulnerable()) continue;
            if (h.GlobalPosition.DistanceTo(position) > Ball.BombRadius) continue;
            OnHit(id, shooterId, 0, -1f);
            touched++;
        }
        GD.Print($"[Match] Bombe : explosion en {position.X:0.0} / {position.Z:0.0}, {touched} adversaire(s) touché(s) par le souffle");
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
        highBonusUsed.RemoveWhere(id => ZoneElevation(id) <= 0.1f);
        foreach (var d in dispensers)
        {
            var pedestal = new Vector3(d.Def.X, d.Def.Y + (PlatformsMove ? ZoneElevation(d.Def.Zone) : 0f), d.Def.Z);
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

        if (hubLauncher != null)
        {
            hubTimer += delta;
            if (hubTimer >= hubLauncher.IntervalSeconds && RoomForBall())
            {
                hubTimer = 0;
                LaunchFromHub(hubLauncher);
            }
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

    /// <summary>
    /// Lanceur du moyeu : lob lent (1,6 s de vol) vers la plaque qui a le moins de balles (à égalité, celle d'une équipe
    /// qui perd) ; on la voit arriver et on l'attrape.
    /// </summary>
    bool LaunchFromHub(BallSpawnDef def, int kind = 0)
    {
        ZoneDef? target = null;
        int fewest = int.MaxValue;
        int trailing = Rules.Kos[0] == Rules.Kos[1] ? neutralFlip++ % 2 : (Rules.Kos[0] < Rules.Kos[1] ? 0 : 1);
        foreach (var z in hubZones)
        {
            if (kind != 0 && z.Team != trailing) continue;   // balle spéciale : toujours chez l'équipe menée
            int count = 0;
            foreach (var n in GetTree().GetNodesInGroup("balls"))
                if (n is Ball { IsHeld: false } b && new Vector2(b.GlobalPosition.X - z.X, b.GlobalPosition.Z - z.Z).Length() < 5.5f) count++;
            // La plaque d'une équipe qui perd passe devant à nombre de balles égal.
            if (count < fewest || (count == fewest && target != null && z.Team == trailing && target.Team != trailing)) { fewest = count; target = z; }
        }
        if (target == null) return false;
        var from = new Vector3(def.X, Mathf.Max(def.Y, 3.5f), def.Z);
        var to = new Vector3(target.X, 0.6f, target.Z);
        for (int attempt = 0; attempt < 8; attempt++)   // pas sur un couvert : la balle roulerait hors de portée
        {
            var c = new Vector3(target.X + rng.RandfRange(-2f, 2f), 0.6f, target.Z + rng.RandfRange(-1.5f, 1.5f));
            bool blocked = false;
            foreach (var o in hubObstacles)
                if (o.Kind != "pit" && o.Y < 1.5f && Mathf.Abs(c.X - o.X) < o.Sx * 0.5f + 0.6f && Mathf.Abs(c.Z - o.Z) < o.Sz * 0.5f + 0.6f) { blocked = true; break; }
            to = c;
            if (!blocked) break;
        }
        const float flight = 1.6f;
        float g = 9.8f * Ball.GravityFactor;
        var d = to - from;
        var ball = SpawnBallAt(from);
        ball.Kind = kind;
        ball.LinearVelocity = new Vector3(d.X / flight * 1.1f, d.Y / flight + 0.5f * g * flight, d.Z / flight * 1.1f);   // +10 % : l'amortissement de la balle la ferait retomber court
        if (ballsCached >= 0) ballsCached++;
        return true;
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
