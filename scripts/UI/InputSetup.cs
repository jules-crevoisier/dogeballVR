using System.Collections.Generic;
using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Actions du clavier-souris, déclarées dans l'InputMap au démarrage. Les touches sont des touches physiques :
/// la rangée « WASD » d'un clavier QWERTY est « ZQSD » sur un AZERTY, sans rien régler. Chaque action a une touche
/// principale réassignable (menu Options) et, parfois, une touche secondaire fixe (flèches, X pour la passe…).
/// À la manette, les boutons et gâchettes des actions sont réassignables aussi ; les sticks et Menu (pause) restent fixes.
/// </summary>
public static class InputSetup
{
    public sealed record ActionDef(string Name, string Label, InputEvent Primary, InputEvent? Secondary = null, InputEvent? Pad = null);

    static InputEventKey K(Key physical) => new() { PhysicalKeycode = physical };
    static InputEventMouseButton M(MouseButton b) => new() { ButtonIndex = b };
    static InputEventJoypadButton J(JoyButton b) => new() { ButtonIndex = b, Device = -1 };
    static InputEventJoypadMotion Ax(JoyAxis a, float v) => new() { Axis = a, AxisValue = v, Device = -1 };

    /// <summary>Ordre d'affichage du menu Touches.</summary>
    public static readonly ActionDef[] Actions =
    {
        new("move_forward", "Avancer", K(Key.W), K(Key.Up), Ax(JoyAxis.LeftY, -1f)),
        new("move_back", "Reculer", K(Key.S), K(Key.Down), Ax(JoyAxis.LeftY, 1f)),
        new("move_left", "Gauche", K(Key.A), K(Key.Left), Ax(JoyAxis.LeftX, -1f)),
        new("move_right", "Droite", K(Key.D), K(Key.Right), Ax(JoyAxis.LeftX, 1f)),
        new("jump", "Sauter", K(Key.Space), null, J(JoyButton.A)),
        new("dash", "Esquive", K(Key.Shift), null, J(JoyButton.B)),
        new("crouch", "S'accroupir", K(Key.Ctrl), K(Key.C), J(JoyButton.LeftStick)),
        new("fire", "Lancer (maintenir = charger)", M(MouseButton.Left), null, Ax(JoyAxis.TriggerRight, 1f)),
        new("catch", "Attraper", M(MouseButton.Right), null, Ax(JoyAxis.TriggerLeft, 1f)),
        new("pass", "Passe", M(MouseButton.Middle), K(Key.X), J(JoyButton.X)),
        new("lob", "Tir en cloche", K(Key.R), null, J(JoyButton.Y)),
        new("feint", "Feinte", K(Key.F), null, J(JoyButton.RightShoulder)),
        new("curve", "Effet (maintenir + gauche/droite)", K(Key.E), null, J(JoyButton.LeftShoulder)),
        new("scoreboard", "Tableau des scores", K(Key.Tab), null, J(JoyButton.Back)),
        new("pause", "Pause", K(Key.Escape), null, J(JoyButton.Start)),
        new("help", "Aide des touches", K(Key.F1)),
    };

    /// <summary>Visée à la manette (stick droit), non réassignable.</summary>
    static readonly (string name, InputEvent e)[] PadLook =
    {
        ("look_left", Ax(JoyAxis.RightX, -1f)), ("look_right", Ax(JoyAxis.RightX, 1f)),
        ("look_up", Ax(JoyAxis.RightY, -1f)), ("look_down", Ax(JoyAxis.RightY, 1f)),
    };

    static readonly Dictionary<string, InputEvent> primary = new();
    static readonly Dictionary<string, InputEvent> pad = new();
    static bool registered;

