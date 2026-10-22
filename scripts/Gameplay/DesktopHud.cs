using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.UI;
using DogeballVR.Visual;

namespace DogeballVR.Gameplay;

/// <summary>
/// HUD du joueur clavier-souris (dessiné pour 1920x1080, mis à l'échelle par le projet), dans l'esprit de Knockout City :
/// tableau de score en haut (KO de la manche, manches gagnées, chrono), cœurs en bas à gauche, balle tenue et jauges
/// d'attrape et de dash en bas au centre (avec leurs touches), jauge de charge en arc autour du viseur, marqueur de touche,
/// fil des KO et bandeau « KO ! » du joueur (haut gauche), flash crème du côté d'où vient le coup et cœur perdu qui se brise,
/// alerte « on te vise » (liseré au bord et chevron autour du viseur, vers la menace),
/// lignes de vitesse au dash, annonces centrales (une seule à la fois), tableau des scores (Tab), aide des touches (F1),
/// compteur d'images. Sous la pause, les annonces, l'alerte et l'aide se cachent ; pendant l'entraînement, le score,
/// le chrono et les annonces de manche aussi.
/// L'état de la partie vient de <see cref="MatchView"/> (hôte ou client). En VR, tout passe par le HUD 3D du MatchController.
/// </summary>
public partial class DesktopHud : CanvasLayer
{
    // Paliers de la balle tenue (passes, attrape parfaite) et paliers de la charge du lancer.
    static readonly Color[] BallTierColors = { Colors.White, UiKit.Yellow, UiKit.Orange, UiKit.Pink };
    static readonly Color[] ChargeColors = { UiKit.Yellow, UiKit.Orange, UiKit.Pink };
    // Pas de rouge dans le HUD : les signaux de menace prennent la couleur de l'équipe qui menace (palette sans rouge).
    static readonly Color Urgent = UiKit.Yellow;

    public DesktopPlayer Player { get; set; } = null!;
    public MatchView View { get; set; } = null!;

    Control root = null!;
    Control centerLayer = null!, helpLayer = null!;   // annonces centrales ; aide et rappel F1 (cachés sous la pause)
    double age;
    MatchView.Snapshot prev;
    bool hudTest;

