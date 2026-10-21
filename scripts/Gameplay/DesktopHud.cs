using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.UI;
using DogeballVR.Visual;

namespace DogeballVR.Gameplay;

/// <summary>
/// HUD du joueur clavier-souris (dessiné pour 1920x1080, mis à l'échelle par le projet), dans l'esprit de Knockout City :
/// tableau de score en haut (KO de la manche, manches gagnées, chrono), cœurs en bas à gauche, balle tenue et jauges
/// d'attrape et de dash en bas au centre, jauge de charge en arc autour du viseur, marqueur de touche, fil des KO,
/// vignette rouge du côté d'où vient le coup, lignes de vitesse au dash, compte à rebours et bandeaux de manche,
/// tableau des scores (Tab), aide des touches (F1), compteur d'images.
/// L'état de la partie vient de <see cref="MatchView"/> (hôte ou client). En VR, tout passe par le HUD 3D du MatchController.
/// </summary>
public partial class DesktopHud : CanvasLayer
{
    // Paliers de la balle tenue (passes, attrape parfaite) et paliers de la charge du lancer.
    static readonly Color[] BallTierColors = { Colors.White, UiKit.Yellow, UiKit.Orange, UiKit.Pink };
    static readonly Color[] ChargeColors = { UiKit.Yellow, UiKit.Orange, UiKit.Pink };
    static readonly Color Red = new("ff2d4a");

    public DesktopPlayer Player { get; set; } = null!;
    public MatchView View { get; set; } = null!;

    Control root = null!;
    double age;
    MatchView.Snapshot prev;
    bool hudTest;

