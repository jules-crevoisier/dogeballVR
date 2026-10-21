using Godot;

namespace DogeballVR.Online.Steam;

/// <summary>Nœud racine persistant : callbacks Steam à chaque image, arrêt propre à la fermeture.</summary>
public partial class SteamRunner : Node
{
    public static SteamRunner? Instance { get; internal set; }
    bool shutDown;

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;   // le menu pause ne coupe pas les callbacks
        Instance = this;
    }

    public override void _Process(double delta) => SteamPlatform.RunCallbacks();

    public override void _Notification(int what)
    {
        if ((what == NotificationWMCloseRequest || what == NotificationPredelete) && !shutDown)
        {
            shutDown = true;
            SteamPlatform.Shutdown();
        }
    }

    public override void _ExitTree()
    {
        if (Instance == this) Instance = null;
    }
}
