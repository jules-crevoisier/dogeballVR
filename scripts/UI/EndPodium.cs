using System;
using System.Collections.Generic;
using Godot;
using DogeballVR.Audio;
using DogeballVR.Gameplay;
using DogeballVR.Visual;

namespace DogeballVR.UI;

/// <summary>
/// Première phase de l'écran de fin, à la Knockout City : l'équipe gagnante en 3D sur une petite scène à ses couleurs
/// (monde isolé dans un SubViewport, rien de la partie n'y entre), le MVP au centre et devant sur un socle, ses coéquipiers
/// en pose de victoire de part et d'autre. Par-dessus : VICTOIRE ou DÉFAITE (en défaite on montre quand même les vainqueurs),
/// bandeau de l'équipe en haut à droite, cartouches des pseudos en bas, celui du MVP avec son étoile et sa stat principale.
/// Sans statistiques (client réseau), pas de MVP : l'équipe seule, le joueur local au centre s'il a gagné.
/// Créé et libéré par <see cref="EndScreen"/>, qui glisse ensuite vers le tableau.
/// </summary>
public partial class EndPodium : Control
{
    public MatchView View { get; set; } = null!;
    public int Winner { get; set; }
    public IReadOnlyDictionary<int, MatchController.PlayerStats>? Stats { get; set; }
    /// <summary>MVP désigné par l'écran de fin, -1 si inconnu (pas de statistiques) : pas de carte MVP.</summary>
    public int MvpId { get; set; } = -1;

    SubViewport world = null!;
    SubViewportContainer view = null!;
    Camera3D cam = null!;
    Control backdrop = null!;
    Label title = null!, sub = null!, teamLabel = null!;
    Control hint = null!;
    EndScreen.Star? star;
    readonly List<(Node3D hero, Control card, bool mvp)> slots = new();
    double t;

    const float CardsBand = 150f;   // hauteur du bandeau du bas, sous les cartouches

    public override void _Ready()
    {
        SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        MouseFilter = MouseFilterEnum.Stop;   // la partie ne reçoit pas les clics (le clic qui passe le podium est lu par l'écran de fin)

        // Fond 2D aux couleurs de l'équipe gagnante : rayons qui tournent derrière les personnages, comme le titre du tableau.
        backdrop = new Control { MouseFilter = MouseFilterEnum.Ignore };
        backdrop.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        backdrop.Draw += DrawBackdrop;
        AddChild(backdrop);

        // Scène 3D isolée, fond transparent : le fond dessiné ci-dessus reste visible derrière les personnages.
        view = new SubViewportContainer { Stretch = true, MouseFilter = MouseFilterEnum.Ignore };
        view.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        AddChild(view);
        world = new SubViewport
        {
            OwnWorld3D = true, TransparentBg = true, Msaa3D = Viewport.Msaa.Msaa4X, HandleInputLocally = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
        };
        view.AddChild(world);
        BuildStage();

        var band = new Control { MouseFilter = MouseFilterEnum.Ignore };
        band.SetAnchorsAndOffsetsPreset(LayoutPreset.FullRect);
        band.Draw += () => DrawBands(band);
        AddChild(band);
        BuildTitle();
        BuildTeamBanner();

        // Les cartouches suivent les personnages : placés à chaque image d'après la caméra (voir _Process).
        foreach (var s in slots) AddChild(s.card);
        hint = UiKit.KeyHint("Entrée", "PASSER", 22);
        hint.Modulate = new Color(1, 1, 1, 0.75f);
        AddChild(hint);
        Layout();
    }

    Color TeamBright => Winner == 0 ? new Color("ff7a1f") : new Color("2f8fff");
    Color TeamDark => Winner == 0 ? new Color("5a1d06") : new Color("0b1f52");
    string TeamLabel => Winner == 0 ? "ÉQUIPE ORANGE" : "ÉQUIPE BLEUE";

