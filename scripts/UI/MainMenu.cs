using System;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Net;

namespace DogeballVR.UI;

/// <summary>
/// Menu principal du mode PC, par-dessus le décor 3D : grand bandeau diagonal plein à gauche (~40 % de l'écran, les
/// personnages occupent la droite), logo, items en grosses lettres penchées surlignés par une barre jaune inclinée qui
/// glisse de l'un à l'autre, comme le menu de Knockout City ; pages Jouer en ligne, Options et Crédits. À la toute
/// première partie, on propose le tutoriel.
/// </summary>
public partial class MainMenu : CanvasLayer
{
    public event Action<NetMode, string>? Chosen;
    public event Action? TutorialChosen;

    Control root = null!;
    VBoxContainer column = null!;
    Control? overlay;
    ColorRect fade = null!;
    MenuBand band = null!;
    SelectBar bar = null!;
    Control? barOn;   // item sous la barre jaune (null : barre cachée)
    Tween? barTween;
    Control escHint = null!;
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

        // Grand bandeau plein qui part du bord gauche et monte en biais : il porte le logo et les items.
        band = new MenuBand { MouseFilter = Control.MouseFilterEnum.Ignore };
        band.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.FullRect);
        band.Resized += band.QueueRedraw;
        root.AddChild(band);
        UiKit.SlideIn(band, 0f, -260f);

        // Logo penché, en autocollant : ombre marine, contour crème épais, puis le texte orange cerné de marine.
        var logoScale = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Scale = Vector2.One * (compact ? 0.82f : 1f) };   // le logo « tape » (Punch remet son échelle à 1) : on réduit son parent
        root.AddChild(logoScale);
        var logoBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(72, compact ? 30 : 42), Size = new Vector2(700, 210), RotationDegrees = -6f };
        logoScale.AddChild(logoBox);
        void LogoLayer(Color color, int outline, Color outlineColor, Vector2 at)
        {
            var l = UiKit.Text("DOGEBALL", 138, color, outline);
            l.AddThemeColorOverride("font_outline_color", outlineColor);
            if (UiKit.Italic is Font it) l.AddThemeFontOverride("font", it);
            l.Position = at;
            logoBox.AddChild(l);
        }
        LogoLayer(new Color("0a0f22"), 60, new Color("0a0f22"), new Vector2(9, 12));
        LogoLayer(UiKit.Cream, 60, UiKit.Cream, Vector2.Zero);
        LogoLayer(UiKit.Orange, 30, UiKit.Navy, Vector2.Zero);
        var tag = UiKit.Text("ESQUIVE · ATTRAPE · RENVOIE", 30, UiKit.Yellow, 10);
        if (UiKit.Italic is Font ti) tag.AddThemeFontOverride("font", ti);
        tag.Position = new Vector2(22, 170);
        logoBox.AddChild(tag);
        UiKit.Punch(logoBox, 1.3f, 0.45);
        // Le logo respire doucement.
        var bob = logoBox.CreateTween().SetLoops();
        bob.TweenProperty(logoBox, "rotation_degrees", -4.5f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        bob.TweenProperty(logoBox, "rotation_degrees", -6f, 1.8).SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);

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

        // Barre jaune de sélection, sous les items (elle suit le focus dans _Process).
        bar = new SelectBar { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        root.AddChild(bar);

        column = new VBoxContainer { Position = new Vector2(110, 300 + top) };
        column.AddThemeConstantOverride("separation", compact ? 10 : 14);
        root.AddChild(column);

        string ver = ProjectSettings.GetSetting("application/config/version", "").AsString();
        var version = UiKit.Text($"Accès anticipé · v{(ver == "" ? "0.1" : ver)}", 18, new Color(1, 1, 1, 0.8f), 5, HorizontalAlignment.Right);
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
        // Captures d'écran : --menu-page=online | lan | credits | first ouvre la page au bout d'une seconde.
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

    /// <summary>Rectangle de la barre pour un item : du texte jusqu'un peu au-delà du liseré du bandeau, comme dans KC.</summary>
    Rect2 BarRect(Control item)
    {
        float y = column.Position.Y + item.Position.Y;
        float x0 = column.Position.X - BarLead;
        float x1 = band.EdgeX(y + item.Size.Y / 2f) + 44f;
        return new Rect2(x0, y + 3, x1 - x0, item.Size.Y - 6);
    }

    void Fill(params Control[] items)
    {
        foreach (var c in column.GetChildren()) c.QueueFree();
        // Les items du menu racine se touchent (la barre fait l'espacement) ; les pages à boutons gardent leur écart.
        column.AddThemeConstantOverride("separation", Array.Exists(items, c => c.HasMeta(ItemMeta)) ? 0 : compact ? 10 : 14);
        int i = 0;
        foreach (var c in items)
        {
            // Items en lettres penchées et grasses, comme les menus de Knockout City.
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
        var b = new Button { Text = text, Flat = true, Alignment = HorizontalAlignment.Left, FocusMode = Control.FocusModeEnum.All, CustomMinimumSize = new Vector2(560, main ? 84 : 52) };
        b.SetMeta(ItemMeta, true);
        foreach (var st in new[] { "normal", "hover", "pressed", "focus", "hover_pressed", "disabled" }) b.AddThemeStyleboxOverride(st, pad);
        b.AddThemeFontSizeOverride("font_size", main ? 70 : 38);
        if (UiKit.Italic is Font it) b.AddThemeFontOverride("font", it);
        b.AddThemeColorOverride("font_outline_color", UiKit.Navy);
        var rest = restColor ?? (main ? Colors.White : UiKit.Lavender);   // gris lavande : 3e niveau de hiérarchie, comme « SETTINGS » dans KC
        int restOutline = main ? 12 : 8;
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
        b.FocusEntered += () => { if (bar.Visible) Sfx.Play(b, SfxKind.UiHover); Paint(UiKit.Navy, 2); Shift(main ? 36f : 30f); };
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

    void PlayBots() => FirstMatch(() => Leave(() => Chosen?.Invoke(NetMode.Offline, "")));

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
            ("Police", "Lilita One, Juan Montoreano (SIL OFL)"),
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
            row.AddChild(UiKit.Text(what, 24, Colors.White, 6));
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
        if (c is OptionsMenu o) o.Closed += () => { overlay = null; column.Visible = true; ShowMain(); };
        root.AddChild(c);
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

    /// <summary>Barre de sélection jaune inclinée, qui vire à l'or vers le bas, avec l'étoile marine devant le texte (« ✦ JUGAR »).</summary>
    sealed partial class SelectBar : Control
    {
        static readonly Color Gold = new("ffb21f");

        public override void _Draw()
        {
            float w = Size.X, h = Size.Y;
            if (w < 2f) return;
            float s = Mathf.Min(h * 0.3f, w);   // biais : le haut part vers la droite, comme le texte penché
            Vector2[] Quad(Vector2 o) => new[] { o + new Vector2(s, 0), o + new Vector2(w, 0), o + new Vector2(w - s, h), o + new Vector2(0, h) };
            DrawColoredPolygon(Quad(new Vector2(7, 7)), new Color(0.02f, 0.03f, 0.1f, 0.55f));
            DrawPolygon(Quad(Vector2.Zero), new[] { UiKit.Yellow, UiKit.Yellow, Gold, Gold });
            if (w < BarLead + 24f) return;
            var c = new Vector2(BarLead + 10f, h / 2f);
            float r = Mathf.Min(h * 0.24f, 15f), k = r * 0.3f;
            DrawColoredPolygon(new[] { c + new Vector2(0, -r), c + new Vector2(k, -k), c + new Vector2(r, 0), c + new Vector2(k, k), c + new Vector2(0, r), c + new Vector2(-k, k), c + new Vector2(-r, 0), c + new Vector2(-k, -k) }, UiKit.Navy);
        }
    }
}
