using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Godot;

namespace DogeballVR.Online;

/// <summary>
/// Journaux de crash. Godot écrit déjà son journal dans <c>user://logs/</c> (5 derniers gardés). Ici : un témoin de session
/// posé au démarrage et retiré à la sortie propre ; s'il est encore là au lancement suivant, le jeu a planté (crash natif
/// compris) et on copie le journal de la session précédente dans <c>user://crash/</c>, avec la version et la machine.
/// Les exceptions C# non rattrapées sont aussi écrites là. Le joueur peut joindre ces fichiers à un rapport de bug.
/// Le rapport commence par un résumé lisible (ce qui s'est passé, machine, dernière activité, erreurs regroupées), puis la fin du journal.
/// Au lancement qui suit un plantage, un bandeau le signale au joueur avec un bouton qui ouvre le dossier.
/// Données personnelles : pseudos, SteamID, adresses IP et codes de salle sont masqués avant l'écriture du rapport,
/// et seule la fin des journaux est lue (un journal gonflé ne ralentit pas le lancement).
/// </summary>
public partial class CrashReporter : Node
{
    const string SessionDir = "user://sessions", CrashDir = "user://crash";
    // Un témoin par processus : deux jeux ouverts sur le même PC (tests à deux) ne se croient pas plantés l'un l'autre.
    static string Marker => $"{SessionDir}/{OS.GetProcessId()}.lock";
    static bool started;
    /// <summary>Rapport écrit au démarrage pour la session précédente (null : elle s'est bien terminée).</summary>
    public static string? LastReport { get; private set; }

