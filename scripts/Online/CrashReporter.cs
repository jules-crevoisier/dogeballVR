using System;
using System.Linq;
using Godot;

namespace DogeballVR.Online;

/// <summary>
/// Journaux de crash. Godot écrit déjà son journal dans <c>user://logs/</c> (5 derniers gardés). Ici : un témoin de session
/// posé au démarrage et retiré à la sortie propre ; s'il est encore là au lancement suivant, le jeu a planté (crash natif
/// compris) et on copie le journal de la session précédente dans <c>user://crash/</c>, avec la version et la machine.
/// Les exceptions C# non rattrapées sont aussi écrites là. Le joueur peut joindre ces fichiers à un rapport de bug.
/// </summary>
public partial class CrashReporter : Node
{
    const string SessionDir = "user://sessions", CrashDir = "user://crash";
    // Un témoin par processus : deux jeux ouverts sur le même PC (tests à deux) ne se croient pas plantés l'un l'autre.
    static string Marker => $"{SessionDir}/{OS.GetProcessId()}.lock";
    static bool started;

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
        using (var f = FileAccess.Open(Marker, FileAccess.ModeFlags.Write)) f?.StoreString($"{DateTime.Now:yyyy-MM-dd HH:mm:ss} {Header()}");
        AppDomain.CurrentDomain.UnhandledException += (_, e) => Write("exception", e.ExceptionObject?.ToString() ?? "?");
        tree.Root.CallDeferred(Node.MethodName.AddChild, new CrashReporter { Name = "CrashReporter" });
        GD.Print($"[Crash] Session : {Header()}");
    }

    /// <summary>Sortie propre (fenêtre fermée, Quit) : le témoin disparaît.</summary>
    public override void _ExitTree() => DirAccess.RemoveAbsolute(Marker);

    static string Header() =>
        $"DogeballVR {OnlineLobby.GameVersion} | {OS.GetName()} {OS.GetVersion()} | {OS.GetProcessorName()} | {RenderingServer.GetVideoAdapterName()} | {Engine.GetVersionInfo()["string"]}";

    static void SavePreviousLog(string session)
    {
        string log = "";
        var logs = DirAccess.GetFilesAt("user://logs").Where(n => n.EndsWith(".log") && n != "godot.log").OrderBy(n => n).ToList();
        if (logs.Count > 0) log = FileAccess.GetFileAsString($"user://logs/{logs[^1]}");
        var lines = log.Split('\n');
        Write("crash", $"Session précédente non terminée : {session}\n\n--- fin du journal ---\n" + string.Join('\n', lines.Skip(Math.Max(0, lines.Length - 300))));
        GD.Print("[Crash] La session précédente a planté : rapport écrit dans user://crash/");
    }

    static void Write(string kind, string body)
    {
        try
        {
            string path = $"{CrashDir}/{kind}-{DateTime.Now:yyyyMMdd-HHmmss}.txt";
            using var f = FileAccess.Open(path, FileAccess.ModeFlags.Write);
            f?.StoreString($"{Header()}\n{body}\n");
            // On garde les 10 derniers rapports.
            var old = DirAccess.GetFilesAt(CrashDir).OrderBy(n => n).ToList();
            for (int i = 0; i < old.Count - 10; i++) DirAccess.RemoveAbsolute($"{CrashDir}/{old[i]}");
        }
        catch (Exception) { }
    }

    /// <summary>Dossier des rapports, pour un bouton « ouvrir le dossier des journaux » dans les options.</summary>
    public static string Folder => ProjectSettings.GlobalizePath(CrashDir);
}
