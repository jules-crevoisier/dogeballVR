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
    public sealed record ActionDef(string Name, string Label, InputEvent Primary, InputEvent? Secondary = null);

    static InputEventKey K(Key physical) => new() { PhysicalKeycode = physical };
    static InputEventMouseButton M(MouseButton b) => new() { ButtonIndex = b };

    /// <summary>Ordre d'affichage du menu Touches.</summary>
    public static readonly ActionDef[] Actions =
    {
        new("move_forward", "Avancer", K(Key.W), K(Key.Up)),
        new("move_back", "Reculer", K(Key.S), K(Key.Down)),
        new("move_left", "Gauche", K(Key.A), K(Key.Left)),
        new("move_right", "Droite", K(Key.D), K(Key.Right)),
        new("jump", "Sauter", K(Key.Space)),
        new("dash", "Dash / esquive", K(Key.Shift)),
        new("crouch", "S'accroupir", K(Key.Ctrl), K(Key.C)),
        new("fire", "Lancer (maintenir = charger)", M(MouseButton.Left)),
        new("catch", "Attraper", M(MouseButton.Right)),
        new("pass", "Passe à un allié", M(MouseButton.Middle), K(Key.X)),
        new("lob", "Cloche", K(Key.R)),
        new("feint", "Feinte", K(Key.F)),
        new("curve", "Effet (maintenir + gauche/droite)", K(Key.E)),
        new("scoreboard", "Tableau des scores", K(Key.Tab)),
        new("pause", "Pause", K(Key.Escape)),
        new("help", "Aide des touches", K(Key.F1)),
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
    }

    static void Rebuild(ActionDef a)
    {
        InputMap.ActionEraseEvents(a.Name);
        InputMap.ActionAddEvent(a.Name, primary[a.Name]);
        if (a.Secondary != null && !SameInput(a.Secondary, primary[a.Name])) InputMap.ActionAddEvent(a.Name, a.Secondary);
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
