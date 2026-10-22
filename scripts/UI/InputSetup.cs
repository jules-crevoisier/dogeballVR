using System.Collections.Generic;
using Godot;

namespace DogeballVR.UI;

/// <summary>
/// Actions du clavier-souris, déclarées dans l'InputMap au démarrage. Les touches sont des touches physiques :
/// la rangée « WASD » d'un clavier QWERTY est « ZQSD » sur un AZERTY, sans rien régler. Chaque action a une touche
/// principale réassignable (menu Options) et, parfois, une touche secondaire fixe (flèches, X pour la passe…).
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
        new("dash", "Dash / esquive", K(Key.Shift), null, J(JoyButton.B)),
        new("crouch", "S'accroupir", K(Key.Ctrl), K(Key.C), J(JoyButton.LeftStick)),
        new("fire", "Lancer (maintenir = charger)", M(MouseButton.Left), null, Ax(JoyAxis.TriggerRight, 1f)),
        new("catch", "Attraper", M(MouseButton.Right), null, Ax(JoyAxis.TriggerLeft, 1f)),
        new("pass", "Passe à un allié", M(MouseButton.Middle), K(Key.X), J(JoyButton.X)),
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
    static bool registered;

    public static void Register()
    {
        if (registered) return;
        registered = true;
        foreach (var a in Actions)
        {
            if (!InputMap.HasAction(a.Name)) InputMap.AddAction(a.Name, 0.2f);
            if (!primary.ContainsKey(a.Name)) primary[a.Name] = a.Primary;
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
        if (a.Pad != null) InputMap.ActionAddEvent(a.Name, a.Pad);
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
        foreach (var a in Actions) { primary[a.Name] = a.Primary; Rebuild(a); }
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

    /// <summary>Touche principale d'une action, telle qu'affichée dans les aides (« Clic G », « Maj », « Z »…).</summary>
    public static string KeyName(string action) { Register(); return Describe(PrimaryOf(action)); }

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
        }
    }
}