    public override void _Ready()
    {
        Layer = 5;
        hudTest = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--hud-test") >= 0;
        root = new Control { Name = "Root", Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        vignette = Add(new Vignette());
        speedLines = Add(new SpeedLines { Hud = this });
        crosshair = Add(new Crosshair { Hud = this });
        BuildScoreBoard();
        BuildHearts();
        BuildBottomCenter();
        BuildFeed();
        BuildCenterTexts();
        BuildHelp();
        BuildScoreTable();

        fps = UiKit.Text("", 22, UiKit.Green, 6);
        fps.Position = new Vector2(16, 10);
        root.AddChild(fps);

        prev = View.State;
        lastLives = View.Lives(View.LocalId);
        View.Hit += OnHit;
        View.Ko += OnKo;
    }

    public override void _ExitTree()
    {
        View.Hit -= OnHit;
        View.Ko -= OnKo;
        View.Detach();
    }

    T Add<T>(T c) where T : Control
    {
        c.MouseFilter = Control.MouseFilterEnum.Ignore;
        c.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(c);
        return c;
    }

    static Panel MakePanel(Color bg, float skew = 0f, int radius = 14, int border = 0, Color? borderColor = null)
    {
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", UiKit.Box(bg, radius, skew, border, borderColor, 5));
        return p;
    }

    static string Clock(int seconds) => $"{seconds / 60}:{seconds % 60:00}";

    // ------------------------------------------------------------------ tableau de score (haut centre)

    Control board = null!;
    readonly Label[] koLabels = new Label[2];
    readonly Panel[] teamBoxes = new Panel[2];
    readonly List<Control>[] roundPips = { new(), new() };
    Label timer = null!, roundLabel = null!, overtime = null!;
    Panel timerBox = null!;
    readonly StyleBoxFlat timerStyle = UiKit.Box(UiKit.Navy, 16, 0f, 0, null, 5);   // fond du chrono, rougit sous 10 s
    int lastSecond = -1;

    void BuildScoreBoard()
    {
        board = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(700, 140) };
        board.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        board.OffsetLeft = -350; board.OffsetRight = 350; board.OffsetTop = 18; board.OffsetBottom = 158;
        root.AddChild(board);

        for (int team = 0; team < 2; team++)
        {
            var color = team == 0 ? UiKit.Orange : UiKit.Blue;
            bool mine = team == View.LocalTeam;
            var box = MakePanel(color, team == 0 ? -0.18f : 0.18f, 14, mine ? 4 : 0, UiKit.Yellow);
            box.Position = new Vector2(team == 0 ? 0 : 450, 0);
            box.Size = new Vector2(250, 92);
            box.PivotOffset = box.Size / 2;
            board.AddChild(box);
            teamBoxes[team] = box;

            var name = UiKit.Text(team == 0 ? "ORANGE" : "BLEUE", 22, UiKit.Cream, 6, team == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
            name.Position = new Vector2(team == 0 ? 26 : 18, 8);
            name.Size = new Vector2(206, 30);
            box.AddChild(name);
            var tag = UiKit.Text(mine ? "TON ÉQUIPE" : "ADVERSES", 16, mine ? UiKit.Yellow : new Color(1, 1, 1, 0.75f), 5, team == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
            tag.Position = new Vector2(team == 0 ? 22 : 22, 58);
            tag.Size = new Vector2(206, 24);
            box.AddChild(tag);

            var ko = UiKit.Text("0", 76, Colors.White, 12, team == 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left);
            ko.Position = new Vector2(team == 0 ? 120 : 24, -6);
            ko.Size = new Vector2(100, 96);
            ko.PivotOffset = ko.Size / 2;
            box.AddChild(ko);
            koLabels[team] = ko;

            // Pastilles des manches gagnées, du côté du chrono.
            for (int i = 0; i < 5; i++)
            {
                var pip = new Pip { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(22, 22) };
                pip.Position = new Vector2(team == 0 ? 222 - i * 28 : 456 + i * 28, 100);
                pip.PivotOffset = new Vector2(11, 11);
                board.AddChild(pip);
                roundPips[team].Add(pip);
            }
        }

        timerBox = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        timerBox.AddThemeStyleboxOverride("panel", timerStyle);
        timerBox.Position = new Vector2(262, -4);
        timerBox.Size = new Vector2(176, 104);
        timerBox.PivotOffset = timerBox.Size / 2;
        board.AddChild(timerBox);
        timer = UiKit.Text("2:00", 58, Colors.White, 10, HorizontalAlignment.Center);
        timer.Position = new Vector2(0, 2);
        timer.Size = new Vector2(176, 70);
        timer.PivotOffset = timer.Size / 2;
        timerBox.AddChild(timer);
        roundLabel = UiKit.Text("MANCHE 1/5", 20, UiKit.Sky, 5, HorizontalAlignment.Center);
        roundLabel.Position = new Vector2(0, 68);
        roundLabel.Size = new Vector2(176, 28);
        timerBox.AddChild(roundLabel);

        overtime = UiKit.Text("PROLONGATION", 30, UiKit.Yellow, 8, HorizontalAlignment.Center);
        overtime.Position = new Vector2(200, 104);
        overtime.Size = new Vector2(300, 40);
        overtime.PivotOffset = overtime.Size / 2;
        overtime.Visible = false;
        board.AddChild(overtime);
    }

    /// <summary>Pastille ronde de manche gagnée (pleine) ou à jouer (creuse).</summary>
    sealed partial class Pip : Control
    {
        public Color Color;
        public bool On;
        public override void _Draw()
        {
            var c = Size / 2;
            DrawCircle(c + new Vector2(0, 2), 10f, new Color(0, 0, 0, 0.45f));
            DrawCircle(c, 10f, UiKit.Navy);
            DrawCircle(c, 7f, On ? Color : new Color(1, 1, 1, 0.15f));
            if (On) DrawCircle(c + new Vector2(-2.5f, -2.5f), 2.2f, new Color(1, 1, 1, 0.7f));
        }
    }

    void UpdateScoreBoard(MatchView.Snapshot s)
    {
        for (int t = 0; t < 2; t++)
        {
            int kos = s.Kos(t);
            if (koLabels[t].Text != kos.ToString())
            {
                koLabels[t].Text = kos.ToString();
                if (age > 0.5) { UiKit.Punch(koLabels[t], 1.9f, 0.35); Flash(teamBoxes[t]); }
            }
            for (int i = 0; i < 5; i++)
            {
                var pip = (Pip)roundPips[t][i];
                bool on = i < s.Rounds(t);
                if (pip.On == on) continue;
                pip.On = on;
                pip.QueueRedraw();
                if (on && age > 0.5) UiKit.Punch(pip, 2.4f, 0.4);
            }
        }
        // Entre deux manches le chrono reste figé sur sa dernière valeur (l'hôte l'envoie à 0).
        bool live = s.Phase is MatchView.Phase.Playing or MatchView.Phase.Countdown;
        int sec = live ? s.Seconds : Mathf.Max(0, lastSecond);
        timer.Text = Clock(sec);
        roundLabel.Text = s.Round > s.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {Mathf.Max(1, s.Round)}/{s.MaxRounds}";
        bool urgent = s.Phase == MatchView.Phase.Playing && sec <= 10;
        if (urgent && sec != lastSecond)
        {
            UiKit.Punch(timer, 1.35f, 0.25);
            if (sec <= 5 && sec > 0) Sfx.Play(this, SfxKind.ChargeTick, null, 1.2f, -4f);
        }
        lastSecond = sec;
        float pulse = urgent ? 0.5f + 0.5f * Mathf.Sin((float)age * 10f) : 0f;
        timer.AddThemeColorOverride("font_color", urgent ? Colors.White.Lerp(Red, 0.6f + 0.4f * pulse) : Colors.White);
        timerStyle.BgColor = urgent ? UiKit.Navy.Lerp(new Color("5a0d1c"), pulse) : UiKit.Navy;
        bool ot = s.Overtime && s.Phase is MatchView.Phase.Playing or MatchView.Phase.Countdown;
        if (ot && !overtime.Visible) UiKit.Punch(overtime, 2f, 0.35);
        overtime.Visible = ot;
        if (ot) overtime.Scale = Vector2.One * (1f + 0.06f * Mathf.Sin((float)age * 8f));
    }

    void Flash(Control c)
    {
        c.Modulate = new Color(2f, 2f, 2f);
        var tw = c.CreateTween();
        tw.TweenProperty(c, "modulate", Colors.White, 0.35);
    }

    // ------------------------------------------------------------------ cœurs (bas gauche)

    readonly List<Heart> hearts = new();
    int lastLives;
    Label livesCaption = null!;

    sealed partial class Heart : Control
    {
        public bool Full = true;
        public float Shake;
        static Vector2[]? shape;
        static Vector2[] Shape()
        {
            if (shape != null) return shape;
            var pts = new List<Vector2>();
            for (int i = 0; i < 48; i++)
            {
                float t = Mathf.Tau * i / 48f;
                float x = 16f * Mathf.Pow(Mathf.Sin(t), 3);
                float y = 13f * Mathf.Cos(t) - 5f * Mathf.Cos(2 * t) - 2f * Mathf.Cos(3 * t) - Mathf.Cos(4 * t);
                pts.Add(new Vector2(x, -y) / 17f);
            }
            return shape = pts.ToArray();
        }
        Vector2[] At(Vector2 c, float r)
        {
            var src = Shape();
            var pts = new Vector2[src.Length];
            for (int i = 0; i < src.Length; i++) pts[i] = c + src[i] * r;
            return pts;
        }
        public override void _Draw()
        {
            var c = Size / 2 + new Vector2(Mathf.Sin(Shake * 60f) * Shake * 14f, 0);
            float r = Size.X * 0.45f;
            DrawColoredPolygon(At(c + new Vector2(0, 4), r + 5), new Color(0, 0, 0, 0.4f));
            DrawColoredPolygon(At(c, r + 5), UiKit.Navy);
            DrawColoredPolygon(At(c, r), Full ? new Color("ff3d5a") : new Color(0.25f, 0.28f, 0.4f));
            if (Full) DrawColoredPolygon(At(c + new Vector2(-r * 0.32f, -r * 0.3f), r * 0.28f), new Color(1, 1, 1, 0.55f));
        }
    }

    void BuildHearts()
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        holder.OffsetLeft = 36; holder.OffsetTop = -150; holder.OffsetRight = 400; holder.OffsetBottom = -30;
        root.AddChild(holder);
        var back = MakePanel(new Color(UiKit.Navy, 0.55f), -0.15f, 18);
        back.Position = new Vector2(0, 30);
        back.Size = new Vector2(70 + 92 * View.MaxLives, 90);
        holder.AddChild(back);
        livesCaption = UiKit.Text("VIES", 22, UiKit.Sky, 6);
        livesCaption.Position = new Vector2(18, 0);
        holder.AddChild(livesCaption);
        for (int i = 0; i < View.MaxLives; i++)
        {
            var h = new Heart { MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(84, 84), Position = new Vector2(30 + i * 92, 32) };
            h.PivotOffset = h.Size / 2;
            holder.AddChild(h);
            hearts.Add(h);
        }
    }

    void UpdateHearts(int n, double delta)
    {
        for (int i = 0; i < hearts.Count; i++)
        {
            var h = hearts[i];
            bool full = i < n;
            if (h.Full != full)
            {
                h.Full = full;
                var tw = h.CreateTween();
                if (!full)
                {
                    // Cœur perdu : il gonfle, tremble et retombe (rebond).
                    h.Shake = 1f;
                    h.Scale = Vector2.One * 1.7f;
                    tw.TweenProperty(h, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
                }
                else
                {
                    h.Scale = Vector2.One * 0.3f;
                    tw.TweenProperty(h, "scale", Vector2.One, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
                }
            }
            // Dernier cœur : il bat.
            if (full && n == 1 && h.Shake <= 0) h.Scale = Vector2.One * (1f + 0.08f * Mathf.Max(0, Mathf.Sin((float)age * 7f)));
            if (h.Shake > 0) h.Shake = Mathf.Max(0, h.Shake - (float)delta * 2.5f);
            h.QueueRedraw();
        }
    }

    // ------------------------------------------------------------------ balle tenue, attrape, dash (bas centre)

    BallSlot ballSlot = null!;
    RoundGauge catchGauge = null!, dashGauge = null!;

    /// <summary>Emplacement de la balle tenue : vide (cercle pointillé) ou balle colorée avec son palier.</summary>
    sealed partial class BallSlot : Control
    {
        public bool Holding;
        public Color BallColor = Palette.BallCore;
        public int Tier;
        public float Pop;
        public override void _Draw()
        {
            var c = Size / 2;
            float r = 46f;
            DrawCircle(c + new Vector2(0, 4), r + 6, new Color(0, 0, 0, 0.35f));
            DrawCircle(c, r + 6, new Color(UiKit.Navy, 0.8f));
            if (!Holding)
            {
                for (int i = 0; i < 16; i++)
                {
                    float a = Mathf.Tau * i / 16f;
                    DrawArc(c, r - 6, a, a + Mathf.Tau / 32f, 4, new Color(1, 1, 1, 0.3f), 4f, true);
                }
                return;
            }
            float rr = r - 6 + Pop * 8f;
            DrawCircle(c, rr, BallColor);
            DrawArc(c, rr * 0.72f, -2.4f, -0.7f, 16, new Color(1, 1, 1, 0.35f), 5f, true);
            DrawCircle(c + new Vector2(-rr * 0.35f, -rr * 0.38f), rr * 0.18f, new Color(1, 1, 1, 0.6f));
            if (Tier > 0)
            {
                var col = BallTierColors[Mathf.Clamp(Tier, 0, 3)];
                DrawArc(c, r + 2, 0, Mathf.Tau, 48, col, 6f, true);
                for (int i = 0; i < Tier; i++)   // pastilles de palier au-dessus de la balle
                {
                    var p = c + new Vector2((i - (Tier - 1) / 2f) * 20f, -r - 16);
                    DrawCircle(p, 8f, UiKit.Navy);
                    DrawCircle(p, 6f, col);
                }
            }
        }
    }

    /// <summary>Jauge ronde (attrape, dash) : pictogramme, remplissage en camembert pendant la recharge, charges en pastilles.</summary>
    sealed partial class RoundGauge : Control
    {
        public float Fill = 1f;
        public Color Color = UiKit.Green;
        public bool Dash;
        public int Charges, MaxCharges;
        public float Glow;
        public override void _Draw()
        {
            var c = new Vector2(Size.X / 2, 40);
            float r = 34f;
            DrawCircle(c + new Vector2(0, 3), r + 5, new Color(0, 0, 0, 0.35f));
            DrawCircle(c, r + 5, UiKit.Navy);
            DrawCircle(c, r, new Color(0.18f, 0.22f, 0.36f));
            if (Fill > 0.001f)
            {
                var pts = new List<Vector2> { c };
                int n = Mathf.Max(2, (int)(40 * Fill));
                for (int i = 0; i <= n; i++) pts.Add(c + Vector2.FromAngle(-Mathf.Pi / 2 + Mathf.Tau * Fill * i / n) * r);
                DrawColoredPolygon(pts.ToArray(), new Color(Color, Fill >= 1f ? 1f : 0.55f));
            }
            if (Glow > 0) DrawArc(c, r + 9, 0, Mathf.Tau, 40, new Color(Color, Glow), 5f, true);
            var ink = UiKit.Navy;
            if (Dash)
            {
                for (int k = 0; k < 2; k++)   // deux chevrons : élan
                {
                    float x = -9 + k * 14;
                    DrawPolyline(new[] { c + new Vector2(x - 6, -12), c + new Vector2(x + 6, 0), c + new Vector2(x - 6, 12) }, ink, 6f, true);
                }
                for (int i = 0; i < MaxCharges; i++)
                {
                    var p = c + new Vector2((i - (MaxCharges - 1) / 2f) * 20f, r + 16);
                    DrawCircle(p, 8f, UiKit.Navy);
                    DrawCircle(p, 6f, i < Charges ? Color : new Color(1, 1, 1, 0.2f));
                }
            }
            else
            {
                // Main ouverte stylisée : paume et trois doigts, une balle au-dessus.
                DrawArc(c + new Vector2(0, 2), 13f, 0.1f, Mathf.Pi - 0.1f, 16, ink, 6f, true);
                DrawLine(c + new Vector2(-13, 2), c + new Vector2(-15, -10), ink, 5f, true);
                DrawLine(c + new Vector2(13, 2), c + new Vector2(15, -10), ink, 5f, true);
                DrawCircle(c + new Vector2(0, -8), 7f, ink);
            }
        }
    }

    Label catchKey = null!, dashKey = null!;

    void BuildBottomCenter()
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        holder.OffsetLeft = -230; holder.OffsetRight = 230; holder.OffsetTop = -170; holder.OffsetBottom = -20;
        root.AddChild(holder);
        catchGauge = new RoundGauge { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(10, 40), Size = new Vector2(110, 110) };
        catchGauge.PivotOffset = new Vector2(55, 40);
        holder.AddChild(catchGauge);
        ballSlot = new BallSlot { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(170, 10), Size = new Vector2(120, 120) };
        ballSlot.PivotOffset = ballSlot.Size / 2;
        holder.AddChild(ballSlot);
        dashGauge = new RoundGauge { Dash = true, MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(340, 40), Size = new Vector2(110, 110) };
        dashGauge.PivotOffset = new Vector2(55, 40);
        holder.AddChild(dashGauge);
        catchKey = KeyCap(holder, new Vector2(10, 124));
        dashKey = KeyCap(holder, new Vector2(340, 124));
    }

    /// <summary>Petite touche de clavier (nom de la touche réassignable) sous une jauge.</summary>
    Label KeyCap(Control parent, Vector2 pos)
    {
        var l = UiKit.Text("", 18, UiKit.Navy, 0, HorizontalAlignment.Center);
        l.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = UiKit.Cream, CornerRadiusTopLeft = 6, CornerRadiusTopRight = 6, CornerRadiusBottomLeft = 6, CornerRadiusBottomRight = 6,
            BorderWidthBottom = 4, BorderColor = new Color("b9ab92"), ContentMarginLeft = 8, ContentMarginRight = 8, ContentMarginTop = 0, ContentMarginBottom = 0,
        });
        l.Position = pos;
        l.Size = new Vector2(110, 28);
        parent.AddChild(l);
        return l;
    }