    /// <summary>
    /// Personnage du podium, seul point qui instancie un héros et lance son animation : à rebrancher sur les nouveaux héros
    /// sans toucher au reste. Renvoie un socle à poser (pieds à l'origine, regard vers -Z) ; le MVP célèbre en boucle,
    /// ses coéquipiers tiennent « Victoire » si le modèle l'a, sinon « Celebration » décalée dans le temps.
    /// </summary>
    internal static Node3D SpawnPodiumHero(int team, int playerId, bool mvp)
    {
        var socle = new Node3D { Name = $"Heros{playerId}" };
        var avatar = Avatar.Create(team, false, playerId);
        // Hors jeu : l'avatar ne réagit plus aux balles ni ne revient au repos ; seule son animation tourne.
        avatar.ProcessMode = ProcessModeEnum.Disabled;
        socle.AddChild(avatar);
        avatar.Pose(new Vector3(0, 1.62f, 0), 0f, new Vector3(-0.35f, 1.0f, -0.2f), new Vector3(0.35f, 1.0f, -0.2f));
        if (avatar.FindChild("AnimationPlayer", true, false) is not AnimationPlayer ap) return socle;
        ap.ProcessMode = ProcessModeEnum.Pausable;
        string anim = !mvp && ap.HasAnimation("Victoire") ? "Victoire" : ap.HasAnimation("Celebration") ? "Celebration" : "Repos";
        if (!ap.HasAnimation(anim)) return socle;
        ap.GetAnimation(anim).LoopMode = Animation.LoopModeEnum.Linear;
        avatar.PlayAction(anim, double.MaxValue);
        if (!mvp)
        {
            // Pas en chœur : chacun part d'un autre instant de l'animation, un peu plus lentement que le MVP.
            double length = ap.GetAnimation(anim).Length;
            ap.Seek(length * (0.3 + 0.23 * Mathf.PosMod(playerId, 3)), true);
            ap.SpeedScale = 0.9f;
        }
        return socle;
    }

    /// <summary>Vainqueurs, MVP (ou joueur local, ou premier) en tête, puis du meilleur au moins bon quand on a les chiffres.</summary>
    List<int> Winners()
    {
        var ids = new List<int>();
        foreach (var p in View.Players) if (p.Team == Winner) ids.Add(p.Id);
        int Score(int id) => Stats != null && Stats.TryGetValue(id, out var st) ? st.Score : 0;
        int lead = MvpId >= 0 ? MvpId : ids.Contains(View.LocalId) ? View.LocalId : -1;
        ids.Sort((a, b) => a == lead ? -1 : b == lead ? 1 : Score(b) != Score(a) ? Score(b).CompareTo(Score(a)) : a.CompareTo(b));
        return ids;
    }

