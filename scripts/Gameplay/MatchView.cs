using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>
/// État de la partie tel que l'affichent le HUD du joueur PC et l'écran de fin, quelle que soit la machine :
/// l'hôte le lit directement dans le <see cref="MatchController"/> (statistiques par joueur comprises) ;
/// le client réseau ne reçoit que le texte du tableau de score (<see cref="MatchController.HudText"/>) et le relit.
/// </summary>
public sealed class MatchView
{
    public enum Phase { Countdown, Playing, RoundOver, MatchOver }

    /// <summary>Photographie de la partie. Winner : équipe gagnante de la manche (RoundOver) ou de la partie (MatchOver), -1 sinon.</summary>
    public readonly record struct Snapshot(
        Phase Phase, int Countdown, int Seconds, int Kos0, int Kos1, int Rounds0, int Rounds1,
        int Round, int MaxRounds, bool Overtime, string Notice, int Winner, int KoTarget)
    {
        public int Kos(int team) => team == 0 ? Kos0 : Kos1;
        public int Rounds(int team) => team == 0 ? Rounds0 : Rounds1;
    }

    public sealed record PlayerInfo(int Id, int Team);

    public int LocalId { get; init; }
    public int LocalTeam { get; init; }
    public int MaxLives { get; init; } = 2;
    public Func<int, int> Lives { get; init; } = _ => 0;
    public IReadOnlyList<PlayerInfo> Players { get; init; } = Array.Empty<PlayerInfo>();
    public Snapshot State { get; private set; }
    /// <summary>Manches gagnantes qui finissent la partie (0 = toutes les manches se jouent) : nombre de pastilles par équipe.</summary>
    public int RoundsToWin => match?.Rules.RoundsToWin ?? roundsToWin;
    int roundsToWin;
    /// <summary>Durée d'un KO avant le retour en jeu.</summary>
    public float RespawnSeconds => match?.Rules.RespawnSeconds ?? respawnSeconds;
    float respawnSeconds = 3f;

    /// <summary>Client d'une partie au format en ligne : objectif de KO et manches gagnantes connus d'avance (le texte de l'hôte ne les porte pas).</summary>
    public void UseOnlineFormat(int teamSize)
    {
        roundsToWin = Rules.MatchRules.OnlineRoundsToWin;
        respawnSeconds = Rules.MatchRules.OnlineRespawnSeconds;
        State = State with { KoTarget = Rules.MatchRules.OnlineKoTarget(teamSize), MaxRounds = Rules.MatchRules.OnlineMaxRounds };
    }

    /// <summary>Statistiques par joueur : lues dans le match chez l'hôte, reçues de l'hôte chez le client (null tant que rien n'est arrivé).</summary>
    public IReadOnlyDictionary<int, MatchController.PlayerStats>? Stats => match != null ? match.Stats : remoteStats;
    public bool HasStats => Stats != null;

    // Client : dernières statistiques et liste des bots envoyées par l'hôte (une seule partie à la fois par processus).
    static Dictionary<int, MatchController.PlayerStats>? remoteStats;
    static HashSet<int>? remoteBots;

    /// <summary>Hôte : statistiques et bots à plat pour le réseau (id, bot, KO, touches, attrapes, lancers, attrapes parfaites).</summary>
    public static int[] EncodeStats(IReadOnlyDictionary<int, MatchController.PlayerStats> stats, Func<int, bool> isBot)
    {
        var data = new List<int>();
        foreach (var (id, st) in stats) data.AddRange(new[] { id, isBot(id) ? 1 : 0, st.Kos, st.Hits, st.Catches, st.Throws, st.PerfectCatches });
        return data.ToArray();
    }

    /// <summary>Client : statistiques reçues de l'hôte.</summary>
    public static void ApplyRemoteStats(int[] data)
    {
        var stats = new Dictionary<int, MatchController.PlayerStats>();
        var bots = new HashSet<int>();
        for (int i = 0; i + 6 < data.Length; i += 7)
        {
            stats[data[i]] = new MatchController.PlayerStats { Kos = data[i + 2], Hits = data[i + 3], Catches = data[i + 4], Throws = data[i + 5], PerfectCatches = data[i + 6] };
            if (data[i + 1] != 0) bots.Add(data[i]);
        }
        remoteStats = stats;
        remoteBots = bots;
    }
    /// <summary>Seul l'hôte relance une partie ; le client attend.</summary>
    public bool CanRestart => match != null;
    /// <summary>La partie suivante démarrera seule (pas d'attente du joueur sur l'écran de fin).</summary>
    public bool AutoRestart => match?.AutoRestart ?? false;

