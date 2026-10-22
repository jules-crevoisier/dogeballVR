using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Gameplay;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Fin de partie dans le casque (EVA) : à la fin du match, une petite scène apparaît devant le joueur, à 4 m dans la direction
/// de son regard (plus près devant un couvert), réduite pour garder la taille apparente de la mise en page à 7,5 m. Titre (VICTOIRE, DÉFAITE ou ÉGALITÉ), manches, l'équipe gagnante en pose de victoire sur des socles (le MVP au
/// centre et plus haut), et à côté le tableau des joueurs : KO, touches, attrapes. En bas, le décompte avant la partie suivante.
/// Tout en 3D, sans texte collé au regard ; le panneau mural de score reste affiché. La scène disparaît quand la partie suivante
/// commence. Mêmes données que l'écran de fin du PC (<see cref="MatchView"/>), chez l'hôte comme chez le client.
/// </summary>
public partial class VrEndStage : Node3D
{
    public MatchView View { get; set; } = null!;
    /// <summary>Caméra du joueur (casque) : la scène se place face à son regard au moment de la fin.</summary>
    public Func<Camera3D?> Head { get; set; } = () => null;
    /// <summary>Hauteur du sol sous le joueur (plaque, lift).</summary>
    public Func<float> FloorY { get; set; } = () => 0f;
    /// <summary>Durée de l'écran avant la partie suivante (décompte affiché) ; 0 = pas de relance automatique.</summary>
    public float HoldSeconds { get; set; } = 20f;
    public float Distance { get; set; } = 4f;
    /// <summary>Test de capture au clavier seulement : la scène suit la caméra (en casque, elle reste fixe dans le monde).</summary>
    public bool FollowCamera { get; set; }

    Node3D? stage;
    Label3D? footer;
    float placedAt;
    double clock, shownAt, nextRead;   // temps de jeu (suit --fixed-fps), pas l'horloge murale

    /// <summary>Distance de la mise en page de référence (capture au clavier) ; plus près, toute la scène est réduite d'autant
    /// pour garder la même taille apparente (titre lisible sans tourner la tête).</summary>
    const float LayoutDistance = 7.5f;
    /// <summary>Lecture de l'état du match (sur un client, analyse du texte du HUD) : 5 fois par seconde suffit.</summary>
    const double ReadEvery = 0.2;

    // Abscisses dans le repère du tableau, vu depuis -Z : +X est à la gauche du joueur.
    const float ColumnName = 1.25f, ColumnKo = 0.1f, ColumnHits = -0.45f, ColumnCatches = -1.1f;

    public override void _Process(double delta)
    {
        clock += delta;
        if (clock >= nextRead)
        {
            nextRead = clock + ReadEvery;
            View.Update();
        }
        bool over = View.State.Phase == MatchView.Phase.MatchOver;
        if (over && stage == null) ShowStage();
        else if (!over && stage != null) HideStage();
        if (stage != null && FollowCamera) Place();
        if (stage != null && footer != null)
        {
            double left = HoldSeconds - (clock - shownAt);
            footer.Text = HoldSeconds > 0 ? $"Nouvelle partie dans {Mathf.Max(0, Mathf.CeilToInt((float)left))} s" : "En attente de l'opérateur";
        }
    }

    public override void _ExitTree() => View.Detach();