    int lastCatchState, lastDashCharges = -1, lastTier;
    bool lastHolding;

    void UpdateBottomCenter()
    {
        var ball = Player.HeldBall;
        bool holding = ball != null;
        if (holding != lastHolding)
        {
            ballSlot.Scale = Vector2.One * (holding ? 1.3f : 0.85f);
            ballSlot.CreateTween().TweenProperty(ballSlot, "scale", Vector2.One, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        }
        lastHolding = holding;
        ballSlot.Holding = holding;
        ballSlot.Tier = ball?.Charge ?? 0;
        if (ballSlot.Tier > lastTier && holding) UiKit.Punch(ballSlot, 1.35f, 0.3);
        lastTier = ballSlot.Tier;
        ballSlot.BallColor = ball?.Kind switch { Ball.KindBomb => new Color(1f, 0.8f, 0.1f), Ball.KindBounce => new Color(0.7f, 0.4f, 1f), _ => Palette.BallCore };
        ballSlot.Pop = Player.ThrowCharge01 * (0.5f + 0.5f * Mathf.Sin((float)age * 30f));
        ballSlot.QueueRedraw();

        // Attrape : verte prête, jaune pendant la fenêtre, grise qui se remplit pendant la recharge ; éteinte balle en main.
        int cs = Player.CatchState;
        catchGauge.Fill = holding ? 0f : Player.CatchCooldown01;
        catchGauge.Color = cs == 1 ? UiKit.Yellow : cs == 2 ? new Color(0.6f, 0.63f, 0.72f) : UiKit.Green;
        catchGauge.Glow = cs == 1 ? 0.9f : Mathf.Max(0, catchGauge.Glow - 0.05f);
        if (cs == 1 && lastCatchState != 1) UiKit.Punch(catchGauge, 1.25f, 0.2);
        if (cs == 0 && lastCatchState == 2) UiKit.Punch(catchGauge, 1.15f, 0.2);   // de nouveau prête
        lastCatchState = cs;
        catchGauge.Modulate = new Color(1, 1, 1, holding ? 0.45f : 1f);
        catchGauge.QueueRedraw();

        int charges = Player.DashCharges;
        dashGauge.Charges = charges;
        dashGauge.MaxCharges = Player.DashMax;
        dashGauge.Fill = charges >= 1 ? 1f : Player.DashRecharge01;
        dashGauge.Color = charges >= 1 ? UiKit.Sky : new Color(0.6f, 0.63f, 0.72f);
        dashGauge.Glow = Player.Dashing ? 0.9f : Mathf.Max(0, dashGauge.Glow - 0.05f);
        if (lastDashCharges >= 0 && charges > lastDashCharges) UiKit.Punch(dashGauge, 1.2f, 0.2);
        lastDashCharges = charges;
        dashGauge.QueueRedraw();

        catchKey.Text = InputSetup.KeyName("catch");
        dashKey.Text = InputSetup.KeyName("dash");
    }

    // ------------------------------------------------------------------ viseur, charge en arc, marqueur de touche

    Crosshair crosshair = null!;
    double hitMarkerLeft;
    bool hitMarkerKill;
    float chargeShown;

    sealed partial class Crosshair : Control
    {
        public DesktopHud Hud = null!;

        /// <summary>Jeu libre : une balle ennemie rapide hors de l'écran (derrière, sur le côté) est signalée par une flèche au bord, jusqu'à 25 m.</summary>
        void DrawBallArrows(Vector2 c)
        {
            if (!Hud.Player.FreeRoam) return;
            var cam = GetViewport().GetCamera3D();
            if (cam == null) return;
            var view = Size;
            var xf = GetViewport().GetFinalTransform().AffineInverse();   // pixels de la fenêtre -> coordonnées du HUD (mise à l'échelle)
            foreach (var node in GetTree().GetNodesInGroup("balls"))
            {
                if (node is not Ball { IsHeld: false } b || b.OwnerTeam < 0 || b.OwnerTeam == Hud.Player.Hurtbox.Team || b.IsPass || b.LinearVelocity.Length() < 12f) continue;
                if (b.GlobalPosition.DistanceTo(Hud.Player.Hurtbox.GlobalPosition) > 25f) continue;
                bool behind = cam.IsPositionBehind(b.GlobalPosition);
                var p = xf * cam.UnprojectPosition(b.GlobalPosition);
                if (!behind && p.X > 0 && p.Y > 0 && p.X < view.X && p.Y < view.Y) continue;   // visible : pas besoin de flèche
                var dir = (p - c).Normalized();
                if (behind) dir = -dir;
                float m = 70f;
                float k = Mathf.Min(Mathf.Abs(dir.X) > 1e-3f ? (view.X * 0.5f - m) / Mathf.Abs(dir.X) : 1e9f, Mathf.Abs(dir.Y) > 1e-3f ? (view.Y * 0.5f - m) / Mathf.Abs(dir.Y) : 1e9f);
                var at = c + dir * k;
                var perp = new Vector2(-dir.Y, dir.X);
                var tri = new[] { at + dir * 28f, at - dir * 14f + perp * 20f, at - dir * 14f - perp * 20f };
                var outline = new[] { at + dir * 36f, at - dir * 19f + perp * 27f, at - dir * 19f - perp * 27f };
                DrawColoredPolygon(outline, UiKit.Navy);
                DrawColoredPolygon(tri, new Color(Palette.Team(b.OwnerTeam), 0.95f));
            }
        }

        public override void _Draw()
        {
            var c = Size * 0.5f;
            var p = Hud.Player;
            var col = p.AimingAtEnemy ? Red : new Color(1, 1, 1, 0.95f);
            var shadow = new Color(UiKit.Navy, 0.7f);
            float charge = Hud.chargeShown;
            float spread = 10f + charge * 18f;      // le viseur s'ouvre quand on arme un lancer
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var a = c + new Vector2(dx, dy) * spread;
                var b = c + new Vector2(dx, dy) * (spread + 14f);
                DrawLine(a, b, shadow, 7f);
                DrawLine(a, b, col, 3.5f);
            }
            DrawCircle(c, 4.5f, shadow);
            DrawCircle(c, 3f, col);

            // Charge du lancer : trois arcs autour du viseur (un par palier de son), qui se remplissent.
            if (charge > 0.01f || p.HeldBall != null)
            {
                float r = 54f, gap = 0.14f, seg = (Mathf.Tau * 0.75f - 2 * gap) / 3f, start = Mathf.Pi * 0.75f;
                float alpha = p.HeldBall != null ? Mathf.Max(0.35f, Mathf.Min(1f, charge * 4f + 0.35f)) : charge;
                for (int i = 0; i < 3; i++)
                {
                    float a0 = start + i * (seg + gap);
                    DrawArc(c, r, a0, a0 + seg, 24, new Color(UiKit.Navy, 0.55f * alpha), 12f, true);
                    float f = Mathf.Clamp(charge * 3f - i, 0f, 1f);
                    if (f > 0) DrawArc(c, r, a0, a0 + seg * f, 24, new Color(ChargeColors[i], alpha), 7f, true);
                }
                if (charge >= 0.999f)   // pleine charge : halo qui pulse
                {
                    float pulse = 0.5f + 0.5f * Mathf.Sin((float)Hud.age * 18f);
                    DrawArc(c, r + 10f, start, start + Mathf.Tau * 0.75f, 48, new Color(UiKit.Pink, 0.35f + 0.4f * pulse), 4f, true);
                }
            }

            DrawBallArrows(c);
            if (Hud.hitMarkerLeft > 0)
            {
                // Marqueur de touche : quatre traits en X qui s'écartent ; rouge et plus grand pour un KO.
                float t = (float)(1 - Hud.hitMarkerLeft / 0.35);
                var mc = Hud.hitMarkerKill ? Red : Colors.White;
                float grow = Hud.hitMarkerKill ? 1.5f : 1f;
                float a0 = (16f + 8f * t) * grow, a1 = (32f + 8f * t) * grow;
                var alpha = new Color(mc, 1f - t * t);
                foreach (var (dx, dy) in new[] { (1, 1), (-1, 1), (1, -1), (-1, -1) })
                {
                    var d = new Vector2(dx, dy).Normalized();
                    DrawLine(c + d * a0, c + d * a1, new Color(UiKit.Navy, alpha.A), 9f);
                    DrawLine(c + d * a0, c + d * a1, alpha, 5f);
                }
            }
        }
    }

