using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using Godot;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Résolution des bibliothèques natives Steam (absentes du NuGet Steamworks.NET, voir tools/steam/fetch-steam-natives.sh).
/// Cherche steam_api64.dll / libsteam_api.so dans : dossier de l'exécutable, ses dossiers data_*, res://addons/steam_natives
/// (éditeur, lancement depuis le projet), AppContext.BaseDirectory. Introuvable : le chargement par défaut échoue
/// (DllNotFoundException), que <see cref="SteamPlatform"/> traite comme « Steam indisponible ».
/// </summary>
static class SteamNatives
{
    static bool installed;
    static readonly List<string> searchDirs = new();

    /// <summary>Chemin de la bibliothèque chargée (null : pas encore chargée ou introuvable).</summary>
    public static string? LoadedFrom { get; private set; }

    /// <summary>À appeler une fois, avant le premier appel natif Steamworks (les dossiers sont calculés ici, sur le fil principal).</summary>
    public static void Install()
    {
        if (installed) return;
        installed = true;
        searchDirs.Clear();
        try
        {
            string exeDir = Path.GetDirectoryName(OS.GetExecutablePath()) ?? "";
            if (exeDir != "")
            {
                searchDirs.Add(exeDir);
                foreach (var d in Directory.GetDirectories(exeDir, "data_*")) searchDirs.Add(d);
            }
        }
        catch (Exception) { /* dossier illisible : on passe aux suivants */ }
        string res = ProjectSettings.GlobalizePath("res://addons/steam_natives");
        if (res != "" && !res.StartsWith("res://")) searchDirs.Add(res);
        searchDirs.Add(AppContext.BaseDirectory);
        try
        {
            NativeLibrary.SetDllImportResolver(typeof(Steamworks.SteamAPI).Assembly, Resolve);
        }
        catch (InvalidOperationException) { /* déjà posé (rechargement des scripts dans l'éditeur) */ }
    }

    static string[] FileNames(string libraryName)
    {
        if (OperatingSystem.IsWindows()) return new[] { "steam_api64.dll", "steam_api.dll", libraryName + ".dll" };
        if (OperatingSystem.IsMacOS()) return new[] { "libsteam_api.dylib", "lib" + libraryName + ".dylib" };
        return new[] { "libsteam_api.so", "lib" + libraryName + ".so" };
    }

    static IntPtr Resolve(string libraryName, Assembly assembly, DllImportSearchPath? searchPath)
    {
        if (!libraryName.Contains("steam_api", StringComparison.OrdinalIgnoreCase)) return IntPtr.Zero;
        foreach (var dir in searchDirs)
            foreach (var file in FileNames(libraryName))
            {
                string path = Path.Combine(dir, file);
                if (File.Exists(path) && NativeLibrary.TryLoad(path, out IntPtr handle))
                {
                    LoadedFrom = path;
                    return handle;
                }
            }
        return IntPtr.Zero;   // chargement par défaut (PATH, LD_LIBRARY_PATH…) ; échec => DllNotFoundException
    }
}
