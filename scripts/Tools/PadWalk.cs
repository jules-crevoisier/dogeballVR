using System.Collections.Generic;
using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Parcours des écrans à la manette, sans manette : <c>--pad-walk=bas,bas,a,shot,b</c> envoie un appui toutes les
/// 0,4 s (après 2 s d'attente) et écrit à chaque fois le contrôle qui a le focus (« [Manette] 3 bas → … »).
/// Appuis : <c>haut bas gauche droite</c> (croix), <c>s-haut s-bas s-gauche s-droite</c> (stick gauche),
/// <c>a b x y lb rb start back</c>, <c>pause</c> (rien pendant 0,4 s), <c>APPUI+</c> (tenu jusqu'à <c>lache</c>),
/// <c>flot-bas</c>… (rafale de valeurs comme un vrai stick), <c>shot</c> (capture PNG dans
/// <c>--pad-walk-shots=DOSSIER</c>, nommée <c>pad-NN.png</c>). Le jeu se ferme à la fin du parcours.
/// Outil de développement (preuve que chaque écran se parcourt à la manette).
/// </summary>
public partial class PadWalk : Node
{
    string[] steps = System.Array.Empty<string>();
    string? folder;
    int next, shots;
    double clock = -2.0;
    string? release;

    /// <summary>Ajoute le parcours si la ligne de commande le demande.</summary>
    public static void AttachIfAsked(Node parent)
    {
        string? seq = DevFlags.Value("--pad-walk=");
        if (seq == null) return;
        var w = new PadWalk { Name = "PadWalk", steps = seq.Split(','), folder = DevFlags.Value("--pad-walk-shots=") };
        w.ProcessMode = ProcessModeEnum.Always;   // la pause du jeu ne l'arrête pas
        parent.AddChild(w);
    }

    public override void _Ready()
    {
        foreach (var ui in new[] { "ui_accept", "ui_cancel", "ui_down" })
        {
            var names = new List<string>();
            foreach (var e in InputMap.ActionGetEvents(ui)) names.Add(e.AsText());
            GD.Print($"[Manette] {ui} : {string.Join(" | ", names)}");
        }
    }

    public override void _Process(double delta)
    {
        if (release != null) { Send(release, false); release = null; }
        clock += delta;
        if (clock < 0.4) return;
        clock = 0;
        // Résultat de l'appui précédent, lu juste avant le suivant (sous xvfb une image peut durer plus de 0,2 s).
        if (next > 0) GD.Print($"[Manette] {next} {steps[next - 1].Trim()} → {Describe()}");
        if (next >= steps.Length)
        {
            GD.Print($"[Manette] parcours terminé ({steps.Length} appuis)");
            GetTree().Quit();
            return;
        }
        string step = steps[next++].Trim();
        if (step == "shot") Shot();
        else if (step == "lache")
        {
            foreach (var axis in new[] { JoyAxis.LeftX, JoyAxis.LeftY }) Input.ParseInputEvent(new InputEventJoypadMotion { Axis = axis, AxisValue = 0f, Device = 0 });
            foreach (var b in Buttons.Values) Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = false, Device = 0 });
        }
        else if (step.StartsWith("flot-") && Sticks.TryGetValue("s-" + step[5..], out var fl))
        {
            // Comme un vrai stick : une rafale de valeurs croissantes au-delà du seuil, puis retour au centre.
            foreach (float v in new[] { 0.3f, 0.55f, 0.7f, 0.85f, 1f, 1f }) Input.ParseInputEvent(new InputEventJoypadMotion { Axis = fl.axis, AxisValue = v * fl.sign, Device = 0 });
            release = "s-" + step[5..];
        }
        else if (step.EndsWith("+"))
        {
            if (!Send(step[..^1], true)) GD.PrintErr($"[Manette] appui inconnu : « {step} »");   // tenu jusqu'à « lache »
        }
        else if (step != "pause")
        {
            if (!Send(step, true)) GD.PrintErr($"[Manette] appui inconnu : « {step} »");
            else release = step;
        }
    }

    static readonly Dictionary<string, JoyButton> Buttons = new()
    {
        ["haut"] = JoyButton.DpadUp, ["bas"] = JoyButton.DpadDown, ["gauche"] = JoyButton.DpadLeft, ["droite"] = JoyButton.DpadRight,
        ["a"] = JoyButton.A, ["b"] = JoyButton.B, ["x"] = JoyButton.X, ["y"] = JoyButton.Y,
        ["lb"] = JoyButton.LeftShoulder, ["rb"] = JoyButton.RightShoulder, ["start"] = JoyButton.Start, ["back"] = JoyButton.Back,
    };

    static readonly Dictionary<string, (JoyAxis axis, float sign)> Sticks = new()
    {
        ["s-haut"] = (JoyAxis.LeftY, -1f), ["s-bas"] = (JoyAxis.LeftY, 1f), ["s-gauche"] = (JoyAxis.LeftX, -1f), ["s-droite"] = (JoyAxis.LeftX, 1f),
    };

    static bool Send(string step, bool down)
    {
        if (Buttons.TryGetValue(step, out var b))
            Input.ParseInputEvent(new InputEventJoypadButton { ButtonIndex = b, Pressed = down, Device = 0 });
        else if (Sticks.TryGetValue(step, out var s))
            Input.ParseInputEvent(new InputEventJoypadMotion { Axis = s.axis, AxisValue = down ? s.sign : 0f, Device = 0 });
        else return false;
        return true;
    }

    string Describe()
    {
        var f = GetViewport().GuiGetFocusOwner();
        if (f == null) return "aucun focus";
        string text = f switch
        {
            Button bt => bt.Text,
            LineEdit le => le.Text.Length > 0 ? le.Text : le.PlaceholderText,
            _ => "",
        };
        if (text.Length == 0)   // bouton dessiné (texte dans un Label enfant)
            foreach (var c in f.FindChildren("*", "Label", true, false))
                if (c is Label { Visible: true } l && l.Text.Length > 0) { text = l.Text; break; }
        return $"« {text.Replace('\n', ' ')} » ({f.GetClass()}, {f.Name})";
    }

    void Shot()
    {
        if (folder == null) return;
        DirAccess.MakeDirRecursiveAbsolute(folder);
        string path = $"{folder}/pad-{++shots:00}.png";
        GetViewport().GetTexture().GetImage().SavePng(path);
        GD.Print($"[Manette] capture {path}");
    }
}