    public override void _Ready()
    {
        Layer = 5;
        ProcessMode = ProcessModeEnum.Always;   // tourne sous la pause (arbre gelé) pour cacher les annonces : voir _Process
        hudTest = System.Array.IndexOf(OS.GetCmdlineUserArgs(), "--hud-test") >= 0;
        root = new Control { Name = "Root", Theme = UiKit.Theme, MouseFilter = Control.MouseFilterEnum.Ignore };
        root.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        AddChild(root);

        vignette = Add(new Vignette());
        threat = Add(new ThreatOverlay());
        speedLines = Add(new SpeedLines { Hud = this });
        crosshair = Add(new Crosshair { Hud = this });
        centerLayer = Add(new Control());
        helpLayer = Add(new Control());
        BuildScoreBoard();
        BuildHearts();
        BuildBottomCenter();
        BuildFeed();
        BuildCenterTexts();
        BuildHelp();
        BuildScoreTable();
        BuildKoBanner();

        fps = UiKit.Text("", 22, UiKit.Green, 6);
        fps.Position = new Vector2(16, 10);
        root.AddChild(fps);

        prev = View.State;
        // HUD créé en plein compte à rebours : on fait comme s'il commençait, pour annoncer « MANCHE N » dès le premier chiffre.
        if (prev.Phase == MatchView.Phase.Countdown) prev = prev with { Phase = MatchView.Phase.Playing, Countdown = 0 };
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

    static Panel MakePanel(Color bg, float skew = 0f, int radius = 14, int border = 0, Color? borderColor = null, int bottom = 5)
    {
        var p = new Panel { MouseFilter = Control.MouseFilterEnum.Ignore };
        p.AddThemeStyleboxOverride("panel", UiKit.Box(bg, radius, skew, border, borderColor, bottom));
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

            // Nom d'équipe seul (le liseré jaune dit déjà laquelle est la mienne), gros chiffre des KO et « KO » discret dessous.
            var align = team == 0 ? HorizontalAlignment.Right : HorizontalAlignment.Left;
            var name = UiKit.Text(team == 0 ? "ORANGE" : "BLEUE", 24, UiKit.Cream, 6, team == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
            name.Position = new Vector2(team == 0 ? 26 : 18, 28);
            name.Size = new Vector2(206, 32);
            box.AddChild(name);

            var ko = UiKit.Text("0", 76, Colors.White, 12, align);
            ko.Position = new Vector2(team == 0 ? 120 : 24, -11);
            ko.Size = new Vector2(100, 92);
            ko.PivotOffset = ko.Size / 2;
            box.AddChild(ko);
            koLabels[team] = ko;
            var koTag = UiKit.Text("KO", 17, new Color(UiKit.Cream, 0.8f), 5, align);
            koTag.Position = new Vector2(team == 0 ? 118 : 26, 63);
            koTag.Size = new Vector2(100, 22);
            box.AddChild(koTag);

            // Pastilles des manches gagnées (16 px), du côté du chrono.
            for (int i = 0; i < 5; i++)
            {
                var pip = new Pip { Color = color, MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(16, 16) };
                pip.Position = new Vector2(team == 0 ? 226 - i * 22 : 458 + i * 22, 103);
                pip.PivotOffset = new Vector2(8, 8);
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
        roundLabel = UiKit.Text("MANCHE 1/5", 21, UiKit.Sky, 5, HorizontalAlignment.Center);
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
            float r = Size.X / 2;
            DrawCircle(c + new Vector2(0, 2), r, new Color(0, 0, 0, 0.45f));
            DrawCircle(c, r, UiKit.Navy);
            DrawCircle(c, r * 0.7f, On ? Color : new Color(1, 1, 1, 0.18f));
            if (On) DrawCircle(c + new Vector2(-r * 0.25f, -r * 0.25f), r * 0.22f, new Color(1, 1, 1, 0.7f));
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
        timer.AddThemeColorOverride("font_color", urgent ? Colors.White.Lerp(Urgent, 0.6f + 0.4f * pulse) : Colors.White);
        timerStyle.BgColor = urgent ? UiKit.Navy.Lerp(new Color("5a3a08"), pulse) : UiKit.Navy;
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
    const float HeartSize = 80f;   // contrôle de 80 px : cœur dessiné d'environ 64 px, liseré compris
    Control heartsHolder = null!;
    HeartBreak heartBreak = null!;

    sealed partial class Heart : Control
    {
        public bool Full = true;
        public float Shake;
        public Color Fill = UiKit.Orange;
        static Vector2[]? shape;
        /// <summary>Contour du cœur (48 points, unité ~1) : indices 0 à 24 pour la moitié droite (encoche en haut à 0, pointe en bas à 24).</summary>
        public static Vector2[] Shape()
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
            var c = Size / 2 + new Vector2(Mathf.Sin(Shake * 60f) * Shake * Size.X * 0.12f, 0);
            float r = Size.X * 0.36f;
            DrawColoredPolygon(At(c + new Vector2(0, 3), r + 5), new Color(0, 0, 0, 0.4f));
            DrawColoredPolygon(At(c, r + 5), UiKit.Navy);
            DrawColoredPolygon(At(c, r), Full ? Fill : new Color(0.25f, 0.28f, 0.4f));
            if (Full) DrawColoredPolygon(At(c + new Vector2(-r * 0.32f, -r * 0.3f), r * 0.28f), new Color(1, 1, 1, 0.55f));
        }
    }

    void BuildHearts()
    {
        // Bas à gauche, à la Knockout City : mon nom (ou « Toi ») au-dessus de gros cœurs.
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsPreset(Control.LayoutPreset.BottomLeft);
        holder.OffsetLeft = 32; holder.OffsetTop = -158; holder.OffsetRight = 420; holder.OffsetBottom = -28;
        root.AddChild(holder);
        heartsHolder = holder;
        string me = MatchView.HumanName?.Invoke(View.LocalId) is { Length: > 0 } pseudo ? pseudo : View.NameOf(View.LocalId);
        var who = UiKit.Text(me, 28, Colors.White, 8);
        who.Position = new Vector2(8, 0);
        who.Size = new Vector2(380, 36);
        holder.AddChild(who);
        var back = MakePanel(new Color(UiKit.Navy, 0.6f), -0.15f, 16, 0, null, 4);
        back.Position = new Vector2(0, 38);
        back.Size = new Vector2(24 + (HeartSize + 8) * View.MaxLives, HeartSize + 12);
        holder.AddChild(back);
        for (int i = 0; i < View.MaxLives; i++)
        {
            var h = new Heart { Fill = Palette.Team(View.LocalTeam), MouseFilter = Control.MouseFilterEnum.Ignore, Size = new Vector2(HeartSize, HeartSize), Position = new Vector2(14 + i * (HeartSize + 8), 44) };
            h.PivotOffset = h.Size / 2;
            holder.AddChild(h);
            hearts.Add(h);
        }
        // Cœur perdu en grand, par-dessus le reste du HUD (bris et éclats).
        heartBreak = new HeartBreak { MouseFilter = Control.MouseFilterEnum.Ignore };
        heartBreak.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        root.AddChild(heartBreak);
    }

    /// <summary>
    /// Coup reçu : le cœur perdu jaillit de sa place, grossit (environ 2,6 fois) en blanchissant, se fend, puis ses deux moitiés
    /// tombent en tournant au milieu d'éclats et disparaissent (environ 1 s). Temps piloté par le HUD.
    /// </summary>
    sealed partial class HeartBreak : Control
    {
        public float T = -1f;
        Color fill;
        Vector2 from;
        float baseR;
        readonly Vector2[] shardVel = new Vector2[12];
        readonly float[] shardSpin = new float[12];
        readonly RandomNumberGenerator rng = new();
        const float Grow = 0.24f, Life = 1.0f;
        static readonly Vector2[] Fissure = { new(0.06f, -0.08f), new(-0.1f, 0.14f), new(0.1f, 0.42f), new(-0.07f, 0.68f), new(0f, 0.88f) };   // de l'encoche vers la pointe (finit droit : le bas du cœur est trop fin pour zigzaguer)
        static Vector2[]? leftHalf, rightHalf;

        public void Start(Vector2 at, float r, Color color)
        {
            from = at; baseR = r; fill = color; T = 0f;
            for (int i = 0; i < shardVel.Length; i++)
            {
                float a = -Mathf.Pi / 2 + rng.RandfRange(-1.4f, 1.4f);
                shardVel[i] = Vector2.FromAngle(a) * rng.RandfRange(260f, 620f);
                shardSpin[i] = rng.RandfRange(-9f, 9f);
            }
        }

        public void Advance(float dt)
        {
            if (T < 0) return;
            T += dt;
            if (T > Life) T = -1f;
            QueueRedraw();
        }

        static Vector2[] Half(bool right)
        {
            var src = Heart.Shape();
            var pts = new List<Vector2>();
            if (right)
            {
                for (int i = 0; i <= 24; i++) pts.Add(src[i]);
                for (int i = Fissure.Length - 1; i >= 0; i--) pts.Add(Fissure[i]);
            }
            else
            {
                for (int i = 24; i <= 48; i++) pts.Add(src[i % 48]);
                foreach (var p in Fissure) pts.Add(p);
            }
            return pts.ToArray();
        }

        /// <summary>Forme liserée de marine, posée en <paramref name="at"/>, tournée, à l'échelle <paramref name="r"/>.</summary>
        void Body(Vector2[] pts, Vector2 at, float rot, float r, Color col, float alpha, bool outline = true)
        {
            var xf = new Transform2D(rot, at);
            var p = new Vector2[pts.Length];
            if (outline)
            {
                for (int i = 0; i < pts.Length; i++) p[i] = xf * (pts[i] * (r + 7f));
                DrawColoredPolygon(p, new Color(UiKit.Navy, alpha));
            }
            for (int i = 0; i < pts.Length; i++) p[i] = xf * (pts[i] * r);
            DrawColoredPolygon(p, new Color(col, col.A * alpha));
        }

        public override void _Draw()
        {
            if (T < 0) return;
            leftHalf ??= Half(false);
            rightHalf ??= Half(true);
            float k = Mathf.Min(1f, T / Grow);
            var c = from + new Vector2(110f, -210f) * Mathf.Sin(k * Mathf.Pi * 0.5f);
            float r = baseR * (1f + 2.3f * Mathf.Sin(k * Mathf.Pi * 0.5f));
            if (T < Grow)
            {
                var col = fill.Lerp(UiKit.Cream, Mathf.Clamp((k - 0.45f) / 0.55f, 0f, 1f));   // blanchit juste avant de casser
                Body(Heart.Shape(), c + new Vector2(0, 6), 0f, r + 7f, new Color(0, 0, 0, 0.35f), 1f, false);
                Body(Heart.Shape(), c, 0f, r, col, 1f);
                if (k > 0.55f)   // la fêlure apparaît
                {
                    var crack = new Vector2[Fissure.Length + 2];
                    crack[0] = c + Heart.Shape()[0] * r;
                    for (int i = 0; i < Fissure.Length; i++) crack[i + 1] = c + Fissure[i] * r;
                    crack[^1] = c + Heart.Shape()[24] * r;
                    DrawPolyline(crack, UiKit.Navy, 8f, true);
                }
                return;
            }
            float t = T - Grow;
            float u = t / (Life - Grow);
            float a = 1f - u * u;
            float fall = 560f * u * u;
            Body(leftHalf, c + new Vector2(-100f * u, fall), -0.9f * u, r, fill, a);
            Body(rightHalf, c + new Vector2(100f * u, fall * 0.9f), 0.8f * u, r, fill, a);
            // Éclats : petits triangles qui fusent puis retombent.
            for (int i = 0; i < shardVel.Length; i++)
            {
                var p = c + shardVel[i] * t + new Vector2(0, 1400f * t * t);
                float s = r * (i % 3 == 0 ? 0.22f : 0.14f) * (1f - 0.5f * u);
                float rot = shardSpin[i] * t;
                var tri = new[] { p + Vector2.FromAngle(rot) * s, p + Vector2.FromAngle(rot + 2.3f) * s, p + Vector2.FromAngle(rot + 4.1f) * s * 0.8f };
                DrawColoredPolygon(tri, new Color(i % 2 == 0 ? fill : UiKit.Cream, a));
                DrawPolyline(new[] { tri[0], tri[1], tri[2], tri[0] }, new Color(UiKit.Navy, a), 3f, true);
            }
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
                    // Cœur perdu : sa place tremble et s'éteint, et le cœur lui-même jaillit en grand et se brise (HeartBreak).
                    h.Shake = 1f;
                    h.Scale = Vector2.One * 1.3f;
                    tw.TweenProperty(h, "scale", Vector2.One, 0.5).SetTrans(Tween.TransitionType.Elastic).SetEase(Tween.EaseType.Out);
                    if (age > 0.2) heartBreak.Start(h.GetGlobalRect().GetCenter() - root.GetGlobalRect().Position, HeartSize * 0.36f, h.Fill);
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
        heartBreak.Advance((float)delta);
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

    Label catchKey = null!, dashKey = null!, fireKey = null!;

    Control bottomHolder = null!;
    Label fireAction = null!;
    // La caméra d'épaule place le personnage à gauche du centre (pieds vers x = 650-820 en 1080p) : le groupe d'icônes est
    // décalé à droite et remonté pour ne pas toucher ses pieds, et laisse la place au nom de l'action sous chaque icône.
    const float BottomShift = 250f;

    void BuildBottomCenter()
    {
        var holder = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        holder.SetAnchorsPreset(Control.LayoutPreset.CenterBottom);
        holder.OffsetLeft = -230 + BottomShift; holder.OffsetRight = 230 + BottomShift; holder.OffsetTop = -232; holder.OffsetBottom = -32;
        root.AddChild(holder);
        bottomHolder = holder;
        catchGauge = new RoundGauge { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(10, 30), Size = new Vector2(110, 110) };
        catchGauge.PivotOffset = new Vector2(55, 40);
        holder.AddChild(catchGauge);
        ballSlot = new BallSlot { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(170, 4), Size = new Vector2(120, 120) };
        ballSlot.PivotOffset = ballSlot.Size / 2;
        holder.AddChild(ballSlot);
        dashGauge = new RoundGauge { Dash = true, MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(340, 30), Size = new Vector2(110, 110) };
        dashGauge.PivotOffset = new Vector2(55, 40);
        holder.AddChild(dashGauge);
        // Sous chaque emplacement, centrés : le nom de l'action puis sa touche (attraper, lancer la balle tenue, esquive).
        ActionUnder(holder, 10, 110, 131, "Attraper");
        fireAction = ActionUnder(holder, 170, 120, 131, "Lancer");
        ActionUnder(holder, 340, 110, 131, "Esquive");
        catchKey = KeyUnder(holder, 10, 110, 163);
        fireKey = KeyUnder(holder, 170, 120, 163);
        dashKey = KeyUnder(holder, 340, 110, 163);
    }

    Label ActionUnder(Control parent, float x, float width, float y, string text)
    {
        var l = UiKit.Text(text, 22, Colors.White, 7, HorizontalAlignment.Center);
        l.Position = new Vector2(x - 20, y);
        l.Size = new Vector2(width + 40, 30);
        parent.AddChild(l);
        return l;
    }

    Label KeyUnder(Control parent, float x, float width, float y)
    {
        var center = new CenterContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Position = new Vector2(x, y), Size = new Vector2(width, 34) };
        parent.AddChild(center);
        var l = KeyCap("", 19);
        center.AddChild(l);
        return l;
    }

    /// <summary>
    /// Touche de clavier dessinée : cadre clair arrondi, liseré gris, tranche plus foncée en bas (relief), texte marine.
    /// Sert sous les jauges, dans l'aide (F1) et dans le rappel « F1 : commandes ».
    /// </summary>
    public static Label KeyCap(string text, int size)
    {
        var l = UiKit.Text(text, size, UiKit.Navy, 0, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.AddThemeStyleboxOverride("normal", new StyleBoxFlat
        {
            BgColor = new Color("f6f1e7"), AntiAliasing = true,
            CornerRadiusTopLeft = 7, CornerRadiusTopRight = 7, CornerRadiusBottomLeft = 7, CornerRadiusBottomRight = 7,
            BorderWidthLeft = 2, BorderWidthRight = 2, BorderWidthTop = 2, BorderWidthBottom = 5, BorderColor = new Color("8d97ad"),
            ContentMarginLeft = 10, ContentMarginRight = 10, ContentMarginTop = 1, ContentMarginBottom = 3,
            ShadowColor = new Color(UiKit.Navy, 0.6f), ShadowSize = 3,
        });
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
        fireKey.Text = InputSetup.KeyName("fire");
        dashKey.Text = InputSetup.KeyName("dash");
        fireKey.Modulate = fireAction.Modulate = new Color(1, 1, 1, holding ? 1f : 0.6f);   // lancer : utile balle en main seulement
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
            var col = p.AimingAtEnemy ? Palette.Team(1 - Hud.View.LocalTeam) : new Color(1, 1, 1, 0.95f);   // adversaire visé : couleur de son équipe
            // Viseur compact (environ 24 px en 1080p) : quatre branches et un point, cernés d'un contour sombre de 2 px
            // pour rester lisibles sur un ciel clair.
            var outline = new Color(UiKit.Navy, 0.95f);
            float charge = Hud.chargeShown;
            float spread = 5f + charge * 12f;      // le viseur s'ouvre quand on arme un lancer
            foreach (var (dx, dy) in new[] { (1, 0), (-1, 0), (0, 1), (0, -1) })
            {
                var d = new Vector2(dx, dy);
                var a = c + d * spread;
                var b = c + d * (spread + 7f);
                DrawLine(a - d * 2f, b + d * 2f, outline, 7f);
                DrawLine(a, b, col, 3f);
            }
            DrawCircle(c, 3.6f, outline);
            DrawCircle(c, 1.8f, col);

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
                var mc = Hud.hitMarkerKill ? UiKit.Yellow : Colors.White;
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
        if (kill) ShowKoBanner(target);
    }

    // ------------------------------------------------------------------ bandeau « KO ! » du joueur (haut gauche)

    Control koBanner = null!;
    Label koBannerName = null!;
    Tween? koBannerTween;

    /// <summary>KO marqué par le joueur, à la Knockout City : grand bandeau incliné à sa couleur d'équipe, « KO ! » et le nom de la victime.</summary>
    void BuildKoBanner()
    {
        koBanner = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false, Rotation = -0.06f };
        koBanner.Position = new Vector2(0, 54);
        koBanner.Size = new Vector2(660, 150);
        root.AddChild(koBanner);
        var band = MakePanel(View.LocalTeam == 0 ? UiKit.Orange : UiKit.Blue, -0.3f, 0, 5, UiKit.Navy, 6);
        band.Position = new Vector2(-80, 10);
        band.Size = new Vector2(590, 130);   // s'arrête avant le tableau de score (x = 610)
        koBanner.AddChild(band);
        var ko = UiKit.Text("KO !", 92, Colors.White, 18);
        ko.Position = new Vector2(40, -6);
        ko.Size = new Vector2(260, 110);
        ko.PivotOffset = new Vector2(90, 60);
        koBanner.AddChild(ko);
        koBannerWord = ko;
        koBannerName = UiKit.Text("", 40, UiKit.Cream, 10);
        koBannerName.Position = new Vector2(46, 90);
        koBannerName.Size = new Vector2(440, 50);
        koBanner.AddChild(koBannerName);
    }

    Label koBannerWord = null!;

    void ShowKoBanner(int victim)
    {
        koBannerName.Text = View.NameOf(victim);
        koBannerTween?.Kill();
        koBanner.Visible = true;
        koBanner.Modulate = Colors.White;
        koBanner.Position = koBanner.Position with { X = -720 };
        var tw = koBannerTween = koBanner.CreateTween();
        tw.TweenProperty(koBanner, "position:x", 0.0, 0.22).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenInterval(1.05);
        tw.TweenProperty(koBanner, "position:x", -720.0, 0.23).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.In);
        tw.Parallel().TweenProperty(koBanner, "modulate:a", 0.0, 0.23);
        tw.TweenCallback(Callable.From(() => koBanner.Visible = false));
        UiKit.Punch(koBannerWord, 1.6f, 0.3);
    }

    // ------------------------------------------------------------------ fil des KO (haut droite)

    // Placement à la main (pas de conteneur) : un conteneur replace ses enfants à chaque mise en page et se battait avec
    // l'animation d'entrée, d'où des lignes qui sautaient d'une image à l'autre. Ici, chaque ligne est collée au bord droit,
    // à 24 px du coin, et ne bouge que pour entrer (glissé) ou monter d'un cran quand la plus ancienne part.
    const float FeedMargin = 24f, FeedGap = 8f;
    const double FeedHold = 4.0;
    Control feed = null!;
    sealed class FeedEntry { public PanelContainer Panel = null!; public double Born; public float Y = -1; }
    readonly List<FeedEntry> feedEntries = new();

    void BuildFeed()
    {
        feed = new Control { MouseFilter = Control.MouseFilterEnum.Ignore };
        feed.SetAnchorsPreset(Control.LayoutPreset.TopRight);
        feed.OffsetLeft = -FeedMargin; feed.OffsetRight = -FeedMargin; feed.OffsetTop = FeedMargin; feed.OffsetBottom = FeedMargin;
        root.AddChild(feed);
    }

    void UpdateFeed(double delta)
    {
        feedEntries.RemoveAll(e =>
        {
            if (age - e.Born < FeedHold + 0.55) return false;
            e.Panel.QueueFree();
            return true;
        });
        float y = 0;
        foreach (var e in feedEntries)
        {
            var p = e.Panel;
            p.Size = p.GetCombinedMinimumSize();
            float t = (float)(age - e.Born);
            float enter = Mathf.Clamp(t / 0.25f, 0f, 1f);
            float slide = 90f * Mathf.Pow(1f - enter, 3);                       // arrive de la droite en ralentissant
            float alpha = Mathf.Min(1f, t / 0.15f) * Mathf.Clamp((float)(FeedHold + 0.4 - t) / 0.4f, 0f, 1f);
            e.Y = e.Y < 0 ? y : Mathf.MoveToward(e.Y, y, (float)delta * 600f);   // monte d'un cran sans à-coup
            p.Position = new Vector2(Mathf.Round(-p.Size.X + slide), Mathf.Round(e.Y));
            p.Modulate = new Color(1, 1, 1, alpha);
            y += p.Size.Y + FeedGap;
        }
    }

    void OnKo(int by, int victim)
    {
        bool mine = by == View.LocalId || victim == View.LocalId;
        var panel = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Box(new Color(UiKit.Navy, 0.82f), 10, -0.15f, mine ? 3 : 0, UiKit.Yellow, 3);
        box.ContentMarginLeft = 18; box.ContentMarginRight = 18; box.ContentMarginTop = 4; box.ContentMarginBottom = 4;
        panel.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 12);
        panel.AddChild(line);
        if (by >= 0)
        {
            line.AddChild(UiKit.Text(View.NameOf(by), 32, Palette.Team(View.TeamOf(by)).Lightened(0.1f), 6));
            line.AddChild(new FeedBall { CustomMinimumSize = new Vector2(58, 40), MouseFilter = Control.MouseFilterEnum.Ignore });
        }
        else line.AddChild(UiKit.Text("KO", 32, UiKit.Yellow, 7));
        line.AddChild(UiKit.Text(View.NameOf(victim), 32, Palette.Team(View.TeamOf(victim)).Lightened(0.1f), 6));
        panel.Modulate = new Color(1, 1, 1, 0);   // UpdateFeed le place et le fait apparaître (entrée par la droite, départ après 4 s)
        feed.AddChild(panel);
        feedEntries.Add(new FeedEntry { Panel = panel, Born = age });
        while (feedEntries.Count > 5) { feedEntries[0].Panel.QueueFree(); feedEntries.RemoveAt(0); }
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

    /// <summary>
    /// Flash crème des bords au coup reçu (0,3 s) : franc et large du côté d'où vient le coup, à peine marqué ailleurs
    /// (direction en repère écran, nulle = tout le bord). Une bande pleine collée au bord, puis un dégradé vers l'intérieur.
    /// </summary>
    sealed partial class Vignette : Control
    {
        public float Strength;
        public Vector2 From;
        public override void _Draw()
        {
            if (Strength <= 0.01f) return;
            var s = Size;
            float e = Strength * Strength * (3f - 2f * Strength);   // lissé : fort d'un coup, s'éteint en douceur
            void Edge(Vector2 a, Vector2 b, Vector2 inward, Vector2 normal)
            {
                float k = From == Vector2.Zero ? 0.8f : Mathf.Clamp(0.12f + 1.2f * normal.Dot(From), 0.12f, 1f);
                float w = Mathf.Min(s.X, s.Y) * (0.12f + 0.24f * k);
                float band = 26f * k;
                var solid = new Color(UiKit.Cream, Mathf.Min(0.95f, e * k));
                var outer = new Color(1f, 1f, 1f, Mathf.Min(0.8f, e * k * 0.85f));
                var inner = new Color(1f, 1f, 1f, 0f);
                DrawPolygon(new[] { a, b, b + inward * band, a + inward * band }, new[] { solid, solid, solid, solid });
                DrawPolygon(new[] { a + inward * band, b + inward * band, b + inward * w, a + inward * w }, new[] { outer, outer, inner, inner });
            }
            Edge(Vector2.Zero, new Vector2(s.X, 0), Vector2.Down, Vector2.Up);
            Edge(new Vector2(0, s.Y), s, Vector2.Up, Vector2.Down);
            Edge(Vector2.Zero, new Vector2(0, s.Y), Vector2.Right, Vector2.Left);
            Edge(new Vector2(s.X, 0), s, Vector2.Left, Vector2.Right);
        }
    }

    // ------------------------------------------------------------------ alerte « on te vise »

    ThreatOverlay threat = null!;
    CanvasLayer? threatEdges;
    double threatEdgesSearch;

    /// <summary>
    /// Alerte de menace, à la couleur de l'équipe qui menace : dégradé sur le bord d'où elle vient et chevron autour du viseur, pointé vers elle.
    /// Deux niveaux : verrou (un adversaire balle en main me prend pour cible : signal posé, un chevron) et tir entrant (la balle
    /// arrive sur moi : signal fort qui bat de plus en plus vite, double chevron). Direction en repère écran vue de dessus :
    /// x à droite, y vers le bas = derrière le joueur.
    /// </summary>
    sealed partial class ThreatOverlay : Control
    {
        public float Strength, Urgency, Clock;
        public bool Incoming;
        /// <summary>Un texte occupe le centre (compte à rebours, bandeau, annonce) : les chevrons s'effacent, le liseré reste.</summary>
        public bool CenterBusy;
        float chevronAlpha = 1f;
        public Color Color = UiKit.Blue;
        public Vector2 Dir = Vector2.Up;
        public override void _Draw()
        {
            if (Strength <= 0.01f) return;
            var s = Size;
            // Tout bat : doucement pour un verrou, de plus en plus vite pour un tir entrant.
            float pulse = Incoming ? 0.6f + 0.4f * Mathf.Sin(Clock * Mathf.Lerp(14f, 32f, Urgency)) : 0.72f + 0.28f * Mathf.Sin(Clock * 6f);
            float w = Mathf.Min(s.X, s.Y) * (Incoming ? 0.3f : 0.2f);
            const float Rim = 40f;   // liseré : dégradé vif de 40 px collé au bord
            var hue = Color.Lightened(0.15f);
            var rim = Color.Lightened(0.4f);
            void Edge(Vector2 a, Vector2 b, Vector2 inward, Vector2 normal)
            {
                float k = Mathf.Clamp(normal.Dot(Dir) * 1.3f, 0f, 1f);   // le bord face à la menace à fond, les voisins à moitié
                if (k <= 0.05f) return;
                float alpha = Mathf.Min(0.95f, Strength * pulse * k);
                var outer = new Color(hue, alpha * 0.6f);
                var inner = new Color(hue, 0f);
                DrawPolygon(new[] { a, b, b + inward * w * k, a + inward * w * k }, new[] { outer, outer, inner, inner });
                // Liseré vif : le dégradé large seul se perd sur un décor de la même teinte.
                float rw = Rim * (0.5f + 0.5f * k);
                var rimOut = new Color(rim, Mathf.Min(1f, alpha * 1.25f));
                var rimIn = new Color(rim, 0f);
                DrawPolygon(new[] { a, b, b + inward * rw, a + inward * rw }, new[] { rimOut, rimOut, rimIn, rimIn });
            }
            Edge(Vector2.Zero, new Vector2(s.X, 0), Vector2.Down, Vector2.Up);
            Edge(new Vector2(0, s.Y), s, Vector2.Up, Vector2.Down);
            Edge(Vector2.Zero, new Vector2(0, s.Y), Vector2.Right, Vector2.Left);
            Edge(new Vector2(s.X, 0), s, Vector2.Left, Vector2.Right);

            // Chevrons sur un anneau proche du viseur (juste au-delà des arcs de charge), pointés vers la menace :
            // contour blanc épais et ombre marine pour se détacher de n'importe quel décor.
            chevronAlpha = Mathf.MoveToward(chevronAlpha, CenterBusy ? 0f : 1f, 0.2f);
            float ca = Strength * chevronAlpha;
            if (ca <= 0.01f) return;
            var c = s / 2;
            var d = Dir;
            var n = new Vector2(-d.Y, d.X);
            int count = Incoming ? 2 : 1;
            for (int i = 0; i < count; i++)
            {
                const float Ring = 92f;   // dos du premier chevron ; les arcs de charge s'arrêtent à 64 px
                float wing = 34f - i * 4f, depth = 26f - i * 3f, thick = 16f;
                var tip = c + d * (Ring + depth + thick + i * 34f + (Incoming ? 8f * pulse : 0f));
                var pts = new[] { tip, tip - d * depth + n * wing, tip - d * (depth + thick) + n * wing, tip - d * thick, tip - d * (depth + thick) - n * wing, tip - d * depth - n * wing };
                var closed = new Vector2[pts.Length + 1];
                pts.CopyTo(closed, 0);
                closed[^1] = pts[0];
                float a = ca * (i == 0 ? 1f : 0.8f);
                var shadowPts = new Vector2[closed.Length];
                for (int k = 0; k < closed.Length; k++) shadowPts[k] = closed[k] + new Vector2(0, 4);
                DrawPolyline(shadowPts, new Color(UiKit.Navy, 0.85f * a), 15f, true);   // ombre marine portée
                DrawPolyline(closed, new Color(UiKit.Navy, 0.95f * a), 13f, true);
                DrawPolyline(closed, new Color(1f, 1f, 1f, a), 7f, true);              // contour blanc épais
                DrawColoredPolygon(pts, new Color(hue, a));
            }
        }
    }

    /// <summary>
    /// Tir entrant : même calcul que l'alerte de tir (<see cref="ThreatIndicator"/>), la balle adverse libre dont l'impact prévu sur moi
    /// tombe sous <see cref="ThreatIndicator.WarnSeconds"/>. Verrou : un adversaire tient une balle à portée (16 m, la portée utile
    /// des bots) et je suis l'adversaire le plus proche de lui (c'est celui que les bots visent). Rien quand c'est mon équipe qui tire.
    /// Tout se lit sur les balles et les hurtboxes déjà présentes chez l'hôte comme chez le client.
    /// </summary>
    bool FindThreat(out Vector3 from, out int team, out bool incoming, out float timeToImpact)
    {
        from = Vector3.Zero; team = -1; incoming = false; timeToImpact = float.PositiveInfinity;
        var me = Player.Hurtbox;
        if (me == null || me.Eliminated || me.DashInvulnerable) return false;
        var mePos = me.GlobalPosition;
        float lockBest = 16f;
        foreach (var n in GetTree().GetNodesInGroup("balls"))
        {
            if (n is not Ball b || b.OwnerTeam < 0 || b.OwnerTeam == me.Team || b.IsPass || b.HasExploded) continue;
            if (!b.IsHeld)
            {
                float t = ThreatIndicator.TimeToImpact(b.GlobalPosition, b.LinearVelocity, mePos);
                if (t <= ThreatIndicator.WarnSeconds && t < timeToImpact) { timeToImpact = t; from = b.GlobalPosition; team = b.OwnerTeam; incoming = true; }
                continue;
            }
            if (incoming) continue;
            float d = b.GlobalPosition.DistanceTo(mePos);
            if (d >= lockBest) continue;
            bool closest = true;
            foreach (var h in GetTree().GetNodesInGroup("hurtboxes"))
                if (h is Hurtbox o && o != me && o.Team == me.Team && !o.Eliminated && o.GlobalPosition.DistanceTo(b.GlobalPosition) < d) { closest = false; break; }
            if (!closest) continue;
            lockBest = d; from = b.GlobalPosition; team = b.OwnerTeam;
        }
        return team >= 0;
    }

    void UpdateThreat(double delta, bool hidden)
    {
        // Au clavier-souris, cette alerte remplace le liseré plein écran de ThreatIndicator (couleur de ma propre équipe, montré aussi
        // au tireur) : on cache sa couche. Le VR n'a pas ce HUD et garde son liseré.
        if (threatEdges == null && age >= threatEdgesSearch)
        {
            threatEdgesSearch = age + 1.0;
            threatEdges = GetTree().CurrentScene?.FindChild("ThreatEdges", true, false) as CanvasLayer;
        }
        if (threatEdges != null && IsInstanceValid(threatEdges)) threatEdges.Visible = false;

        bool on = FindThreat(out var src, out int team, out bool incoming, out float t);
        bool fake = hudTest && age >= 4.4 && age < 5.6;   // test : verrou puis tir venant de derrière à droite, équipe adverse
        if (fake) { on = true; team = 1 - View.LocalTeam; incoming = age >= 4.9; t = incoming ? (float)(5.6 - age) / 0.7f * ThreatIndicator.WarnSeconds : t; }
        on &= !hidden;
        if (on)
        {
            threat.Incoming = incoming;
            threat.Color = Palette.Team(team);
            threat.Urgency = incoming ? 1f - Mathf.Clamp(t / ThreatIndicator.WarnSeconds, 0f, 1f) : 0f;
            float target = incoming ? 0.65f + 0.35f * threat.Urgency : 0.45f;
            threat.Strength = Mathf.MoveToward(threat.Strength, target, (float)delta * 8f);
            var cam = GetViewport().GetCamera3D();
            if (fake) threat.Dir = new Vector2(0.75f, 0.66f).Normalized();
            else if (cam != null)
            {
                var local = cam.GlobalTransform.Basis.Inverse() * (src - cam.GlobalPosition);
                var dir = new Vector2(local.X, local.Z);
                if (dir.LengthSquared() > 0.0001f) threat.Dir = dir.Normalized();
            }
        }
        else threat.Strength = hidden ? 0f : Mathf.MoveToward(threat.Strength, 0f, (float)delta * 6f);
        threat.Clock = (float)age;
        static bool Shown(Control c) => c.Visible && c.Modulate.A > 0.05f;   // les annonces s'effacent en fondu sans se cacher
        threat.CenterBusy = Shown(countdown) || Shown(countdownRound) || Shown(pop) || Shown(bannerBox) || Shown(koTitle);
        threat.QueueRedraw();
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

    Label CenterLabel(string text, int size, Color color, int outline, float y, float h, Control? parent = null)
    {
        var l = UiKit.Text(text, size, color, outline, HorizontalAlignment.Center);
        l.VerticalAlignment = VerticalAlignment.Center;
        l.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        l.OffsetLeft = -700; l.OffsetRight = 700; l.OffsetTop = y; l.OffsetBottom = y + h;
        l.PivotOffset = new Vector2(700, h / 2);
        (parent ?? centerLayer).AddChild(l);
        return l;
    }

    void BuildCenterTexts()
    {
        countdownRound = CenterLabel("", 50, UiKit.Sky, 12, 220, 70);   // au-dessus du chiffre, même quand il grossit au punch
        countdown = CenterLabel("", 220, UiKit.Yellow, 26, 400, 260);
        notice = CenterLabel("", 34, UiKit.Yellow, 10, 190, 50);
        notice.AddThemeStyleboxOverride("normal", UiKit.Box(new Color(UiKit.Navy, 0.8f), 12, -0.12f, 0, null, 4));
        notice.SetAnchorsPreset(Control.LayoutPreset.CenterTop);
        notice.GrowHorizontal = Control.GrowDirection.Both;
        notice.OffsetLeft = -360; notice.OffsetRight = 360;
        notice.PivotOffset = new Vector2(360, 25);
        toast = CenterLabel("", 34, Colors.White, 10, 250, 50);
        pop = CenterLabel("", 96, UiKit.Yellow, 18, 600, 120);
        koTitle = CenterLabel("KO !", 150, UiKit.Cream, 24, 330, 180);
        koSub = CenterLabel("", 40, Colors.White, 10, 500, 60);
        resume = CenterLabel("Souris libérée : clique pour reprendre", 34, Colors.White, 10, 700, 60, root);
        foreach (var l in new[] { countdown, countdownRound, notice, toast, pop, koTitle, koSub }) l.Visible = false;

        // Bandeau de fin de manche : bande inclinée de la couleur de l'équipe gagnante, qui traverse l'écran.
        // Assez haute pour le titre et le sous-titre (« Manches 3 – 2 ») sans que le bord de la bande les coupe.
        bannerBox = new Control { MouseFilter = Control.MouseFilterEnum.Ignore, Visible = false };
        bannerBox.SetAnchorsPreset(Control.LayoutPreset.HcenterWide);
        bannerBox.OffsetTop = -140; bannerBox.OffsetBottom = 140;
        centerLayer.AddChild(bannerBox);
        bannerBand = MakePanel(UiKit.Orange, -0.25f, 0, 6, UiKit.Navy);
        bannerBand.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bannerBand.OffsetLeft = -80; bannerBand.OffsetRight = 80; bannerBand.OffsetTop = 20; bannerBand.OffsetBottom = -20;
        bannerBox.AddChild(bannerBand);
        bannerText = UiKit.Text("", 88, Colors.White, 16, HorizontalAlignment.Center);
        bannerText.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bannerText.OffsetTop = 34; bannerText.OffsetBottom = -100;
        bannerText.VerticalAlignment = VerticalAlignment.Center;
        bannerBox.AddChild(bannerText);
        bannerSub = UiKit.Text("", 38, UiKit.Cream, 9, HorizontalAlignment.Center);
        bannerSub.SetAnchorsPreset(Control.LayoutPreset.FullRect);
        bannerSub.OffsetTop = 178; bannerSub.OffsetBottom = -46;
        bannerSub.VerticalAlignment = VerticalAlignment.Center;
        bannerBox.AddChild(bannerSub);
    }

    // File des annonces centrales : une seule à l'écran. Une annonce plus importante remplace la courante ; une moins importante
    // attend son tour (et tombe si elle a trop attendu). Priorités : bandeau de manche 3, compte à rebours et GO 2, éclats 1.
    const int PrioPop = 1, PrioCountdown = 2, PrioBanner = 3;
    sealed record Pending(int Prio, double Duration, double Expires, System.Action Show);
    readonly List<Pending> annQueue = new();
    readonly List<Tween> annTweens = new();
    int annPrio = -1;
    double annUntil;

    bool CenterBusyFor(int prio) => (age < annUntil && annPrio > prio) || (koTitle.Visible && prio < PrioBanner);

    void Announce(int prio, double duration, System.Action show, double maxWait = 1.2)
    {
        if (CenterBusyFor(prio))
        {
            if (maxWait > 0) annQueue.Add(new Pending(prio, duration, age + maxWait, show));
            return;
        }
        bool keepRound = prio == PrioCountdown && annPrio == PrioCountdown;   // « MANCHE N » reste entre 3, 2 et 1 (GO le retire)
        ClearCenter(keepRound);
        annPrio = prio;
        annUntil = age + duration;
        show();
    }

    void ClearCenter(bool keepRound = false)
    {
        foreach (var t in annTweens) if (t.IsValid()) t.Kill();
        annTweens.Clear();
        countdown.Visible = pop.Visible = bannerBox.Visible = false;
        if (!keepRound) countdownRound.Visible = false;
        annPrio = -1;
        annUntil = 0;
    }

    Tween AnnTween(Node n)
    {
        var t = n.CreateTween();
        annTweens.Add(t);
        return t;
    }

    void PumpAnnouncements()
    {
        if (age < annUntil && annPrio >= 0) return;
        annQueue.RemoveAll(p => p.Expires < age);
        if (annQueue.Count == 0) return;
        var next = annQueue[0];
        foreach (var p in annQueue) if (p.Prio > next.Prio) next = p;
        if (CenterBusyFor(next.Prio)) return;
        annQueue.Remove(next);
        Announce(next.Prio, next.Duration, next.Show, 0);
    }

    void ShowCenterPop(string text, Color color, int size) => Announce(PrioPop, 0.95, () =>
    {
        pop.Text = text;
        pop.AddThemeColorOverride("font_color", color);
        pop.AddThemeFontSizeOverride("font_size", size);
        pop.Visible = true;
        pop.Modulate = Colors.White;
        UiKit.Punch(pop, 2.2f, 0.3);
        var tw = AnnTween(pop);
        tw.TweenInterval(0.6);
        tw.TweenProperty(pop, "modulate:a", 0.0, 0.3);
    });

    /// <summary>« 3, 2, 1 » sous « MANCHE N » (première valeur : le titre glisse), puis « GO ! » seul : le titre s'efface aussitôt.</summary>
    void ShowCountdown(string text, bool go, string roundTitle = "")
    {
        if (Tutorial.Running) return;   // l'entraînement n'a ni manches ni départ
        Sfx.Play(this, go ? SfxKind.Go : SfxKind.Countdown);
        Announce(PrioCountdown, go ? 0.85 : 1.05, () =>
        {
            if (roundTitle != "")
            {
                countdownRound.Text = roundTitle;
                countdownRound.Visible = true;
                countdownRound.Modulate = Colors.White;
                UiKit.SlideIn(countdownRound, 0f, -120f);
            }
            if (go) countdownRound.Visible = false;
            countdown.Text = text;
            countdown.Visible = true;
            countdown.Modulate = Colors.White;
            countdown.AddThemeColorOverride("font_color", go ? UiKit.Green : UiKit.Yellow);
            UiKit.Punch(countdown, go ? 2.6f : 2.1f, 0.32);
            var tw = AnnTween(countdown);
            tw.TweenInterval(go ? 0.5 : 0.62);
            tw.TweenProperty(countdown, "modulate:a", 0.0, go ? 0.35 : 0.25);
        }, 0);
    }

    void ShowBanner(MatchView.Snapshot s, bool matchOver)
    {
        if (Tutorial.Running) return;
        Sfx.Play(this, SfxKind.Whistle);
        int w = s.Winner;
        // Formulé pour le joueur local (le texte de MatchController, « MANCHE POUR L'ÉQUIPE … », reste celui du réseau et du VR).
        string title = matchOver ? (w == View.LocalTeam ? "VICTOIRE !" : "DÉFAITE")
            : w < 0 ? "MANCHE NULLE" : w == View.LocalTeam ? "MANCHE GAGNÉE !" : "MANCHE PERDUE";
        int mine = s.Rounds(View.LocalTeam), theirs = s.Rounds(1 - View.LocalTeam);
        string sub = $"Manches   {mine}  –  {theirs}";
        var color = w < 0 ? UiKit.Grey : w == 0 ? UiKit.Orange : UiKit.Blue;
        double hold = matchOver ? 1.4 : 2.2;
        Announce(PrioBanner, 0.3 + hold + 0.35, () =>
        {
            bannerText.Text = title;
            bannerSub.Text = sub;
            bannerBand.AddThemeStyleboxOverride("panel", UiKit.Box(color, 0, -0.25f, 6, UiKit.Navy, 6));
            bannerBox.Visible = true;
            bannerBox.Modulate = Colors.White;
            // Entrée : la bande glisse de la gauche, le texte tape ; sortie en fondu.
            bannerBand.Position = bannerBand.Position with { X = -2400 };
            var tw = AnnTween(bannerBox);
            tw.TweenProperty(bannerBand, "position:x", -80.0, 0.3).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            tw.TweenInterval(hold);
            tw.TweenProperty(bannerBox, "modulate:a", 0.0, 0.35);
            tw.TweenCallback(Callable.From(() => bannerBox.Visible = false));
            bannerText.PivotOffset = new Vector2(GetViewport().GetVisibleRect().Size.X / 2, 50);
            UiKit.Punch(bannerText, 1.8f, 0.35);
        });
    }

    void UpdateCenter(MatchView.Snapshot s, int lives)
    {
        // Compte à rebours de début de manche : « 3, 2, 1 », puis « GO ! » au passage en jeu.
        if (s.Phase == MatchView.Phase.Countdown && (prev.Phase != MatchView.Phase.Countdown || s.Countdown != prev.Countdown) && s.Countdown > 0)
        {
            // Chaque manche est annoncée avec son objectif : « MANCHE 2 · Premier à 3 KO ».
            string goal = s.KoTarget > 0 ? $"  ·  Premier à {s.KoTarget} KO" : "";
            string title = prev.Phase != MatchView.Phase.Countdown ? (s.Overtime ? "PROLONGATION" : (s.Round > s.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {s.Round}") + goal) : "";
            ShowCountdown(s.Countdown.ToString(), false, title);
        }
        if (prev.Phase == MatchView.Phase.Countdown && s.Phase == MatchView.Phase.Playing) ShowCountdown("GO !", true);
        if (s.Phase == MatchView.Phase.RoundOver && prev.Phase != MatchView.Phase.RoundOver) ShowBanner(s, false);
        if (s.Phase == MatchView.Phase.MatchOver && prev.Phase != MatchView.Phase.MatchOver) ShowBanner(s, true);
        if (s.Overtime && !prev.Overtime && s.Phase == MatchView.Phase.Playing) { ShowCenterPop("MORT SUBITE !", UiKit.Yellow, 90); Sfx.Play(this, SfxKind.Whistle, null, 0.8f); }
        if (Tutorial.Running && (countdown.Visible || countdownRound.Visible || bannerBox.Visible) && annPrio >= PrioCountdown) ClearCenter();

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

        // Joueur KO : grand titre « KO ! » et retour dans n secondes (l'écran est grisé par Feedback). Il occupe le centre :
        // un éclat en cours s'efface, ceux qui suivent attendent.
        bool ko = lives <= 0 && s.Phase is MatchView.Phase.Playing or MatchView.Phase.Countdown;
        if (ko && koSince < 0)
        {
            koSince = age;
            if (annPrio == PrioPop) ClearCenter();
            UiKit.Punch(koTitle, 2.4f, 0.35);
        }
        if (!ko) koSince = -1;
        koTitle.Visible = koSub.Visible = ko;
        if (ko)
        {
            int left = Mathf.CeilToInt(3f - (float)(age - koSince));
            koSub.Text = left > 0 ? $"Retour dans {left}" : "Retour imminent…";
        }
        PumpAnnouncements();

        resume.Visible = Input.MouseMode != Input.MouseModeEnum.Captured && !Player.Autoplay && !PauseMenu.IsOpen && !EndScreen.IsOpen;
    }

    // ------------------------------------------------------------------ aide des touches (F1, bas droite)

    PanelContainer help = null!;
    Control helpHint = null!;
    bool helpShown;
    const double HintSeconds = 5.0;

    void BuildHelp()
    {
        help = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Modulate = new Color(1, 1, 1, 0) };
        var box = UiKit.Box(new Color(UiKit.Navy, 0.82f), 14, 0f, 0, null, 4);
        box.ContentMarginLeft = 18; box.ContentMarginRight = 18; box.ContentMarginTop = 12; box.ContentMarginBottom = 14;
        help.AddThemeStyleboxOverride("panel", box);
        help.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        help.GrowHorizontal = Control.GrowDirection.Begin;
        help.GrowVertical = Control.GrowDirection.Begin;
        help.OffsetRight = -28; help.OffsetBottom = -28; help.OffsetLeft = -28; help.OffsetTop = -28;
        helpLayer.AddChild(help);
        var col = new VBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        col.AddThemeConstantOverride("separation", 8);
        help.AddChild(col);
        col.AddChild(UiKit.Text("COMMANDES", 24, UiKit.Yellow, 6));
        var grid = new GridContainer { Columns = 2, MouseFilter = Control.MouseFilterEnum.Ignore };
        grid.AddThemeConstantOverride("h_separation", 14);
        grid.AddThemeConstantOverride("v_separation", 6);
        col.AddChild(grid);
        string move = InputSetup.KeyName("move_forward") + InputSetup.KeyName("move_left") + InputSetup.KeyName("move_back") + InputSetup.KeyName("move_right");
        void Row(string key, string label)
        {
            var cell = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
            cell.AddChild(KeyCap(key, 19));
            grid.AddChild(cell);
            var text = UiKit.Text(label, 20, Colors.White, 5);
            text.VerticalAlignment = VerticalAlignment.Center;
            text.SizeFlagsVertical = Control.SizeFlags.Fill;
            grid.AddChild(text);
        }
        Row(move, "Se déplacer");
        foreach (var a in InputSetup.Actions)
        {
            if (a.Name.StartsWith("move_")) continue;
            if (!Player.FreeRoam && a.Name is "pass" or "lob" or "feint" or "curve") continue;   // coups du jeu libre seulement
            Row(InputSetup.KeyName(a.Name), a.Name == "help" ? "Fermer cette aide" : a.Label);
        }

        // Rappel discret des premières secondes : « [F1] commandes ».
        var hint = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore, Alignment = BoxContainer.AlignmentMode.End };
        hint.AddThemeConstantOverride("separation", 10);
        hint.SetAnchorsPreset(Control.LayoutPreset.BottomRight);
        hint.GrowHorizontal = Control.GrowDirection.Begin;
        hint.GrowVertical = Control.GrowDirection.Begin;
        hint.OffsetRight = -36; hint.OffsetBottom = -40; hint.OffsetLeft = -36; hint.OffsetTop = -40;
        hint.AddChild(KeyCap(InputSetup.KeyName("help"), 19));
        var words = UiKit.Text("commandes", 22, new Color(1, 1, 1, 0.9f), 6);
        words.VerticalAlignment = VerticalAlignment.Center;
        hint.AddChild(words);
        helpLayer.AddChild(hint);
        helpHint = hint;
    }

    void SetHelp(bool show)
    {
        if (helpShown == show) return;
        helpShown = show;
        var tw = help.CreateTween();
        tw.SetParallel();
        tw.TweenProperty(help, "modulate:a", show ? 1.0 : 0.0, 0.2);
        help.Scale = Vector2.One * (show ? 0.9f : 1f);
        help.PivotOffset = help.Size;
        tw.TweenProperty(help, "scale", Vector2.One, 0.25).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
    }

    bool helpWanted;

    public override void _UnhandledInput(InputEvent e)
    {
        if (PauseMenu.IsOpen || EndScreen.IsOpen) return;
        if (e.IsActionPressed("help")) helpWanted = !helpWanted;
    }

    void UpdateHelp()
    {
        SetHelp(helpWanted && !EndScreen.IsOpen);
        // Le rappel s'efface en fin de délai, ou dès que l'aide est ouverte.
        float hintAlpha = helpWanted ? 0f : Mathf.Clamp((float)(HintSeconds - age) / 0.5f, 0f, 1f);
        helpHint.Modulate = new Color(1, 1, 1, hintAlpha);
        helpHint.Visible = hintAlpha > 0.01f;
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
        var box = UiKit.Box(UiKit.Navy, 22, 0f, 0, null, 8);
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
        tableRows.AddChild(UiKit.Spacer(4));
        tableRows.AddChild(StatRow(null, "JOUEUR", new[] { "VIES", "KO", "TOUCHES", "BALLES\nATTRAPÉES", "LANCERS" }, true));
        foreach (int team in new[] { View.LocalTeam, 1 - View.LocalTeam })   // mon équipe d'abord
        {
            if (team != View.LocalTeam) tableRows.AddChild(UiKit.Spacer(6));
            tableRows.AddChild(TeamHeader(team, s));
            foreach (var p in View.Players)
            {
                if (p.Team != team) continue;
                var st = View.Stats != null && View.Stats.TryGetValue(p.Id, out var x) ? x : null;
                string D(int v) => st == null ? "-" : v.ToString();
                tableRows.AddChild(StatRow(p, View.NameOf(p.Id), new[] { $"{Mathf.Max(0, View.Lives(p.Id))}/{View.MaxLives}", D(st?.Kos ?? 0), D(st?.Hits ?? 0), D(st?.Catches ?? 0), D(st?.Throws ?? 0) }, false));
            }
        }
        if (!View.HasStats) tableRows.AddChild(UiKit.Text("Statistiques détaillées tenues par l'hôte", 20, new Color(1, 1, 1, 0.6f), 5, HorizontalAlignment.Center));
    }

    /// <summary>En-tête d'équipe du tableau : nom (« ton équipe » pour la mienne), manches gagnées en pastilles, KO de la manche.</summary>
    Control TeamHeader(int team, MatchView.Snapshot s)
    {
        var color = team == 0 ? UiKit.Orange : UiKit.Blue;
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        var box = UiKit.Box(new Color(UiKit.NavySoft, 0.9f), 10, -0.1f, 0, null, 0);
        box.BorderWidthLeft = 12; box.BorderColor = color;
        box.ContentMarginTop = 4; box.ContentMarginBottom = 4; box.ContentMarginLeft = 22; box.ContentMarginRight = 22;
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 10);
        row.AddChild(line);
        line.AddChild(UiKit.Text(team == 0 ? "ÉQUIPE ORANGE" : "ÉQUIPE BLEUE", 30, color.Lightened(0.25f), 8));
        if (team == View.LocalTeam)
        {
            var mine = UiKit.Text("ton équipe", 20, new Color(1, 1, 1, 0.75f), 5);
            mine.VerticalAlignment = VerticalAlignment.Center;
            mine.SizeFlagsVertical = Control.SizeFlags.Fill;
            line.AddChild(mine);
        }
        line.AddChild(new Control { SizeFlagsHorizontal = Control.SizeFlags.ExpandFill, MouseFilter = Control.MouseFilterEnum.Ignore });
        var rounds = UiKit.Text("MANCHES", 20, UiKit.Sky, 5);
        rounds.VerticalAlignment = VerticalAlignment.Center;
        rounds.SizeFlagsVertical = Control.SizeFlags.Fill;
        line.AddChild(rounds);
        for (int i = 0; i < roundPips[team].Count; i++)
        {
            var pip = new Pip { Color = color, On = i < s.Rounds(team), CustomMinimumSize = new Vector2(20, 20), SizeFlagsVertical = Control.SizeFlags.ShrinkCenter, MouseFilter = Control.MouseFilterEnum.Ignore };
            line.AddChild(pip);
        }
        var kos = UiKit.Text($"KO  {s.Kos(team)}", 26, Colors.White, 6, HorizontalAlignment.Right);
        kos.CustomMinimumSize = new Vector2(110, 0);
        line.AddChild(kos);
        return row;
    }

    Control StatRow(MatchView.PlayerInfo? p, string name, string[] cells, bool header)
    {
        var row = new PanelContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        // Couleurs d'équipe pleines et vives (comme l'écran de fin) ; ma ligne plus claire et cerclée de jaune.
        bool me = p != null && p.Id == View.LocalId;
        var bg = header ? new Color(0, 0, 0, 0) : p!.Team == 0 ? new Color(me ? "ff7a1f" : "d9520c") : new Color(me ? "2f8fff" : "1f63c9");
        var box = UiKit.Box(bg, 8, -0.1f, p != null && p.Id == View.LocalId ? 3 : 0, UiKit.Yellow, 0);
        box.ContentMarginTop = 2; box.ContentMarginBottom = 2; box.ContentMarginLeft = 18; box.ContentMarginRight = 18;
        row.AddThemeStyleboxOverride("panel", box);
        var line = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
        line.AddThemeConstantOverride("separation", 8);
        row.AddChild(line);
        int size = header ? 20 : 28;
        var n = UiKit.Text(name, size, header ? UiKit.Sky : Colors.White, 6);
        n.CustomMinimumSize = new Vector2(240, 0);
        n.VerticalAlignment = VerticalAlignment.Center;
        line.AddChild(n);
        foreach (var c in cells)
        {
            var l = UiKit.Text(c, size, header ? UiKit.Sky : Colors.White, 6, HorizontalAlignment.Center);
            l.CustomMinimumSize = new Vector2(130, 0);
            l.VerticalAlignment = VerticalAlignment.Center;
            if (header) l.AddThemeConstantOverride("line_spacing", -4);   // en-têtes sur deux lignes, serrées
            line.AddChild(l);
        }
        return row;
    }

    void UpdateScoreTable(double delta)
    {
        bool want = (Input.IsActionPressed("scoreboard") && !PauseMenu.IsOpen && !EndScreen.IsOpen && !Tutorial.Running) || (hudTest && age > 6 && age < 9);
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
    /// fil des KO à 2 s, marqueurs de touche à 2,5 et 3 s (KO à 3 s : bandeau « KO ! » en haut à gauche), cœur perdu côté gauche à 4 s,
    /// verrou adverse à 4,4 s puis tir entrant de 4,9 à 5,6 s, tableau des scores de 6 à 9 s, aide (F1) de 9,6 à 11 s.
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
        if (At(9.6) || At(11.0)) helpWanted = !helpWanted;
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
        // Ce qui se cache sous la pause (annonces, compte à rebours, alerte, aide) et pendant l'entraînement (score, chrono).
        bool paused = PauseMenu.IsOpen;
        centerLayer.Visible = !paused;
        helpLayer.Visible = !paused && prev.Phase != MatchView.Phase.MatchOver;
        board.Visible = !Tutorial.Running && prev.Phase != MatchView.Phase.MatchOver;
        if (paused) threat.Strength = 0f;
        // Arbre gelé (pause hors ligne) : le HUD tourne quand même pour se cacher, mais n'avance rien d'autre.
        if (GetTree().Paused) { threat.QueueRedraw(); return; }

        age += delta;
        View.Update();
        var s = View.State;
        int n = View.Lives(View.LocalId);
        if (hudTest) n = HudTest(delta, n);
        // Musique plus tendue en fin de manche serrée et en mort subite.
        bool tense = s.Phase == MatchView.Phase.Playing && (s.Overtime || s.Seconds <= 20
            || Mathf.Max(s.Kos0, s.Kos1) >= s.KoTarget - 1 && Mathf.Abs(s.Kos0 - s.Kos1) <= 1);
        Audio.Music.PlayIntensity(tense);
        if (prev.Phase == MatchView.Phase.MatchOver && s.Phase != MatchView.Phase.MatchOver) Audio.Music.PlayMatch(0.6f);   // revanche : la musique repart après le jingle
        if (s.Phase != prev.Phase)
            GD.Print($"[HUD] {s.Phase} : manche {s.Round}/{s.MaxRounds}, manches {s.Rounds0}-{s.Rounds1}, KO {s.Kos0}-{s.Kos1}, gagnant {s.Winner}{(View.HasStats ? "" : " (client)")}");

        // Touché : flash crème du côté du coup (0,3 s) ; le cœur perdu se brise près des cœurs (UpdateHearts).
        if (n < lastLives && n >= 0) { HurtFrom(); if (hudTest) vignette.From = Vector2.Left; vignette.Strength = 1f; }
        lastLives = n;
        vignette.Strength = Mathf.Max(0, vignette.Strength - (float)delta / 0.3f);
        vignette.QueueRedraw();
        speedLines.Strength = Player.Dashing ? 1f : Mathf.Max(0, speedLines.Strength - (float)delta * 5f);
        speedLines.QueueRedraw();
        UpdateThreat(delta, paused || EndScreen.IsOpen || s.Phase is MatchView.Phase.RoundOver or MatchView.Phase.MatchOver);

        float charge = hudTest && age > 9.5 && age < 11.5 ? Mathf.Clamp((float)(age - 9.5) / 1.2f, 0f, 1f) : Player.ThrowCharge01;   // test : charge simulée
        chargeShown = Mathf.Lerp(chargeShown, charge, Mathf.Min(1f, (float)delta * 25f));
        hitMarkerLeft = Mathf.Max(0, hitMarkerLeft - delta);
        crosshair.Visible = n > 0 && s.Phase != MatchView.Phase.MatchOver;
        crosshair.QueueRedraw();

        UpdateScoreBoard(s);
        UpdateHearts(n, delta);
        UpdateBottomCenter();
        UpdateFeed(delta);
        UpdateCenter(s, n);
        UpdateScoreTable(delta);
        UpdateHelp();

        fps.Visible = GameSettings.ShowFps;
        if (fps.Visible) fps.Text = $"{Engine.GetFramesPerSecond():0} IPS";
        // Fin de partie : seul le bandeau « VICTOIRE ! » / « DÉFAITE » reste, puis l'écran de fin remplace tout le HUD.
        bool matchOver = s.Phase == MatchView.Phase.MatchOver;
        board.Visible = !Tutorial.Running && !matchOver;
        heartsHolder.Visible = bottomHolder.Visible = feed.Visible = !matchOver;
        helpLayer.Visible = !paused && !matchOver;
        if (matchOver) { koBanner.Visible = false; table.Visible = false; vignette.Strength = 0f; heartBreak.T = -1f; }
        root.Visible = !EndScreen.IsOpen;
        prev = s;
    }
}
