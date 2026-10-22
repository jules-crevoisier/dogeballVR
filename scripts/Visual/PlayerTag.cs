using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;

namespace DogeballVR.Visual;

/// <summary>
/// Étiquette au-dessus d'un autre joueur : nom puis cœurs, à la couleur de son équipe, taille stable à l'écran.
/// Adversaire à portée : visible à travers les obstacles (comme Knockout City), on sait toujours où il est.
/// Adversaire lointain : seulement là où on le voit. Coéquipier : plus petit, plus pâle, masqué par le décor.
/// Joueur KO : étiquette cachée jusqu'à son retour. Toutes cachées pendant le compte à rebours du joueur PC (<see cref="HideAll"/>).
/// </summary>
public partial class PlayerTag : Node3D
{
    /// <summary>Distance (m) en deçà de laquelle un adversaire reste repérable derrière un obstacle.</summary>
    public const float ThroughWallRange = 28f;
    const float HeadAbove = 0.95f;   // du centre de la hurtbox au sommet du bonnet

    public Hurtbox Target { get; set; } = null!;
    public Func<int> Lives { get; set; } = () => 0;
    public int Max { get; set; } = 2;
    public Func<string> PlayerName { get; set; } = () => "";
    /// <summary>Équipe du joueur qui regarde (−1 : inconnue, tout le monde est traité en adversaire).</summary>
    public Func<int> ViewerTeam { get; set; } = () => -1;

    static Font? font;
    static readonly List<PlayerTag> all = new();
    Label3D nameLabel = null!, hearts = null!;
    Sprite3D symbol = null!;   // option daltonisme : rond pour l'équipe orange, triangle pour la bleue, à gauche du nom
    float nameEm = 1f;
    string lastHearts = "", lastName = "";
    bool? throughWalls;
    float widthEm = 2f;   // largeur du bloc nom + cœurs, contours compris (1 em = 24 px pour un adversaire)
    float unitPx;         // taille d'un em en pixels de la fenêtre, à la dernière image affichée

    /// <summary>Toutes les étiquettes cachées (compte à rebours du joueur PC : le chiffre et « GO ! » ne les chevauchent pas).</summary>
    public static bool HideAll { get; set; }

    public override void _EnterTree() => all.Add(this);
    public override void _ExitTree() => all.Remove(this);

    /// <summary>Étiquettes actuellement affichées (pour que les autocollants « BAM ! » évitent de les couvrir).</summary>
    public static IEnumerable<PlayerTag> Shown()
    {
        foreach (var t in all) if (IsInstanceValid(t) && t.Visible && t.unitPx > 0f) yield return t;
    }

    /// <summary>Étiquette posée au-dessus de ce joueur, s'il en a une.</summary>
    public static PlayerTag? Of(Node3D? target)
    {
        if (target == null) return null;
        foreach (var t in all) if (IsInstanceValid(t) && t.Target == target) return t;
        return null;
    }

    /// <summary>Rectangle occupé à l'écran (pixels de la fenêtre), contours compris ; null si l'étiquette est cachée.</summary>
    public Rect2? ScreenRect(Camera3D cam)
    {
        if (!Visible || unitPx <= 0f || cam.IsPositionBehind(GlobalPosition)) return null;
        var p = cam.UnprojectPosition(GlobalPosition);
        float half = 0.5f * widthEm * unitPx;
        return new Rect2(p.X - half, p.Y - TopEm * unitPx, 2f * half, (TopEm + BottomEm) * unitPx);
    }

    const float TopEm = 2.6f, BottomEm = 0.35f;   // du pied des cœurs au haut du nom (contours compris)

    public override void _Ready()
    {
        TopLevel = true;
        font ??= GD.Load<Font>("res://assets/fonts/LilitaOne-Regular.ttf");
        var color = Palette.Team(Target.Team).Lerp(Colors.White, 0.15f);
        nameLabel = new Label3D
        {
            Font = font, FontSize = 64, PixelSize = 1f / 64f, OutlineSize = 40, Modulate = color, OutlineModulate = Palette.Panel,
            VerticalAlignment = VerticalAlignment.Bottom, Position = new Vector3(0, 1.05f, 0), RenderPriority = 1, OutlineRenderPriority = 0,
        };
        // Cœurs : police par défaut (les glyphes ♥ ♡ n'existent pas dans Lilita One).
        hearts = new Label3D
        {
            FontSize = 64, PixelSize = 1f / 64f, OutlineSize = 44, Modulate = color, OutlineModulate = Palette.Panel,
            VerticalAlignment = VerticalAlignment.Bottom, RenderPriority = 1, OutlineRenderPriority = 0, Scale = Vector3.One * (28f / 24f),
        };
        symbol = new Sprite3D
        {
            Texture = SymbolTexture(Target.Team), PixelSize = 1.1f / 64f, Modulate = color, Shaded = false, RenderPriority = 1,
            Billboard = BaseMaterial3D.BillboardModeEnum.Disabled, TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps, Visible = false,
        };
        AddChild(symbol);
        foreach (var l in new[] { nameLabel, hearts })
        {
            l.Billboard = BaseMaterial3D.BillboardModeEnum.Disabled;
            l.Shaded = false;
            l.TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps;
            AddChild(l);
        }
    }