    /// <summary>Le bouton de manette de cette action peut-il être changé (boutons et gâchettes ; pas les sticks ni Menu) ?</summary>
    public static bool PadRemappable(ActionDef a) => a.Name != "pause"
        && a.Pad is InputEventJoypadButton or InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight };

    public static void Register()
    {
        if (registered) return;
        registered = true;
        foreach (var a in Actions)
        {
            if (!InputMap.HasAction(a.Name)) InputMap.AddAction(a.Name, 0.2f);
            if (!primary.ContainsKey(a.Name)) primary[a.Name] = a.Primary;
            if (a.Pad != null && !pad.ContainsKey(a.Name)) pad[a.Name] = a.Pad;
            Rebuild(a);
        }
        foreach (var (name, e) in PadLook)
        {
            if (!InputMap.HasAction(name)) InputMap.AddAction(name, 0.15f);
            InputMap.ActionEraseEvents(name);
            InputMap.ActionAddEvent(name, e);
        }
        // Menus à la manette. Godot 4.3 ne relie que la croix aux actions ui_* : A ne validait rien et B ne revenait
        // pas en arrière. On ajoute A (valider), B (retour) et le stick gauche (c'est lui qu'on pousse d'abord).
        AddUi("ui_accept", J(JoyButton.A));
        AddUi("ui_cancel", J(JoyButton.B));
        AddUi("ui_up", Ax(JoyAxis.LeftY, -1f));
        AddUi("ui_down", Ax(JoyAxis.LeftY, 1f));
        AddUi("ui_left", Ax(JoyAxis.LeftX, -1f));
        AddUi("ui_right", Ax(JoyAxis.LeftX, 1f));
    }

    static void AddUi(string ui, InputEvent e)
    {
        foreach (var ev in InputMap.ActionGetEvents(ui))
            if (SameInput(ev, e)) return;
        InputMap.ActionAddEvent(ui, e);
    }

    static void Rebuild(ActionDef a)
    {
        InputMap.ActionEraseEvents(a.Name);
        InputMap.ActionAddEvent(a.Name, primary[a.Name]);
        if (a.Secondary != null && !SameInput(a.Secondary, primary[a.Name])) InputMap.ActionAddEvent(a.Name, a.Secondary);
        if (pad.TryGetValue(a.Name, out var p)) InputMap.ActionAddEvent(a.Name, p);
    }

    public static InputEvent? PadOf(string action) { Register(); return pad.TryGetValue(action, out var p) ? p : null; }

    /// <summary>Nouveau bouton ou gâchette de manette ; l'action qui l'avait prend l'ancien (échange, pas de doublon).
    /// Faux si l'appui ne convient pas (stick, action fixe).</summary>
    public static bool RebindPad(string action, InputEvent e)
    {
        Register();
        var def = Find(action);
        if (!PadRemappable(def)) return false;
        InputEvent? clean = e switch
        {
            InputEventJoypadButton b => J(b.ButtonIndex),
            InputEventJoypadMotion { Axis: JoyAxis.TriggerLeft or JoyAxis.TriggerRight } m when m.AxisValue > 0.5f => Ax(m.Axis, 1f),
            _ => null,
        };
        if (clean == null || clean is InputEventJoypadButton { ButtonIndex: JoyButton.Start }) return false;
        var old = pad[action];
        foreach (var a in Actions)
            if (a.Name != action && pad.TryGetValue(a.Name, out var other) && SameInput(other, clean))
            {
                if (!PadRemappable(a)) return false;   // Menu reste à la pause
                pad[a.Name] = old;
                Rebuild(a);
            }
        pad[action] = clean;
        Rebuild(def);
        return true;
    }

    public static InputEvent PrimaryOf(string action) => primary.TryGetValue(action, out var e) ? e : Find(action).Primary;
    static ActionDef Find(string action) => System.Array.Find(Actions, a => a.Name == action)!;

    /// <summary>Nouvelle touche principale ; si une autre action l'avait, elle reprend sa touche par défaut (pas de doublon silencieux).</summary>
    public static void Rebind(string action, InputEvent e)
    {
        Register();
        var clean = Normalize(e);
        foreach (var a in Actions)
            if (a.Name != action && SameInput(primary[a.Name], clean)) { primary[a.Name] = a.Primary; Rebuild(a); }
        primary[action] = clean;
        Rebuild(Find(action));
    }

    public static void ResetAll()
    {
        foreach (var a in Actions) { primary[a.Name] = a.Primary; if (a.Pad != null) pad[a.Name] = a.Pad; Rebuild(a); }
    }

    static InputEvent Normalize(InputEvent e) => e switch
    {
        InputEventKey k => new InputEventKey { PhysicalKeycode = k.PhysicalKeycode != Key.None ? k.PhysicalKeycode : k.Keycode },
        InputEventMouseButton m => new InputEventMouseButton { ButtonIndex = m.ButtonIndex },
        _ => e,
    };

    static bool SameInput(InputEvent a, InputEvent b) => (a, b) switch
    {
        (InputEventKey x, InputEventKey y) => x.PhysicalKeycode == y.PhysicalKeycode,
        (InputEventMouseButton x, InputEventMouseButton y) => x.ButtonIndex == y.ButtonIndex,
        (InputEventJoypadButton x, InputEventJoypadButton y) => x.ButtonIndex == y.ButtonIndex,
        (InputEventJoypadMotion x, InputEventJoypadMotion y) => x.Axis == y.Axis && Mathf.Sign(x.AxisValue) == Mathf.Sign(y.AxisValue),
        _ => false,
    };

    /// <summary>Nom lisible d'une touche, dans la disposition du clavier du joueur (Z sur AZERTY pour la touche physique W).</summary>
    public static string Describe(InputEvent e)
    {
        switch (e)
        {
            case InputEventKey k:
                var shown = DisplayServer.GetName() == "headless" ? k.PhysicalKeycode : DisplayServer.KeyboardGetKeycodeFromPhysical(k.PhysicalKeycode);
                return L.T(shown switch
                {
                    Key.Space => "Espace", Key.Shift => "Maj", Key.Ctrl => "Ctrl", Key.Escape => "Échap", Key.Tab => "Tab",
                    Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→", Key.Enter => "Entrée", Key.Alt => "Alt",
                    _ => OS.GetKeycodeString(shown),
                });
            case InputEventMouseButton m:
                return L.T(m.ButtonIndex switch
                {
                    MouseButton.Left => "Clic G", MouseButton.Right => "Clic D", MouseButton.Middle => "Molette",
                    MouseButton.Xbutton1 => "Souris 4", MouseButton.Xbutton2 => "Souris 5", _ => L.F("Souris {0}", (int)m.ButtonIndex),
                });
            default: return "?";
        }
    }

    /// <summary>Touche principale d'une action, telle qu'affichée dans les aides (« Clic G », « Maj », « Z »…), ou son bouton de manette si on joue à la manette.</summary>
    public static string KeyName(string action)
    {
        Register();
        if (UsingPad && pad.TryGetValue(action, out var p)) return DescribePad(p);
        return Describe(PrimaryOf(action));
    }

    /// <summary>Touche clavier de l'action, quel que soit le dernier appareil utilisé.</summary>
    public static string KeyboardName(string action) { Register(); return Describe(PrimaryOf(action)); }

    /// <summary>Bouton de manette de l'action (« Menu » pour la pause), ou chaîne vide si elle n'en a pas.</summary>
    public static string PadName(string action) { Register(); return pad.TryGetValue(action, out var p) ? DescribePad(p) : ""; }

    /// <summary>Dernier appareil utilisé : manette (aides en boutons de manette) ou clavier-souris. Manette d'emblée sur Steam Deck.</summary>
    public static bool UsingPad { get; private set; } = GameSettings.OnSteamDeck;

    public static void Track(InputEvent e)
    {
        if (e is InputEventJoypadButton { Pressed: true } || e is InputEventJoypadMotion jm && Mathf.Abs(jm.AxisValue) > 0.5f) UsingPad = true;
        // Sur Steam Deck, le pavé tactile et l'écran envoient des mouvements de souris : seuls un clic ou une touche repassent
        // les aides au clavier.
        else if (e is InputEventKey { Pressed: true } || e is InputEventMouseButton { Pressed: true }
            || e is InputEventMouseMotion mm && mm.Relative.LengthSquared() > 25f && !GameSettings.OnSteamDeck) UsingPad = false;
    }

    /// <summary>Boutons à la façon d'une manette Xbox (disposition reprise par Steam Input et le Steam Deck).</summary>
    static string DescribePad(InputEvent e) => L.T(DescribePadFr(e));

    static string DescribePadFr(InputEvent e) => e switch
    {
        InputEventJoypadButton b => b.ButtonIndex switch
        {
            JoyButton.A => "A", JoyButton.B => "B", JoyButton.X => "X", JoyButton.Y => "Y",
            JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB", JoyButton.LeftStick => "L3", JoyButton.RightStick => "R3",
            JoyButton.Back => "Vue", JoyButton.Start => "Menu",
            JoyButton.DpadUp => "Croix ↑", JoyButton.DpadDown => "Croix ↓", JoyButton.DpadLeft => "Croix ←", JoyButton.DpadRight => "Croix →",
            _ => L.F("Bouton {0}", (int)b.ButtonIndex),
        },
        InputEventJoypadMotion m => m.Axis switch
        {
            JoyAxis.TriggerRight => "RT", JoyAxis.TriggerLeft => "LT",
            JoyAxis.LeftX or JoyAxis.LeftY => "Stick G", JoyAxis.RightX or JoyAxis.RightY => "Stick D",
            _ => "?",
        },
        _ => "?",
    };

    public static void LoadBindings(ConfigFile cfg)
    {
        Register();
        foreach (var a in Actions)
        {
            if (!cfg.HasSectionKey("keys", a.Name)) continue;
            string v = cfg.GetValue("keys", a.Name).AsString();
            InputEvent? e = null;
            if (v.StartsWith("key:") && int.TryParse(v[4..], out int key)) e = new InputEventKey { PhysicalKeycode = (Key)key };
            else if (v.StartsWith("mouse:") && int.TryParse(v[6..], out int btn)) e = new InputEventMouseButton { ButtonIndex = (MouseButton)btn };
            if (e != null) { primary[a.Name] = e; Rebuild(a); }
        }
        foreach (var a in Actions)
        {
            if (!PadRemappable(a) || !cfg.HasSectionKey("pad", a.Name)) continue;
            string v = cfg.GetValue("pad", a.Name).AsString();
            InputEvent? e = null;
            if (v.StartsWith("btn:") && int.TryParse(v[4..], out int b)) e = J((JoyButton)b);
            else if (v.StartsWith("axis:") && int.TryParse(v[5..], out int ax) && (JoyAxis)ax is JoyAxis.TriggerLeft or JoyAxis.TriggerRight) e = Ax((JoyAxis)ax, 1f);
            if (e != null) { pad[a.Name] = e; Rebuild(a); }
        }
    }

    public static void SaveBindings(ConfigFile cfg)
    {
        Register();
        foreach (var a in Actions)
        {
            string v = primary[a.Name] switch
            {
                InputEventKey k => $"key:{(long)k.PhysicalKeycode}",
                InputEventMouseButton m => $"mouse:{(long)m.ButtonIndex}",
                _ => "",
            };
            if (v != "") cfg.SetValue("keys", a.Name, v);
            if (!PadRemappable(a)) continue;
            string pv = pad[a.Name] switch
            {
                InputEventJoypadButton b => $"btn:{(long)b.ButtonIndex}",
                InputEventJoypadMotion m => $"axis:{(long)m.Axis}",
                _ => "",
            };
            if (pv != "") cfg.SetValue("pad", a.Name, pv);
        }
    }
}

