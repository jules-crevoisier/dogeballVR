using System;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Net;

namespace DogeballVR.UI;

/// <summary>
/// Menu principal du mode PC, DA « Affiche », par-dessus le décor 3D (une arène vivante) : colonne d'encre droite à gauche
/// (un tiers de l'écran, les personnages occupent la droite), logo en double tirage décalé, items en grandes capitales Anton
/// surlignés par une bande de papier à languette vert acide qui glisse de l'un à l'autre ; pages Jouer en ligne, Options et
/// Crédits. À la toute première partie, on propose le tutoriel.
/// </summary>
public partial class MainMenu : CanvasLayer
{
    public event Action<NetMode, string>? Chosen;
    public event Action? TutorialChosen;
    /// <summary>Partie solo contre les bots sur la carte choisie (nom du fichier de maps/).</summary>
    public event Action<string>? SoloChosen;

    /// <summary>Cartes proposées en solo, dans l'ordre du sélecteur.</summary>
    static readonly (string Map, string Name, string Info)[] SoloMaps =
    {
        ("marche_suspendu", "TOITS DU MARCHÉ", "3 contre 3. Deux toits au-dessus d'une rue : tenir les ponts, un tacle et c'est la chute."),
        ("piscine", "LA PISCINE", "3 contre 3. Un bassin vide et un plongeoir : le bas contre le haut, les bords ouverts sur le vide."),
        ("depot", "LE DÉPÔT", "3 contre 3. Deux fosses, deux trams, trois couloirs : le toit du tram domine, à un tacle de la chute."),
        ("croix_neon", "CROIX NÉON", "2 contre 2. Quatre plateaux autour d'un moyeu, le rotor balaie le centre."),
        ("fosse_croisee", "FOSSE CROISÉE", "2 contre 2. Le moulinet passe au-dessus d'une fosse : un tacle peut y pousser l'adversaire."),
        ("tour_4v4", "LA TOUR", "4 contre 4. Terrasses, fosse et rotor : la plus grande carte."),
        ("la_fosse", "LA FOSSE", "2 contre 2. Zones face à face : chacun garde la sienne, E pour esquiver."),
        ("neon_court", "NÉON COURT", "2 contre 2. Terrain court en zones : chacun garde la sienne, E pour esquiver."),
    };

    Control root = null!;
    VBoxContainer column = null!;
    Control? overlay;
    ColorRect fade = null!;
    MenuBand band = null!;
    SelectBar bar = null!;
    Control? barOn;   // item sous la barre jaune (null : barre cachée)
    Tween? barTween;
    Control escHint = null!;
    Control hintBar = null!;   // aides clavier : toujours au-dessus des fenêtres et de leur voile
    Action? back;   // page précédente (Échap) : null sur le menu racine
    bool leaving, compact;

    const string ItemMeta = "menu_item";
    const float BarLead = 46f;   // la barre commence un peu à gauche du texte
    static readonly string[] FontColors = { "font_color", "font_hover_color", "font_pressed_color", "font_focus_color", "font_hover_pressed_color" };