    void BuildStage()
    {
        var env = new Godot.Environment
        {
            BackgroundMode = Godot.Environment.BGMode.ClearColor,
            AmbientLightSource = Godot.Environment.AmbientSource.Color,
            AmbientLightColor = new Color(0.86f, 0.88f, 1f), AmbientLightEnergy = 0.55f,
        };
        world.AddChild(new WorldEnvironment { Environment = env });
        // Lumière flatteuse : clé chaude de trois quarts face, débouchage froid de l'autre côté, contre-jour par personnage.
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-32, -28, 0), LightEnergy = 1.05f, LightColor = new Color("fff0dc") });
        world.AddChild(new DirectionalLight3D { RotationDegrees = new Vector3(-15, 40, 0), LightEnergy = 0.3f, LightColor = new Color("c8d8ff") });

        // Scène ronde aux couleurs de l'équipe, socle du MVP cerclé de jaune.
        var floorMat = new StandardMaterial3D { AlbedoColor = TeamDark.Lerp(TeamBright, 0.35f), Roughness = 0.8f };
        world.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 4.2f, BottomRadius = 4.2f, Height = 0.1f, RadialSegments = 64 }, Position = new Vector3(0, -0.05f, -0.6f), MaterialOverride = floorMat });
        world.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 4.12f, OuterRadius = 4.3f, Rings = 64 }, Position = new Vector3(0, 0f, -0.6f), MaterialOverride = Materials.Glow(TeamBright, 0.6f) });

        var ids = Winners();
        for (int i = 0; i < ids.Count; i++)
        {
            int id = ids[i];
            bool center = i == 0;
            bool mvp = center && MvpId == id;
            // Centre devant ; les autres en alternance gauche / droite, un peu en retrait.
            int rank = (i + 1) / 2;
            float side = i % 2 == 1 ? -1f : 1f;
            var pos = center ? new Vector3(0, 0, 0.55f) : new Vector3(side * 1.7f * rank, 0, -0.3f - 0.4f * (rank - 1));
            if (center)
            {
                world.AddChild(new MeshInstance3D { Mesh = new CylinderMesh { TopRadius = 0.72f, BottomRadius = 0.8f, Height = 0.28f, RadialSegments = 48 }, Position = pos + new Vector3(0, 0.14f, 0), MaterialOverride = new StandardMaterial3D { AlbedoColor = UiKit.Navy.Lightened(0.15f), Roughness = 0.6f } });
                world.AddChild(new MeshInstance3D { Mesh = new TorusMesh { InnerRadius = 0.7f, OuterRadius = 0.78f, Rings = 48 }, Position = pos + new Vector3(0, 0.28f, 0), MaterialOverride = Materials.Glow(UiKit.Yellow, 1.2f) });
                pos.Y = 0.28f;
            }
            var hero = SpawnPodiumHero(Winner, id, center);
            hero.Position = pos;
            // Tous regardent un point juste derrière la caméra : de face au centre, de trois quarts sur les côtés.
            var dir = new Vector3(0, 0, 9f) - pos;
            hero.Rotation = new Vector3(0, Mathf.Atan2(-dir.X, -dir.Z), 0);
            world.AddChild(hero);
            // Ombre de contact (pas d'ombres temps réel ici) et contre-jour à la couleur de l'équipe derrière chacun.
            world.AddChild(new MeshInstance3D
            {
                Mesh = new CylinderMesh { TopRadius = 0.42f, BottomRadius = 0.42f, Height = 0.005f }, Position = pos + new Vector3(0, 0.01f, 0),
                MaterialOverride = new StandardMaterial3D { AlbedoColor = new Color(0, 0, 0, 0.35f), Transparency = BaseMaterial3D.TransparencyEnum.Alpha, ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded },
            });
            world.AddChild(new OmniLight3D { Position = pos + new Vector3(0, 1.5f, -0.8f), LightColor = center ? UiKit.Yellow : TeamBright, LightEnergy = 0.38f, OmniRange = 3f });
            // Entrée : chacun jaillit à son tour, le MVP en dernier.
            hero.Scale = Vector3.One * 0.01f;
            var tw = hero.CreateTween();
            tw.TweenInterval(center ? 0.4 : 0.1 + 0.1 * i);
            tw.TweenProperty(hero, "scale", Vector3.One, 0.35).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
            slots.Add((hero, Card(id, mvp, center ? 0.6f : 0.25f + 0.1f * i), mvp));
        }

        cam = new Camera3D { Fov = 30f, Current = true };
        world.AddChild(cam);
        PlaceCamera();
    }

    /// <summary>
    /// Lente avancée de la caméra pendant le podium (de 10 m à 9,3 m), à peine plongeante : les têtes restent sous le titre,
    /// les pieds au-dessus des cartouches.
    /// </summary>
    void PlaceCamera()
    {
        float k = Mathf.Clamp((float)t / 3.5f, 0f, 1f);
        k = 1f - (1f - k) * (1f - k);
        cam.Position = new Vector3(0.3f * (1f - k), 1.75f, 10f - 0.7f * k);
        cam.LookAt(new Vector3(0, 1.4f, 0), Vector3.Up);
    }

    void BuildTitle()
    {
        bool win = Winner == View.LocalTeam;
        title = UiKit.Text(win ? "VICTOIRE !" : "DÉFAITE", 150, win ? UiKit.Yellow : new Color("9fb4d8"), 28, HorizontalAlignment.Center);
        AddChild(title);
        var s = View.State;
        sub = UiKit.Text($"MANCHES  {s.Rounds(Winner)} - {s.Rounds(1 - Winner)}", 34, UiKit.Cream, 10, HorizontalAlignment.Center);
        AddChild(sub);
        title.Scale = Vector2.One * 2.2f;
        title.Modulate = new Color(1, 1, 1, 0);
        var tw = title.CreateTween().SetParallel();
        tw.TweenProperty(title, "scale", Vector2.One, 0.45).SetTrans(Tween.TransitionType.Back).SetEase(Tween.EaseType.Out);
        tw.TweenProperty(title, "modulate:a", 1.0, 0.2);
        sub.Modulate = new Color(1, 1, 1, 0);
        sub.CreateTween().TweenProperty(sub, "modulate:a", 1.0, 0.3).SetDelay(0.3);
    }

    /// <summary>Bandeau de l'équipe gagnante en haut à droite, penché, comme « Blue Team » chez Knockout City.</summary>
    void BuildTeamBanner()
    {
        teamLabel = UiKit.Text(TeamLabel, 50, Colors.White, 10, HorizontalAlignment.Right);
        if (UiKit.Italic is Font it) teamLabel.AddThemeFontOverride("font", it);
        AddChild(teamLabel);
        teamLabel.Modulate = new Color(1, 1, 1, 0);
        teamLabel.CreateTween().TweenProperty(teamLabel, "modulate:a", 1.0, 0.25).SetDelay(0.15);
    }

    /// <summary>Cartouche d'un vainqueur : pseudo sur plaque crème ; le joueur local cerclé de jaune ; le MVP avec étoile et stat.</summary>
    Control Card(int id, bool mvp, float delay)
    {
        bool local = id == View.LocalId;
        var plate = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
        // Plaque crème penchée, ombre encre décalée (comme les items du menu) ; la mienne cerclée de jaune.
        var box = new KcBox { Top = UiKit.Cream, Bottom = new Color("efe2c8"), Slant = 12, BorderWidth = local ? 4 : 0, BorderColor = UiKit.Yellow, ShadowOffset = new Vector2(6, 6), ShadowColor = new Color("0a0f22", 0.6f) };
        box.ContentMarginLeft = 30; box.ContentMarginRight = 30; box.ContentMarginTop = 6; box.ContentMarginBottom = 8;
        plate.AddThemeStyleboxOverride("panel", box);
        plate.CustomMinimumSize = new Vector2(mvp ? 320 : 280, 0);
        var name = UiKit.Text(View.NameOf(id), mvp ? 42 : 36, UiKit.Navy, 0, HorizontalAlignment.Center);
        if (UiKit.Italic is Font it) name.AddThemeFontOverride("font", it);
        plate.AddChild(name);
        Control card = plate;
        if (mvp)
        {
            // Carte MVP : cadre bleu nuit liseré de jaune, étoile qui respire, MVP, pseudo, puis la stat principale (KO).
            var frame = new PanelContainer { MouseFilter = MouseFilterEnum.Ignore };
            var fb = UiKit.PanelBox(14, 18);
            fb.BorderColor = UiKit.Yellow;
            fb.ContentMarginLeft = 22; fb.ContentMarginRight = 22; fb.ContentMarginTop = 4; fb.ContentMarginBottom = 14;
            frame.AddThemeStyleboxOverride("panel", fb);
            var col = new VBoxContainer { MouseFilter = MouseFilterEnum.Ignore };
            col.AddThemeConstantOverride("separation", 4);
            frame.AddChild(col);
            var head = new HBoxContainer { Alignment = BoxContainer.AlignmentMode.Center, MouseFilter = MouseFilterEnum.Ignore };
            head.AddThemeConstantOverride("separation", 4);
            star = new EndScreen.Star { CustomMinimumSize = new Vector2(84, 76), MouseFilter = MouseFilterEnum.Ignore, Radius = 30f };
            head.AddChild(star);
            var mvpLabel = UiKit.Title("MVP", 56, HorizontalAlignment.Left);
            mvpLabel.VerticalAlignment = VerticalAlignment.Center;
            head.AddChild(mvpLabel);
            col.AddChild(head);
            col.AddChild(plate);
            if (Stats != null && Stats.TryGetValue(id, out var st))
                col.AddChild(UiKit.Text($"{st.Kos} KO   ·   {st.Hits} {(st.Hits > 1 ? "touches" : "touche")}", 28, UiKit.Cream, 6, HorizontalAlignment.Center));
            card = frame;
        }
        card.Modulate = new Color(1, 1, 1, 0);
        var tw = card.CreateTween();
        tw.TweenInterval(delay);
        tw.TweenCallback(Callable.From(() =>
        {
            card.PivotOffset = card.Size / 2;
            UiKit.Punch(card, mvp ? 1.5f : 1.25f, mvp ? 0.32 : 0.25);
            if (mvp) Sfx.Play(this, SfxKind.Perfect);
        }));
        tw.TweenProperty(card, "modulate:a", 1.0, 0.15);
        return card;
    }

    /// <summary>
    /// Mise en page, à chaque image (la taille de l'écran peut changer, l'interface agrandie la réduit) : titres centrés,
    /// nom d'équipe dans son bandeau, et cartouches sous leur personnage (projection de ses pieds) ; le MVP déborde vers le haut.
    /// </summary>
    void Layout()
    {
        title.Position = new Vector2((Size.X - title.Size.X) / 2, 10);
        title.PivotOffset = title.Size / 2;
        sub.Position = new Vector2((Size.X - sub.Size.X) / 2, 176);
        teamLabel.Position = new Vector2(Size.X - teamLabel.Size.X - 36, 22);
        hint.Position = new Vector2(Size.X - hint.Size.X - 28, Size.Y - CardsBand - hint.Size.Y - 18);
        if (world.Size.X <= 0 || view.Size.X <= 0) return;
        var k = view.Size / new Vector2(world.Size.X, world.Size.Y);
        foreach (var (hero, card, mvp) in slots)
        {
            var feet = cam.UnprojectPosition(hero.GlobalPosition) * k;
            float bottom = Size.Y - (mvp ? 26 : CardsBand / 2 - 34);
            card.Position = new Vector2(feet.X - card.Size.X / 2, bottom - card.Size.Y);
        }
    }

    public override void _Process(double delta)
    {
        t += delta;
        PlaceCamera();
        Layout();
        backdrop.QueueRedraw();
        if (star != null) { star.T = (float)t; star.QueueRedraw(); }
    }

    void DrawBackdrop()
    {
        var size = backdrop.Size;
        backdrop.DrawRect(new Rect2(Vector2.Zero, size), TeamDark);
        var c = new Vector2(size.X / 2, size.Y * 0.5f);
        float r = size.Length();
        const int n = 16;
        for (int i = 0; i < n; i++)
        {
            float a = (float)t * 0.1f + Mathf.Tau * i / n;
            backdrop.DrawPolygon(new[] { c, c + Vector2.FromAngle(a) * r, c + Vector2.FromAngle(a + Mathf.Tau / n * 0.5f) * r },
                new[] { new Color(TeamBright, 0.32f), new Color(TeamBright, 0.06f), new Color(TeamBright, 0.06f) });
        }
        for (int k = 7; k >= 1; k--) backdrop.DrawCircle(c, 90f * k, new Color(TeamBright.Lightened(0.3f), 0.04f));
    }

    /// <summary>Bandeau bas penché, bleu nuit liseré de la couleur de l'équipe, sur lequel reposent les cartouches.</summary>
    void DrawBands(Control band)
    {
        var s = band.Size;
        float top = s.Y - CardsBand;
        band.DrawColoredPolygon(new[] { new Vector2(0, top + 26), new Vector2(s.X, top - 10), new Vector2(s.X, s.Y), new Vector2(0, s.Y) }, TeamBright);
        band.DrawColoredPolygon(new[] { new Vector2(0, top + 36), new Vector2(s.X, top), new Vector2(s.X, s.Y), new Vector2(0, s.Y) }, UiKit.Navy);
        // Bandeau d'équipe en haut à droite : parallélogramme sous le nom.
        band.DrawColoredPolygon(new[] { new Vector2(s.X - 470, 18), new Vector2(s.X, 6), new Vector2(s.X, 96), new Vector2(s.X - 500, 104) }, UiKit.Navy);
        band.DrawColoredPolygon(new[] { new Vector2(s.X - 456, 26), new Vector2(s.X, 15), new Vector2(s.X, 88), new Vector2(s.X - 482, 95) }, TeamBright);
    }
}
