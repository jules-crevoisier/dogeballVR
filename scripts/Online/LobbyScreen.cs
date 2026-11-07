using System.Collections.Generic;
using System.Linq;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;
using DogeballVR.Net;
using DogeballVR.UI;

namespace DogeballVR.Online;

/// <summary>
/// Écran de la salle d'avant-match, DA « Affiche », construit comme l'écran de fin de manche : les deux moitiés d'équipe en
/// aplats (orange à gauche, bleue à droite, trame de points au bord), le nom de l'équipe en très gros et ses joueurs en
/// plaques rangées vers le centre (le joueur local cerclé de lime, pastille PRÊT), les places libres en plaques éteintes
/// tenues par des bots. Au centre, une carte d'encre : aperçu de l'arène, son nom et son format (l'hôte la change avec les
/// flèches ou Q / E, LB / RB), puis le code de la salle à partager. En bas : l'état de la salle (lancement, revanche,
/// attente) dans le même encart que « MANCHE 2 DANS », les actions et les touches.
/// </summary>
public partial class LobbyScreen : CanvasLayer
{
    static readonly Color Orange = UiKit.Orange, Blue = UiKit.Blue, Gray = UiKit.Grey;
    /// <summary>Écran dessiné pour 1920×1080, centré (plus large : les aplats s'étendent, le contenu reste au milieu).</summary>
    const float W = 1920f, H = 1080f;

    Control content = null!, halves = null!, plainBg = null!;
    OnlineLobby lobby = null!;
    bool dirty = true;
    Label? rematchLabel, quickLabel, quickNumber;

