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
    /// <summary><c>--shots-clean</c> : sans HUD ni étiquettes au moment de la capture (fonds des capsules Steam).</summary>
    bool clean;
    int cleanFrames = -1;
    readonly System.Collections.Generic.List<GodotObject> hidden = new();

    /// <summary>Ajoute le preneur de captures si la ligne de commande le demande.</summary>
    public static void AttachIfAsked(Node parent)
    {
        string? folder = null, at = null;
        string prefix = "shot";
        bool clean = false;
        foreach (var a in OS.GetCmdlineUserArgs())
        {
            if (a.StartsWith("--shots=")) folder = a["--shots=".Length..];
            else if (a.StartsWith("--shots-at=")) at = a["--shots-at=".Length..];
            else if (a.StartsWith("--shots-name=")) prefix = a["--shots-name=".Length..];
            else if (a == "--shots-clean") clean = true;
        }
        if (folder == null) return;
        var shots = new StoreShots { Name = "StoreShots", Folder = folder, Prefix = prefix, clean = clean };
        if (at != null)
        {
            var parts = at.Split(',');
            var list = new System.Collections.Generic.List<double>();
            foreach (var p in parts) if (double.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out double t)) list.Add(t);
            if (list.Count > 0) shots.times = list.ToArray();
        }
        parent.AddChild(shots);
    }

    void HideOverlays(Node n)
    {
        if (n is CanvasLayer { Visible: true } layer) { layer.Visible = false; hidden.Add(layer); }
        else if (n is Label3D { Visible: true } label) { label.Visible = false; hidden.Add(label); }
        foreach (var c in n.GetChildren()) HideOverlays(c);
    }

    void RestoreOverlays()
    {
        foreach (var o in hidden)
            if (IsInstanceValid(o)) { if (o is CanvasLayer l) l.Visible = true; else if (o is Label3D t) t.Visible = true; }
        hidden.Clear();
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
        if (clean && cleanFrames < 0) { HideOverlays(GetTree().Root); cleanFrames = 0; return; }
        if (clean && ++cleanFrames < 3) return;   // l'image lue est celle déjà rendue : on laisse passer deux images sans HUD
        var image = GetViewport().GetTexture().GetImage();
        if (clean) { RestoreOverlays(); cleanFrames = -1; }
        string path = $"{Folder}/{Prefix}-{next + 1:00}.png";
        var err = image.SavePng(path);
        GD.Print($"[Captures] {path} ({image.GetWidth()}x{image.GetHeight()}) : {err}");
        next++;
        if (next >= times.Length) GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
    }
}
