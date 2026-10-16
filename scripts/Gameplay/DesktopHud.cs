using Godot;
using DogeballVR.Visual;

namespace DogeballVR.Gameplay;

/// <summary>
/// Affichage 2D du joueur clavier-souris : viseur (rouge sur un adversaire), marqueur de touche, vies, balle tenue
/// et jauge de lancer, état de l'attrape et de l'esquive, flash rouge quand on perd une vie, aide des touches (F1).
/// (En VR, tout passe par le HUD 3D du MatchController.)
/// </summary>
public partial class DesktopHud : CanvasLayer
{
    static readonly Color[] ChargeColors =
    {
        new(0.9f, 0.9f, 0.9f), new(1f, 0.9f, 0.2f), new(1f, 0.5f, 0.1f), new(1f, 0.1f, 0.1f),
    };

    const string Help =
        "ZQSD / WASD   bouger\n" +
        "Maj   sprinter      Espace   sauter\n" +
        "Ctrl / C   s'accroupir (ramasser, ascenseur)\n" +
        "E   esquive\n" +
        "Clic gauche   lancer (maintenir = plus fort)\n" +
        "Clic droit   attraper (0,5 s)\n" +
        "PgHaut / PgBas   sensibilité\n" +
        "Échap   libérer la souris      F1   cacher l'aide";

    public DesktopPlayer Player { get; set; } = null!;
    /// <summary>Vies restantes du joueur local (hôte : règles ; client : snapshot de l'hôte).</summary>
    public System.Func<int> Lives { get; set; } = null!;
    public int MaxLives { get; set; } = 2;

    /// <summary>Marqueur de touche : vrai quand on vient de toucher un adversaire (hôte et hors ligne).</summary>
    public static bool ShowHitMarkers { get; set; } = true;