    public override void _Ready()
    {
        Layer = 15;
        lobby = OnlineLobby.Instance!;
        var root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);
        // Connexion, lancement : fond d'encre uni. Dans la salle : les deux moitiés d'équipe.
        plainBg = new ColorRect { Color = UiKit.VioletMid, MouseFilter = Control.MouseFilterEnum.Ignore };
        plainBg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(plainBg);
        halves = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        halves.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(halves);
        for (int t = 0; t < 2; t++)
        {
            var half = new DesktopHud.InterHalf { Team = t, MouseFilter = Control.MouseFilterEnum.Ignore };
            half.SetAnchorsPreset(Control.LayoutPreset.FullRect);
            halves.AddChild(half);
        }
        content = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        content.SetAnchorsPreset(Control.LayoutPreset.Center);
        content.OffsetLeft = -W / 2; content.OffsetRight = W / 2; content.OffsetTop = -H / 2; content.OffsetBottom = H / 2;
        root.AddChild(content);
        lobby.Changed += MarkDirty;
        Input.MouseMode = Input.MouseModeEnum.Visible;
    }

    public override void _ExitTree() => lobby.Changed -= MarkDirty;

    void MarkDirty() => dirty = true;

    public override void _Process(double delta)
    {
        if (rematchLabel != null && IsInstanceValid(rematchLabel))
            rematchLabel.Text = Mathf.CeilToInt((float)lobby.RematchIn).ToString();
        if (quickLabel != null && IsInstanceValid(quickLabel))
        {
            bool all = lobby.Members.All(m => m.Ready);
            quickLabel.Text = all ? L.T("LANCEMENT DANS") : L.T("LANCEMENT EN PAUSE");
            if (quickNumber != null && IsInstanceValid(quickNumber))
            {
                quickNumber.Visible = all;
                quickNumber.Text = Mathf.CeilToInt((float)lobby.QuickStartIn).ToString();
            }
        }
        if (!dirty) return;
        dirty = false;
        rematchLabel = quickLabel = quickNumber = null;
        string? focused = (GetViewport().GuiGetFocusOwner() as Button)?.Text;
        foreach (var c in content.GetChildren()) c.QueueFree();
        CallDeferred(MethodName.RestoreFocus, focused ?? "");
        bool room = lobby.State == LobbyState.InRoom;
        halves.Visible = room;
        plainBg.Visible = !room;
        switch (lobby.State)
        {
            case LobbyState.InRoom: BuildRoom(); break;
            case LobbyState.InMatch: Message("LANCEMENT…", "", null, null); break;
            case LobbyState.Idle:
                Message("PARTIE EN LIGNE", lobby.Status == "" ? L.T("Salle fermée.") : OnlineLobby.Display(lobby.Status), "RETOUR AU MENU", BackToMenu);
                break;
            default:
                Message(lobby.State == LobbyState.Reconnecting ? "RECONNEXION" : "CONNEXION", OnlineLobby.Display(lobby.Status), "ANNULER", BackToMenu);
                break;
        }
    }

    /// <summary>Connexion, reconnexion, salle fermée, lancement : grand titre d'affiche, une ligne d'état, un bouton.</summary>
    void Message(string title, string text, string? button, System.Action? onPress)
    {
        var col = new VBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, Position = new Vector2(0, 0), Size = new Vector2(W, H) };
        col.AddThemeConstantOverride("separation", 26);
        content.AddChild(col);
        col.AddChild(UiKit.Title(title, 110));
        if (text != "") col.AddChild(Wrapped(UiKit.Raw(UiKit.Text(text, 30, UiKit.Cream, 0, HorizontalAlignment.Center)), 1100));
        if (button != null && onPress != null)
        {
            var b = MakeButton(button, Gray, onPress, 380);
            b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
            col.AddChild(b);
        }
    }

    void BuildRoom()
    {
        var c = content;
        var me = lobby.Members.FirstOrDefault(m => m.PeerId == lobby.Multiplayer.GetUniqueId());
        int myTeam = me?.Team ?? 0;

        // En-tête : le mode de la salle (titre d'affiche papier), puis le code à partager juste dessous.
        string kind = lobby.QuickStartIn > 0 ? "PARTIE RAPIDE" : "PARTIE PERSO";
        c.AddChild(Place(UiKit.Poster(UiKit.Text(L.T(kind), 66, UiKit.Cream, 0, HorizontalAlignment.Center), null, 5), 0, 26, W, 90));
        BuildCode(c);

        // Équipes : nom en gros, « TON ÉQUIPE · 1/3 », joueurs puis places libres, rangés vers le centre.
        int perTeam = Mathf.Max(1, lobby.MaxPlayers / 2);
        for (int t = 0; t < 2; t++)
        {
            bool left = t == 0;
            var align = left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            float x = left ? 70 : 1250;
            var inTeam = lobby.Members.Where(m => m.Team == t).ToList();
            c.AddChild(Place(UiKit.Poster(UiKit.Text(L.T(left ? "ORANGE" : "BLEUE"), 88, Colors.White, 0, align)), x, 236, 600, 110));
            var sub = UiKit.Text(L.F(t == myTeam ? "TON ÉQUIPE · {0}/{1}" : "ADVERSAIRES · {0}/{1}", inTeam.Count, perTeam), 28, UiKit.Cream, 0, align);
            if (UiKit.Heavy is Font hv) sub.AddThemeFontOverride("font", hv);
            c.AddChild(Place(UiKit.Poster(sub, null, 2), x, 344, 600, 40));
            var list = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(x, 396), Size = new Vector2(600, 0) };
            list.AddThemeConstantOverride("separation", 10);
            c.AddChild(list);
            foreach (var m in inTeam) list.AddChild(PlayerRow(m, me, left));
            for (int k = inTeam.Count; k < perTeam; k++) list.AddChild(FreeRow(t, k == inTeam.Count));
        }

        BuildMapCard(c);
        BuildStatus(c);
        BuildActions(c, me);

        // Aides de bas d'écran, centrées sous les boutons.
        var hints = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center, Position = new Vector2(0, 984), Size = new Vector2(W, 40) };
        hints.AddThemeConstantOverride("separation", 40);
        hints.AddChild(UiKit.KeyHint("Entrée", "Valider", 22));
        if (lobby.IsHost) hints.AddChild(UiKit.KeyHint("Q / E", "Carte", 22));
        hints.AddChild(UiKit.KeyHint(InputSetup.KeyName("pause"), "Quitter la salle", 22));
        c.AddChild(hints);
    }

    /// <summary>Code de la salle sous le titre : « CODE  60001-047K1  [COPIER]  [INVITER DES AMIS] ».</summary>
    void BuildCode(Control c)
    {
        if (lobby.Code == "")
        {
            // Invité sans code à afficher : la salle de qui.
            var host = lobby.Members.FirstOrDefault(m => m.Host);
            if (host == null) return;
            var who = UiKit.Raw(UiKit.Text(L.F("SALLE DE {0}", host.Name.ToUpperInvariant()), 26, UiKit.Cream, 0, HorizontalAlignment.Center));
            if (UiKit.Heavy is Font hvw) who.AddThemeFontOverride("font", hvw);
            c.AddChild(Place(UiKit.Poster(who, null, 2), 0, 128, W, 50));
            return;
        }
        // Cartouche d'encre (comme la carte et le statut) : la séparation des équipes ne passe pas derrière le code.
        var holder = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(0, 124), Size = new Vector2(W, 72) };
        c.AddChild(holder);
        var plate = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var plateBox = UiKit.Box(UiKit.Navy, 0, 0, 0, null, 0);
        plateBox.ContentMarginLeft = 26; plateBox.ContentMarginRight = 12; plateBox.ContentMarginTop = 6; plateBox.ContentMarginBottom = 8;
        plateBox.ShadowColor = new Color(UiKit.Ink, 0.6f); plateBox.ShadowOffset = new Vector2(8, 8); plateBox.ShadowSize = 1;
        plate.AddThemeStyleboxOverride("panel", plateBox);
        holder.AddChild(plate);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        plate.AddChild(row);
        // Libellé en Barlow sans ombre : la valeur porte le poids. Le réseau est dit ici, pas dans le titre.
        var label = UiKit.Text(L.T(lobby.CodeIsInternet ? "CODE · INTERNET" : lobby.ListedOnLan ? "CODE · RÉSEAU LOCAL" : "CODE"), 22, UiKit.Cream, 0);
        if (UiKit.Heavy is Font hvc) label.AddThemeFontOverride("font", hvc);
        label.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(label);
        var code = UiKit.Raw(UiKit.Poster(UiKit.Text(lobby.Code, 46, Colors.White, 0), null, 3));
        code.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(code);
        var copy = MakeButton("COPIER", Gray, () => DisplayServer.ClipboardSet(lobby.Code), 140, 48, 22);
        copy.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        row.AddChild(copy);
        if (SteamBridge.Available && lobby.IsHost)
        {
            var invite = MakeButton("INVITER DES AMIS", Gray, SteamBridge.OpenInvite, 280, 48, 22);
            invite.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
            row.AddChild(invite);
        }
    }

    /// <summary>
    /// Plaque d'un joueur, en aplat d'encre plein : nom (le mien en lime, languette lime au bord extérieur), couronne et « HÔTE »,
    /// pastille de plateforme, état PRÊT / PAS PRÊT côté centre.
    /// </summary>
    Control PlayerRow(LobbyMember m, LobbyMember? me, bool left)
    {
        bool mine = me != null && m.PeerId == me.PeerId;
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = left ? Control.SizeFlags.ShrinkEnd : Control.SizeFlags.ShrinkBegin };
        // Joueur local : nom lime et languette lime au bord extérieur (le liseré complet est réservé au focus des boutons).
        var box = UiKit.Box(UiKit.Navy, 0, 0, 0, UiKit.Yellow, 0);
        if (mine) { if (left) box.BorderWidthLeft = 10; else box.BorderWidthRight = 10; }
        box.ContentMarginLeft = box.ContentMarginRight = 24; box.ContentMarginTop = 6; box.ContentMarginBottom = 8;
        box.ShadowColor = new Color(UiKit.Ink, 0.5f); box.ShadowOffset = new Vector2(6, 6); box.ShadowSize = 1;
        row.AddThemeStyleboxOverride("panel", box);
        row.CustomMinimumSize = new Vector2(524, 78);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 18);
        row.AddChild(line);
        var a = left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var names = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(260, 0), SizeFlagsHorizontal = Control.SizeFlags.ExpandFill };
        names.AddThemeConstantOverride("separation", 0);
        var name = UiKit.Raw(UiKit.Text(m.Name, 40, mine ? UiKit.Yellow : Colors.White, 0, a));
        if (UiKit.Heavy is Font hv) name.AddThemeFontOverride("font", hv);
        name.TextOverrunBehavior = TextServer.OverrunBehavior.TrimEllipsis;
        name.CustomMinimumSize = new Vector2(260, 0);
        names.AddChild(name);
        // Ligne d'étiquettes : [PC] (ou [VR]), couronne + « HÔTE ».
        var tags = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = left ? BoxContainer.AlignmentMode.End : BoxContainer.AlignmentMode.Begin };
        tags.AddThemeConstantOverride("separation", 10);
        var platform = Badge(m.Vr ? "VR" : "PC");
        Control? host = null;
        if (m.Host)
        {
            var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            h.AddThemeConstantOverride("separation", 6);
            h.AddChild(new Crown());
            var ht = UiKit.Text(L.T("HÔTE"), 18, UiKit.Cream, 0);
            if (UiKit.Heavy is Font hv2) ht.AddThemeFontOverride("font", hv2);
            ht.VerticalAlignment = VerticalAlignment.Center;
            h.AddChild(ht);
            host = h;
        }
        if (left) { if (host != null) tags.AddChild(host); tags.AddChild(platform); }
        else { tags.AddChild(platform); if (host != null) tags.AddChild(host); }
        names.AddChild(tags);
        var chip = Chip(m.Ready, left);
        var mono = Monogram(m.Name, m.Team);
        if (left) { line.AddChild(mono); line.AddChild(names); line.AddChild(chip); }
        else { line.AddChild(chip); line.AddChild(names); line.AddChild(mono); }
        return row;
    }

    /// <summary>Monogramme du joueur (à la place d'un avatar) : initiale en Anton dans un carré à la couleur de l'équipe, côté extérieur.</summary>
    static Control Monogram(string name, int team)
    {
        string initial = name.Trim().Length > 0 ? name.Trim()[..1].ToUpperInvariant() : "?";
        var l = UiKit.Raw(UiKit.Poster(UiKit.Text(initial, 36, Colors.White, 0, HorizontalAlignment.Center), null, 3));
        l.VerticalAlignment = VerticalAlignment.Center;
        l.CustomMinimumSize = new Vector2(58, 58);
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var box = UiKit.Box(team == 0 ? UiKit.Orange : UiKit.Blue, 0, 0, 0, null, 0);
        box.ContentMarginLeft = box.ContentMarginRight = box.ContentMarginTop = box.ContentMarginBottom = 0;
        l.AddThemeStyleboxOverride("normal", box);
        return l;
    }

    /// <summary>Pastille de plateforme : capitales encre sur papier, petites (comme une touche).</summary>
    static Control Badge(string text)
    {
        var l = UiKit.Raw(UiKit.Text(text, 16, UiKit.Navy, 0, HorizontalAlignment.Center));
        if (UiKit.Heavy is Font hv) l.AddThemeFontOverride("font", hv);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        var box = UiKit.Box(new Color(UiKit.Cream, 0.85f), 0, 0, 0, null, 0);
        box.ContentMarginLeft = box.ContentMarginRight = 7; box.ContentMarginTop = 0; box.ContentMarginBottom = 1;
        l.AddThemeStyleboxOverride("normal", box);
        return l;
    }

    /// <summary>Couronne de l'hôte : trois pointes papier sur un bandeau.</summary>
    sealed partial class Crown : Control
    {
        public Crown() { CustomMinimumSize = new Vector2(22, 18); MouseFilter = MouseFilterEnum.Ignore; SizeFlagsVertical = SizeFlags.ShrinkCenter; }
        public override void _Draw()
        {
            float w = Size.X, h = Size.Y;
            DrawColoredPolygon(new[] { new Vector2(0, h * 0.25f), new Vector2(w * 0.3f, h * 0.55f), new Vector2(w * 0.5f, 0), new Vector2(w * 0.7f, h * 0.55f),
                new Vector2(w, h * 0.25f), new Vector2(w * 0.88f, h * 0.8f), new Vector2(w * 0.12f, h * 0.8f) }, UiKit.Cream);
            DrawRect(new Rect2(w * 0.12f, h * 0.86f, w * 0.76f, h * 0.14f), UiKit.Cream);
        }
    }

    /// <summary>
    /// Place libre : boîte vide à contour en tirets sur l'aplat de l'équipe (motif d'affiche « à remplir »), « PLACE LIBRE »
    /// et, dessous, « BOT SI PERSONNE » (la place sera tenue par un bot si personne n'arrive).
    /// </summary>
    static Control FreeRow(int team, bool first)
    {
        bool left = team == 0;
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, SizeFlagsHorizontal = left ? Control.SizeFlags.ShrinkEnd : Control.SizeFlags.ShrinkBegin, CustomMinimumSize = new Vector2(524, 78) };
        var box = new StyleBoxFlat { BgColor = (left ? UiKit.Orange : UiKit.Blue).Darkened(0.14f) };
        box.ContentMarginLeft = box.ContentMarginRight = 24; box.ContentMarginTop = 8; box.ContentMarginBottom = 8;
        row.AddThemeStyleboxOverride("panel", box);
        row.AddChild(new Dashes());
        var a = left ? HorizontalAlignment.Right : HorizontalAlignment.Left;
        var names = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        names.AddThemeConstantOverride("separation", -2);
        var free = UiKit.Text(L.T("PLACE LIBRE"), 30, new Color(UiKit.Cream, 0.9f), 0, a);
        if (UiKit.Display is Font d) free.AddThemeFontOverride("font", d);
        names.AddChild(free);
        if (first) names.AddChild(UiKit.Text(L.T("BOT SI PERSONNE"), 18, new Color(UiKit.Cream, 0.75f), 0, a));   // une fois par équipe
        row.AddChild(names);
        return row;
    }

    /// <summary>Contour en tirets papier d'une place libre (dessiné sur toute la plaque).</summary>
    sealed partial class Dashes : Control
    {
        public Dashes() { MouseFilter = MouseFilterEnum.Ignore; }
        public override void _Notification(int what) { if (what == NotificationResized) QueueRedraw(); }
        public override void _Draw()
        {
            var col = new Color(UiKit.Cream, 0.7f);
            // La plaque a des marges : le contour suit le bord de la plaque, pas le contenu.
            var r = new Rect2(-24, -8, Size.X + 48, Size.Y + 16);
            const float dash = 12f, gap = 8f, th = 2.5f;
            void Edge(Vector2 a, Vector2 b)
            {
                float len = a.DistanceTo(b);
                var dir = (b - a) / len;
                for (float t = 0; t < len; t += dash + gap) DrawLine(a + dir * t, a + dir * Mathf.Min(t + dash, len), col, th);
            }
            Edge(r.Position, new Vector2(r.End.X, r.Position.Y));
            Edge(new Vector2(r.End.X, r.Position.Y), r.End);
            Edge(r.End, new Vector2(r.Position.X, r.End.Y));
            Edge(new Vector2(r.Position.X, r.End.Y), r.Position);
        }
    }

    /// <summary>
    /// État du joueur, en texte sur l'encre de la plaque (pas une boîte : rien ne doit ressembler à un bouton) : coche papier et
    /// « PRÊT », ou tiret et « PAS PRÊT » atténués.
    /// </summary>
    static Control Chip(bool ready, bool left)
    {
        var h = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(120, 0), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter,
            Alignment = left ? BoxContainer.AlignmentMode.End : BoxContainer.AlignmentMode.Begin };
        h.AddThemeConstantOverride("separation", 8);
        var col = new Color(UiKit.Cream, ready ? 1f : 0.5f);
        if (ready) h.AddChild(new Check());
        var l = UiKit.Text(L.T(ready ? "PRÊT" : "– PAS PRÊT"), 24, col, 0);
        if (UiKit.Display is Font d) l.AddThemeFontOverride("font", d);
        l.VerticalAlignment = VerticalAlignment.Center;
        h.AddChild(l);
        return h;
    }

    /// <summary>Coche papier épaisse (prêt).</summary>
    sealed partial class Check : Control
    {
        public Check() { CustomMinimumSize = new Vector2(24, 22); MouseFilter = MouseFilterEnum.Ignore; SizeFlagsVertical = SizeFlags.ShrinkCenter; }
        public override void _Draw() =>
            DrawPolyline(new[] { new Vector2(2, Size.Y * 0.55f), new Vector2(Size.X * 0.4f, Size.Y - 3), new Vector2(Size.X - 2, 3) }, UiKit.Cream, 5f, true);
    }

    /// <summary>Carte d'encre du centre : aperçu de l'arène, son nom (flèches pour l'hôte) et son format.</summary>
    void BuildMapCard(Control c)
    {
        const float cw = 540, cx = (W - cw) / 2, cy = 236;
        var plate = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(cx, cy), Size = new Vector2(cw, 0) };
        var plateBox = UiKit.Box(UiKit.Navy, 0, 0, 0, null, 0);
        plateBox.ContentMarginLeft = plateBox.ContentMarginRight = 20; plateBox.ContentMarginTop = 18; plateBox.ContentMarginBottom = 16;
        plateBox.ShadowColor = new Color(UiKit.Ink, 0.6f); plateBox.ShadowOffset = new Vector2(10, 10); plateBox.ShadowSize = 1;
        plate.AddThemeStyleboxOverride("panel", plateBox);
        c.AddChild(plate);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        plate.AddChild(col);

        var (name, format) = MapLabel(lobby.Map);
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var frameBox = new KcBox { Top = UiKit.Cream, Bottom = UiKit.Cream, ShadowOffset = new Vector2(8, 8), ShadowColor = UiKit.Ink };
        frameBox.ContentMarginLeft = frameBox.ContentMarginRight = frameBox.ContentMarginTop = frameBox.ContentMarginBottom = 5;
        frame.AddThemeStyleboxOverride("panel", frameBox);
        col.AddChild(frame);
        string path = $"res://assets/ui/cartes/{lobby.Map}.jpg";
        frame.AddChild(new TextureRect
        {
            CustomMinimumSize = new Vector2(490, 270), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
            Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null,
            Material = Duotone,
        });

        var nameRow = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center };
        nameRow.AddThemeConstantOverride("separation", 10);
        col.AddChild(nameRow);
        if (lobby.IsHost) nameRow.AddChild(Arrow("◀", -1));
        var title = UiKit.Poster(UiKit.Text(L.T(name).ToUpperInvariant(), 46, UiKit.Cream, 0, HorizontalAlignment.Center), null, 3);
        title.CustomMinimumSize = new Vector2(lobby.IsHost ? 360 : 480, 0);
        title.VerticalAlignment = VerticalAlignment.Center;
        nameRow.AddChild(title);
        if (lobby.IsHost) nameRow.AddChild(Arrow("▶", 1));
        var fmt = UiKit.Text(L.T(format), 24, new Color(UiKit.Cream, 0.75f), 0, HorizontalAlignment.Center);
        if (UiKit.Heavy is Font hv) fmt.AddThemeFontOverride("font", hv);
        col.AddChild(fmt);
    }

    /// <summary>
    /// Vignette de carte « tirée » en sérigraphie : la luminance passe pour moitié dans un dégradé encre → papier, les couleurs
    /// du rendu 3D restent dessous (la carte se reconnaît, mais elle appartient à l'affiche).
    /// </summary>
    static ShaderMaterial Duotone => duotone ??= MakeDuotone();
    static ShaderMaterial? duotone;
    static ShaderMaterial MakeDuotone()
    {
        var sh = new Shader { Code = @"shader_type canvas_item;
uniform vec4 ink : source_color;
uniform vec4 paper : source_color;
uniform float amount = 0.72;
void fragment() {
    vec4 c = texture(TEXTURE, UV);
    float l = dot(c.rgb, vec3(0.299, 0.587, 0.114));
    vec3 d = mix(ink.rgb, paper.rgb, smoothstep(0.04, 0.92, l));
    COLOR = vec4(mix(c.rgb, d, amount), c.a);
}" };
        var m = new ShaderMaterial { Shader = sh };
        m.SetShaderParameter("ink", UiKit.Navy);
        m.SetShaderParameter("paper", UiKit.Cream);
        return m;
    }

    /// <summary>Flèche de changement de carte (hôte) : petit carré d'encre, plaque papier au focus.</summary>
    Button Arrow(string glyph, int dir)
    {
        var b = UiKit.Button(glyph, Gray, () => CycleMap(dir), 26, new Vector2(56, 52));
        b.SizeFlagsVertical = Control.SizeFlags.ShrinkCenter;
        foreach (var k in new[] { "normal", "hover", "pressed", "hover_pressed", "focus" })
            if (b.GetThemeStylebox(k) is KcBox box) { box.ContentMarginLeft = box.ContentMarginRight = 0; box.AccentWidth = 0; }
        if (UiKit.Heavy is Font hv) b.AddThemeFontOverride("font", hv);
        b.TooltipText = L.T(dir < 0 ? "Carte précédente (Q)" : "Carte suivante (E)");
        return b;
    }

    /// <summary>Hôte : carte suivante ou précédente de la liste (une carte refusée, trop petite ou PC seul, est sautée).</summary>
    void CycleMap(int dir)
    {
        if (!lobby.IsHost) return;
        var maps = OnlineLobby.Maps;
        int i = Mathf.Max(0, maps.ToList().IndexOf(lobby.Map));
        for (int k = 1; k < maps.Count; k++)
        {
            string next = maps[(i + dir * k + maps.Count * k) % maps.Count];
            string before = lobby.Map;
            lobby.SetMap(next);
            if (lobby.Map != before) { Sfx.Play(this, SfxKind.UiHover); return; }
        }
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (lobby.State != LobbyState.InRoom || !lobby.IsHost) return;
        bool prev = e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.Q } || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.LeftShoulder };
        bool next = e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.E } || e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.RightShoulder };
        if (!prev && !next) return;
        GetViewport().SetInputAsHandled();
        CycleMap(prev ? -1 : 1);
    }

    /// <summary>
    /// État de la salle, à place fixe sous la carte, en bandeau d'encre plein (une étiquette, pas un bouton) : revanche ou
    /// lancement avec le décompte (le chiffre est le seul accent lime), sinon ce que la salle attend (« 2/3 PRÊTS · EN
    /// ATTENTE DE RICOCHET »). Une ligne d'aide dessous, à place réservée.
    /// </summary>
    void BuildStatus(Control c)
    {
        string big;
        string? help = null;
        Label? number = null;
        var members = lobby.Members;
        var me = members.FirstOrDefault(m => m.PeerId == lobby.Multiplayer.GetUniqueId());
        if (lobby.RematchIn > 0)
        {
            big = L.T("REVANCHE DANS");
            number = rematchLabel = Number();
            help = lobby.IsHost ? "Mêmes équipes, même carte. « ATTENDRE » arrête le décompte." : "Mêmes équipes, même carte. « PAS PRÊT » arrête le décompte.";
        }
        else if (lobby.QuickStartIn > 0)
        {
            big = L.T("LANCEMENT DANS");
            number = quickNumber = Number();
            help = members.Count < lobby.MaxPlayers ? "Les places libres seront tenues par des bots. D'autres joueurs peuvent encore arriver." : "Salle pleine !";
        }
        else
        {
            var waiting = members.Where(m => !m.Ready).ToList();
            if (waiting.Count == 0) big = L.T(lobby.IsHost ? "TOUT LE MONDE EST PRÊT" : "L'HÔTE VA LANCER LA PARTIE");
            else if (!lobby.IsHost && me != null && !me.Ready) big = L.T("APPUIE SUR « PRÊT ! »");
            else big = waiting.Count == 1
                ? L.F("EN ATTENTE DE {0}", waiting[0].Name.ToUpperInvariant())
                : L.F("EN ATTENTE DE {0} JOUEURS", waiting.Count);
        }
        if (help == null && lobby.IsHost && members.Count == 1)
            help = SteamBridge.Available ? "Invite tes amis avec Steam ou donne-leur le code."
                : lobby.ListedOnLan ? "Tes amis te trouvent avec « Partie rapide » sur le même réseau, ou entrent le code."
                : "Donne le code à tes amis. Par Internet, ouvre le port dans Options › Partie.";
        help ??= lobby.IsHost ? "Toi seul choisis la carte et lances la partie." : "L'hôte choisit la carte et lance la partie.";
        if (lobby.Status.StartsWith("Trop") || lobby.Status.StartsWith("Carte réservée")) help = OnlineLobby.Display(lobby.Status);

        // Sous la carte, même largeur (colonne centrale) ; un nom long élargit le bandeau des deux côtés.
        var statusHolder = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center, Position = new Vector2(0, 700), Size = new Vector2(W, 0) };
        c.AddChild(statusHolder);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, CustomMinimumSize = new Vector2(540, 70) };
        var box = UiKit.Box(UiKit.Navy, 0, 0, 0, null, 0);
        box.ContentMarginLeft = box.ContentMarginRight = 18; box.ContentMarginTop = 4; box.ContentMarginBottom = 12;
        box.ShadowColor = new Color(UiKit.Ink, 0.6f); box.ShadowOffset = new Vector2(8, 8); box.ShadowSize = 1;
        panel.AddThemeStyleboxOverride("panel", box);
        statusHolder.AddChild(panel);
        var stack = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        stack.AddThemeConstantOverride("separation", 0);
        panel.AddChild(stack);
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center, CustomMinimumSize = new Vector2(0, 60) };
        row.AddThemeConstantOverride("separation", 22);
        stack.AddChild(row);
        var head = UiKit.Raw(UiKit.Poster(UiKit.Text(big, 34, UiKit.Cream, 0), null, 2));
        head.VerticalAlignment = VerticalAlignment.Center;
        row.AddChild(head);
        if (lobby.QuickStartIn > 0 && lobby.RematchIn <= 0) quickLabel = head;
        if (number != null) row.AddChild(number);
        if (help == null) return;
        // Aide dans le même cartouche, sous le statut (Barlow, deux lignes au plus).
        var helpText = UiKit.Raw(UiKit.Text(L.T(help), 20, new Color(UiKit.Cream, 0.92f), 0, HorizontalAlignment.Center));
        helpText.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        helpText.CustomMinimumSize = new Vector2(504, 0);
        stack.AddChild(helpText);
    }

    static Label Number()
    {
        var n = UiKit.Poster(UiKit.Text("", 50, UiKit.Yellow, 0, HorizontalAlignment.Center));
        n.CustomMinimumSize = new Vector2(70, 0);
        n.VerticalAlignment = VerticalAlignment.Center;
        return n;
    }

    /// <summary>Actions, à place fixe : changer d'équipe (languette de l'équipe d'arrivée), prêt / lancer, attendre, quitter.</summary>
    void BuildActions(Control c, LobbyMember? me)
    {
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Center, Position = new Vector2(0, 872), Size = new Vector2(W, 64) };
        row.AddThemeConstantOverride("separation", 18);
        c.AddChild(row);
        if (me != null)
        {
            row.AddChild(MakeButton("CHANGER D'ÉQUIPE", me.Team == 0 ? Blue : Orange, () => lobby.SetTeam(1 - me.Team), 300));
            if (!lobby.IsHost) row.AddChild(MakeButton(me.Ready ? "PAS PRÊT" : "PRÊT !", me.Ready ? Gray : Orange, () => lobby.SetReady(!me.Ready), 260, primary: !me.Ready));
        }
        if (lobby.IsHost)
        {
            var start = MakeButton("LANCER LA PARTIE", Orange, lobby.StartMatch, 320, primary: true);
            start.Disabled = !lobby.CanStart;
            row.AddChild(start);
            if (lobby.RematchIn > 0) row.AddChild(MakeButton("ATTENDRE", Gray, lobby.CancelRematch, 220));
        }
        row.AddChild(MakeButton("QUITTER LA SALLE", Gray, BackToMenu, 300));
    }

    public override void _Input(InputEvent e)
    {
        // Échap / B : quitter la salle (comme l'aide de bas d'écran l'annonce).
        if (lobby.State == LobbyState.InRoom && e.IsActionPressed("ui_cancel"))
        {
            GetViewport().SetInputAsHandled();
            Sfx.Play(this, SfxKind.UiBack);
            BackToMenu();
        }
    }

    // Manette (Steam Deck) : l'écran est reconstruit à chaque changement de la salle ; le focus revient sur le même bouton,
    // sinon sur l'action principale, pour que la croix et A marchent toujours.
    static readonly string[] FocusOrder = { "PRÊT !", "PAS PRÊT", "LANCER LA PARTIE", "ATTENDRE", "ANNULER", "RETOUR AU MENU", "CHANGER D'ÉQUIPE", "QUITTER LA SALLE" };

    void RestoreFocus(string previous)
    {
        var buttons = new List<Button>();
        Collect(content, buttons);
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

    /// <summary>Nom de la carte et son format (traduits à l'affichage).</summary>
    static (string name, string format) MapLabel(string map) => map switch
    {
        "marche_suspendu" => ("Toits du Marché", "ARÈNE · 3 CONTRE 3"),
        "piscine" => ("La Piscine", "ARÈNE · 3 CONTRE 3"),
        "depot" => ("Le Dépôt", "ARÈNE · 3 CONTRE 3"),
        "croix_neon" => ("Croix Néon", "JEU LIBRE"),
        "fosse_croisee" => ("Fosse croisée", "JEU LIBRE"),
        "tour_4v4" => ("La Tour", "JEU LIBRE · 4 CONTRE 4"),
        "neon_court" => ("Néon Court", "ZONES · VR"),
        "la_fosse" => ("La Fosse", "ZONES · VR"),
        _ => (map, ""),
    };

    static Label Place(Label l, float x, float y, float w, float h)
    {
        l.Position = new Vector2(x, y);
        l.Size = new Vector2(w, h);
        l.VerticalAlignment = VerticalAlignment.Center;
        return l;
    }

    static Label Wrapped(Label l, float width)
    {
        l.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        l.CustomMinimumSize = new Vector2(width, 0);
        l.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        return l;
    }

    /// <summary>
    /// Bouton de la salle. Repos : encre pleine à liseré papier et languette de couleur (gris : aucune) ; action principale :
    /// papier permanent, texte encre. Focus et survol : même aplat, liseré lime épais et ombre plus longue (la languette garde
    /// sa couleur, on voit à la fois quel bouton est sélectionné et lequel est le principal). Désactivé : encre à plat, sans
    /// liseré ni ombre, texte papier atténué (il s'enfonce dans la page).
    /// </summary>
    static Button MakeButton(string text, Color color, System.Action onPress, int width = 420, int height = 60, int font = 26, bool primary = false)
    {
        var b = UiKit.Button(text, color, onPress, font, new Vector2(width, height));
        b.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        if (b.GetThemeStylebox("normal") is not KcBox baseBox) return b;
        var idle = baseBox.Copy();
        idle.Top = idle.Bottom = primary ? UiKit.Cream : UiKit.Navy;
        idle.BorderWidth = primary ? 0 : 2;
        idle.BorderColor = new Color(UiKit.Cream, 0.85f);
        idle.AccentColor = primary || color == Gray ? new Color(0, 0, 0, 0) : color;   // languette : seulement l'équipe d'arrivée
        idle.AccentWidth = 10;
        idle.ShadowOffset = new Vector2(6, 6); idle.ShadowColor = new Color(UiKit.Ink, 0.7f);
        var lit = idle.Copy();
        lit.BorderWidth = 4; lit.BorderColor = UiKit.Yellow; lit.ShadowOffset = new Vector2(10, 10);
        var down = lit.Copy();
        down.ShadowOffset = new Vector2(3, 3);
        var off = idle.Copy();
        off.Top = off.Bottom = UiKit.Navy; off.BorderWidth = 0; off.AccentColor = new Color(0, 0, 0, 0); off.ShadowColor = new Color(0, 0, 0, 0);
        b.AddThemeStyleboxOverride("normal", idle);
        b.AddThemeStyleboxOverride("hover", lit);
        b.AddThemeStyleboxOverride("focus", lit);
        b.AddThemeStyleboxOverride("pressed", down);
        b.AddThemeStyleboxOverride("hover_pressed", down);
        b.AddThemeStyleboxOverride("disabled", off);
        var rest = primary ? UiKit.Navy : color == Gray ? UiKit.Cream : Colors.White;
        foreach (var k in new[] { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" }) b.AddThemeColorOverride(k, rest);
        b.AddThemeColorOverride("font_disabled_color", new Color(UiKit.Cream, 0.38f));
        return b;
    }
}