/// <summary>Suit le dernier appareil utilisé (manette ou clavier-souris) pour les aides de touches.</summary>
public partial class InputDeviceWatch : Node
{
    // Menus à la manette (quand un contrôle a le focus) :
    // - un vrai stick envoie un flot d'événements au-delà du seuil : seul le franchissement du seuil passe à l'interface,
    //   le reste est avalé (l'état des actions, lu par le jeu, n'est pas touché ; Godot 4.3 filtre déjà, c'est une sûreté) ;
    // - stick ou croix maintenus : répétition après 0,4 s, toutes les 0,09 s (listes, curseurs).
    const float Threshold = 0.5f, RepeatDelay = 0.4f, RepeatEvery = 0.09f;
    readonly int[] stick = new int[2];   // direction tenue par axe (LeftX, LeftY) : -1, 0, 1
    JoyButton? dpad;
    double held, nextRepeat;
    bool pushing;

    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;

    public override void _Input(InputEvent e)
    {
        InputSetup.Track(e);
        if (pushing) return;
        if (e is InputEventJoypadMotion { Axis: JoyAxis.LeftX or JoyAxis.LeftY } m)
        {
            int axis = m.Axis == JoyAxis.LeftX ? 0 : 1;
            int dir = m.AxisValue >= Threshold ? 1 : m.AxisValue <= -Threshold ? -1 : 0;
            if (dir != 0 && dir == stick[axis] && Focused()) { GetViewport().SetInputAsHandled(); return; }   // même direction encore tenue
            if (dir != stick[axis]) { stick[axis] = dir; held = 0; nextRepeat = RepeatDelay; }
        }
        else if (e is InputEventJoypadButton { ButtonIndex: JoyButton.DpadUp or JoyButton.DpadDown or JoyButton.DpadLeft or JoyButton.DpadRight } b)
        {
            if (b.Pressed) { dpad = b.ButtonIndex; held = 0; nextRepeat = RepeatDelay; }
            else if (dpad == b.ButtonIndex) dpad = null;
        }
    }

    public override void _Process(double delta)
    {
        bool any = stick[0] != 0 || stick[1] != 0 || dpad != null;
        if (!any || !Focused()) { held = 0; nextRepeat = RepeatDelay; return; }
        held += delta;
        if (held < nextRepeat) return;
        nextRepeat += RepeatEvery;
        // Répétition : le même appui renvoyé à l'interface seulement (PushInput ne passe pas par l'état des actions).
        // Le stick est répété comme la croix : Godot ne déplace le focus sur un mouvement de stick qu'au moment où l'action
        // vient d'être enfoncée, jamais sur un mouvement renvoyé.
        JoyButton button = dpad ?? (stick[1] < 0 ? JoyButton.DpadUp : stick[1] > 0 ? JoyButton.DpadDown : stick[0] < 0 ? JoyButton.DpadLeft : JoyButton.DpadRight);
        InputEvent again = new InputEventJoypadButton { ButtonIndex = button, Pressed = true, Device = 0 };
        pushing = true;
        GetViewport().PushInput(again);
        pushing = false;
    }

    bool Focused() => GetViewport().GuiGetFocusOwner() != null;
}