    public override void _Ready()
    {
        Layer = 20;
        root = new Control { Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        // Écran bas (interface agrandie : 1080 / 1,3 ≈ 830 px utiles) : logo réduit, colonne remontée, le pied de page reste dégagé.
        compact = root.GetViewportRect().Size.Y < 960f;
        float top = compact ? -44f : 0f;

        // Colonne d'encre à gauche : elle porte le logo et les items.
        band = new MenuBand { MouseFilter = Control.MouseFilterEnum.Ignore };
        band.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        band.Resized += band.QueueRedraw;
        root.AddChild(band);
        UiKit.SlideIn(band, 0f, -200f);

        // Logo d'affiche : DOGEBALL en capitales Anton, trois tirages décalés (bleu, orange, papier) comme une sérigraphie mal
        // calée ; dessous, la devise en vert acide sur un filet.
        var logoScale = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Scale = Vector2.One * (compact ? 0.8f : 1f) };   // le logo « tape » (Punch remet son échelle à 1) : on réduit son parent
        root.AddChild(logoScale);
        var logoBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(64, compact ? 18 : 34), Size = new Vector2(600, 240) };
        logoScale.AddChild(logoBox);
        void LogoLayer(Color color, Vector2 at)
        {
            var l = UiKit.Text("DOGEBALL", 150, color, 0);
            if (UiKit.Display is Font d) l.AddThemeFontOverride("font", d);
            l.Position = at;
            logoBox.AddChild(l);
        }
        LogoLayer(UiKit.Blue, new Vector2(-6, 7));
        LogoLayer(UiKit.Orange, new Vector2(7, 7));
        LogoLayer(UiKit.Cream, Vector2.Zero);
        var rule = new ColorRect { Color = UiKit.Yellow, Position = new Vector2(4, 214), Size = new Vector2(548, 5), MouseFilter = Control.MouseFilterEnum.Ignore };
        logoBox.AddChild(rule);
        var tag = UiKit.Text("ESQUIVE  ·  ATTRAPE  ·  RENVOIE", 26, UiKit.Yellow, 0);
        if (UiKit.Heavy is Font hv) tag.AddThemeFontOverride("font", hv);
        tag.Position = new Vector2(4, 222);
        logoBox.AddChild(tag);
        UiKit.Punch(logoBox, 1.15f, 0.35);

        // Aides clavier en bas à gauche, en touches dessinées (« [Entrée] Valider  [Échap] Retour »).
        var hints = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        hints.AddThemeConstantOverride("separation", 36);
        hints.AddChild(UiKit.KeyHint("Entrée", "Valider", 24));
        escHint = UiKit.KeyHint(InputSetup.KeyName("pause"), "Retour", 24);   // sous-menus seulement : le menu racine n'a pas de retour
        escHint.Visible = false;
        hints.AddChild(escHint);
        hints.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        hints.GrowVertical = Control.GrowDirection.Begin;
        hints.OffsetLeft = 72; hints.OffsetTop = -36; hints.OffsetRight = 900; hints.OffsetBottom = -36;
        root.AddChild(hints);
        hintBar = hints;

        // Barre jaune de sélection, sous les items (elle suit le focus dans _Process).
        bar = new SelectBar { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        root.AddChild(bar);

        column = new VBoxContainer { Position = new Vector2(96, 318 + top) };
        column.AddThemeConstantOverride("separation", compact ? 10 : 14);
        root.AddChild(column);

        string ver = ProjectSettings.GetSetting("application/config/version", "").AsString();
        var version = UiKit.Text(L.F("ACCÈS ANTICIPÉ · V{0}", ver == "" ? "0.1" : ver), 18, new Color(UiKit.Cream, 0.85f), 5, HorizontalAlignment.Right);
        version.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        version.OffsetLeft = -700; version.OffsetTop = -44; version.OffsetRight = -24; version.OffsetBottom = -16;
        root.AddChild(version);

        fade = new ColorRect { Color = new Color(0, 0, 0, 1), MouseFilter = Control.MouseFilterEnum.Ignore };
        fade.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(fade);
        fade.CreateTween().TweenProperty(fade, "color:a", 0.0, 0.6);

        Input.MouseMode = Input.MouseModeEnum.Visible;
        Music.PlayMenu();
        ShowMain();
        if (Tools.DevFlags.Has("--profile-test")) ShowProfile();   // captures
        if (Tools.DevFlags.Has("--options-test")) OpenOverlay(new OptionsMenu { FirstTab = 1 });
        // Captures d'écran : --menu-page=online | lan | credits | first | maps ouvre la page au bout d'une seconde.
        foreach (var arg in Tools.DevFlags.Args)
            if (arg.StartsWith("--menu-page="))
            {
                string page = arg["--menu-page=".Length..];
                GetTree().CreateTimer(1.0).Timeout += () =>
                {
                    if (page == "online") ShowOnline();
                    else if (page == "lan") ShowLan();
                    else if (page == "credits") ShowCredits();
                    else if (page == "first") { GameSettings.TutorialDone = false; PlayBots(); }
                    else if (page == "maps") ShowMapPicker();
                };
            }
    }

