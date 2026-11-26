using System.Globalization;
using Godot;

namespace DogeballVR.Tools;

/// <summary>
/// Captures pour la page Steam, prises dans le vrai jeu : <c>--shots=DOSSIER</c> enregistre l'image du jeu
/// aux instants de <c>--shots-at=6,14,24</c> (secondes), fichiers <c>NOM-01.png</c>… avec <c>--shots-name=NOM</c>, en PNG à la taille de la fenêtre, puis ferme le jeu.
/// <c>--shots-cam=X,Y,Z,CX,CY,CZ[,FOV]</c> : caméra de plateau posée en X,Y,Z et tournée vers CX,CY,CZ le temps de chaque capture
/// (vue d'ensemble d'une arène pour les capsules ; la partie continue derrière avec la caméra du joueur).
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
    float[]? camPose;
    Camera3D? stageCam, playerCam;

    /// <summary>Ajoute le preneur de captures si la ligne de commande le demande.</summary>
    public static void AttachIfAsked(Node parent)
    {
        string? folder = null, at = null;
        string prefix = "shot";
        bool clean = false;
        string? cam = null;
        foreach (var a in DevFlags.Args)   // écrit des PNG dans un dossier choisi en ligne de commande : outil de l'éditeur seulement
        {
            if (a.StartsWith("--shots=")) folder = a["--shots=".Length..];
            else if (a.StartsWith("--shots-at=")) at = a["--shots-at=".Length..];
            else if (a.StartsWith("--shots-name=")) prefix = a["--shots-name=".Length..];
            else if (a == "--shots-clean") clean = true;
            else if (a.StartsWith("--shots-cam=")) cam = a["--shots-cam=".Length..];
        }
        if (folder == null) return;
        var shots = new StoreShots { Name = "StoreShots", Folder = folder, Prefix = prefix, clean = clean };
        if (cam != null)
        {
            var v = new System.Collections.Generic.List<float>();
            foreach (var p in cam.Split(',')) if (float.TryParse(p, NumberStyles.Float, CultureInfo.InvariantCulture, out float f)) v.Add(f);
            if (v.Count >= 6) shots.camPose = v.ToArray();
            else GD.PrintErr($"[Captures] --shots-cam : 6 ou 7 nombres attendus, reçu « {cam} »");
        }
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
        else if (n is Visual.SpeechBubble { Visible: true } bubble) { bubble.Visible = false; hidden.Add(bubble); }   // « PASSE ! », « FEINTE ! »
        foreach (var c in n.GetChildren()) HideOverlays(c);
    }

    void RestoreOverlays()
    {
        foreach (var o in hidden)
            if (IsInstanceValid(o)) { if (o is CanvasLayer l) l.Visible = true; else if (o is Node3D t) t.Visible = true; }
        hidden.Clear();
    }

    /// <summary>Caméra de plateau (--shots-cam) rendue courante ; celle du joueur est reprise après la capture.</summary>
    void UseStageCamera()
    {
        var p = camPose!;
        playerCam = GetViewport().GetCamera3D();
        if (stageCam == null)
        {
            stageCam = new Camera3D { Name = "CameraDePlateau", Fov = p.Length >= 7 ? p[6] : 62f, Far = 400f };
            GetTree().Root.AddChild(stageCam);
            stageCam.LookAtFromPosition(new Vector3(p[0], p[1], p[2]), new Vector3(p[3], p[4], p[5]), Vector3.Up);
        }
        stageCam.MakeCurrent();
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
        bool staged = clean || camPose != null;
        if (staged && cleanFrames < 0)
        {
            if (clean) { HideOverlays(GetTree().Root); Visual.PlayerTag.HiddenForShot = true; }
            if (camPose != null) UseStageCamera();
            cleanFrames = 0;
            return;
        }
        if (staged && ++cleanFrames < 3) return;   // l'image lue est celle déjà rendue : on laisse passer deux images préparées
        var image = GetViewport().GetTexture().GetImage();
        if (staged)
        {
            RestoreOverlays();
            if (clean) Visual.PlayerTag.HiddenForShot = false;
            if (playerCam != null && IsInstanceValid(playerCam)) playerCam.MakeCurrent();
            cleanFrames = -1;
        }
        string path = $"{Folder}/{Prefix}-{next + 1:00}.png";
        var err = image.SavePng(path);
        GD.Print($"[Captures] {path} ({image.GetWidth()}x{image.GetHeight()}) : {err}");
        next++;
        if (next >= times.Length) GetTree().CreateTimer(0.5).Timeout += () => GetTree().Quit();
    }
}
