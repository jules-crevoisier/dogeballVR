using System.Linq;
using Godot;
using DogeballVR.Net;

namespace DogeballVR.Online;

/// <summary>
/// Écran de la salle d'avant-match : code à partager, carte (choisie par l'hôte), deux équipes, « prêt », lancement.
/// Sobre et fonctionnel ; le fil interfaces peut le restyler (il ne fait qu'appeler <see cref="OnlineLobby"/>).
/// </summary>
public partial class LobbyScreen : CanvasLayer
{
    static readonly Color Ink = new("101830"), Orange = new("FF6A1A"), Blue = new("3A7BD5"), Gray = new("55607F");
    static readonly string[] TeamNames = { "ÉQUIPE ORANGE", "ÉQUIPE BLEUE" };
    static readonly Color[] TeamColors = { new("FF6A1A"), new("2F8BFF") };

    VBoxContainer root = null!;
    OnlineLobby lobby = null!;
    bool dirty = true;
    Label? rematchLabel;

    public override void _Ready()
    {
        Layer = 15;
        lobby = OnlineLobby.Instance!;
        var bg = new ColorRect { Color = new Color("4C8DE8") };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);
        var center = new CenterContainer();
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        root = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
        root.AddThemeConstantOverride("separation", 14);
        center.AddChild(root);
        lobby.Changed += MarkDirty;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _ExitTree() => lobby.Changed -= MarkDirty;

    void MarkDirty() => dirty = true;

    public override void _Process(double delta)
    {
        if (rematchLabel != null && IsInstanceValid(rematchLabel))
            rematchLabel.Text = $"Revanche dans {Mathf.CeilToInt((float)lobby.RematchIn)} s : mêmes équipes, même carte";
        if (!dirty) return;
        dirty = false;
        rematchLabel = null;
        foreach (var c in root.GetChildren()) c.QueueFree();
        switch (lobby.State)
        {
            case LobbyState.InRoom: BuildRoom(); break;
            case LobbyState.InMatch: root.AddChild(Title("LANCEMENT…")); break;
            case LobbyState.Idle:
                root.AddChild(Title("PARTIE EN LIGNE"));
                root.AddChild(Text(lobby.Status == "" ? "Salle fermée." : lobby.Status, 24));
                root.AddChild(MakeButton("RETOUR AU MENU", Gray, BackToMenu));
                break;
            default:
                root.AddChild(Title(lobby.State == LobbyState.Reconnecting ? "RECONNEXION" : "CONNEXION"));
                root.AddChild(Text(lobby.Status, 24));
                root.AddChild(MakeButton("ANNULER", Gray, BackToMenu));
                break;
        }
    }

    void BuildRoom()
    {
        root.AddChild(Title(lobby.IsHost ? "TA SALLE" : "SALLE"));
        if (lobby.Code != "")
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(Text(lobby.CodeIsInternet ? $"Code : {lobby.Code}" : $"Code (réseau local) : {lobby.Code}", 28));
            row.AddChild(MakeButton("COPIER", Gray, () => DisplayServer.ClipboardSet(lobby.Code), 160));
            if (SteamBridge.Available && lobby.IsHost) row.AddChild(MakeButton("INVITER DES AMIS", Blue, SteamBridge.OpenInvite, 280));
            root.AddChild(row);
        }

        // Carte : l'hôte la choisit, les autres la voient.
        var mapRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        mapRow.AddThemeConstantOverride("separation", 12);
        mapRow.AddChild(Text("Carte :", 24));
        if (lobby.IsHost)
        {
            var pick = new OptionButton { CustomMinimumSize = new Vector2(320, 48) };
            pick.AddThemeFontSizeOverride("font_size", 22);
            for (int i = 0; i < OnlineLobby.Maps.Count; i++)
            {
                pick.AddItem(MapLabel(OnlineLobby.Maps[i]), i);
                if (OnlineLobby.Maps[i] == lobby.Map) pick.Selected = i;
            }
            pick.ItemSelected += i => lobby.SetMap(OnlineLobby.Maps[(int)i]);
            mapRow.AddChild(pick);
        }
        else mapRow.AddChild(Text(MapLabel(lobby.Map), 24));
        root.AddChild(mapRow);

