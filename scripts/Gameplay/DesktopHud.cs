using Godot;

namespace DogeballVR.Gameplay;

/// <summary>
/// Affichage 2D pour le test au clavier-souris : viseur, vies, jauge de lancer, couleur de la balle tenue.
/// (En VR, tout passe par le HUD 3D du MatchController.)
/// </summary>
public partial class DesktopHud : CanvasLayer
{
    static readonly Color[] ChargeColors =
    {
        new(0.9f, 0.9f, 0.9f), new(1f, 0.9f, 0.2f), new(1f, 0.5f, 0.1f), new(1f, 0.1f, 0.1f),
    };

    public DesktopPlayer Player { get; set; } = null!;
    public MatchController Match { get; set; } = null!;
    public int PlayerId { get; set; }

    Control root = null!;
    Label lives = null!;
    Label state = null!;
    ColorRect bar = null!;
    ColorRect barBack = null!;

    public override void _Ready()
    {
        root = new Control { Name = "Root" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        // Viseur : un petit point au centre de l'écran.
        var dot = new ColorRect { Color = new Color(1, 1, 1, 0.85f), Size = new Vector2(6, 6) };
        dot.SetAnchorsPreset(Control.LayoutPreset.Center);
        dot.Position = new Vector2(-3, -3);
        root.AddChild(dot);

        lives = MakeLabel(new Vector2(24, 20), 34);
        state = MakeLabel(new Vector2(24, 64), 20);

        barBack = new ColorRect { Color = new Color(0, 0, 0, 0.5f), Size = new Vector2(240, 14) };
        barBack.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        barBack.Position = new Vector2(-120, -70);
        root.AddChild(barBack);
        bar = new ColorRect { Color = Colors.White, Size = new Vector2(0, 14) };
        barBack.AddChild(bar);
    }

    Label MakeLabel(Vector2 pos, int size)
    {
        var label = new Label { Position = pos };
        label.AddThemeFontSizeOverride("font_size", size);
        label.AddThemeColorOverride("font_outline_color", Colors.Black);
        label.AddThemeConstantOverride("outline_size", 6);
        root.AddChild(label);
        return label;
    }

    public override void _Process(double delta)
    {
        int n = Match.Rules.LivesOf(PlayerId);
        lives.Text = n > 0 ? new string('♥', n) + new string('♡', Match.Rules.LivesPerPlayer - n) : "KO : retour dans quelques secondes";
        var ball = Player.HeldBall;
        state.Text = ball == null
            ? "Clic droit : attraper   |   Maj : s'accroupir"
            : $"Balle chargée x{ball.Charge}   |   clic gauche maintenu puis relâché : lancer";
        float c = Player.ThrowCharge01;
        barBack.Visible = ball != null;
        bar.Size = new Vector2(240 * c, 14);
        bar.Color = ball != null ? ChargeColors[ball.Charge] : Colors.White;
    }
}