    public static void Start(SceneTree tree)
    {
        if (started) return;
        started = true;
        DirAccess.MakeDirRecursiveAbsolute(CrashDir);
        DirAccess.MakeDirRecursiveAbsolute(SessionDir);
        foreach (var lockFile in DirAccess.GetFilesAt(SessionDir))
        {
            if (!int.TryParse(lockFile.Replace(".lock", ""), out int pid) || pid == OS.GetProcessId() || OS.IsProcessRunning(pid)) continue;
            SavePreviousLog(FileAccess.GetFileAsString($"{SessionDir}/{lockFile}"));
            DirAccess.RemoveAbsolute($"{SessionDir}/{lockFile}");
        }
        PruneLogBackups();
        using (var f = FileAccess.Open(Marker, FileAccess.ModeFlags.Write)) f?.StoreString($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {Header()}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("exception", Summary("exception C# non rattrapée, le jeu va se fermer.", CurrentLog())
            + "\n--- exception ---\n" + (e.ExceptionObject?.ToString() ?? "?"));
        tree.Root.CallDeferred(Node.MethodName.AddChild, new CrashReporter { Name = "CrashReporter" });
        GD.Print($"[Crash] Session : {Header()}");
    }

    /// <summary>Sortie propre (fenêtre fermée, Quit) : le témoin disparaît. La ligne écrite sert aussi à la CI (tools/ci/godot-fg.sh).</summary>
    public override void _ExitTree()
    {
        DirAccess.RemoveAbsolute(Marker);
        GD.Print("[Crash] Fermeture propre");
    }

    static string Header() =>
        $"DogeballVR {OnlineLobby.GameVersion} | {OS.GetName()} {OS.GetVersion()} | {OS.GetProcessorName()} | {RenderingServer.GetVideoAdapterName()} | {Engine.GetVersionInfo()["string"]}";

    static void SavePreviousLog(string session)
    {
        // Godot copie le journal de la session précédente en godotDATE.log au démarrage : la copie faite il y a moins de 5 min.
        string log = "";
        var logs = LogBackups();
        if (logs.Count > 0 && Time.GetUnixTimeFromSystem() - FileAccess.GetModifiedTime($"{LogDir}/{logs[^1]}") < 300)
            log = Tail($"{LogDir}/{logs[^1]}");
        else log = "(journal de la session introuvable : sans doute effacé par la rotation des journaux de Godot)";
        var lines = log.Split('\n');
        string openedAt = session.Length >= 19 ? session[..19] : session;
        lines = lines.Select(Scrub).ToArray();
        LastReport = Write("crash", Summary(
            $"le jeu s'est fermé sans passer par « Quitter » (plantage, arrêt forcé ou coupure). Session ouverte le {openedAt}.", lines)
            + "\n--- fin du journal (300 dernières lignes) ---\n" + string.Join('\n', lines.Skip(Math.Max(0, lines.Length - 300))));
        GD.Print("[Crash] La session précédente a planté : rapport écrit dans user://crash/");
    }

    const string LogDir = "user://logs";

    static List<string> LogBackups() =>
        DirAccess.GetFilesAt(LogDir).Where(n => n.StartsWith("godot") && n.EndsWith(".log") && n != "godot.log").OrderBy(n => n).ToList();

    /// <summary>
    /// Godot 4.3 garde 4 copies de journaux, mais au-delà il en efface au hasard (ensemble non trié), parfois la plus récente :
    /// celle de la session qui vient de planter. On n'en laisse que 3, pour que la prochaine copie n'en fasse jamais effacer.
    /// </summary>
    static void PruneLogBackups()
    {
        var logs = LogBackups();
        for (int i = 0; i < logs.Count - 3; i++) DirAccess.RemoveAbsolute($"{LogDir}/{logs[i]}");
    }

    static readonly Regex Activity = new(@"^\[(Lobby|Match|Net|Steam|HUD)\]");
    static readonly Regex Error = new(@"(ERROR|Exception|SCRIPT ERROR|Fatal)", RegexOptions.IgnoreCase);

    static string[] CurrentLog()
    {
        try { return Tail("user://logs/godot.log").Split('\n').Select(Scrub).ToArray(); }
        catch (Exception) { return Array.Empty<string>(); }
    }

    const long TailBytes = 256 * 1024;

    /// <summary>Les 256 derniers Ko d'un journal (la première ligne, coupée, est retirée).</summary>
    static string Tail(string path)
    {
        using var f = FileAccess.Open(path, FileAccess.ModeFlags.Read);
        if (f == null) return "";
        ulong len = f.GetLength();
        if (len <= TailBytes) return f.GetBuffer((long)len).GetStringFromUtf8();
        f.Seek(len - (ulong)TailBytes);
        string text = f.GetBuffer(TailBytes).GetStringFromUtf8();
        int cut = text.IndexOf('\n');
        return cut >= 0 ? text[(cut + 1)..] : text;
    }

    static readonly Regex SteamId = new(@"\b7656119\d{10}\b");
    static readonly Regex Ipv4 = new(@"\b(\d{1,3}\.){3}\d{1,3}\b");
    static readonly Regex RoomCode = new(@"\b[0-9A-Z]{5}(-[0-9A-Z]{1,5}){1,3}\b");   // JoinCode : groupes de 5, IP+port ou salon Steam
    // Lignes qui citent un pseudo (OnlineLobby, SteamPlatform) : « [Lobby] X entre dans la salle », « Partie trouvée : X (… ».
    static readonly Regex NamedBefore = new(@"^(\[Lobby\] ).{1,40}?( entre dans la salle| a quitté la salle)");
    static readonly Regex NamedAfter = new(@"(Partie trouvée : |Connecté : ).{1,40}? \(");

    /// <summary>Masque ce qui identifie quelqu'un : notre pseudo, les SteamID, les adresses IP, les codes de salle et les pseudos annoncés.</summary>
    static string Scrub(string line)
    {
        string me = Identity.Name;
        if (me.Length >= 2) line = line.Replace(me, "<pseudo>");
        line = SteamId.Replace(line, "<steamid>");
        line = Ipv4.Replace(line, m => m.Value is "127.0.0.1" or "0.0.0.0" ? m.Value : "<ip>");
        line = RoomCode.Replace(line, "<code>");
        line = NamedBefore.Replace(line, m => m.Groups[1].Value + "<joueur>" + m.Groups[2].Value);
        return NamedAfter.Replace(line, m => m.Groups[1].Value + "<joueur> (");
    }

    /// <summary>Résumé lisible : ce qui s'est passé, machine, dernières lignes d'activité de la partie, erreurs regroupées par message.</summary>
    static string Summary(string what, string[] log)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("RAPPORT DE PLANTAGE");
        sb.AppendLine($"Date : {DateTime.Now:dd/MM/yyyy HH:mm:ss}");
        sb.AppendLine($"Ce qui s'est passé : {what}");
        sb.AppendLine($"Jeu : DogeballVR {OnlineLobby.GameVersion} ; moteur Godot {Engine.GetVersionInfo()["string"]}");
        sb.AppendLine($"Machine : {OS.GetName()} {OS.GetVersion()} ; {OS.GetProcessorName()} ; carte graphique {RenderingServer.GetVideoAdapterName()} {RenderingServer.GetVideoAdapterApiVersion()}");
        var activity = log.Select(l => l.Trim()).Where(l => Activity.IsMatch(l)).TakeLast(10).ToList();
        sb.AppendLine();
        sb.AppendLine("Dernière activité :");
        if (activity.Count == 0) sb.AppendLine("  (aucune ligne de partie dans le journal)");
        foreach (var l in activity) sb.AppendLine("  " + l);
        var errors = new List<(string msg, int count)>();
        foreach (var l in log.Select(l => l.Trim()).Where(l => Error.IsMatch(l) && !l.Contains("OpenXR") && !l.StartsWith("[Steam] indisponible")))
        {
            int i = errors.FindIndex(e => e.msg == l);
            if (i >= 0) errors[i] = (l, errors[i].count + 1); else errors.Add((l, 1));
        }
        sb.AppendLine();
        sb.AppendLine(errors.Count == 0 ? "Erreurs relevées : aucune" : $"Erreurs relevées ({errors.Count} différentes, les 15 dernières) :");
        foreach (var (msg, count) in errors.TakeLast(15)) sb.AppendLine($"  {(count > 1 ? $"{count}× " : "")}{msg}");
        sb.AppendLine();
        sb.AppendLine("Pour nous aider : joindre ce fichier au rapport de bug. Pseudos, identifiants Steam, adresses IP et codes de salle y sont masqués.");
        return sb.ToString();
    }