    public override void _Process(double delta)
    {
        if (!IsInstanceValid(Target)) { QueueFree(); return; }
        if (Target.Eliminated || HideAll) { Visible = false; return; }
        var head = Target.GlobalPosition + new Vector3(0, HeadAbove, 0);
        var cam = GetViewport().GetCamera3D();
        float mpp = ScreenSize.MetersPerPixel(cam, head);
        if (mpp <= 0f) { Visible = false; return; }

        bool mate = ViewerTeam() >= 0 && ViewerTeam() == Target.Team;
        float dist = cam!.GlobalPosition.DistanceTo(head);
        bool walls = !mate && dist < ThroughWallRange;
        if (throughWalls != walls)
        {
            throughWalls = walls;
            nameLabel.NoDepthTest = hearts.NoDepthTest = walls;
        }
        // Taille : nom 24 px, cœurs 28 px (image 720 lignes) ; coéquipiers aux trois quarts et plus pâles.
        float k = mate ? 0.75f : 1f;
        float alpha = mate ? 0.7f : 1f;
        float em = 24f * k * mpp;   // 1 unité locale = 1 em de 24 px
        var basis = cam.GlobalTransform.Basis.Orthonormalized();
        GlobalTransform = new Transform3D(basis * Basis.FromScale(Vector3.One * em), head + basis.Y * (6f * mpp));

        string n = PlayerName();
        if (n != lastName)
        {
            nameLabel.Text = n;
            lastName = n;
            nameEm = font!.GetStringSize(n, HorizontalAlignment.Left, -1, 64).X / 64f + 0.65f;
            widthEm = Mathf.Max(nameEm, Max * 0.85f * (28f / 24f) + 0.7f);
        }
        unitPx = 24f * k * GetViewport().GetVisibleRect().Size.Y / 720f;
        int lives = Mathf.Clamp(Lives(), 0, Max);
        var t = new string('♥', lives) + new string('♡', Max - lives);
        if (t != lastHearts) { hearts.Text = t; lastHearts = t; }
        SetAlpha(nameLabel, alpha);
        SetAlpha(hearts, alpha);
        symbol.Visible = UI.GameSettings.TeamSymbols;
        if (symbol.Visible)
        {
            symbol.Position = new Vector3(-(nameEm / 2f + 0.5f), 1.05f + 0.45f, 0);
            symbol.NoDepthTest = nameLabel.NoDepthTest;
            var sm = symbol.Modulate; sm.A = alpha; symbol.Modulate = sm;
        }
        Visible = true;
    }

    /// <summary>Forme de l'équipe, blanche à contour sombre (teintée par Modulate) : rond (orange), triangle pointe en haut (bleue).</summary>
    static ImageTexture SymbolTexture(int team)
    {
        team = Mathf.Clamp(team, 0, 1);   // une texture par étiquette (64x64) : rien de statique qui survivrait à la fermeture
        const int n = 64;
        var img = Image.CreateEmpty(n, n, false, Image.Format.Rgba8);
        var dark = new Color(Palette.Panel, 1f);
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float u = (x + 0.5f) / n * 2f - 1f, v = (y + 0.5f) / n * 2f - 1f;   // -1..1, v vers le bas
                // Distance signée approchée à la forme (négative dedans), en unités de l'image.
                float d = team == 0
                    ? Mathf.Sqrt(u * u + v * v) - 0.78f
                    : Mathf.Max(v - 0.72f, Mathf.Max(-0.866f * u - 0.5f * v, 0.866f * u - 0.5f * v) - 0.36f);
                if (d < -0.16f) img.SetPixel(x, y, Colors.White);
                else if (d < 0f) img.SetPixel(x, y, dark);
                else if (d < 0.03f) img.SetPixel(x, y, new Color(dark, 1f - d / 0.03f));
            }
        return ImageTexture.CreateFromImage(img);
    }

    static void SetAlpha(Label3D l, float a)
    {
        var m = l.Modulate; m.A = a; l.Modulate = m;
        var o = l.OutlineModulate; o.A = a; l.OutlineModulate = o;
    }
}