        // Deux équipes ; les places vides seront tenues par des bots.
        var teams = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        teams.AddThemeConstantOverride("separation", 24);
        int perTeam = Mathf.Max(1, lobby.MaxPlayers / 2);
        for (int t = 0; t < 2; t++)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
            panel.AddThemeStyleboxOverride("panel", Box(new Color(1, 1, 1, 0.92f), 16, TeamColors[t]));
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 6);
            panel.AddChild(col);
            var head = Text(TeamNames[t], 24);
            head.AddThemeColorOverride("font_color", TeamColors[t]);
            col.AddChild(head);
            var inTeam = lobby.Members.Where(m => m.Team == t).ToList();
            foreach (var m in inTeam)
            {
                string tags = (m.Host ? " · hôte" : "") + (m.Vr ? " · VR" : " · PC");
                var line = Text($"{(m.Ready ? "✔" : "…")}  {m.Name}{tags}", 22, HorizontalAlignment.Left);
                line.AddThemeColorOverride("font_color", Ink);
                col.AddChild(line);
            }
            for (int k = inTeam.Count; k < perTeam; k++)
            {
                var bot = Text("     Bot", 22, HorizontalAlignment.Left);
                bot.AddThemeColorOverride("font_color", new Color(Ink, 0.45f));
                col.AddChild(bot);
            }
            teams.AddChild(panel);
        }
        root.AddChild(teams);

        var me = lobby.Members.FirstOrDefault(m => m.PeerId == lobby.Multiplayer.GetUniqueId());
        var buttons = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        buttons.AddThemeConstantOverride("separation", 12);
        if (me != null)
        {
            buttons.AddChild(MakeButton("CHANGER D'ÉQUIPE", Blue, () => lobby.SetTeam(1 - me.Team), 280));
            if (!lobby.IsHost) buttons.AddChild(MakeButton(me.Ready ? "PAS PRÊT" : "PRÊT !", me.Ready ? Gray : Orange, () => lobby.SetReady(!me.Ready), 220));
        }
        if (lobby.IsHost)
        {
            var start = MakeButton("LANCER LA PARTIE", Orange, lobby.StartMatch, 300);
            start.Disabled = !lobby.CanStart;
            buttons.AddChild(start);
        }
        root.AddChild(buttons);
        if (lobby.RematchIn > 0)
        {
            var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
            row.AddThemeConstantOverride("separation", 12);
            row.AddChild(rematchLabel = Text("", 24));
            if (lobby.IsHost) row.AddChild(MakeButton("ATTENDRE", Gray, lobby.CancelRematch, 200));
            root.AddChild(row);
            if (!lobby.IsHost) root.AddChild(Text("« PAS PRÊT » arrête le compte à rebours", 18));
        }
        else if (lobby.IsHost && !lobby.CanStart) root.AddChild(Text("En attente que tout le monde soit prêt", 20));
        else if (!lobby.IsHost) root.AddChild(Text("L'hôte lance la partie quand tout le monde est prêt", 20));
        if (lobby.Status != "" && lobby.Status.StartsWith("Trop")) root.AddChild(Text(lobby.Status, 20));
        root.AddChild(MakeButton("QUITTER LA SALLE", Gray, BackToMenu));
        if (lobby.IsHost && lobby.Members.Count == 1)
            root.AddChild(Text(SteamBridge.Available ? "Invite tes amis avec Steam ou donne-leur le code." : "Sur le même réseau, tes amis te trouvent avec « Partie rapide ». Par Internet : donne le code (port UDP 7777 ouvert).", 18));
    }

    void BackToMenu()
    {
        lobby.Leave();
        GetTree().ReloadCurrentScene();
    }

    static string MapLabel(string map) => map switch
    {
        "croix_neon" => "Croix Néon (jeu libre)",
        "fosse_croisee" => "Fosse croisée (jeu libre)",
        "tour_4v4" => "Tour 4 contre 4 (jeu libre)",
        "neon_court" => "Néon Court (zones, VR)",
        "la_fosse" => "La Fosse (zones, VR)",
        _ => map,
    };

    static Label Title(string text)
    {
        var l = Text(text, 64);
        l.AddThemeColorOverride("font_color", Orange);
        l.AddThemeColorOverride("font_outline_color", Ink);
        l.AddThemeConstantOverride("outline_size", 16);
        return l;
    }

    static Label Text(string text, int size, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        var l = new Label { Text = text, HorizontalAlignment = align, AutowrapMode = TextServer.AutowrapMode.WordSmart };
        l.AddThemeFontSizeOverride("font_size", size);
        l.AddThemeColorOverride("font_color", Colors.White);
        l.AddThemeColorOverride("font_outline_color", Ink);
        l.AddThemeConstantOverride("outline_size", 6);
        return l;
    }

    static StyleBoxFlat Box(Color c, int radius = 18, Color? border = null) => new()
    {
        BgColor = c, CornerRadiusTopLeft = radius, CornerRadiusTopRight = radius, CornerRadiusBottomLeft = radius, CornerRadiusBottomRight = radius,
        ContentMarginLeft = 22, ContentMarginRight = 22, ContentMarginTop = 12, ContentMarginBottom = 12,
        BorderWidthBottom = 6, BorderColor = border ?? Ink,
    };

    static Button MakeButton(string text, Color color, System.Action onPress, int width = 420)
    {
        var b = new Button { Text = text, CustomMinimumSize = new Vector2(width, 58), FocusMode = Control.FocusModeEnum.All };
        b.AddThemeFontSizeOverride("font_size", 24);
        b.AddThemeColorOverride("font_color", Colors.White);
        b.AddThemeColorOverride("font_hover_color", Colors.White);
        b.AddThemeStyleboxOverride("normal", Box(color));
        b.AddThemeStyleboxOverride("hover", Box(color.Lightened(0.18f)));
        b.AddThemeStyleboxOverride("pressed", Box(color.Darkened(0.2f)));
        b.AddThemeStyleboxOverride("focus", Box(color.Lightened(0.1f)));
        b.AddThemeStyleboxOverride("disabled", Box(color.Darkened(0.35f)));
        b.Pressed += onPress;
        return b;
    }
}