    static string? Write(string kind, string body)
    {
        try
        {
            string path = $"{CrashDir}/{kind}-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            f?.StoreString($"{body}\n");
            // On garde les 10 derniers rapports.
            var old = DirAccess.GetFilesAt(CrashDir).OrderBy(n => FileAccess.GetModifiedTime($"{CrashDir}/{n}")).ThenBy(n => n[(n.IndexOf('-') + 1)..]).ToList();
            for (int i = 0; i < old.Count - 10; i++) DirAccess.RemoveAbsolute($"{CrashDir}/{old[i]}");
            return path;
        }
        catch (Exception) { return null; }
    }

    /// <summary>Lancement qui suit un plantage : bandeau discret en bas d'écran, avec un bouton qui ouvre le dossier des rapports.</summary>
    public override void _Ready()
    {
        if (LastReport == null || DisplayServer.GetName() == "headless" || UI.GameSettings.LaunchedFromCommandLine) return;
        var layer = new CanvasLayer { Layer = 90 };
        AddChild(layer);
        var bar = new PanelContainer { Theme = UI.UiKit.Theme };
        bar.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.CenterBottom);
        bar.GrowHorizontal = Control.GrowDirection.Both;
        bar.GrowVertical = Control.GrowDirection.Begin;
        bar.OffsetBottom = -24;
        layer.AddChild(bar);
        var row = new HBoxContainer();
        row.AddThemeConstantOverride("separation", 18);
        bar.AddChild(row);
        row.AddChild(UI.UiKit.Text("La dernière partie s'est mal terminée. Un rapport est prêt à joindre à un signalement.", 22, Colors.White, 6));
        row.AddChild(UI.UiKit.Button("OUVRIR LE DOSSIER", UI.UiKit.Blue, () => OS.ShellOpen(Folder), 20, new Vector2(240, 46)));
        row.AddChild(UI.UiKit.Button("OK", UI.UiKit.Grey, layer.QueueFree, 20, new Vector2(90, 46)));
        GetTree().CreateTimer(20).Timeout += () => { if (IsInstanceValid(layer)) layer.QueueFree(); };
    }

    /// <summary>Dossier des rapports, pour un bouton « ouvrir le dossier des journaux » dans les options.</summary>
    public static string Folder => ProjectSettings.GlobalizePath(CrashDir);
}
