using System;
using Godot;
using Steamworks;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Succès et stats. Toujours écrits dans user://progress.cfg (hors ligne, sans Steam, build non-Steam) ; avec Steam,
/// recopiés sur Steam dès que les stats de l'utilisateur sont reçues. Les succès débloqués hors ligne sont donc
/// reportés au prochain lancement avec Steam, et une stat garde le maximum des deux côtés.
/// </summary>
public static class Achievements
{
    const string ProgressPath = "user://progress.cfg";
    const string SectionAchievements = "achievements", SectionStats = "stats";
    const double StoreInterval = 2.0;   // StoreStats est limité par Steam : au plus un envoi groupé toutes les 2 s

    static ConfigFile? progress;
    static bool steamStatsReady, steamDirty;
    static double lastStore = -10;
    static Callback<UserStatsReceived_t>? statsReceivedCb;
    static Callback<UserStatsStored_t>? statsStoredCb;

    /// <summary>Succès débloqué (id API), pour une notification en jeu (Steam affiche la sienne de son côté).</summary>
    public static event Action<AchievementDef>? Unlocked;

    static ConfigFile Progress
    {
        get
        {
            if (progress != null) return progress;
            progress = new ConfigFile();
            var err = progress.Load(ProgressPath);
            if (err != Error.Ok && err != Error.FileNotFound) GD.PrintErr($"[Succès] {ProgressPath} illisible ({err}) : progression repartie de zéro");
            return progress;
        }
    }

    public static bool IsUnlocked(string api) => (bool)Progress.GetValue(SectionAchievements, api, false);

    public static int GetStat(string api) => (int)Progress.GetValue(SectionStats, api, 0);

    /// <summary>Débloque un succès (sans effet s'il l'est déjà).</summary>
    public static void Unlock(string api)
    {
        if (IsUnlocked(api)) return;
        Progress.SetValue(SectionAchievements, api, true);
        SaveLocal();
        var def = AchievementDefs.Find(api);
        GD.Print($"[Succès] Débloqué : {def?.NameFr ?? api} ({api})");
        if (SteamPlatform.Available && steamStatsReady)
        {
            SteamUserStats.SetAchievement(api);
            steamDirty = true;
        }
        if (def != null) Unlocked?.Invoke(def);
    }

    /// <summary>Ajoute <paramref name="delta"/> à une stat et débloque les succès dont le seuil est atteint.</summary>
    public static void AddStat(string api, int delta)
    {
        if (delta == 0) return;
        int value = GetStat(api) + delta;
        Progress.SetValue(SectionStats, api, value);
        SaveLocal();
        if (SteamPlatform.Available && steamStatsReady)
        {
            SteamUserStats.SetStat(api, value);
            steamDirty = true;
        }
        foreach (var a in AchievementDefs.All)
            if (a.Stat == api && a.Threshold > 0 && value >= a.Threshold) Unlock(a.Id);
    }

    /// <summary>Envoie tout de suite les changements à Steam (fin de match, fermeture) et sauve le fichier local.</summary>
    public static void StoreStats()
    {
        SaveLocal();
        if (!SteamPlatform.Available || !steamStatsReady || !steamDirty) return;
        steamDirty = !SteamUserStats.StoreStats();
        lastStore = Time.GetTicksMsec() / 1000.0;
    }

    /// <summary>Appelé à chaque image par SteamPlatform.RunCallbacks : envoi groupé et limité des changements.</summary>
    internal static void Flush()
    {
        if (steamDirty && Time.GetTicksMsec() / 1000.0 - lastStore >= StoreInterval) StoreStats();
    }

    internal static void OnSteamReady()
    {
        statsReceivedCb = Callback<UserStatsReceived_t>.Create(OnStatsReceived);
        statsStoredCb = Callback<UserStatsStored_t>.Create(r =>
        {
            if (r.m_eResult != EResult.k_EResultOK) { steamDirty = true; GD.Print($"[Succès] Envoi Steam refusé ({r.m_eResult}), nouvel essai plus tard"); }
        });
        SteamUserStats.RequestCurrentStats();
    }

    internal static void OnSteamUnavailable() => steamStatsReady = false;

    static void OnStatsReceived(UserStatsReceived_t r)
    {
        if (r.m_nGameID != SteamPlatform.AppId || r.m_steamIDUser.m_SteamID != SteamPlatform.LocalSteamId) return;
        if (r.m_eResult != EResult.k_EResultOK) { GD.Print($"[Succès] Stats Steam indisponibles ({r.m_eResult}) : progression locale seulement"); return; }
        steamStatsReady = true;
        int pushed = 0, pulled = 0;
        foreach (var s in AchievementDefs.Stats)
        {
            if (!SteamUserStats.GetStat(s.Id, out int remote)) continue;   // stat non déclarée dans Steamworks
            int local = GetStat(s.Id);
            if (local > remote) { SteamUserStats.SetStat(s.Id, local); pushed++; }
            else if (remote > local) { Progress.SetValue(SectionStats, s.Id, remote); pulled++; }
        }
        foreach (var a in AchievementDefs.All)
        {
            if (!SteamUserStats.GetAchievement(a.Id, out bool remote)) continue;   // succès non déclaré dans Steamworks
            bool local = IsUnlocked(a.Id);
            if (local && !remote) { SteamUserStats.SetAchievement(a.Id); pushed++; }
            else if (remote && !local) { Progress.SetValue(SectionAchievements, a.Id, true); pulled++; }
        }
        SaveLocal();
        if (pushed > 0) { steamDirty = true; StoreStats(); }
        GD.Print($"[Succès] Stats Steam reçues : {pushed} envoyé(s) depuis la progression locale, {pulled} récupéré(s)");
    }

    static void SaveLocal()
    {
        var err = Progress.Save(ProgressPath);
        if (err != Error.Ok) GD.PrintErr($"[Succès] Sauvegarde impossible de {ProgressPath} ({err})");
    }
}