    void ShowStage()
    {
        shownAt = clock;
        stage = new Node3D { Name = "Scene" };
        AddChild(stage);
        Place();

        int winner = View.State.Winner;
        var stats = EndScreen.TestStats(View, View.Stats, winner);   // chiffres crédibles dans le seul test de capture
        int mvp = Mvp(winner, stats);
        Color teamColor = winner >= 0 ? Palette.Team(winner) : Palette.Lines;
        string title = winner < 0 ? "ÉGALITÉ" : winner == View.LocalTeam ? "VICTOIRE !" : "DÉFAITE";
        AddText(title, new Vector3(0, 3.3f, 0), 0.012f, 120, teamColor);
        string teamName = winner == 0 ? "ÉQUIPE ORANGE" : winner == 1 ? "ÉQUIPE BLEUE" : "PERSONNE";
        AddText($"{teamName} GAGNE  {View.State.Rounds(0)} - {View.State.Rounds(1)}", new Vector3(0, 2.7f, 0), 0.006f, 64, Palette.Lines);

        // Podium : l'équipe gagnante (sans vainqueur, l'équipe du joueur), le MVP au centre sur le socle haut.
        int shown = winner >= 0 ? winner : View.LocalTeam;
        var ids = new List<int>();
        foreach (var p in View.Players) if (p.Team == shown) ids.Add(p.Id);
        int Score(int id) => stats != null && stats.TryGetValue(id, out var st) ? st.Score : 0;
        int lead = mvp >= 0 ? mvp : ids.Contains(View.LocalId) ? View.LocalId : -1;
        ids.Sort((a, b) => a == lead ? -1 : b == lead ? 1 : Score(b).CompareTo(Score(a)) != 0 ? Score(b).CompareTo(Score(a)) : a.CompareTo(b));
        var podium = new Node3D { Name = "Podium", Position = new Vector3(-0.9f, 0, 0) };
        stage.AddChild(podium);
        for (int i = 0; i < ids.Count; i++)
        {
            int rank = (i + 1) / 2;
            float side = i % 2 == 1 ? -1f : 1f;
            float h = i == 0 ? 0.35f : 0.18f;
            var pos = i == 0 ? new Vector3(0, 0, 0) : new Vector3(side * 1.05f * rank, 0, 0.35f * rank);
            podium.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.48f, Height = h, RadialSegments = 32 },
                Position = pos + new Vector3(0, h / 2, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = UiKit.Navy.Lightened(0.18f), Roughness = 0.6f },
            });
            podium.AddChild(new MeshInstance3D
            {
                Mesh = new TorusMesh { InnerRadius = 0.41f, OuterRadius = 0.47f, Rings = 32 }, Position = pos + new Vector3(0, h, 0),
                MaterialOverride = Materials.Glow(i == 0 && mvp >= 0 ? UiKit.Yellow : Palette.Team(shown), 1.1f),
            });
            var hero = EndPodium.SpawnPodiumHero(shown, ids[i], i == 0);
            hero.Position = pos + new Vector3(0, h, 0);
            hero.Rotation = Vector3.Zero;   // l'avatar regarde vers -Z : face au joueur
            hero.Scale = Vector3.One * 0.01f;
            podium.AddChild(hero);
            var tw = hero.CreateTween();
            tw.TweenInterval(i == 0 ? 0.5 : 0.15 + 0.12 * i);
            tw.TweenProperty(hero, "scale", Vector3.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            string star = ids[i] == mvp ? "★ MVP  " : "";
            var tag = AddText(star + View.NameOf(ids[i]), pos + new Vector3(0, h + 2.0f, -0.05f), 0.0045f, 48, ids[i] == mvp ? UiKit.Yellow : Palette.Lines, podium);
            tag.Billboard = BaseMaterial3D.BillboardModeEnum.FixedY;
        }

        BuildBoard(stats, mvp);
        GD.Print($"[FinVR] Scène devant le joueur à {placedAt:0.0} m (échelle {stage.Scale.X:0.00}) : {title}, podium de {ids.Count} joueur(s), MVP {(mvp >= 0 ? View.NameOf(mvp) : "aucun")}, "
            + $"tableau {(stats != null ? View.Players.Count : 0)} ligne(s), relance {(HoldSeconds > 0 ? $"dans {HoldSeconds:0} s" : "par l'opérateur")}");
    }

    /// <summary>Repère de la scène : origine au sol, à Distance devant le regard (à plat) ; -Z de la scène vers le joueur.</summary>
    void Place()
    {
        var cam = Head();
        var eye = cam?.GlobalPosition ?? new Vector3(0, 1.6f, 0);
        var look = cam != null ? -cam.GlobalTransform.Basis.Z : Vector3.Forward;
        look.Y = 0;
        look = look.LengthSquared() < 1e-4f ? Vector3.Forward : look.Normalized();
        var feet = new Vector3(eye.X, FloorY(), eye.Z);
        placedAt = ClearDistance(feet, look);
        // Réduction centrée sur la hauteur des yeux (pas sur le sol) : chaque élément garde l'angle sous lequel on le voit
        // dans la mise en page de référence ; le podium flotte alors un peu au-dessus du sol, comme un hologramme.
        float scale = Mathf.Min(1f, placedAt / LayoutDistance);
        float eyeHeight = Mathf.Max(0.5f, eye.Y - feet.Y);
        stage!.GlobalPosition = feet + look * placedAt + Vector3.Up * (1f - scale) * eyeHeight;
        stage.GlobalRotation = new Vector3(0, Mathf.Atan2(look.X, look.Z), 0);
        stage.Scale = Vector3.One * scale;
    }

    /// <summary>
    /// Distance de la scène, ramenée devant le premier décor solide (couvert, mur) pour que le podium ne soit pas caché derrière :
    /// trois rayons à 1 m du sol (centre, côté podium, côté tableau), jamais plus près que 2 m.
    /// </summary>
    float ClearDistance(Vector3 feet, Vector3 look)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return Distance;
        var side = new Vector3(look.Z, 0, -look.X);   // +X de la scène
        float best = Distance;
        foreach (float offset in new[] { 0f, -1.0f, 1.9f })
        {
            var from = feet + Vector3.Up * 1.0f;
            var to = from + look * Distance + side * offset;
            var query = PhysicsRayQueryParameters3D.Create(from, to);
            query.CollideWithAreas = false;
            var hit = space.IntersectRay(query);
            if (hit.Count > 0 && hit["collider"].AsGodotObject() is StaticBody3D)
                best = Mathf.Min(best, from.DistanceTo(hit["position"].AsVector3()) - 0.6f);
        }
        return Mathf.Max(2f, best);
    }

    /// <summary>Tableau des joueurs à droite du podium, tourné de 20° vers le joueur : une ligne par joueur, équipe gagnante en haut.</summary>
    void BuildBoard(IReadOnlyDictionary<int, MatchController.PlayerStats>? stats, int mvp)
    {
        // Face lisible vers -Z local ; le joueur est en (-1,9 ; -Distance) vu du tableau : rotation positive pour lui faire face.
        var board = new Node3D { Name = "Tableau", Position = new Vector3(1.9f, 0, 0.2f), Rotation = new Vector3(0, Mathf.DegToRad(20), 0) };
        stage!.AddChild(board);
        int rows = Mathf.Max(1, View.Players.Count);
        float top = 2.45f, step = 0.22f, height = 0.45f + rows * step;   // à l'échelle 1 (mise en page à 7,5 m), au-dessus des couverts
        board.AddChild(new MeshInstance3D
        {
            Mesh = new BoxMesh { Size = new Vector3(2.9f, height, 0.04f) }, Position = new Vector3(0, top - height / 2 + 0.2f, 0.05f),
            MaterialOverride = new StandardMaterial3D
            {
                AlbedoColor = new Color(UiKit.Navy, 0.85f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                RenderPriority = 1,   // sous le texte ; test de profondeur gardé : un coéquipier plus proche passe devant
            },
        });
        void Cell(string text, float x, float y, Color c, HorizontalAlignment align = HorizontalAlignment.Center, int size = 52)
        {
            var l = AddText(text, new Vector3(x, y, 0), 0.0032f, size, c, board);
            l.HorizontalAlignment = align;
        }
        Cell("JOUEUR", ColumnName, top, UiKit.Lavender, HorizontalAlignment.Left, 40);
        Cell("KO", ColumnKo, top, UiKit.Lavender, size: 40);
        Cell("TOUCHES", ColumnHits, top, UiKit.Lavender, size: 40);
        Cell("ATTRAPES", ColumnCatches, top, UiKit.Lavender, size: 40);
        // Décompte sous le tableau.
        footer = AddText("", new Vector3(0, top - height - 0.1f, 0), 0.0042f, 56, Palette.Lines, board);
        if (stats == null)
        {
            Cell("Statistiques en attente de l'hôte", 0, top - step * 1.5f, Palette.Lines);
            return;
        }
        var order = new List<MatchView.PlayerInfo>(View.Players);
        int winner = View.State.Winner;
        int Key(MatchView.PlayerInfo p) => stats.TryGetValue(p.Id, out var st) ? st.Score : 0;
        order.Sort((a, b) => a.Team != b.Team ? (a.Team == winner ? -1 : b.Team == winner ? 1 : a.Team.CompareTo(b.Team)) : Key(b).CompareTo(Key(a)));
        for (int i = 0; i < order.Count; i++)
        {
            var p = order[i];
            stats.TryGetValue(p.Id, out var st);
            float y = top - step * (i + 1.3f);
            var c = Palette.Team(p.Team);
            string name = (p.Id == mvp ? "★ " : "") + View.NameOf(p.Id);
            if (name.Length > 12) name = name[..11] + "…";   // la colonne KO commence 1,15 m plus loin
            Cell(name, ColumnName, y, p.Id == View.LocalId ? UiKit.Yellow : c, HorizontalAlignment.Left);
            Cell($"{st?.Kos ?? 0}", ColumnKo, y, c);
            Cell($"{st?.Hits ?? 0}", ColumnHits, y, c);
            Cell($"{st?.Catches ?? 0}", ColumnCatches, y, c);
        }
    }

    Label3D AddText(string text, Vector3 pos, float pixel, int size, Color color, Node3D? parent = null)
    {
        var l = new Label3D
        {
            Text = text, Position = pos, PixelSize = pixel, FontSize = size, Modulate = color, OutlineModulate = UiKit.Navy, OutlineSize = size / 5,
            Rotation = new Vector3(0, Mathf.Pi, 0), Shaded = false, DoubleSided = false, RenderPriority = 2,   // au-dessus du panneau ; la scène est déjà ramenée devant les couverts
        };
        if (UiKit.Font != null) l.Font = UiKit.Font;
        (parent ?? stage!).AddChild(l);
        return l;
    }

    void HideStage()
    {
        GD.Print($"[FinVR] Scène retirée après {clock - shownAt:0.0} s");
        stage?.QueueFree();
        stage = null;
        footer = null;
    }

    /// <summary>MVP pris dans l'équipe gagnante (même règle que l'écran de fin du PC) ; sans vainqueur, le meilleur des deux équipes.</summary>
    int Mvp(int winner, IReadOnlyDictionary<int, MatchController.PlayerStats>? stats)
    {
        int best = -1, bestScore = -1, bestKos = -1;
        foreach (var p in View.Players)
        {
            if (winner >= 0 && p.Team != winner) continue;
            if (stats == null || !stats.TryGetValue(p.Id, out var st)) continue;
            if (st.Score > bestScore || (st.Score == bestScore && st.Kos > bestKos)) { best = p.Id; bestScore = st.Score; bestKos = st.Kos; }
        }
        return bestScore > 0 ? best : -1;
    }
}