    sealed partial class Crosshair : Control
    {
        public DesktopHud Hud = null!;
        public override void _Draw()
        {
            var c = Size * 0.5f;
            var col = Hud.Player.AimingAtEnemy ? new Color(Palette.TeamA, 1f) : new Color(1, 1, 1, 0.9f);
            var shadow = new Color(0, 0, 0, 0.55f);
            float spread = 7f + Hud.Player.ThrowCharge01 * 10f;      // le viseur s'ouvre quand on arme un lancer
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var a = c + new Vector2(dx, dy) * spread;
                var b = c + new Vector2(dx, dy) * (spread + 9f);
                DrawLine(a + Vector2.One, b + Vector2.One, shadow, 4f);
                DrawLine(a, b, col, 2f);
            }
            DrawCircle(c, 2.2f, col);
            if (Hud.hitMarkerLeft > 0)
            {
                var mc = Hud.hitMarkerKill ? new Color(1f, 0.15f, 0.15f) : Colors.White;
                float a0 = 12f, a1 = 24f;
                foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                    DrawLine(c + new Vector2(dx, dy) * a0, c + new Vector2(dx, dy) * a1, mc, 3f);
            }
        }
    }

    Control root = null!;
    Crosshair crosshair = null!;
    Label lives = null!, state = null!, help = null!, toast = null!, catchLabel = null!;
    ColorRect bar = null!, barBack = null!, catchBack = null!, catchFill = null!, flash = null!;
    double hitMarkerLeft;
    bool hitMarkerKill;
    int lastLives = -1;
    double helpHideAt = 25;
    double age;

    public override void _Ready()
    {
        root = new Control { Name = "Root" };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.MouseFilter = Control.MouseFilterEnum.Ignore;
        AddChild(root);

        flash = new ColorRect { Color = new Color(0.9f, 0.1f, 0.1f, 0f), MouseFilter = Control.MouseFilterEnum.Ignore };
        flash.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(flash);

        crosshair = new Crosshair { Hud = this, MouseFilter = Control.MouseFilterEnum.Ignore };
        crosshair.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(crosshair);

        lives = MakeLabel(new Vector2(24, 20), 34);
        state = MakeLabel(new Vector2(24, 64), 20);

        barBack = new ColorRect { Color = new Color(Palette.Panel, 0.75f), Size = new Vector2(240, 14) };
        barBack.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        barBack.Position = new Vector2(-120, -70);
        root.AddChild(barBack);
        bar = new ColorRect { Color = Colors.White, Size = new Vector2(0, 14) };
        barBack.AddChild(bar);

        catchBack = new ColorRect { Color = new Color(Palette.Panel, 0.75f), Size = new Vector2(240, 10) };
        catchBack.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        catchBack.Position = new Vector2(-120, -44);
        root.AddChild(catchBack);
        catchFill = new ColorRect { Color = Colors.White, Size = new Vector2(240, 10) };
        catchBack.AddChild(catchFill);
        catchLabel = new Label { Text = "ATTRAPER", Position = new Vector2(250, -7) };
        catchLabel.AddThemeFontSizeOverride("font_size", 14);
        catchLabel.AddThemeColorOverride("font_outline_color", Colors.Black);
        catchLabel.AddThemeConstantOverride("outline_size", 4);
        catchBack.AddChild(catchLabel);

        help = MakeLabel(new Vector2(0, 0), 16);
        help.Text = Help;
        help.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        help.OffsetLeft = -470; help.OffsetRight = -20; help.OffsetTop = 20;
        help.HorizontalAlignment = HorizontalAlignment.Right;
        help.CustomMinimumSize = new Vector2(450, 0);

        toast = MakeLabel(new Vector2(0, 0), 26);
        toast.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        toast.OffsetLeft = -300; toast.OffsetRight = 300; toast.OffsetTop = 120;
        toast.HorizontalAlignment = HorizontalAlignment.Center;

        MatchController.HitScored += OnHitScored;
    }

    public override void _ExitTree() => MatchController.HitScored -= OnHitScored;

    void OnHitScored(int shooterId, int targetId, bool kill)
    {
        if (!ShowHitMarkers || shooterId != Player.Hurtbox.PlayerId) return;
        hitMarkerLeft = 0.3;
        hitMarkerKill = kill;
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e is InputEventKey { Pressed: true, Echo: false, Keycode: Key.F1 })
            helpHideAt = help.Visible ? 0 : age + 1e9;
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
        age += delta;
        int n = Lives();
        lives.Text = n > 0 ? new string('♥', n) + new string('♡', Mathf.Max(0, MaxLives - n)) : "KO : retour dans quelques secondes";
        if (lastLives >= 0 && n < lastLives) flash.Color = new Color(0.9f, 0.1f, 0.1f, 0.45f);    // touché : l'écran rougit
        lastLives = n;
        flash.Color = new Color(flash.Color, Mathf.Max(0, flash.Color.A - (float)delta * 1.2f));

        var ball = Player.HeldBall;
        state.Text = ball == null
            ? (Player.CatchState == 1 ? "Attrape !" : "Cherche une balle : passe dessus, ou clic droit pour attraper")
            : (ball.Charge > 0 ? $"Balle chargée x{ball.Charge}" : "Balle en main") + "   |   clic gauche : lancer (maintenir = plus fort)";
        float c = Player.ThrowCharge01;
        barBack.Visible = ball != null;
        bar.Size = new Vector2(240 * c, 14);
        bar.Color = ball != null ? ChargeColors[ball.Charge] : Colors.White;

        // Attrape : verte quand prête, lime pendant la fenêtre, grise qui se remplit pendant la recharge.
        catchFill.Size = new Vector2(240 * Player.CatchCooldown01, 10);
        catchFill.Color = Player.CatchState switch { 1 => Palette.BallCore, 2 => new Color(0.55f, 0.58f, 0.66f), _ => new Color(0.45f, 0.9f, 0.55f) };
        catchLabel.Text = Player.DodgeCooldown01 >= 1f ? "ATTRAPER   |   E : esquive prête" : "ATTRAPER   |   esquive…";

        hitMarkerLeft = Mathf.Max(0, hitMarkerLeft - delta);
        help.Visible = age < helpHideAt;
        toast.Text = Player.Time01 < Player.ToastUntil ? Player.Toast : "";
        crosshair.QueueRedraw();
    }
}