    void OnHit(int shooter, int target, bool kill)
    {
        if (shooter != View.LocalId) return;
        if (GameSettings.HitMarkers) { hitMarkerLeft = 0.35; hitMarkerKill = kill; }
        Sfx.Play(this, kill ? SfxKind.KoConfirm : SfxKind.Click, null, kill ? 1f : 1.15f);
        if (kill) ShowCenterPop("K.O. !", Red, 96);
    }

    // ------------------------------------------------------------------ fil des KO (haut droite)

    VBoxContainer feed = null!;

    void BuildFeed()
    {
        feed = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.Begin };
        feed.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        feed.OffsetLeft = -560; feed.OffsetRight = -28; feed.OffsetTop = 24; feed.OffsetBottom = 400;
        feed.GrowHorizontal = Control.GrowDirection.Begin;
        feed.AddThemeConstantOverride("separation", 8);
        root.AddChild(feed);
    }

    void OnKo(int by, int victim)
    {
        bool mine = by == View.LocalId || victim == View.LocalId;
        var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        row.AddThemeConstantOverride("separation", 0);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Box(new Color(UiKit.Navy, 0.82f), 10, -0.15f, mine ? 3 : 0, UiKit.Yellow, 3);
        box.ContentMarginLeft = 18; box.ContentMarginRight = 18; box.ContentMarginTop = 4; box.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 12);
        panel.AddChild(line);
        if (by >= 0)
        {
            line.AddChild(UiKit.Text(View.NameOf(by), 28, Palette.Team(View.TeamOf(by)).Lightened(0.1f), 6));
            line.AddChild(new FeedBall { CustomMinimumSize = new Vector2(54, 36), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        else line.AddChild(UiKit.Text("K.O.", 28, UiKit.Yellow, 6));
        line.AddChild(UiKit.Text(View.NameOf(victim), 28, Palette.Team(View.TeamOf(victim)).Lightened(0.1f), 6));
        row.AddChild(panel);
        feed.AddChild(row);
        while (feed.GetChildCount() > 5) feed.GetChild(0).QueueFree();

        // Entrée par la droite, disparition après 4 s.
        panel.Modulate = new Color(1, 1, 1, 0);
        var tw = panel.CreateTween();
        tw.TweenProperty(panel, "modulate:a", 1.0, 0.15);
        tw.TweenInterval(4.0);
        tw.TweenProperty(panel, "modulate:a", 0.0, 0.4);
        tw.TweenCallback(Callable.From(row.QueueFree));
        UiKit.SlideIn(panel, 0f, 80f);
    }

    /// <summary>Petite balle lancée (pictogramme du fil des KO).</summary>
    sealed partial class FeedBall : Control
    {
        public override void _Draw()
        {
            var c = new Vector2(Size.X - 14, Size.Y / 2);
            for (int i = 0; i < 3; i++) DrawLine(c + new Vector2(-40 + i * 4, -7 + i * 7), c + new Vector2(-16, -7 + i * 7), new Color(1, 1, 1, 0.55f), 3f);
            DrawCircle(c, 12f, UiKit.Navy);
            DrawCircle(c, 9f, Palette.BallCore);
        }
    }

    // ------------------------------------------------------------------ vignette de dégâts et lignes de vitesse

    Vignette vignette = null!;
    SpeedLines speedLines = null!;

    /// <summary>Bords rouges : plus forts du côté d'où vient le coup (direction en repère écran, nulle = tout le bord).</summary>
    sealed partial class Vignette : Control
    {
        public float Strength;
        public Vector2 From;
        public override void _Draw()
        {
            if (Strength <= 0.01f) return;
            var s = Size;
            float w = Mathf.Min(s.X, s.Y) * 0.28f;
            void Edge(Vector2 a, Vector2 b, Vector2 inward, Vector2 normal)
            {
                float k = From == Vector2.Zero ? 0.75f : 0.18f + 0.95f * Mathf.Max(0, normal.Dot(From));
                var outer = new Color(0.85f, 0.05f, 0.1f, Mathf.Min(0.85f, Strength * k));
                var inner = new Color(0.85f, 0.05f, 0.1f, 0f);
                DrawPolygon(new[] { a, b, b + inward * w, a + inward * w }, new[] { outer, outer, inner, inner });
            }
            Edge(Vector2.Zero, new Vector2(s.X, 0), Vector2.Down, Vector2.Up);
            Edge(new Vector2(0, s.Y), s, Vector2.Up, Vector2.Down);
            Edge(Vector2.Zero, new Vector2(0, s.Y), Vector2.Right, Vector2.Left);
            Edge(new Vector2(s.X, 0), s, Vector2.Left, Vector2.Right);
        }
    }

    /// <summary>Lignes de vitesse sur les bords pendant un dash.</summary>
    sealed partial class SpeedLines : Control
    {
        public DesktopHud Hud = null!;
        public float Strength;
        readonly RandomNumberGenerator rng = new();
        public override void _Draw()
        {
            if (Strength <= 0.01f) return;
            var c = Size / 2;
            float diag = c.Length();
            for (int i = 0; i < 34; i++)
            {
                float a = rng.RandfRange(0, Mathf.Tau);
                var d = Vector2.FromAngle(a);
                float r0 = diag * rng.RandfRange(0.62f, 0.8f);
                float len = diag * rng.RandfRange(0.12f, 0.3f);
                DrawLine(c + d * r0, c + d * (r0 + len), new Color(1, 1, 1, 0.5f * Strength), rng.RandfRange(2f, 6f));
            }
        }
    }

    void HurtFrom()
    {
        // D'où vient le coup : le tireur connu (hôte), sinon la balle libre la plus proche, sinon tout le bord.
        vignette.From = Vector2.Zero;
        var cam = GetViewport().GetCamera3D();
        if (cam == null) return;
        Vector3? src = null;
        int shooter = View.RecentShooterOf(View.LocalId);
        if (shooter >= 0)
            foreach (var n in GetTree().GetNodesInGroup("hurtboxes"))
                if (n is Hurtbox h && h.PlayerId == shooter) src = h.GlobalPosition;
        if (src == null)
        {
            float best = 4f;
            var me = Player.Hurtbox.GlobalPosition;
            foreach (var n in GetTree().GetNodesInGroup("balls"))
                if (n is Ball { IsHeld: false } b && b.GlobalPosition.DistanceTo(me) < best)
                {
                    best = b.GlobalPosition.DistanceTo(me);
                    var v = b.LinearVelocity;
                    src = v.LengthSquared() > 1f ? b.GlobalPosition - v.Normalized() * 3f : b.GlobalPosition;
                }
        }
        if (src is not Vector3 s) return;
        var local = cam.GlobalTransform.Basis.Inverse() * (s - cam.GlobalPosition);
        var dir = new Vector2(local.X, local.Z);   // x à droite, z vers l'arrière = bas de l'écran
        if (dir.LengthSquared() > 0.01f) vignette.From = dir.Normalized();
    }

    // ------------------------------------------------------------------ textes centraux : compte à rebours, bandeaux, annonces

    Label countdown = null!, countdownRound = null!, notice = null!, toast = null!, koTitle = null!, koSub = null!, resume = null!, pop = null!;
    Control bannerBox = null!;
    Panel bannerBand = null!;
    Label bannerText = null!, bannerSub = null!;
    string lastNotice = "", lastToast = "";
    double koSince = -1;

    Label CenterLabel(string text, int size, Color color, int outline, float y, float h)
    {
        var l = UiKit.Text(text, size, color, outline, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        l.OffsetLeft = -700; l.OffsetRight = 700; l.OffsetTop = y; l.OffsetBottom = y + h;
        l.PivotOffset = new Vector2(700, h / 2);
        root.AddChild(l);
        return l;
    }

    void BuildCenterTexts()
    {
        countdownRound = CenterLabel("", 54, UiKit.Sky, 12, 300, 70);
        countdown = CenterLabel("", 220, UiKit.Yellow, 26, 360, 260);
        notice = CenterLabel("", 34, UiKit.Yellow, 10, 190, 50);
        notice.AddThemeStyleboxOverride("normal", UiKit.Box(new Color(UiKit.Navy, 0.8f), 12, -0.12f, 0, null, 4));
        notice.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        notice.GrowHorizontal = Control.GrowDirection.Both;
        notice.OffsetLeft = -360; notice.OffsetRight = 360;
        notice.PivotOffset = new Vector2(360, 25);
        toast = CenterLabel("", 34, Colors.White, 10, 250, 50);
        pop = CenterLabel("", 96, Red, 18, 600, 120);
        koTitle = CenterLabel("K.O. !", 150, UiKit.Pink, 24, 330, 180);
        koSub = CenterLabel("", 40, Colors.White, 10, 500, 60);
        resume = CenterLabel("Souris libérée : clique pour reprendre", 34, Colors.White, 10, 700, 60);
        foreach (var l in new[] { countdown, countdownRound, notice, toast, pop, koTitle, koSub }) l.Visible = false;

        // Bandeau de fin de manche : bande inclinée de la couleur de l'équipe, qui traverse l'écran.
        bannerBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        bannerBox.SetAnchorsPreset(Control.LayoutPreset.HcenterWide);
        bannerBox.OffsetTop = -110; bannerBox.OffsetBottom = 110;
        root.AddChild(bannerBox);
        bannerBand = MakePanel(UiKit.Orange, -0.25f, 0, 6, UiKit.Navy);
        bannerBand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bannerBand.OffsetLeft = -80; bannerBand.OffsetRight = 80; bannerBand.OffsetTop = 20; bannerBand.OffsetBottom = -20;
        bannerBox.AddChild(bannerBand);
        bannerText = UiKit.Text("", 84, Colors.White, 16, HorizontalAlignment.Center);
        bannerText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bannerText.OffsetTop = 18; bannerText.OffsetBottom = -70;
        bannerText.VerticalAlignment = VerticalAlignment.Center;
        bannerBox.AddChild(bannerText);
        bannerSub = UiKit.Text("", 36, UiKit.Cream, 8, HorizontalAlignment.Center);
        bannerSub.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bannerSub.OffsetTop = 140; bannerSub.OffsetBottom = -24;
        bannerSub.VerticalAlignment = VerticalAlignment.Center;
        bannerBox.AddChild(bannerSub);
    }

    void ShowCenterPop(string text, Color color, int size)
    {
        pop.Text = text;
        pop.AddThemeColorOverride("font_color", color);
        pop.AddThemeFontSizeOverride("font_size", size);
        pop.Visible = true;
        pop.Modulate = Colors.White;
        UiKit.Punch(pop, 2.2f, 0.3);
        var tw = pop.CreateTween();
        tw.TweenInterval(0.6);
        tw.TweenProperty(pop, "modulate:a", 0.0, 0.3);
    }

    void ShowCountdown(string text, bool go)
    {
        countdown.Text = text;
        countdown.Visible = true;
        countdown.Modulate = Colors.White;
        countdown.AddThemeColorOverride("font_color", go ? UiKit.Green : UiKit.Yellow);
        UiKit.Punch(countdown, go ? 2.6f : 2.1f, 0.32);
        var tw = countdown.CreateTween();
        tw.TweenInterval(go ? 0.5 : 0.62);
        tw.TweenProperty(countdown, "modulate:a", 0.0, go ? 0.35 : 0.25);
        Sfx.Play(this, go ? SfxKind.Go : SfxKind.Countdown);
        if (go)
        {
            var tw2 = countdownRound.CreateTween();
            tw2.TweenInterval(0.4);
            tw2.TweenProperty(countdownRound, "modulate:a", 0.0, 0.3);
            tw2.TweenCallback(Callable.From(() => countdownRound.Visible = false));
        }
    }

    void ShowBanner(MatchView.Snapshot s, bool matchOver)
    {
        int w = s.Winner;
        if (matchOver) bannerText.Text = w == View.LocalTeam ? "VICTOIRE !" : "DÉFAITE";
        else bannerText.Text = w < 0 ? "MANCHE NULLE" : $"MANCHE POUR L'ÉQUIPE {MatchController.TeamName(w)}";
        bannerSub.Text = $"Manches   {s.Rounds0}  -  {s.Rounds1}";
        var color = w < 0 ? UiKit.Grey : w == 0 ? UiKit.Orange : UiKit.Blue;
        bannerBand.AddThemeStyleboxOverride("panel", UiKit.Box(color, 0, -0.25f, 6, UiKit.Navy, 6));
        bannerBox.Visible = true;
        bannerBox.Modulate = Colors.White;
        // Entrée : la bande glisse de la gauche, le texte tape ; sortie en fondu.
        bannerBand.Position = bannerBand.Position with { X = -2400 };
        var tw = bannerBox.CreateTween();
        tw.TweenProperty(bannerBand, "position:x", -80.0, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(matchOver ? 1.4 : 2.2);
        tw.TweenProperty(bannerBox, "modulate:a", 0.0, 0.35);
        tw.TweenCallback(Callable.From(() => bannerBox.Visible = false));
        bannerText.PivotOffset = new Vector2(GetViewport().GetVisibleRect().Size.X / 2, 50);
        UiKit.Punch(bannerText, 1.8f, 0.35);
        Sfx.Play(this, SfxKind.Whistle);
    }

    void UpdateCenter(MatchView.Snapshot s, int lives)
    {
        // Compte à rebours de début de manche : « 3, 2, 1 », puis « GO ! » au passage en jeu.
        if (s.Phase == MatchView.Phase.Countdown && (prev.Phase != MatchView.Phase.Countdown || s.Countdown != prev.Countdown) && s.Countdown > 0)
        {
            if (prev.Phase != MatchView.Phase.Countdown)
            {
                countdownRound.Text = s.Overtime ? "PROLONGATION" : s.Round > s.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {s.Round}";
                countdownRound.Visible = true;
                countdownRound.Modulate = Colors.White;
                UiKit.SlideIn(countdownRound, 0f, -120f);
            }
            ShowCountdown(s.Countdown.ToString(), false);
        }
        if (prev.Phase == MatchView.Phase.Countdown && s.Phase == MatchView.Phase.Playing) ShowCountdown("GO !", true);
        if (s.Phase == MatchView.Phase.RoundOver && prev.Phase != MatchView.Phase.RoundOver) ShowBanner(s, false);
        if (s.Phase == MatchView.Phase.MatchOver && prev.Phase != MatchView.Phase.MatchOver) ShowBanner(s, true);
        if (s.Overtime && !prev.Overtime && s.Phase == MatchView.Phase.Playing) { ShowCenterPop("MORT SUBITE !", UiKit.Yellow, 90); Sfx.Play(this, SfxKind.Whistle, null, 0.8f); }

        // Annonces (balle spéciale, échange de plaques) et messages du joueur.
        if (s.Notice != lastNotice)
        {
            lastNotice = s.Notice;
            notice.Text = s.Notice;
            notice.Visible = s.Notice != "";
            if (notice.Visible) { UiKit.Punch(notice, 1.5f, 0.3); Sfx.Play(this, SfxKind.Alert, null, 0.8f, -6f); }
        }
        if (notice.Visible) notice.Modulate = new Color(1, 1, 1, 0.75f + 0.25f * Mathf.Sin((float)age * 8f));
        string t = Player.Time01 < Player.ToastUntil ? Player.Toast : "";
        if (t != lastToast)
        {
            lastToast = t;
            toast.Text = t;
            toast.Visible = t != "";
            if (toast.Visible) UiKit.Punch(toast, 1.4f, 0.25);
        }

        // Joueur KO : grand titre et retour dans n secondes (l'écran est grisé par Feedback).
        bool ko = lives <= 0 && s.Phase is MatchView.Phase.Playing or MatchView.Phase.Countdown;
        if (ko && koSince < 0) { koSince = age; UiKit.Punch(koTitle, 2.4f, 0.35); }
        if (!ko) koSince = -1;
        koTitle.Visible = koSub.Visible = ko;
        if (ko)
        {
            int left = Mathf.CeilToInt(3f - (float)(age - koSince));
            koSub.Text = left > 0 ? $"Retour dans {left}" : "Retour imminent…";
        }

        resume.Visible = Input.MouseMode != Input.MouseModeEnum.Captured && !Player.Autoplay && !PauseMenu.IsOpen && !EndScreen.IsOpen;
    }

    // ------------------------------------------------------------------ aide des touches (F1, bas droite)

    PanelContainer help = null!;
    bool helpShown = true;
    double helpHideAt = 8;

    void BuildHelp()
    {
        help = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Box(new Color(UiKit.Navy, 0.72f), 14, 0f, 0, null, 4);
        box.ContentMarginLeft = 18; box.ContentMarginRight = 18; box.ContentMarginTop = 10; box.ContentMarginBottom = 10;
        help.AddThemeStyleboxOverride("panel", box);
        help.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        help.GrowHorizontal = Control.GrowDirection.Begin;
        help.GrowVertical = Control.GrowDirection.Begin;
        help.OffsetRight = -28; help.OffsetBottom = -28; help.OffsetLeft = -28; help.OffsetTop = -28;
        root.AddChild(help);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 2);
        help.AddChild(col);
        col.AddChild(UiKit.Text($"COMMANDES   ({InputSetup.KeyName("help")})", 20, UiKit.Yellow, 6));
        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 16);
        grid.AddThemeConstantOverride("v_separation", 0);
        col.AddChild(grid);
        string move = InputSetup.KeyName("move_forward") + InputSetup.KeyName("move_left") + InputSetup.KeyName("move_back") + InputSetup.KeyName("move_right");
        void Row(string key, string label)
        {
            grid.AddChild(UiKit.Text(key, 18, UiKit.Yellow, 5, HorizontalAlignment.Right));
            grid.AddChild(UiKit.Text(label, 18, Colors.White, 5));
        }
        Row(move, "Se déplacer");
        foreach (var a in InputSetup.Actions)
        {
            if (a.Name.StartsWith("move_") || a.Name == "help") continue;
            if (!Player.FreeRoam && a.Name is "pass" or "lob" or "feint" or "curve") continue;   // coups du jeu libre seulement
            Row(InputSetup.KeyName(a.Name), a.Label);
        }
    }

    void SetHelp(bool show)
    {
        if (helpShown == show) return;
        helpShown = show;
        var tw = help.CreateTween();
        tw.SetParallel();
        tw.TweenProperty(help, "modulate:a", show ? 1.0 : 0.0, 0.25);
        help.Scale = Vector2.One * (show ? 0.9f : 1f);
        help.PivotOffset = help.Size;
        tw.TweenProperty(help, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    public override void _UnhandledInput(InputEvent e)
    {
        if (e.IsActionPressed("help")) helpHideAt = helpShown ? 0 : age + 1e9;
    }

    // ------------------------------------------------------------------ tableau des scores (Tab)

    Control table = null!;
    VBoxContainer tableRows = null!;
    bool tableShown;
    double tableRefresh;

    void BuildScoreTable()
    {
        table = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        table.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(table);
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Box(new Color(UiKit.Navy, 0.9f), 22, 0f, 0, null, 8);
        box.ContentMarginLeft = 36; box.ContentMarginRight = 36; box.ContentMarginTop = 24; box.ContentMarginBottom = 28;
        panel.AddThemeStyleboxOverride("panel", box);
        table.AddChild(panel);
        tableRows = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        tableRows.AddThemeConstantOverride("separation", 6);
        panel.AddChild(tableRows);
    }

    void FillScoreTable()
    {
        foreach (var c in tableRows.GetChildren()) c.QueueFree();
        var s = View.State;
        tableRows.AddChild(UiKit.Text("TABLEAU DES SCORES", 46, UiKit.Yellow, 12, HorizontalAlignment.Center));
        tableRows.AddChild(UiKit.Text($"Manches  {s.Rounds0} - {s.Rounds1}      KO de la manche  {s.Kos0} - {s.Kos1}", 24, UiKit.Sky, 6, HorizontalAlignment.Center));
        tableRows.AddChild(UiKit.Spacer(6));
        tableRows.AddChild(StatRow(null, "JOUEUR", new[] { "VIES", "KO", "TOUCHES", "ATTRAPES", "LANCERS" }, true));
        for (int team = 0; team < 2; team++)
            foreach (var p in View.Players)
            {
                if (p.Team != team) continue;
                var st = View.Stats != null && View.Stats.TryGetValue(p.Id, out var x) ? x : null;
                string D(int v) => st == null ? "-" : v.ToString();
                tableRows.AddChild(StatRow(p, View.NameOf(p.Id), new[] { $"{Mathf.Max(0, View.Lives(p.Id))}/{View.MaxLives}", D(st?.Kos ?? 0), D(st?.Hits ?? 0), D(st?.Catches ?? 0), D(st?.Throws ?? 0) }, false));
            }
        if (!View.HasStats) tableRows.AddChild(UiKit.Text("Statistiques détaillées tenues par l'hôte", 20, new Color(1, 1, 1, 0.6f), 5, HorizontalAlignment.Center));
    }

    Control StatRow(MatchView.PlayerInfo? p, string name, string[] cells, bool header)
    {
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var bg = header ? new Color(0, 0, 0, 0) : new Color(Palette.Team(p!.Team), p.Id == View.LocalId ? 0.55f : 0.28f);
        var box = UiKit.Box(bg, 8, -0.1f, p != null && p.Id == View.LocalId ? 3 : 0, UiKit.Yellow, 0);
        box.ContentMarginTop = 2; box.ContentMarginBottom = 2; box.ContentMarginLeft = 18; box.ContentMarginRight = 18;
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 8);
        row.AddChild(line);
        int size = header ? 20 : 28;
        var n = UiKit.Text(name, size, header ? UiKit.Sky : Colors.White, 6);
        n.CustomMinimumSize = new Vector2(240, 0);
        line.AddChild(n);
        foreach (var c in cells)
        {
            var l = UiKit.Text(c, size, header ? UiKit.Sky : Colors.White, 6, HorizontalAlignment.Center);
            l.CustomMinimumSize = new Vector2(130, 0);
            line.AddChild(l);
        }
        return row;
    }

    void UpdateScoreTable(double delta)
    {
        bool want = (Input.IsActionPressed("scoreboard") && !PauseMenu.IsOpen && !EndScreen.IsOpen) || (hudTest && age > 6 && age < 9);
        if (want && !tableShown)
        {
            FillScoreTable();
            table.Visible = true;
            table.Modulate = new Color(1, 1, 1, 0);
            table.Scale = Vector2.One * 0.94f;
            table.PivotOffset = table.Size / 2;
            var tw = table.CreateTween().SetParallel();
            tw.TweenProperty(table, "modulate:a", 1.0, 0.12);
            tw.TweenProperty(table, "scale", Vector2.One, 0.18).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tableRefresh = 0.5;
        }
        else if (!want && tableShown) table.Visible = false;
        tableShown = want;
        if (want && (tableRefresh -= delta) <= 0) { tableRefresh = 0.5; FillScoreTable(); }
    }

    /// <summary>
    /// Test de capture (--hud-test) : faux événements pour voir chaque élément sans attendre le hasard du jeu :
    /// fil des KO à 2 s, marqueurs de touche à 2,5 et 3 s, cœur perdu côté gauche à 4 s, tableau des scores de 6 à 9 s.
    /// </summary>
    int HudTest(double delta, int lives)
    {
        bool At(double t) => age >= t && age - delta < t;
        int foe = -1, mate = -1;
        foreach (var p in View.Players)
        {
            if (p.Team != View.LocalTeam && foe < 0) foe = p.Id;
            if (p.Team == View.LocalTeam && p.Id != View.LocalId && mate < 0) mate = p.Id;
        }
        if (At(2.0)) { OnKo(View.LocalId, foe); OnKo(foe, mate); }
        if (At(2.5)) OnHit(View.LocalId, foe, false);
        if (At(3.0)) OnHit(View.LocalId, foe, true);
        if (age >= 4.0 && age < 12.0)
        {
            lives = Mathf.Min(lives, View.MaxLives - 1);
            if (At(4.0)) { lastLives = View.MaxLives; }
        }
        return lives;
    }

    // ------------------------------------------------------------------ image par image

    Label fps = null!;

    public override void _Process(double delta)
    {
        age += delta;
        View.Update();
        var s = View.State;
        int n = View.Lives(View.LocalId);
        if (hudTest) n = HudTest(delta, n);
        if (s.Phase != prev.Phase)
            GD.Print($"[HUD] {s.Phase} : manche {s.Round}/{s.MaxRounds}, manches {s.Rounds0}-{s.Rounds1}, KO {s.Kos0}-{s.Kos1}, gagnant {s.Winner}{(View.HasStats ? "" : " (client)")}");

        // Touché : vignette rouge du côté du coup.
        if (n < lastLives && n >= 0) { HurtFrom(); if (hudTest) vignette.From = Vector2.Left; vignette.Strength = 1f; }
        lastLives = n;
        vignette.Strength = Mathf.Max(0, vignette.Strength - (float)delta * 1.6f);
        vignette.QueueRedraw();
        speedLines.Strength = Player.Dashing ? 1f : Mathf.Max(0, speedLines.Strength - (float)delta * 5f);
        speedLines.QueueRedraw();

        float charge = hudTest && age > 9.5 && age < 11.5 ? Mathf.Clamp((float)(age - 9.5) / 1.2f, 0f, 1f) : Player.ThrowCharge01;   // test : charge simulée
        chargeShown = Mathf.Lerp(chargeShown, charge, Mathf.Min(1f, (float)delta * 25f));
        hitMarkerLeft = Mathf.Max(0, hitMarkerLeft - delta);
        crosshair.Visible = n > 0 && s.Phase != MatchView.Phase.MatchOver;
        crosshair.QueueRedraw();

        UpdateScoreBoard(s);
        UpdateHearts(n, delta);
        UpdateBottomCenter();
        UpdateCenter(s, n);
        UpdateScoreTable(delta);
        SetHelp(age < helpHideAt && !EndScreen.IsOpen);

        fps.Visible = GameSettings.ShowFps;
        if (fps.Visible) fps.Text = $"{Engine.GetFramesPerSecond():0} IPS";
        root.Visible = !EndScreen.IsOpen;
        prev = s;
    }
}
