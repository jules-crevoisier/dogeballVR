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

    /// <summary>
    /// Photographie de la partie. Winner : équipe gagnante de la manche (RoundOver) ou de la partie (MatchOver), -1 sinon.
    /// NextRoundIn : pendant la pause entre deux manches, secondes avant la suivante (0 sinon ou inconnu).
    /// </summary>
    public readonly record struct Snapshot(
        Phase Phase, int Countdown, int Seconds, int Kos0, int Kos1, int Rounds0, int Rounds1,
        int Round, int MaxRounds, bool Overtime, string Notice, int Winner, int KoTarget, int NextRoundIn = 0)
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

    /// <summary>Statistiques par joueur : seulement chez l'hôte (null côté client).</summary>
    public IReadOnlyDictionary<int, MatchController.PlayerStats>? Stats => match?.Stats;
    public bool HasStats => match != null;
    /// <summary>Balle spéciale de la partie (intermission : « balle spéciale de la manche suivante ») : hôte seulement, vide sinon.</summary>
    public string SpecialBall => match?.SpecialBallName ?? "";
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
            r.Kos[0], r.Kos[1], r.RoundsWon[0], r.RoundsWon[1], m.Round, r.MaxRounds, r.InOvertime, m.Notice, winner, r.KoTarget,
            phase == Phase.RoundOver ? Mathf.CeilToInt((float)m.BreakLeft) : 0);
    }

    static readonly Regex RoundsRe = new(@"Équipe 1 : (\d+)\s+-\s+(\d+) : Équipe 2");
    static readonly Regex KoRe = new(@"KO (\d+) - (\d+)\s+\|\s+(\d+)s");
    static readonly Regex CountdownRe = new(@"^(.*) - DÉPART DANS (\d+)$");
    static readonly Regex RoundRe = new(@"MANCHE (\d+)/(\d+)");
    static readonly Regex BreakRe = new(@"^(.*) - (?:MANCHE \d+|MANCHE DÉCISIVE) DANS (\d+)$");

    /// <summary>
    /// Relit le texte de <see cref="MatchController"/>.UpdateHud : ligne 1 bandeau (ou annonce de balle spéciale),
    /// ligne 2 facultative (échange de plaques), puis « Équipe 1 : a  -  b : Équipe 2 » et « KO x - y   |   Ns ».
    /// Pendant la pause, le bandeau finit par « - MANCHE n DANS s » (compte à rebours de l'intermission).
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
        int round = prev.Round, max = prev.MaxRounds, countdown = 0, winner = -1, nextIn = 0;
        bool overtime = false;
        Phase phase = Phase.Playing;
        var cd = CountdownRe.Match(banner);
        if (cd.Success) { phase = Phase.Countdown; countdown = int.Parse(cd.Groups[2].Value); banner = cd.Groups[1].Value; }
        var br = BreakRe.Match(banner);
        if (br.Success && (br.Groups[1].Value.StartsWith("MANCHE POUR") || br.Groups[1].Value.StartsWith("MANCHE NULLE")))
        {
            nextIn = int.Parse(br.Groups[2].Value);
            banner = br.Groups[1].Value;
        }
        if (banner.StartsWith("VICTOIRE")) { phase = Phase.MatchOver; winner = banner.Contains("BLEUE") ? 1 : 0; }
        else if (banner.StartsWith("MANCHE POUR")) { phase = Phase.RoundOver; winner = banner.Contains("BLEUE") ? 1 : 0; }
        else if (banner.StartsWith("MANCHE NULLE")) phase = Phase.RoundOver;
        else if (banner.StartsWith("PROLONGATION")) overtime = true;
        else if (banner.StartsWith("MANCHE DÉCISIVE")) round = max + 1;
        else if (RoundRe.Match(banner) is { Success: true } rm) { round = int.Parse(rm.Groups[1].Value); max = int.Parse(rm.Groups[2].Value); }
        else if (phase == Phase.Playing) { notice = banner; overtime = prev.Overtime; }   // annonce à la place du bandeau
        return new Snapshot(phase, countdown, sec, k0, k1, r0, r1, round, max, overtime, notice, winner, prev.KoTarget, phase == Phase.RoundOver ? nextIn : 0);
    }
}