    /// <summary>Touche validée par l'hôte : tireur, cible, KO (hôte seulement : marqueur de touche).</summary>
    public event Action<int, int, bool>? Hit;
    /// <summary>Un joueur passe à 0 vie : auteur (-1 s'il est inconnu, chute ou client), victime.</summary>
    public event Action<int, int>? Ko;

    MatchController? match;
    Func<string>? hudText;
    Func<int, bool>? isBot;
    readonly Dictionary<int, int> lastLives = new();
    readonly Dictionary<int, (int shooter, ulong at)> lastHitBy = new();

    /// <summary>Vue de l'hôte (ou hors ligne).</summary>
    public static MatchView ForHost(MatchController match, MapData map, int localId, int localTeam)
    {
        var view = new MatchView
        {
            LocalId = localId, LocalTeam = localTeam, MaxLives = match.Rules.LivesPerPlayer,
            Lives = id => { try { return match.Rules.LivesOf(id); } catch (KeyNotFoundException) { return 0; } },
            Players = PlayersOf(map),
        };
        view.match = match;
        view.isBot = id => match.IsBot?.Invoke(id) ?? false;
        MatchController.HitScored += view.OnHitScored;
        view.State = view.FromHost();
        return view;
    }

    /// <summary>Vue du client : tout vient du texte du tableau de score et des vies du snapshot.</summary>
    public static MatchView ForClient(Func<string> hudText, Func<int, int> lives, MapData map, int localId, int localTeam)
    {
        var view = new MatchView { LocalId = localId, LocalTeam = localTeam, Lives = lives, Players = PlayersOf(map) };
        view.hudText = hudText;
        remoteStats = null;   // nouvelle partie : les chiffres arrivent avec le premier envoi de l'hôte
        remoteBots = null;
        view.isBot = id => remoteBots?.Contains(id) == true;
        MatchController.HitScored += view.OnHitScored;   // touches relayées par l'hôte, quand le réseau les transmet
        view.State = new Snapshot(Phase.Playing, 0, 0, 0, 0, 0, 0, 1, 5, false, "", -1, map.Rules.KoTarget > 0 ? map.Rules.KoTarget : 3);
        return view;
    }

    static List<PlayerInfo> PlayersOf(MapData map)
    {
        var list = new List<PlayerInfo>();
        foreach (var z in map.Zones) list.Add(new PlayerInfo(z.Id, z.Team));
        return list;
    }

    /// <summary>À appeler quand le HUD disparaît (l'événement de l'hôte est statique).</summary>
    public void Detach()
    {
        MatchController.HitScored -= OnHitScored;
    }

    public void Restart() => match?.StartMatch();

    /// <summary>Dernier tireur à avoir touché ce joueur dans la seconde (hôte), -1 sinon : d'où venait la balle.</summary>
    public int RecentShooterOf(int target) => lastHitBy.TryGetValue(target, out var h) && Time.GetTicksMsec() - h.at < 1000 ? h.shooter : -1;

    /// <summary>Nom d'un joueur humain fourni par la salle en ligne (null : bot ou inconnu). Branché par le module en ligne.</summary>
    public static Func<int, string?>? HumanName { get; set; }

    public string NameOf(int id) => id == LocalId ? "Toi" : HumanName?.Invoke(id) is { Length: > 0 } n ? n : isBot?.Invoke(id) == true ? BotName(id) : $"Joueur {id + 1}";

    /// <summary>Surnoms des bots, à la façon des joueurs de rue : un par numéro de zone (toujours le même d'une partie à l'autre).</summary>
    static readonly string[] BotNames = { "Rafale", "Mistral", "Bolt", "Turbo", "Comète", "Ricochet", "Zigzag", "Volt" };
    /// <summary>Surnom du bot qui tient la zone <paramref name="id"/> (étiquettes, fil des KO, tableaux, bandeaux).</summary>
    public static string BotName(int id) => BotNames[((id % BotNames.Length) + BotNames.Length) % BotNames.Length];
    public int TeamOf(int id) { foreach (var p in Players) if (p.Id == id) return p.Team; return -1; }

    void OnHitScored(int shooter, int target, bool kill)
    {
        lastHitBy[target] = (shooter, Time.GetTicksMsec());
        Hit?.Invoke(shooter, target, kill);
    }

