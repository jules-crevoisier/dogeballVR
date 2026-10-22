using System.Collections.Generic;
using System.Linq;
using Godot;
using DogeballVR.Net;
using DogeballVR.UI;

namespace DogeballVR.Online;

/// <summary>
/// Écran de la salle d'avant-match : code à partager, carte (choisie par l'hôte), deux équipes, « prêt », lancement.
/// Dans la DA du menu principal : fond violet nuit, fenêtre penchée à liseré crème, titres jaunes penchés, boutons à barre jaune.
/// </summary>
public partial class LobbyScreen : CanvasLayer
{
    static readonly Color Orange = UiKit.Orange, Blue = UiKit.Blue, Gray = UiKit.Grey;
    static readonly string[] TeamNames = { "ÉQUIPE ORANGE", "ÉQUIPE BLEUE" };
    static readonly Color[] TeamColors = { new("FF6A1A"), new("2F8BFF") };

    VBoxContainer root = null!;
    OnlineLobby lobby = null!;
    bool dirty = true;
    Label? rematchLabel, quickLabel;

    public override void _Ready()
    {
        Layer = 15;
        lobby = OnlineLobby.Instance!;
        // Fond du menu : dégradé violet profond vers marine, en biais.
        var gradient = new Gradient { Colors = new[] { UiKit.VioletTop, UiKit.VioletMid, UiKit.NavyLow }, Offsets = new[] { 0f, 0.5f, 1f } };
        var bg = new TextureRect { Texture = new GradientTexture2D { Gradient = gradient, FillFrom = new Vector2(0, 0), FillTo = new Vector2(0.35f, 1), Width = 64, Height = 64 }, ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize, StretchMode = TextureRect.StretchModeEnum.Scale };
        bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(bg);
        var center = new CenterContainer { Theme = UiKit.Theme };
        center.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(center);
        var panel = new PanelContainer();
        panel.AddThemeStyleboxOverride("panel", UiKit.PanelBox(22, 34));
        center.AddChild(panel);
        root = new VBoxContainer { CustomMinimumSize = new Vector2(860, 0) };
        root.AddThemeConstantOverride("separation", 14);
        panel.AddChild(root);
        lobby.Changed += MarkDirty;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _ExitTree() => lobby.Changed -= MarkDirty;

    void MarkDirty() => dirty = true;

    public override void _Process(double delta)
    {
        if (rematchLabel != null && IsInstanceValid(rematchLabel))
            rematchLabel.Text = $"Revanche dans {Mathf.CeilToInt((float)lobby.RematchIn)} s : mêmes équipes, même carte";
        if (quickLabel != null && IsInstanceValid(quickLabel))
            quickLabel.Text = QuickText();
        if (!dirty) return;
        dirty = false;
        rematchLabel = quickLabel = null;
        string? focused = (GetViewport().GuiGetFocusOwner() as Button)?.Text;
        foreach (var c in root.GetChildren()) c.QueueFree();
        CallDeferred(MethodName.RestoreFocus, focused ?? "");
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
            row.AddChild(Inline(Text(lobby.CodeIsInternet ? $"Code : {lobby.Code}" : $"Code (réseau local) : {lobby.Code}", 28)));
            row.AddChild(MakeButton("COPIER", Gray, () => DisplayServer.ClipboardSet(lobby.Code), 160));
            if (SteamBridge.Available && lobby.IsHost) row.AddChild(MakeButton("INVITER DES AMIS", Blue, SteamBridge.OpenInvite, 280));
            root.AddChild(row);
        }

        // Carte : l'hôte la choisit, les autres la voient.
        var mapRow = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        mapRow.AddThemeConstantOverride("separation", 12);
        mapRow.AddChild(Inline(Text("Carte :", 24)));
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
        else mapRow.AddChild(Inline(Text(MapLabel(lobby.Map), 24)));
        root.AddChild(mapRow);

        // Deux équipes ; les places vides seront tenues par des bots.
        var teams = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        teams.AddThemeConstantOverride("separation", 24);
        int perTeam = Mathf.Max(1, lobby.MaxPlayers / 2);
        for (int t = 0; t < 2; t++)
        {
            var panel = new PanelContainer { CustomMinimumSize = new Vector2(400, 0) };
            // Encart sombre penché, liseré de la couleur de l'équipe.
            var box = UiKit.InsetBox(null, 10, 3, TeamColors[t]);
            box.ContentMarginTop = box.ContentMarginBottom = 12;
            panel.AddThemeStyleboxOverride("panel", box);
            var col = new VBoxContainer();
            col.AddThemeConstantOverride("separation", 6);
            panel.AddChild(col);
            var head = UiKit.Title(TeamNames[t], 30, HorizontalAlignment.Center, TeamColors[t]);
            col.AddChild(head);
            var inTeam = lobby.Members.Where(m => m.Team == t).ToList();
            foreach (var m in inTeam)
            {
                string tags = (m.Host ? " · hôte" : "") + (m.Vr ? " · VR" : " · PC");
                var line = Text($"{(m.Ready ? "✔" : "…")}  {m.Name}{tags}", 22, HorizontalAlignment.Left);
                col.AddChild(line);
            }
            for (int k = inTeam.Count; k < perTeam; k++)
            {
                var bot = Text("     Bot", 22, HorizontalAlignment.Left);
                bot.AddThemeColorOverride("font_color", UiKit.Lavender);
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
            row.AddChild(rematchLabel = Inline(Text("", 24)));
            if (lobby.IsHost) row.AddChild(MakeButton("ATTENDRE", Gray, lobby.CancelRematch, 200));
            root.AddChild(row);
            if (!lobby.IsHost) root.AddChild(Text("« PAS PRÊT » arrête le compte à rebours", 20));
        }
        else if (lobby.QuickStartIn > 0)
        {
            root.AddChild(quickLabel = Text(QuickText(), 26));
            quickLabel.AddThemeColorOverride("font_color", UiKit.Yellow);
            root.AddChild(Text(lobby.Members.Count < lobby.MaxPlayers ? "Les places libres seront tenues par des bots. D'autres joueurs peuvent encore arriver." : "Salle pleine !", 20));
        }
        else if (lobby.IsHost && !lobby.CanStart) root.AddChild(Text("En attente que tout le monde soit prêt", 20));
        else if (!lobby.IsHost) root.AddChild(Text("L'hôte lance la partie quand tout le monde est prêt", 20));
        if (lobby.Status != "" && lobby.Status.StartsWith("Trop")) root.AddChild(Text(lobby.Status, 20));
        root.AddChild(MakeButton("QUITTER LA SALLE", Gray, BackToMenu));
        if (lobby.IsHost && lobby.Members.Count == 1)
            root.AddChild(Text(SteamBridge.Available ? "Invite tes amis avec Steam ou donne-leur le code."
                : lobby.ListedOnLan ? "Sur le même réseau, tes amis te trouvent avec « Partie rapide ». Par Internet : donne le code (port UDP 7777 ouvert)."
                : "Salle privée : seuls ceux qui ont le code entrent. Par Internet, le port UDP 7777 doit être ouvert (Options > Partie).", 20));
    }

    string QuickText() => lobby.Members.All(m => m.Ready)
        ? $"Lancement dans {Mathf.CeilToInt((float)lobby.QuickStartIn)} s · {lobby.Members.Count}/{lobby.MaxPlayers} joueurs"
        : "Lancement en pause : un joueur n'est pas prêt";

    // Manette (Steam Deck) : l'écran est reconstruit à chaque changement de la salle ; le focus revient sur le même bouton,
    // sinon sur l'action principale, pour que la croix et A marchent toujours.
    static readonly string[] FocusOrder = { "PRÊT !", "PAS PRÊT", "LANCER LA PARTIE", "ATTENDRE", "ANNULER", "RETOUR AU MENU", "CHANGER D'ÉQUIPE", "QUITTER LA SALLE" };

    void RestoreFocus(string previous)
    {
        var buttons = new List<Button>();
        Collect(root, buttons);
        var target = buttons.FirstOrDefault(b => b.Text == previous && !b.Disabled);
        foreach (var name in FocusOrder)
            target ??= buttons.FirstOrDefault(b => b.Text == name && !b.Disabled);
        target?.GrabFocus();
    }

    static void Collect(Node n, List<Button> into)
    {
        foreach (var c in n.GetChildren())
        {
            if (c.IsQueuedForDeletion()) continue;
            if (c is Button b) into.Add(b);
            Collect(c, into);
        }
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

    static Label Title(string text) => UiKit.Title(text, 64);

    /// <summary>Texte dans une rangée (HBox) : sans retour à la ligne, sinon la rangée lui donne une largeur nulle et il s'écrit lettre par lettre.</summary>
    static Label Inline(Label l)
    {
        l.AutowrapMode = TextServer.AutowrapMode.Off;
        return l;
    }

    static Label Text(string text, int size, HorizontalAlignment align = HorizontalAlignment.Center)
    {
        var l = UiKit.Text(text, size, Colors.White, 6, align);
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        return l;
    }

    /// <summary>Bouton du menu (barre jaune au focus) ; gris = action secondaire. Désactivé : aplat éteint, texte lavande.</summary>
    static Button MakeButton(string text, Color color, System.Action onPress, int width = 420)
    {
        var b = UiKit.Button(text, color, onPress, 24, new Vector2(width, 58));
        b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;   // largeur fixe, centré (pas étiré sur toute la fenêtre)
        var off = UiKit.ButtonIdle(new Color(0, 0, 0, 0));
        off.Top = new Color(off.Top, 0.5f); off.Bottom = new Color(off.Bottom, 0.5f); off.BorderColor = new Color(UiKit.Cream, 0.3f);
        b.AddThemeStyleboxOverride("disabled", off);
        b.AddThemeColorOverride("font_disabled_color", new Color(UiKit.Lavender, 0.6f));
        return b;
    }
}
