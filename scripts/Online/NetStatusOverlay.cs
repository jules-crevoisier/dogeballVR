using Godot;
using DogeballVR.Net;

namespace DogeballVR.Online;

/// <summary>
/// État de la connexion en partie : ping en haut à droite (vert, orange, rouge), « connexion instable » quand les snapshots
/// n'arrivent plus, et l'écran de reconnexion quand le lobby retente de joindre l'hôte.
/// </summary>
public partial class NetStatusOverlay : CanvasLayer
{
    public NetSession Net { get; set; } = null!;

    Label ping = null!, banner = null!;
    double refresh;

    public override void _Ready()
    {
        Layer = 20;
        ping = new Label { HorizontalAlignment = HorizontalAlignment.Right, MouseFilter = Control.MouseFilterEnum.Ignore };
        ping.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        ping.OffsetLeft = -260; ping.OffsetRight = -14; ping.OffsetTop = 124;   // sous le fil des KO du HUD (coin haut droit)
        ping.AddThemeFontSizeOverride("font_size", 18);
        ping.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.09f, 0.19f));
        ping.AddThemeConstantOverride("outline_size", 6);
        AddChild(ping);
        banner = new Label { HorizontalAlignment = HorizontalAlignment.Center, VerticalAlignment = VerticalAlignment.Center, Visible = false, MouseFilter = Control.MouseFilterEnum.Ignore };
        banner.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        banner.OffsetLeft = -400; banner.OffsetRight = 400; banner.OffsetTop = 90; banner.OffsetBottom = 150;
        banner.AddThemeFontSizeOverride("font_size", 30);
        banner.AddThemeColorOverride("font_color", Colors.White);
        banner.AddThemeColorOverride("font_outline_color", new Color(0.06f, 0.09f, 0.19f));
        banner.AddThemeConstantOverride("outline_size", 10);
        AddChild(banner);
    }

    public override void _Process(double delta)
    {
        refresh -= delta;
        if (refresh > 0) return;
        refresh = 0.25;
        var lobby = OnlineLobby.Instance;
        if (lobby is { State: LobbyState.Reconnecting })
        {
            banner.Visible = true;
            banner.Text = lobby.Status;
            ping.Text = "";
            return;
        }
        if (Net.IsHost)
        {
            ping.Text = Net.Remotes.Count > 0 ? $"Hôte · {Net.Remotes.Count} joueur(s) en ligne" : "Hôte · en attente de joueurs";
            ping.AddThemeColorOverride("font_color", Colors.White);
            banner.Visible = false;
            return;
        }
        double now = Time.GetTicksMsec() / 1000.0;
        bool stale = Net.LastSnapshotAt > 0 && now - Net.LastSnapshotAt > 0.6;
        banner.Visible = stale;
        banner.Text = "Connexion instable…";
        double rtt = Net.RttMs;
        ping.Text = rtt < 0 ? "Ping …" : $"Ping {rtt:0} ms";
        ping.AddThemeColorOverride("font_color", rtt < 0 || rtt < 80 ? new Color("7CF29A") : rtt < 150 ? new Color("FFC24A") : new Color("FF5A5A"));
    }
}
