using System.Globalization;
using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Captures pour la page Steam, prises dans le vrai jeu : <c>--shots=DOSSIER</c> enregistre l'image du jeu
/// aux instants de <c>--shots-at=6,14,24</c> (secondes), fichiers <c>NOM-01.png</c>… avec <c>--shots-name=NOM</c>, en PNG à la taille de la fenêtre, puis ferme le jeu.
/// À lancer dans une vraie fenêtre (pas en headless), par exemple sous xvfb avec le rendu Compatibilité (tools/steam/shots.sh).
/// </summary>
public partial class StoreShots : Node
{
    public string Folder { get; init; } = "user://shots";
    public string Prefix { get; init; } = "shot";
    double[] times = { 6, 14, 24 };
    double clock;
    int next;

    /// <summary>Ajoute le preneur de captures si la ligne de commande le demande.</summary>
    public static void AttachIfAsked(Node parent)
    {
        string? folder = null, at = null;
        string prefix = "shot";
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--shots=")) folder = a["--shots=".Length..];
            else if (a.StartsWith("--shots-at=")) at = a["--shots-at=".Length..];
            else if (a.StartsWith("--shots-name=")) prefix = a["--shots-name=".Length..];
        }
        if (folder == null) return;
        var shots = new StoreShots { Name = "StoreShots", Folder = folder, Prefix = prefix };
        if (at != null)
        {
            var parts = at.Split(',');
            var list = new System.Collections.Generic.List<double>();
            foreach (var p in parts) if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) list.Add(t);
            if (list.Count > 0) shots.times = list.ToArray();
        }
        parent.AddChild(shots);
    }

    public override void _Ready()
    {
        ProcessMode = ProcessModeEnum.Always;
        DirAccess.MakeDirRecursiveAbsolute(Folder);
    }

    public override void _Process(double delta)
    {
        clock += delta;
        if (next >= times.Length || clock < times[next]) return;
        var image = GetViewport().GetTexture().GetImage();
        string path = $"{Folder}/{Prefix}-{next + 1:00}.png";
        var err = image.SavePng(path);
        GD.Print($"[Captures] {path} ({image.GetWidth()}x{image.GetHeight()}) : {err}");
        next++;
        if (next >= times.Length) GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
    }
}