    void ShowProfile() => OpenOverlay(new ProfileScreen { Back = () => { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); ShowMain(); } });

    public override void _Process(double delta)
    {
        escHint.Visible = back != null || overlay != null;

        // La barre jaune suit l'item qui a le focus (clavier ou survol) et glisse de l'un à l'autre.
        var f = column.Visible ? root.GetViewport().GuiGetFocusOwner() : null;
        Control? on = f != null && f.HasMeta(ItemMeta) && column.IsAncestorOf(f) ? f : null;
        if (on == null) { bar.Visible = false; barOn = null; return; }
        var want = BarRect(on);
        if (on != barOn)
        {
            barTween?.Kill();
            barTween = bar.CreateTween().SetParallel().SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
            if (barOn == null)
            {
                // Première apparition (nouvelle page) : la barre se déroule depuis la gauche.
                bar.Position = want.Position;
                bar.Size = new Vector2(0, want.Size.Y);
                barTween.TweenProperty(bar, "size", want.Size, 0.18);
            }
            else
            {
                barTween.TweenProperty(bar, "position", want.Position, 0.12);
                barTween.TweenProperty(bar, "size", want.Size, 0.12);
            }
            barOn = on;
            bar.Visible = true;
        }
        else if (barTween == null || !barTween.IsRunning())
        {
            bar.Position = want.Position;   // la colonne a pu bouger (mise en page)
            bar.Size = want.Size;
        }
        bar.QueueRedraw();
    }

    /// <summary>Rectangle de la bande de sélection pour un item : du bord gauche de l'écran jusqu'au filet de la colonne.</summary>
    Rect2 BarRect(Control item)
    {
        float y = column.Position.Y + item.Position.Y;
        float x0 = 0f;
        float x1 = band.EdgeX(y + item.Size.Y / 2f) + 30f;
        return new Rect2(x0, y + 4, x1 - x0, item.Size.Y - 8);
    }

    void Fill(params Control[] items)
    {
        foreach (var c in column.GetChildren()) c.QueueFree();
        // Les items du menu racine se touchent (la barre fait l'espacement) ; les pages à boutons gardent leur écart.
        column.AddThemeConstantOverride("separation", Array.Exists(items, c => c.HasMeta(ItemMeta)) ? 0 : compact ? 10 : 14);
        int i = 0;
        foreach (var c in items)
        {
            // Items en capitales d'affiche (Anton).
            if (UiKit.Italic is Font it && c is Button or Label) c.AddThemeFontOverride("font", it);
            column.AddChild(c);
            UiKit.SlideIn(c, 0.05f * i++);
        }
        // Focus sur le premier bouton (les pages En ligne / Réseau local commencent par un titre) : clavier utilisable d'emblée.
        if (Array.Find(items, c => c is Button) is Button b) b.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>
    /// Item du menu racine : texte XXL penché, sans boîte. Le focus (clavier, ou survol de la souris qui le prend) le passe
    /// en marine sur la barre jaune et le pousse un peu vers la droite. Les items secondaires sont plus petits et grisés.
    /// </summary>
    Button Item(string text, Action onPress, bool main = true, Color? restColor = null)
    {
        var pad = new StyleBoxEmpty { ContentMarginLeft = 0, ContentMarginRight = 8, ContentMarginTop = 0, ContentMarginBottom = 4 };
        var b = new Button { Text = text, Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(560, main ? 80 : 50) };
        b.SetMeta(ItemMeta, true);
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) b.AddThemeStyleboxOverride(st, pad);
        b.AddThemeFontSizeOverride("font_size", main ? 64 : 34);
        if (UiKit.Italic is Font it) b.AddThemeFontOverride("font", it);
        b.AddThemeColorOverride("font_outline_color", UiKit.Navy);
        var rest = restColor ?? (main ? UiKit.Cream : UiKit.Lavender);   // gris chaud : 3e niveau de hiérarchie
        int restOutline = 0;
        void Paint(Color c, int outline)
        {
            foreach (var k in FontColors) b.AddThemeColorOverride(k, c);
            b.AddThemeConstantOverride("outline_size", outline);
        }
        Paint(rest, restOutline);
        Tween? slide = null;
        void Shift(float to)
        {
            slide?.Kill();
            slide = b.CreateTween();
            slide.TweenProperty(pad, "content_margin_left", to, 0.12).SetTrans(Tween.TransitionType.Cubic).SetEase(Tween.EaseType.Out);
        }
        b.FocusEntered += () => { if (bar.Visible) Sfx.Play(b, SfxKind.UiHover); Paint(UiKit.Navy, 0); Shift(0f); };   // le texte reste aligné : la plaque papier et la languette lime suffisent
        b.FocusExited += () => { Paint(rest, restOutline); Shift(0f); };
        b.MouseEntered += () => { if (!b.Disabled && !leaving) b.GrabFocus(); };
        b.Pressed += () => { Sfx.Play(b, SfxKind.UiClick); onPress(); };
        return b;
    }

    void ShowMain()
    {
        back = null;
        Fill(
            Item("JOUER", PlayBots),
            Item("ENTRAÎNEMENT", () => Leave(() => TutorialChosen?.Invoke())),
            Item("EN LIGNE", ShowOnline),
            Item("PROFIL", ShowProfile),
            Item("OPTIONS", () => OpenOverlay(new OptionsMenu())),
            UiKit.Spacer(14),
            Item("CRÉDITS", ShowCredits, false),
            Item("QUITTER", () => GetTree().Quit(), false));
    }

    /// <summary>
    /// Actions en ligne (Steam / salle) : branchées par Main sur le module Online quand il est présent.
    /// Sans écouteur, la partie rapide et la salle privée retombent sur l'hébergement réseau local.
    /// </summary>
    public event Action? QuickMatch, CreatePrivate, HostLan;
    public event Action<string>? JoinCode, JoinAddress;

    /// <summary>Clavier virtuel (Steam : Deck, Big Picture) ; branché par Main. Faux s'il n'a pas pu s'ouvrir.</summary>
    public static Func<string, string, int, Rect2I, Action<string?>, bool>? TextInput { get; set; }

    /// <summary>
    /// Champ de texte jouable à la manette : A sur le champ (ou un bouton qui le demande vide) ouvre le clavier virtuel de Steam.
    /// Sur Deck le clavier flottant tape directement dans le champ ; ailleurs le texte revient à la fermeture, puis le focus passe à <paramref name="next"/>.
    /// </summary>
    static void PadKeyboard(LineEdit field, string description, Func<Control?> next)
    {
        field.GuiInput += e =>
        {
            if (e is InputEventJoypadButton { Pressed: true, ButtonIndex: JoyButton.A }) { OpenKeyboard(field, description, next); field.AcceptEvent(); }
        };
    }

    static bool OpenKeyboard(LineEdit field, string description, Func<Control?> next)
    {
        if (TextInput == null) return false;
        var xf = field.GetViewport().GetFinalTransform() * field.GetGlobalTransformWithCanvas();
        var r = xf * new Rect2(Vector2.Zero, field.Size);
        var rect = new Rect2I((Vector2I)r.Position.Round(), (Vector2I)r.Size.Round());
        return TextInput(description, field.Text, field.MaxLength > 0 ? field.MaxLength : 64, rect, text =>
        {
            if (!IsInstanceValid(field)) return;
            if (text != null) { field.Text = text; field.CaretColumn = text.Length; }
            next()?.GrabFocus();
        });
    }

    void ShowOnline()
    {
        back = ShowMain;
        var code = new LineEdit { PlaceholderText = "Code de la salle", CustomMinimumSize = new Vector2(440, 58), Alignment = HorizontalAlignment.Center, MaxLength = 12 };
        Button? join = null;
        PadKeyboard(code, "Code de la salle", () => join);
        // Mêmes items que le menu racine (barre jaune qui glisse), en taille secondaire.
        Fill(
            UiKit.Title("EN LIGNE", 56, HorizontalAlignment.Left),
            Item("PARTIE RAPIDE", () => FirstMatch(() => Online(QuickMatch, () => Chosen?.Invoke(NetMode.Host, ""))), false, Colors.White),
            Item("CRÉER UNE SALLE PRIVÉE", () => Online(CreatePrivate, () => Chosen?.Invoke(NetMode.Host, "")), false, Colors.White),
            code,
            join = Item("REJOINDRE AVEC LE CODE", () =>
            {
                var c = code.Text.Trim().ToUpperInvariant();
                if (c == "") { code.GrabFocus(); if (InputSetup.UsingPad) OpenKeyboard(code, "Code de la salle", () => join); return; }
                if (JoinCode != null) Leave(() => JoinCode(c));
            }, false, Colors.White),
            Item("RÉSEAU LOCAL", ShowLan, false, Colors.White),
            UiKit.Spacer(10),
            Item("RETOUR", () => { Sfx.Play(this, SfxKind.UiBack); ShowMain(); }, false));
    }

    void ShowLan()
    {
        back = ShowOnline;
        var address = new LineEdit { Text = "127.0.0.1", PlaceholderText = "Adresse IP de l'hôte", CustomMinimumSize = new Vector2(440, 58), Alignment = HorizontalAlignment.Center };
        Button? join = null;
        PadKeyboard(address, "Adresse IP de l'hôte", () => join);
        Fill(
            UiKit.Title("RÉSEAU LOCAL", 56, HorizontalAlignment.Left),
            Item("HÉBERGER", () => Online(HostLan, () => Chosen?.Invoke(NetMode.Host, "")), false, Colors.White),
            address,
            join = Item("REJOINDRE", () =>
            {
                var ip = address.Text.Trim();
                if (JoinAddress != null) Leave(() => JoinAddress(ip)); else Leave(() => Chosen?.Invoke(NetMode.Client, ip));
            }, false, Colors.White),
            UiKit.Spacer(10),
            Item("RETOUR", () => { Sfx.Play(this, SfxKind.UiBack); ShowOnline(); }, false));
    }

    void Online(Action? handler, Action fallback) => Leave(handler ?? fallback);

    void PlayBots() => FirstMatch(ShowMapPicker);   // solo : choix de la carte (la première fois, après la proposition d'entraînement)

    /// <summary>Contre les bots ou en partie rapide : la toute première fois, on propose l'entraînement avant <paramref name="play"/>.</summary>
    void FirstMatch(Action play)
    {
        if (GameSettings.TutorialDone) { play(); return; }
        // Première partie : on propose le tutoriel (2 minutes), sans l'imposer.
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        card.AddChild(col);
        col.AddChild(UiKit.Title("PREMIÈRE PARTIE ?", 52));
        col.AddChild(UiKit.Text("L'entraînement t'apprend à lancer, attraper et esquiver en deux minutes.", 24, Colors.White, 6, HorizontalAlignment.Center));
        var row = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center };
        row.AddThemeConstantOverride("separation", 16);
        col.AddChild(row);
        var yes = UiKit.Button("ENTRAÎNEMENT", UiKit.Orange, () => Leave(() => TutorialChosen?.Invoke()), 28, new Vector2(320, 64));
        row.AddChild(yes);
        row.AddChild(UiKit.Button("JOUER DIRECT", UiKit.Blue, () =>
        {
            GameSettings.TutorialDone = true;
            GameSettings.Save();
            play();
        }, 28, new Vector2(320, 64)));
        OpenOverlay(Centered(card));
        yes.CallDeferred(Control.MethodName.GrabFocus);
    }

    /// <summary>
    /// Solo : choix de la carte. Liste à gauche ; à droite, l'aperçu de la carte sélectionnée (cadre papier, ombre dure),
    /// son format et sa description. Aperçus : assets/ui/cartes/&lt;carte&gt;.jpg (vue de la carte en hauteur).
    /// </summary>
    void ShowMapPicker()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 14);
        card.AddChild(col);
        col.AddChild(UiKit.Title("CHOISIS LA CARTE", 52, HorizontalAlignment.Left));
        var split = new HBoxContainer();
        split.AddThemeConstantOverride("separation", 34);
        col.AddChild(split);
        var list = new VBoxContainer();
        list.AddThemeConstantOverride("separation", 6);
        split.AddChild(list);

        var right = new VBoxContainer();
        right.AddThemeConstantOverride("separation", 12);
        split.AddChild(right);
        var frame = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var frameBox = new KcBox { Top = UiKit.Cream, Bottom = UiKit.Cream, ShadowOffset = new Vector2(10, 10), ShadowColor = UiKit.Ink };
        frameBox.ContentMarginLeft = frameBox.ContentMarginRight = frameBox.ContentMarginTop = frameBox.ContentMarginBottom = 6;
        frame.AddThemeStyleboxOverride("panel", frameBox);
        right.AddChild(frame);
        var preview = new TextureRect
        {
            CustomMinimumSize = new Vector2(512, 288), ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize,
            StretchMode = TextureRect.StretchModeEnum.KeepAspectCovered, MouseFilter = Control.MouseFilterEnum.Ignore,
        };
        frame.AddChild(preview);
        var mode = UiKit.Text("", 30, UiKit.Yellow, 0);
        if (UiKit.Heavy is Font hv) mode.AddThemeFontOverride("font", hv);
        right.AddChild(mode);
        var info = UiKit.Text("", 24, UiKit.Cream, 0);
        info.AutowrapMode = TextServer.AutowrapMode.WordSmart;
        info.CustomMinimumSize = new Vector2(524, 64);
        right.AddChild(info);

        Button? first = null;
        foreach (var (map, name, text) in SoloMaps)
        {
            var b = UiKit.ListButton(name, () => Leave(() => SoloChosen?.Invoke(map)), 34, new Vector2(400, 60));
            // « 3 contre 3. Deux toits… » : le format en accent, la description dessous.
            string tr = L.T(text);   // traduit avant d'être coupé en format + description
            int dot = tr.IndexOf(". ", StringComparison.Ordinal);
            string head = dot > 0 ? tr[..dot].ToUpperInvariant() : "", body = dot > 0 ? tr[(dot + 2)..] : tr;
            string path = $"res://assets/ui/cartes/{map}.jpg";
            b.FocusEntered += () =>
            {
                mode.Text = head;
                info.Text = body;
                preview.Texture = ResourceLoader.Exists(path) ? GD.Load<Texture2D>(path) : null;
            };
            list.AddChild(b);
            first ??= b;
        }
        OpenOverlay(Centered(card));
        first?.CallDeferred(Control.MethodName.GrabFocus);
    }

    void ShowCredits()
    {
        var card = new PanelContainer();
        var col = new VBoxContainer();
        col.AddThemeConstantOverride("separation", 10);
        card.AddChild(col);
        col.AddChild(UiKit.Title("CRÉDITS", 56));
        foreach (var (who, what) in new[]
        {
            ("Conception et direction", "jules"),
            ("Moteur", "Godot Engine 4.3 (MIT)"),
            ("Polices", "Anton (The Anton Project Authors), Barlow (Jeremy Tribby), Lilita One (Juan Montoreano), SIL OFL"),
            ("Modèles 3D", "faits maison avec Blender"),
            ("Sons", "voir assets/audio/CREDITS.md"),
            ("Inspiration", "Knockout City (Velan Studios), hommage"),
        })
        {
            var row = new HBoxContainer();
            row.AddThemeConstantOverride("separation", 24);
            var a = UiKit.Text(who, 24, UiKit.Sky, 6, HorizontalAlignment.Right);
            a.CustomMinimumSize = new Vector2(330, 0);
            row.AddChild(a);
            var b = UiKit.Text(what, 24, Colors.White, 6);
            b.AutowrapMode = TextServer.AutowrapMode.WordSmart;   // la ligne des polices passe sur deux lignes au lieu de sortir de l'écran
            b.CustomMinimumSize = new Vector2(620, 0);
            row.AddChild(b);
            col.AddChild(row);
        }
        var back = UiKit.Button("RETOUR", UiKit.Orange, CloseOverlay, 26, new Vector2(300, 58));
        back.SizeFlagsHorizontal = Control.SizeFlags.ShrinkCenter;
        col.AddChild(UiKit.Spacer(6));
        col.AddChild(back);
        OpenOverlay(Centered(card));
        back.CallDeferred(Control.MethodName.GrabFocus);
    }

    static Control Centered(Control card)
    {
        var holder = new Control();
        holder.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        holder.AddChild(UiKit.Dim(0.5f));
        var center = new CenterContainer();
        center.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        center.OffsetLeft = MenuBand.Width + 30;   // centrée à droite de la colonne d'encre, sans chevaucher son filet
        holder.AddChild(center);
        center.AddChild(card);
        UiKit.Punch(card, 0.85f, 0.22);
        return holder;
    }

    void OpenOverlay(Control c)
    {
        CloseOverlay();
        column.Visible = false;
        overlay = c;
        // Options : leur panneau a son bouton RETOUR et sa ligne d'aide ; l'aide du coin bas gauche toucherait son cadre.
        hintBar.Visible = c is not OptionsMenu;
        if (c is OptionsMenu o) o.Closed += () => { overlay = null; column.Visible = true; hintBar.Visible = true; ShowMain(); };
        root.AddChild(c);
        root.MoveChild(hintBar, -1);
        root.MoveChild(fade, -1);
    }

    void CloseOverlay()
    {
        if (overlay is OptionsMenu) return;   // l'écran d'options se ferme lui-même (et enregistre)
        overlay?.QueueFree();
        overlay = null;
        column.Visible = true;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        // ZQSD / WASD (touches de déplacement, réglables) : mêmes sauts de focus que les flèches. Les flèches sont déjà
        // consommées par l'interface ; on laisse l'écran d'options et les champs de saisie gérer leurs touches.
        if (overlay is not OptionsMenu && !leaving && GetViewport().GuiGetFocusOwner() is not LineEdit)
            foreach (var (move, ui) in new[] { ("move_forward", "ui_up"), ("move_back", "ui_down"), ("move_left", "ui_left"), ("move_right", "ui_right") })
                if (e is InputEventKey && e.IsActionPressed(move, true))
                {
                    Input.ParseInputEvent(new InputEventAction { Action = ui, Pressed = true });
                    Input.ParseInputEvent(new InputEventAction { Action = ui, Pressed = false });
                    GetViewport().SetInputAsHandled();
                    return;
                }
        if (!e.IsActionPressed("pause") && !e.IsActionPressed("ui_cancel")) return;
        if (overlay != null && overlay is not OptionsMenu) { Sfx.Play(this, SfxKind.UiBack); CloseOverlay(); ShowMain(); GetViewport().SetInputAsHandled(); }
        else if (overlay == null && back != null && !leaving) { Sfx.Play(this, SfxKind.UiBack); back(); GetViewport().SetInputAsHandled(); }   // En ligne, Réseau local : page précédente
    }

    /// <summary>Fondu au noir puis lancement (la partie se construit derrière le noir, qui se dissipe ensuite).</summary>
    void Leave(Action then)
    {
        if (leaving) return;
        leaving = true;
        var tw = fade.CreateTween();
        tw.TweenProperty(fade, "color:a", 1.0, 0.35);
        tw.TweenCallback(Callable.From(then));
    }

    /// <summary>Bande de sélection : papier, ombre d'encre franche, languette vert acide à gauche du texte.</summary>
    sealed partial class SelectBar : Control
    {
        public override void _Draw()
        {
            float w = Size.X, h = Size.Y;
            if (w < 2f) return;
            DrawRect(new Rect2(8, 8, w, h), new Color(0, 0, 0, 0.55f));
            DrawRect(new Rect2(0, 0, w, h), UiKit.Cream);
            if (w > BarLead + 40f) DrawRect(new Rect2(BarLead + 16f, h * 0.2f, 14f, h * 0.6f), UiKit.Yellow.Darkened(0.12f));
        }
    }
}