    /// <summary>Une fois par image : relit l'état et repère les KO (passage à 0 vie).</summary>
    public void Update()
    {
        State = match != null ? FromHost() : FromText(hudText?.Invoke() ?? "", State);
        foreach (var p in Players)
        {
            int n = Lives(p.Id);
            if (lastLives.TryGetValue(p.Id, out int before) && before > 0 && n <= 0)
            {
                int by = lastHitBy.TryGetValue(p.Id, out var h) && Time.GetTicksMsec() - h.at < 800 ? h.shooter : -1;
                Ko?.Invoke(by, p.Id);
            }
            lastLives[p.Id] = n;
        }
    }

    Snapshot FromHost()
    {
        var m = match!;
        var r = m.Rules;
        Phase phase = m.IsMatchOver ? Phase.MatchOver : !m.InRound ? Phase.RoundOver : m.CountdownLeft > 0 ? Phase.Countdown : Phase.Playing;
        int winner = phase == Phase.MatchOver ? m.Champion : phase == Phase.RoundOver ? m.LastRoundWinner : -1;
        return new Snapshot(phase, Mathf.CeilToInt((float)m.CountdownLeft), Mathf.Max(0, Mathf.CeilToInt((float)m.RoundTimeLeft)),
            r.Kos[0], r.Kos[1], r.RoundsWon[0], r.RoundsWon[1], m.Round, r.MaxRounds, r.InOvertime, m.Notice, winner, r.KoTarget);
    }

    static readonly Regex RoundsRe = new(@"Équipe 1 : (\d+)\s+-\s+(\d+) : Équipe 2");
    static readonly Regex KoRe = new(@"KO (\d+) - (\d+)\s+\|\s+(\d+)s");
    static readonly Regex CountdownRe = new(@"^(.*) - DÉPART DANS (\d+)$");
    static readonly Regex RoundRe = new(@"MANCHE (\d+)/(\d+)");

    /// <summary>
    /// Relit le texte de <see cref="MatchController"/>.UpdateHud : ligne 1 bandeau (ou annonce de balle spéciale),
    /// ligne 2 facultative (échange de plaques), puis « Équipe 1 : a  -  b : Équipe 2 » et « KO x - y   |   Ns ».
    /// Ce qui ne se lit pas garde la valeur précédente.
    /// </summary>
    public static Snapshot FromText(string text, Snapshot prev)
    {
        if (string.IsNullOrEmpty(text)) return prev;
        var lines = text.Split('\n');
        string banner = lines[0].Trim();
        int r0 = prev.Rounds0, r1 = prev.Rounds1, k0 = prev.Kos0, k1 = prev.Kos1, sec = prev.Seconds;
        foreach (var l in lines)
        {
            var m = RoundsRe.Match(l);
            if (m.Success) { r0 = int.Parse(m.Groups[1].Value); r1 = int.Parse(m.Groups[2].Value); }
            m = KoRe.Match(l);
            if (m.Success) { k0 = int.Parse(m.Groups[1].Value); k1 = int.Parse(m.Groups[2].Value); sec = int.Parse(m.Groups[3].Value); }
        }
        string notice = lines.Length >= 4 ? lines[1].Trim() : "";
        int round = prev.Round, max = prev.MaxRounds, countdown = 0, winner = -1;
        bool overtime = false;
        Phase phase = Phase.Playing;
        var cd = CountdownRe.Match(banner);
        if (cd.Success) { phase = Phase.Countdown; countdown = int.Parse(cd.Groups[2].Value); banner = cd.Groups[1].Value; }
        if (banner.StartsWith("VICTOIRE")) { phase = Phase.MatchOver; winner = banner.Contains("BLEUE") ? 1 : 0; }
        else if (banner.StartsWith("MANCHE POUR")) { phase = Phase.RoundOver; winner = banner.Contains("BLEUE") ? 1 : 0; }
        else if (banner.StartsWith("MANCHE NULLE")) phase = Phase.RoundOver;
        else if (banner.StartsWith("PROLONGATION")) overtime = true;
        else if (banner.StartsWith("MANCHE DÉCISIVE")) round = max + 1;
        else if (RoundRe.Match(banner) is { Success: true } rm) { round = int.Parse(rm.Groups[1].Value); max = int.Parse(rm.Groups[2].Value); }
        else if (phase == Phase.Playing) { notice = banner; overtime = prev.Overtime; }   // annonce à la place du bandeau
        return new Snapshot(phase, countdown, sec, k0, k1, r0, r1, round, max, overtime, notice, winner, prev.KoTarget);
    }
}
