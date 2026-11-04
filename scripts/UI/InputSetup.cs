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
                return shown switch
                {
                    Key.Space => "Espace", Key.Shift => "Maj", Key.Ctrl => "Ctrl", Key.Escape => "Échap", Key.Tab => "Tab",
                    Key.Up => "↑", Key.Down => "↓", Key.Left => "←", Key.Right => "→", Key.Enter => "Entrée", Key.Alt => "Alt",
                    _ => OS.GetKeycodeString(shown),
                };
            case InputEventMouseButton m:
                return m.ButtonIndex switch
                {
                    MouseButton.Left => "Clic G", MouseButton.Right => "Clic D", MouseButton.Middle => "Molette",
                    MouseButton.Xbutton1 => "Souris 4", MouseButton.Xbutton2 => "Souris 5", _ => $"Souris {(int)m.ButtonIndex}",
                };
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

    /// <summary>Dernier appareil utilisé : manette (aides en boutons de manette) ou clavier-souris. Manette d'emblée sur Steam Deck.</summary>
    public static bool UsingPad { get; private set; } = GameSettings.OnSteamDeck;

    public static void Track(InputEvent e)
    {
        if (e is InputEventJoypadButton { Pressed: true } || e is InputEventJoypadMotion jm && Mathf.Abs(jm.AxisValue) > 0.5f) UsingPad = true;
        else if (e is InputEventKey { Pressed: true } || e is InputEventMouseButton { Pressed: true } || e is InputEventMouseMotion mm && mm.Relative.LengthSquared() > 25f) UsingPad = false;
    }

    /// <summary>Boutons à la façon d'une manette Xbox (disposition reprise par Steam Input et le Steam Deck).</summary>
    static string DescribePad(InputEvent e) => e switch
    {
        InputEventJoypadButton b => b.ButtonIndex switch
        {
            JoyButton.A => "A", JoyButton.B => "B", JoyButton.X => "X", JoyButton.Y => "Y",
            JoyButton.LeftShoulder => "LB", JoyButton.RightShoulder => "RB", JoyButton.LeftStick => "L3", JoyButton.RightStick => "R3",
            JoyButton.Back => "Vue", JoyButton.Start => "Menu",
            JoyButton.DpadUp => "Croix ↑", JoyButton.DpadDown => "Croix ↓", JoyButton.DpadLeft => "Croix ←", JoyButton.DpadRight => "Croix →",
            _ => $"Bouton {(int)b.ButtonIndex}",
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
    public override void _Ready() => ProcessMode = ProcessModeEnum.Always;
    public override void _Input(InputEvent e) => InputSetup.Track(e);
}
