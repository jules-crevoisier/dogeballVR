using System;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Maps;
using DogeballVR.UI;

namespace DogeballVR.Visual;

/// <summary>
/// Panneau mural de score (VR) au bord arrière d'une zone, à 2,5 m de haut, hors de l'espace de marche, tourné vers la zone
/// et penché de 10° vers le joueur. DA v2 (refonte des interfaces) : un vrai tableau dessiné en 2D dans une SubViewport et
/// posé sur un quad de 3 × 1,5 m, comme la maquette « panneau mural » de l'étude KC (05-interfaces, § 8 bis) :
/// fond anthracite à liseré crème (pas de violet ni de turquoise dans le monde, réservés à la balle rebond et aux plots),
/// l'équipe de la zone à gauche ; chiffres du score et du chrono de 30 cm, objectif, pastilles de manche, une barre de
/// progression par équipe vers l'objectif, les joueurs de chaque équipe avec leurs cœurs, et une ligne d'événement
/// (départ, manche gagnée, prochaine manche dans 5…) qui prend la place des joueurs le temps de l'annonce.
/// La texture n'est redessinée que quand le contenu change (au plus une fois par seconde en jeu) : rien à payer sur le Quest.
/// </summary>
public partial class ScorePanel : Node3D
{
    /// <summary>Texte du tableau de score de l'hôte (relu par <see cref="MatchView.FromText"/>).</summary>
    public Func<string> Source { get; set; } = () => "";
    /// <summary>Équipe de la zone : affichée à gauche.</summary>
    public int Team { get; set; }
    /// <summary>Cœurs restants d'un joueur (id de zone).</summary>
    public Func<int, int> Lives { get; set; } = _ => 0;
    /// <summary>Nom affiché d'un joueur (id de zone).</summary>
    public Func<int, string> NameOf { get; set; } = id => $"JOUEUR {id + 1}";
    public int MaxLives { get; set; } = 2;
    /// <summary>Joueurs (ids de zone) de chaque équipe, dans l'ordre de la map.</summary>
    public int[][] Roster { get; set; } = { Array.Empty<int>(), Array.Empty<int>() };

    public const float Width = 3.0f, Height = 1.5f;
    const int PxW = 1200, PxH = 600;   // 400 px par mètre : chiffres de 120 px = 30 cm

    MatchView.Snapshot state = new(MatchView.Phase.Playing, 0, 0, 0, 0, 0, 0, 1, 5, false, "", -1, 3);
    SubViewport viewport = null!;
    Board board = null!;
    string lastKey = "";

    public static ScorePanel Create(ZoneDef zone, MapData map, Func<string> source, Func<int, int> lives, int maxLives, Func<int, string> nameOf)
    {
        var basis = new Basis(Vector3.Up, Mathf.DegToRad(zone.FacingDegrees));
        var back = basis * new Vector3(0, 0, map.ZoneDepth * 0.5f + 0.9f);   // derrière le joueur (il regarde vers -Z)
        var panel = new ScorePanel
        {
            Name = $"ScorePanel{zone.Id}", Source = source, Team = zone.Team, Position = zone.Center + back + new Vector3(0, 2.5f, 0),
            Rotation = new Vector3(Mathf.DegToRad(10f), Mathf.DegToRad(zone.FacingDegrees + 180f), 0),
            Lives = lives, MaxLives = Math.Max(1, maxLives), NameOf = nameOf,
        };
        var roster = new[] { new System.Collections.Generic.List<int>(), new System.Collections.Generic.List<int>() };
        foreach (var z in map.Zones) roster[z.Team == 1 ? 1 : 0].Add(z.Id);
        panel.Roster = new[] { roster[0].ToArray(), roster[1].ToArray() };
        panel.state = panel.state with { KoTarget = map.Rules.KoTarget > 0 ? map.Rules.KoTarget : 3 };
        return panel;
    }

