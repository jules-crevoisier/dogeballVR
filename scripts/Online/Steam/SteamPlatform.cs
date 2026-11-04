using System;
using System.Globalization;
using System.IO;
using Godot;
using Steamworks;

namespace DogeballVR.Online.Steam;

/// <summary>
/// Point d'entrée Steam. <see cref="Init"/> est sans risque partout : sans client Steam, sans bibliothèque native
/// ou avec <c>--no-steam</c>, <see cref="Available"/> reste faux, un seul message « [Steam] indisponible : … » est écrit
/// et toute la couche (succès, salons, présence) bascule sur ses replis locaux.
/// AppId : steam_appid.txt (dossier courant, de l'exécutable ou du projet), sinon <see cref="ShippedAppId"/>, sinon 480 (Spacewar, tests).
/// </summary>
public static class SteamPlatform
{
    /// <summary>AppId du jeu publié. 0 tant que la page Steam n'existe pas : pas de RestartAppIfNecessary.</summary>
    // TODO jules : mettre l'AppId attribué par Valve (Steamworks > App Admin) avant la sortie.
    public const uint ShippedAppId = 0;
    public const uint SpacewarAppId = 480;

    static bool attempted;
    static Callback<GameLobbyJoinRequested_t>? lobbyJoinCb;
    static Callback<GameRichPresenceJoinRequested_t>? presenceJoinCb;
    static Callback<GameOverlayActivated_t>? overlayCb;

    public static bool Available { get; private set; }
    /// <summary>Raison de l'indisponibilité (vide si Steam est disponible ou pas encore initialisé).</summary>
    public static string UnavailableReason { get; private set; } = "";
    public static uint AppId { get; private set; }
    public static ulong LocalSteamId { get; private set; }
    public static string PersonaName { get; private set; } = "";

    /// <summary>Invitation acceptée (overlay, liste d'amis) ou lancement avec <c>+connect_lobby &lt;id&gt;</c>.</summary>
    public static event Action<ulong>? LobbyJoinRequested;
    public static event Action<bool>? OverlayActivated;

    /// <summary>Salon demandé au lancement, en attente d'un abonné (lu une fois par <see cref="ConsumePendingLobbyJoin"/>).</summary>
    static ulong pendingLobby;

    public static bool Init()
    {
        if (attempted) return Available;
        attempted = true;
        AddRunner();
        if (HasArg("--no-steam")) return Fail("désactivé par --no-steam");

        string? fileAppId = ReadAppIdFile();
        AppId = fileAppId != null && uint.TryParse(fileAppId, NumberStyles.Integer, CultureInfo.InvariantCulture, out uint id) && id != 0
            ? id : (ShippedAppId != 0 ? ShippedAppId : SpacewarAppId);
        try
        {
            SteamNatives.Install();
            if (fileAppId == null && ShippedAppId != 0 && !OS.HasFeature("editor") && SteamAPI.RestartAppIfNecessary(new AppId_t(ShippedAppId)))
            {
                GD.Print("[Steam] Relance par le client Steam");
                (Engine.GetMainLoop() as SceneTree)?.Quit();
                return Fail("relance par Steam");
            }
            // Hors lancement par Steam : l'AppId passe par l'environnement natif (pas besoin d'écrire steam_appid.txt).
            if (string.IsNullOrEmpty(OS.GetEnvironment("SteamAppId")))
            {
                OS.SetEnvironment("SteamAppId", AppId.ToString(CultureInfo.InvariantCulture));
                OS.SetEnvironment("SteamGameId", AppId.ToString(CultureInfo.InvariantCulture));
            }
            var result = SteamAPI.InitEx(out string message);
            if (result != ESteamAPIInitResult.k_ESteamAPIInitResult_OK)
                return Fail($"{result.ToString().Replace("k_ESteamAPIInitResult_", "")} : {message}".Trim());

            Available = true;
            AppId = SteamUtils.GetAppID().m_AppId;   // l'AppId réel donné par Steam (lancé depuis Steam : pas de steam_appid.txt)
            LocalSteamId = SteamUser.GetSteamID().m_SteamID;
            PersonaName = SteamFriends.GetPersonaName();
            lobbyJoinCb = Callback<GameLobbyJoinRequested_t>.Create(r => RequestLobbyJoin(r.m_steamIDLobby.m_SteamID, "invitation"));
            presenceJoinCb = Callback<GameRichPresenceJoinRequested_t>.Create(r =>
            {
                if (ParseConnectLobby(r.m_rgchConnect) is ulong lobby) RequestLobbyJoin(lobby, "liste d'amis");
            });
            overlayCb = Callback<GameOverlayActivated_t>.Create(r => OverlayActivated?.Invoke(r.m_bActive != 0));
            GD.Print($"[Steam] Connecté : {PersonaName} ({LocalSteamId}), AppId {AppId}" + (SteamNatives.LoadedFrom != null ? $", natif {SteamNatives.LoadedFrom}" : ""));
            Achievements.OnSteamReady();
            SteamLobbies.OnSteamReady();
            if (SteamApps.GetLaunchCommandLine(out string launch, 1024) > 0 && ParseConnectLobby(launch) is ulong fromLaunch)
                RequestLobbyJoin(fromLaunch, "ligne de lancement Steam");
        }
        catch (Exception e) when (e is DllNotFoundException or EntryPointNotFoundException or BadImageFormatException or TypeInitializationException)
        {
            return Fail($"bibliothèque native absente ou invalide ({e.GetType().Name}) — tools/steam/fetch-steam-natives.sh");
        }
        catch (Exception e)
        {
            return Fail($"{e.GetType().Name} : {e.Message}");
        }
        // Argument de lancement direct (Steam ajoute « +connect_lobby <id> » quand on rejoint un ami jeu fermé).
        if (ParseConnectLobby(string.Join(' ', OS.GetCmdlineArgs())) is ulong arg) RequestLobbyJoin(arg, "argument de lancement");
        return true;
    }