    public override void _Ready()
    {
        viewport = new SubViewport
        {
            Size = new Vector2I(PxW, PxH), TransparentBg = true, Disable3D = true, RenderTargetUpdateMode = SubViewport.UpdateMode.Once,
            GuiDisableInput = true,
        };
        AddChild(viewport);
        board = new Board { Panel = this, Size = new Vector2(PxW, PxH) };
        viewport.AddChild(board);
        var mesh = new MeshInstance3D
        {
            Name = "Quad", Mesh = new QuadMesh { Size = new Vector2(Width, Height) },
            CastShadow = GeometryInstance3D.ShadowCastingSetting.Off,
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoTexture = viewport.GetTexture(), ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                Transparency = BaseMaterial3D.TransparencyEnum.AlphaScissor, AlphaScissorThreshold = 0.5f,
                TextureFilter = BaseMaterial3D.TextureFilterEnum.LinearWithMipmaps, CullMode = BaseMaterial3D.CullModeEnum.Back,
            },
        };
        AddChild(mesh);
        if (Tools.DevFlags.Has("--vr-panels")) GD.Print($"[Panneau] {Name} en {GlobalPosition.X:0.00},{GlobalPosition.Y:0.00},{GlobalPosition.Z:0.00}");
    }

    public override void _Process(double delta)
    {
        state = MatchView.FromText(Source(), state);
        var s = state;
        var hearts = new System.Text.StringBuilder();
        foreach (var team in Roster) foreach (var id in team) hearts.Append(Lives(id)).Append(',');
        string key = $"{hearts}{s.Phase}{s.Countdown}|{s.Seconds}|{s.Kos0}-{s.Kos1}|{s.Rounds0}-{s.Rounds1}|{s.Round}/{s.MaxRounds}|{s.Overtime}|{s.Winner}|{s.NextRoundIn}";
        if (key == lastKey) return;
        lastKey = key;
        board.State = s;
        board.QueueRedraw();
        viewport.RenderTargetUpdateMode = SubViewport.UpdateMode.Once;
    }

    /// <summary>Dessin du tableau (repère 1200 × 600 px, 400 px par mètre).</summary>
    sealed partial class Board : Control
    {
        public ScorePanel Panel = null!;
        public MatchView.Snapshot State;
        static readonly Color Back = UiKit.VioletTop, BackLow = UiKit.NavyLow, Ink = new("0a0910");

        static Vector2[] Slanted(Rect2 r, float s) => new[]
        {
            r.Position + new Vector2(s, 0), r.Position + new Vector2(r.Size.X, 0), r.Position + new Vector2(r.Size.X - s, r.Size.Y), r.Position + new Vector2(0, r.Size.Y),
        };

        void Text(string text, Vector2 at, float width, int size, Color color, Font? font, HorizontalAlignment align = HorizontalAlignment.Center, int outline = 0)
        {
            font ??= ThemeDB.FallbackFont;
            var baseLine = at + new Vector2(0, font.GetAscent(size));
            if (outline > 0) DrawStringOutline(font, baseLine, text, align, width, size, outline, Ink);
            DrawString(font, baseLine, text, align, width, size, color);
        }

        public override void _Draw()
        {
            var s = State;
            int left = Panel.Team == 1 ? 1 : 0, right = 1 - left;
            float w = Size.X, h = Size.Y;
            // Fond violet du menu, penché, liseré crème épais, ombre décalée.
            var body = new Rect2(24, 20, w - 48, h - 40);
            DrawColoredPolygon(Slanted(new Rect2(body.Position + new Vector2(10, 12), body.Size), 28), new Color(0, 0, 0, 0.55f));
            DrawColoredPolygon(Slanted(body, 28), UiKit.Cream);
            var inner = body.Grow(-10);
            DrawPolygon(Slanted(inner, 26), new[] { Back, Back, BackLow, BackLow });

            // Plaques d'équipe : gauche = équipe de la zone. Chiffres de 120 px (30 cm).
            for (int k = 0; k < 2; k++)
            {
                int team = k == 0 ? left : right;
                var color = team == 1 ? UiKit.Blue : UiKit.Orange;   // bleu soutenu : chiffre blanc lisible (le bleu ciel des maillots est trop clair)
                var plate = k == 0 ? new Rect2(70, 56, 380, 200) : new Rect2(w - 450, 56, 380, 200);
                DrawColoredPolygon(Slanted(new Rect2(plate.Position + new Vector2(0, 8), plate.Size), 34), new Color(0, 0, 0, 0.4f));
                DrawPolygon(Slanted(plate, 34), new[] { color.Lightened(0.05f), color.Lightened(0.05f), color.Darkened(0.15f), color.Darkened(0.15f) });
                string name = team == 0 ? "ORANGE" : "BLEUE";
                // Nom d'équipe en grand côté extérieur, chiffre des KO côté chrono (comme le HUD du PC).
                Text(name, plate.Position + new Vector2(k == 0 ? 30 : 160, 66), 192, 56, Colors.White, UiKit.Display, k == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right, 6);
                Text(s.Kos(team).ToString(), plate.Position + new Vector2(k == 0 ? 222 : 18, 14), 140, 160, Colors.White, UiKit.Display, HorizontalAlignment.Center, 10);
                // Pastilles de manche sous la plaque.
                int pips = Mathf.Clamp(s.MaxRounds, 1, 5);
                for (int i = 0; i < pips; i++)
                {
                    float x = k == 0 ? plate.Position.X + 40 + i * 66 : plate.End.X - 40 - 50 - i * 66;
                    var pr = new Rect2(x, plate.End.Y + 26, 50, 30);
                    bool on = i < s.Rounds(team);
                    DrawColoredPolygon(Slanted(pr, 10), on ? UiKit.Cream : new Color(UiKit.Cream, 0.5f));
                    DrawColoredPolygon(Slanted(pr.Grow(-5), 8), on ? color : Ink);
                }
            }

            // Chrono au centre (chiffres de 130 px), manche et objectif dessous.
            var mid = new Rect2(w / 2 - 170, 46, 340, 230);
            DrawColoredPolygon(Slanted(mid, 0), Ink);
            DrawRect(new Rect2(mid.Position.X, mid.End.Y - 10, mid.Size.X, 10), UiKit.Cream);
            string clock = $"{s.Seconds / 60}:{s.Seconds % 60:00}";
            bool urgent = s.Phase == MatchView.Phase.Playing && s.Seconds <= 10;
            Text(clock, mid.Position + new Vector2(0, 6), mid.Size.X, 140, urgent ? UiKit.Yellow : Colors.White, UiKit.Display);
            string round = s.Round > s.MaxRounds ? "MANCHE DÉCISIVE" : $"MANCHE {Mathf.Max(1, s.Round)}/{s.MaxRounds}";
            Text(round, mid.Position + new Vector2(0, 160), mid.Size.X, 46, new Color("c9c2e0"), UiKit.Italic);
            if (s.Overtime && s.Phase == MatchView.Phase.Playing)
            {
                var ot = new Rect2(mid.Position.X - 30, mid.End.Y + 14, mid.Size.X + 60, 56);
                DrawColoredPolygon(Slanted(ot, 18), UiKit.Yellow);
                Text("PROLONGATION", ot.Position + new Vector2(0, 2), ot.Size.X, 48, Ink, UiKit.Display);
                Text("LE PROCHAIN KO GAGNE", new Vector2(mid.Position.X - 40, ot.End.Y + 10), mid.Size.X + 80, 34, UiKit.Yellow, UiKit.Italic);
            }
            else Text(s.KoTarget > 0 ? $"PREMIER À {s.KoTarget} KO" : "", new Vector2(mid.Position.X - 40, mid.End.Y + 18), mid.Size.X + 80, 46, UiKit.Cream, UiKit.Italic);
            // Marque de l'arène au centre, comme le logo d'un écran géant de stade.
            Text("DOGEBALL", new Vector2(mid.Position.X - 40, 452), mid.Size.X + 80, 72, new Color(UiKit.Orange, 0.9f), UiKit.Display, HorizontalAlignment.Center, 6);

            // Ligne d'événement en bas : bande penchée à la couleur de l'équipe concernée (neutre sinon).
            string line = ""; Color band = new(1, 1, 1, 0.14f); Color ink = Colors.White;
            if (s.Phase == MatchView.Phase.Countdown && s.Countdown > 0) { line = $"DÉPART DANS {s.Countdown}"; band = UiKit.Cream; ink = Ink; }
            else if (s.Phase == MatchView.Phase.RoundOver)
            {
                line = s.Winner >= 0 ? $"MANCHE POUR LES {(s.Winner == 0 ? "ORANGE" : "BLEUS")}" : "MANCHE NULLE";
                if (s.NextRoundIn > 0) line += $"   ·   SUITE DANS {s.NextRoundIn}";
                band = s.Winner >= 0 ? Palette.Team(s.Winner) : band; ink = s.Winner >= 0 ? Ink : Colors.White;
            }
            else if (s.Phase == MatchView.Phase.MatchOver) { line = s.Winner >= 0 ? $"VICTOIRE DES {(s.Winner == 0 ? "ORANGE" : "BLEUS")} !" : "FIN DU MATCH"; band = s.Winner >= 0 ? Palette.Team(s.Winner) : band; ink = Ink; }
            if (line != "")
            {
                var r = new Rect2(110, h - 140, w - 220, 84);
                DrawColoredPolygon(Slanted(r, 30), band);
                Text(line, r.Position + new Vector2(0, 8), r.Size.X, 60, ink, UiKit.Display);
                return;
            }

            // Joueurs de chaque équipe sous leur plaque : nom, puis cœurs (vides quand perdus, nom grisé à 0).
            for (int k = 0; k < 2; k++)
            {
                int team = k == 0 ? left : right;
                var ids = Panel.Roster[team];
                if (ids.Length == 0) continue;
                float x0 = k == 0 ? 70 : w - 450, colW = 380, top = 382, rowH = Mathf.Min(82f, 166f / ids.Length);
                for (int i = 0; i < ids.Length; i++)
                {
                    int n = Panel.Lives(ids[i]);
                    var row = new Rect2(x0, top + i * rowH, colW, rowH - 8);
                    DrawColoredPolygon(Slanted(row, 14), new Color(1, 1, 1, n > 0 ? 0.07f : 0.03f));
                    DrawRect(new Rect2(k == 0 ? row.Position.X + 10 : row.End.X - 18, row.Position.Y + 8, 8, row.Size.Y - 16), Palette.Team(team) * new Color(1, 1, 1, n > 0 ? 1f : 0.35f));
                    float heartsW = Panel.MaxLives * (row.Size.Y * 0.8f);
                    int size = (int)Mathf.Min(50f, row.Size.Y * 0.72f);
                    string name = Panel.NameOf(ids[i]).ToUpperInvariant();
                    var nameColor = n > 0 ? Colors.White : new Color(1, 1, 1, 0.35f);
                    var nameAt = new Vector2(k == 0 ? row.Position.X + 32 : row.Position.X + heartsW + 20, row.Position.Y + (row.Size.Y - size * 1.1f) / 2);
                    Text(name, nameAt, colW - heartsW - 56, size, nameColor, UiKit.Italic, k == 0 ? HorizontalAlignment.Left : HorizontalAlignment.Right);
                    float r = row.Size.Y * 0.32f;
                    for (int j = 0; j < Panel.MaxLives; j++)
                    {
                        float cx = k == 0 ? row.End.X - 24 - r - j * (r * 2.5f) : row.Position.X + 24 + r + j * (r * 2.5f);
                        HeartAt(new Vector2(cx, row.GetCenter().Y + r * 0.1f), r, j < n ? Palette.Team(team) : (Color?)null);
                    }
                }
            }
        }

        void HeartAt(Vector2 c, float r, Color? fill)
        {
            var src = Gameplay.DesktopHud.Heart.Shape();
            Vector2[] At(Vector2 o, float rr) { var p = new Vector2[src.Length]; for (int i = 0; i < src.Length; i++) p[i] = o + src[i] * rr; return p; }
            DrawColoredPolygon(At(c, r + 4), fill != null ? Ink : new Color(UiKit.Cream, 0.5f));
            DrawColoredPolygon(At(c, r), fill ?? Ink);
            if (fill != null) DrawColoredPolygon(At(c + new Vector2(-r * 0.32f, -r * 0.3f), r * 0.28f), new Color(1, 1, 1, 0.5f));
        }
    }
}