    static bool Fail(string reason)
    {
        Available = false;
        UnavailableReason = reason;
        GD.Print($"[Steam] indisponible : {reason}");
        Achievements.OnSteamUnavailable();
        return false;
    }

    static Callback<GamepadTextInputDismissed_t>? textInputCb;
    static Action<string?>? textInputDone;

    /// <summary>Steam tourne sur un Steam Deck.</summary>
    public static bool OnSteamDeck => Available && SteamUtils.IsSteamRunningOnSteamDeck();

    /// <summary>
    /// Clavier virtuel de Steam pour un champ de texte. Sur Steam Deck : clavier flottant qui tape directement dans le champ
    /// (<paramref name="field"/> en pixels de la fenêtre, pour ne pas le couvrir). Ailleurs (Big Picture, manette) : fenêtre de saisie
    /// de Steam, le texte revient par <paramref name="done"/> (null si annulé). Faux si Steam ne peut pas l'ouvrir.
    /// </summary>
    public static bool ShowTextInput(string description, string current, int maxLength, Rect2I field, Action<string?> done)
    {
        if (!Available) return false;
        if (SteamUtils.IsSteamRunningOnSteamDeck()
            && SteamUtils.ShowFloatingGamepadTextInput(EFloatingGamepadTextInputMode.k_EFloatingGamepadTextInputModeModeSingleLine,
                field.Position.X, field.Position.Y, field.Size.X, field.Size.Y))
            return true;
        textInputCb ??= Callback<GamepadTextInputDismissed_t>.Create(r =>
        {
            var callback = textInputDone;
            textInputDone = null;
            string? text = null;
            if (r.m_bSubmitted && SteamUtils.GetEnteredGamepadTextInput(out string entered, SteamUtils.GetEnteredGamepadTextLength() + 1)) text = entered;
            callback?.Invoke(text);
        });
        textInputDone = done;
        bool shown = SteamUtils.ShowGamepadTextInput(EGamepadTextInputMode.k_EGamepadTextInputModeNormal, EGamepadTextInputLineMode.k_EGamepadTextInputLineModeSingleLine,
            description, (uint)maxLength, current);
        if (!shown) textInputDone = null;
        return shown;
    }

    /// <summary>À chaque image (fait par <see cref="SteamRunner"/>).</summary>
    public static void RunCallbacks()
    {
        if (!Available) return;
        SteamAPI.RunCallbacks();
        Achievements.Flush();
    }

    public static void Shutdown()
    {
        Achievements.StoreStats();
        if (!Available) return;
        SteamLobbies.Leave();
        Available = false;
        SteamAPI.Shutdown();
        GD.Print("[Steam] Arrêt");
    }

    /// <summary>Salon demandé avant qu'un abonné écoute <see cref="LobbyJoinRequested"/> (lancement par une invitation) ; 0 si aucun.</summary>
    public static ulong ConsumePendingLobbyJoin()
    {
        ulong lobby = pendingLobby;
        pendingLobby = 0;
        return lobby;
    }

    static void RequestLobbyJoin(ulong lobby, string source)
    {
        GD.Print($"[Steam] Rejoindre le salon {lobby} ({source})");
        if (LobbyJoinRequested == null) pendingLobby = lobby;
        else LobbyJoinRequested.Invoke(lobby);
    }

    /// <summary>Extrait l'id de « +connect_lobby &lt;id&gt; » d'une ligne de commande ou d'une chaîne de présence.</summary>
    public static ulong? ParseConnectLobby(string? text)
    {
        if (string.IsNullOrEmpty(text)) return null;
        var parts = text.Split(new[] { ' ', '\t', '\n', '"' }, StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length - 1; i++)
            if (parts[i] == "+connect_lobby" && ulong.TryParse(parts[i + 1], NumberStyles.Integer, CultureInfo.InvariantCulture, out ulong id) && id != 0)
                return id;
        return null;
    }

    static string? ReadAppIdFile()
    {
        string exeDir = Path.GetDirectoryName(OS.GetExecutablePath()) ?? "";
        foreach (var dir in new[] { Directory.GetCurrentDirectory(), exeDir, ProjectSettings.GlobalizePath("res://") })
        {
            if (string.IsNullOrEmpty(dir) || dir.StartsWith("res://")) continue;
            string path = Path.Combine(dir, "steam_appid.txt");
            try { if (File.Exists(path)) return File.ReadAllText(path).Trim(); }
            catch (IOException) { }
        }
        return null;
    }

    static bool HasArg(string arg) =>
        Array.IndexOf(OS.GetCmdlineArgs(), arg) >= 0 || Array.IndexOf(OS.GetCmdlineUserArgs(), arg) >= 0;

    /// <summary>Le nœud qui appelle RunCallbacks chaque image et coupe Steam à la fermeture, ajouté à la racine en différé.</summary>
    static void AddRunner()
    {
        if (SteamRunner.Instance != null || Engine.GetMainLoop() is not SceneTree tree) return;
        var runner = new SteamRunner { Name = "SteamRunner" };
        SteamRunner.Instance = runner;
        tree.Root.CallDeferred(Node.MethodName.AddChild, runner);
    }
}
