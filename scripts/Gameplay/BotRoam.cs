using System.Collections.Generic;
using Godot;
using DogeballVR.Maps;

namespace DogeballVR.Gameplay;

/// <summary>Monde partagé par les bots d'une arène libre : géométrie, grille de navigation, places des équipes.</summary>
public sealed class RoamWorld
{
    public ArenaGeometry Geo { get; }
    public NavGrid Nav { get; }
    public MapData Map { get; }
    /// <summary>Balle visée par chaque bot : un seul bot court vers une balle normale ; une balle spéciale est disputée, un bot
    /// par équipe (clé : balle et équipe, -1 pour une balle normale).</summary>
    public Dictionary<(Ball, int), Bot> Claims { get; } = new();

    /// <summary>Clé de réservation d'une balle pour un bot de l'équipe donnée.</summary>
    public static (Ball, int) ClaimKey(Ball b, int team) => (b, b.Kind == Ball.KindNormal ? -1 : team);

    /// <summary>Libère la réservation de ce bot sur cette balle (seulement si c'est la sienne).</summary>
    public void Unclaim(Ball? b, Bot bot)
    {
        if (b == null) return;
        // Sans lire b.Kind : la balle peut déjà être libérée (bombe explosée, balle recyclée).
        foreach (var key in new[] { (b, -1), (b, bot.Hurtbox.Team) })
            if (Claims.TryGetValue(key, out var who) && who == bot) Claims.Remove(key);
    }

    public RoamWorld(MapData map)
    {
        Map = map;
        Geo = map.Geometry!;
        Nav = new NavGrid(Geo, map.Bounds);
    }

    /// <summary>Direction « vers l'adversaire » d'une équipe : du barycentre de ses places vers celui des places adverses.</summary>
    public Vector2 Forward(int team)
    {
        Vector2 own = Vector2.Zero, other = Vector2.Zero; int no = 0, nt = 0;
        foreach (var s in Geo.Def.Spawns)
            if (s.Team == team) { own += new Vector2(s.X, s.Z); no++; } else { other += new Vector2(s.X, s.Z); nt++; }
        if (no == 0 || nt == 0) return Vector2.Up;
        var d = other / nt - own / no;
        return d.LengthSquared() < 0.01f ? Vector2.Up : d.Normalized();
    }
}

/// <summary>
/// Bots en jeu libre (arène PC) : ils courent sur toute la carte au lieu de rester sur une plaque. Sans balle, ils vont
/// chercher la plus proche (une balle par bot) ou tiennent une position couverte face à l'adversaire ; balle en main, ils
/// s'approchent à portée avec une ligne de tir. Ils sautent les murets, descendent des rebords, prennent les tremplins,
/// esquivent sur le côté sans tomber dans le vide, et peuvent être tacles et tomber (ring-out).
/// La lecture des tirs, l'attrape et le lancer restent ceux de <see cref="Bot"/>.
/// </summary>
public partial class Bot
{
    /// <summary>Arène libre en cours (posée par Main) ; null : bots de zone (cartes VR, moulinets).</summary>
    public static RoamWorld? World { get; set; }

    const float BotGravity = 12f, BotJumpSpeed = 5.5f, BotPadGravity = 5f, BotRadius = 0.3f;

    /// <summary>Hauteur des pieds (arène libre).</summary>
    public float Feet { get; private set; }
    /// <summary>Sol sous le bot (altitude envoyée aux clients).</summary>
    public float GroundY { get; private set; }

    Vector2 roamPos;
    float vy;
    bool roamOut, padFlight;
    Vector2 padVel;
    List<NavGrid.Step> path = new();
    int pathIdx;
    double roamClock, repathAt, postUntil, progressCheckAt, pushUntil;
    Vector2 pushVel, lastProgressPos;
    Vector3? goal;
    Ball? goalBall;
    Vector2 roamDodge, strafe, strafeTo;
    Node3D? roamHead;

    float RoamSpeed => Gentle ? 4.2f : Level switch { 0 => 5.0f, 2 => 6.8f, _ => 6.0f };

    /// <summary>À l'entrée en jeu libre : tête pour la hurtbox (chute et tacle jugés comme pour un joueur), position de départ.</summary>
    void SetupRoam()
    {
        roamHead = new Node3D { Name = "RoamHead", Position = new Vector3(0, 1.6f, 0) };
        AddChild(roamHead);
        Hurtbox.Head = roamHead;
        PlaceAtSpawn();
    }

    void PlaceAtSpawn()
    {
        var w = World!;
        roamPos = new Vector2(Zone.X, Zone.Z);
        float g = w.Geo.HighestAt(roamPos.X, roamPos.Y);
        Feet = GroundY = float.IsNegativeInfinity(g) ? 0f : g;
        vy = 0; padFlight = false; path.Clear(); goal = null; goalBall = null; pushUntil = 0; dodgeLeft = 0;
        ApplyRoamTransform();
    }

    /// <summary>Tacle reçu (arène libre) : poussé de 2 m dans la direction donnée ; au bord du vide, c'est la chute.</summary>
    public void ApplyPush(Vector2 dir)
    {
        if (World == null) return;
        pushVel = dir.Normalized() * 8f;
        pushUntil = roamClock + 0.25;
        DropHeld();
        path.Clear();
    }

    void Roam(double delta)
    {
        var w = World!;
        float dt = (float)delta;
        roamClock += delta;
        if (roamOut) { roamOut = false; PlaceAtSpawn(); }

        Vector2 move = Vector2.Zero;
        bool falling = Feet < GroundY - 0.3f || float.IsNegativeInfinity(GroundY);
        if (roamClock < pushUntil) move = pushVel;
        else if (padFlight) move = padVel;
        else if (falling) move = Vector2.Zero;
        else if (dodgeLeft > 0)
        {
            dodgeLeft -= delta;
            move = roamDodge * Speed * 2.5f;
        }
        else
        {
            if (roamClock >= repathAt) ChooseGoal();
            move = FollowPath();
        }

        // Garde-fou : de lui-même, un bot ne marche jamais dans le vide (seules une poussée ou une chute l'y mènent).
        if (roamClock >= pushUntil && !padFlight && move.LengthSquared() > 0.0001f)
        {
            var next = roamPos + move * dt;
            var probe = next + move.Normalized() * 0.35f;
            if (w.Geo.IsVoid(probe.X, probe.Y, 0f) && !w.Geo.IsVoid(roamPos.X, roamPos.Y, 0f)) { move = Vector2.Zero; dodgeLeft = 0; strafe = Vector2.Zero; }
        }
        roamPos += move * dt;
        var b = w.Map.Bounds;
        roamPos = new Vector2(Mathf.Clamp(roamPos.X, b.MinX + 0.5f, b.MaxX - 0.5f), Mathf.Clamp(roamPos.Y, b.MinZ + 0.5f, b.MaxZ - 0.5f));
        w.Geo.Collide(ref roamPos, Feet, BotRadius);
        StepVertical(dt);
        ApplyRoamTransform();
        CheckProgress();
    }

    void StepVertical(float dt)
    {
        var w = World!;
        float ground = w.Geo.GroundAt(roamPos.X, roamPos.Y, Feet);
        GroundY = ground;
        bool grounded = vy <= 0f && !float.IsNegativeInfinity(ground) && Feet <= ground + 0.03f;
        if (grounded)
        {
            Feet = ground; vy = 0f; padFlight = false;
            // Tremplin sur le chemin : on le prend (un bot ne saute pas sur un tremplin par hasard).
            if (pathIdx < path.Count && path[pathIdx].Kind == NavGrid.Link.Pad && w.Geo.PadAt(roamPos.X, roamPos.Y, Feet) is { } pad)
            {
                var (h, v, _) = ArenaGeometry.PadLaunch(pad, roamPos, Feet, BotPadGravity);
                padVel = h; vy = v; padFlight = true;
            }
            else if (pathIdx < path.Count && path[pathIdx].Kind == NavGrid.Link.Jump && path[pathIdx].Point.Y > Feet + w.Geo.StepUp
                     && new Vector2(path[pathIdx].Point.X - roamPos.X, path[pathIdx].Point.Z - roamPos.Y).Length() < 1.8f)
                vy = BotJumpSpeed;
        }
        if (!grounded || vy > 0f)
        {
            float prev = Feet;
            vy -= (padFlight ? BotPadGravity : BotGravity) * dt;
            Feet += vy * dt;
            if (vy < 0f && !float.IsNegativeInfinity(ground) && prev >= ground && Feet <= ground) { Feet = ground; vy = 0f; padFlight = false; }
            if (Feet < w.Geo.KillY - 3f) { Feet = w.Geo.KillY - 3f; vy = 0f; }
        }
    }

    void ApplyRoamTransform()
    {
        GlobalPosition = new Vector3(roamPos.X, Feet, roamPos.Y);
        Hurtbox.Elevation = Feet;
    }

    /// <summary>Le bot n'avance plus (coincé contre un angle) : on recalcule le chemin, puis on change de but.</summary>
    void CheckProgress()
    {
        if (roamClock < progressCheckAt) return;
        progressCheckAt = roamClock + 1.2;
        bool wantsToMove = pathIdx < path.Count;
        if (wantsToMove && roamPos.DistanceTo(lastProgressPos) < 0.4f) { path.Clear(); repathAt = 0; postUntil = 0; goalBall = null; }
        lastProgressPos = roamPos;
    }

    Vector2 FollowPath()
    {
        var w = World!;
        while (pathIdx < path.Count)
        {
            var p = path[pathIdx].Point;
            var to = new Vector2(p.X - roamPos.X, p.Z - roamPos.Y);
            bool sameLevel = Mathf.Abs(p.Y - Feet) < 0.6f;
            if (to.Length() < (sameLevel ? 0.35f : 0.25f) && (sameLevel || path[pathIdx].Kind == NavGrid.Link.Drop)) { pathIdx++; continue; }
            if (path[pathIdx].Kind == NavGrid.Link.Pad) return Vector2.Zero;   // en vol ou sur le tremplin : StepVertical s'en charge
            float speed = RoamSpeed * (held != null ? 0.9f : 1f);
            return to.Normalized() * Mathf.Min(speed, to.Length() / 0.1f);
        }
        // Au but : petits pas latéraux face à l'adversaire (on ne reste pas immobile, cible facile).
        if (held != null || goalBall == null)
        {
            changeDirTimer -= 1.0 / 60.0;
            if (changeDirTimer <= 0)
            {
                changeDirTimer = Level >= 2 ? rng.RandfRange(0.4f, 0.9f) : rng.RandfRange(0.6f, 1.4f);
                var fwd = w.Forward(Hurtbox.Team);
                var side = new Vector2(-fwd.Y, fwd.X) * (rng.Randf() < 0.5f ? -1f : 1f) * rng.RandfRange(0.8f, 2.0f);
                var dest = roamPos + side;
                int idx = w.Nav.IndexOf(dest);
                bool ok = w.Nav.StraightWalk(new Vector3(roamPos.X, Feet, roamPos.Y), new Vector3(dest.X, Feet, dest.Y)) && w.Nav.Walkable(idx) && Mathf.Abs(w.Nav.GroundOf(idx) - Feet) < 0.3f;
                strafe = ok ? side.Normalized() : Vector2.Zero;
                strafeTo = dest;
            }
            if (strafe != Vector2.Zero && roamPos.DistanceTo(strafeTo) < 0.2f) strafe = Vector2.Zero;   // pas de côté fini : on s'arrête là
            return strafe * RoamSpeed * 0.45f;
        }
        return Vector2.Zero;
    }

    /// <summary>Choisit quoi faire : balle à prendre, position de tir, ou poste couvert. Recalcule le chemin.</summary>
    void ChooseGoal()
    {
        var w = World!;
        repathAt = roamClock + 0.45;
        Vector3? want = null;
        if (held == null)
        {
            if (goalBall != null && (!IsInstanceValid(goalBall) || goalBall.IsHeld || !Free(goalBall))) { w.Unclaim(goalBall, this); goalBall = null; }
            // Balle spéciale posée sur son socle : on lâche une balle normale visée pour aller la disputer.
            if (goalBall is { Kind: Ball.KindNormal } && PickBall(specialOnly: true) is { } special) { w.Unclaim(goalBall, this); goalBall = special; }
            goalBall ??= PickBall();
            if (goalBall != null) want = goalBall.GlobalPosition;
            else if (roamClock >= postUntil || goal == null) { want = PickPost(); postUntil = roamClock + rng.RandfRange(2.5f, 4.5f); }
            else return;
        }
        else
        {
            if (goalBall != null) { w.Unclaim(goalBall, this); goalBall = null; }
            var target = Nearest(enemy: true);
            if (target == null) return;
            var tp = target.GlobalPosition;
            float dist = new Vector2(tp.X - roamPos.X, tp.Z - roamPos.Y).Length();
            if (dist <= 14f && LineOfSight(target)) { path.Clear(); pathIdx = 0; goal = GlobalPosition; return; }   // à portée : on tire d'ici
            // Plus près, jusqu'à 11 m de la cible, sur la ligne qui nous en sépare.
            var dir = new Vector2(roamPos.X - tp.X, roamPos.Y - tp.Z);
            var spot = dist > 11f ? new Vector2(tp.X, tp.Z) + dir.Normalized() * 11f : new Vector2(tp.X, tp.Z) + dir.Normalized() * Mathf.Max(dist - 2f, 4f);
            want = new Vector3(spot.X, tp.Y, spot.Y);
        }
        if (want is not Vector3 wv) return;
        int start = w.Nav.NearestWalkable(roamPos, 2f), end = w.Nav.NearestWalkable(new Vector2(wv.X, wv.Z), 3f);
        if (start < 0 || end < 0) { path.Clear(); return; }
        if (goal is Vector3 g && g.DistanceTo(w.Nav.PointOf(end)) < 0.6f && pathIdx < path.Count) return;   // même but, chemin en cours
        path = w.Nav.FindPath(start, end);
        pathIdx = 0;
        goal = w.Nav.PointOf(end);
    }

    /// <summary>Balle libre et ramassable : ni tenue, ni au-dessus du vide, presque immobile (ou posée sur un piédestal).</summary>
    bool Free(Ball b)
    {
        if (b.IsHeld || b.HasExploded) return false;
        var p = b.GlobalPosition;
        return p.Y > -0.5f && b.LinearVelocity.Length() < 7f && !World!.Geo.IsVoid(p.X, p.Z, 0.2f);
    }

    /// <summary>Portée (m) jusqu'à laquelle un bot sans balle va disputer la balle spéciale, et avance qu'on lui donne sur une
    /// balle normale dans le choix (elle vaut un détour).</summary>
    const float SpecialReach = 22f, SpecialBonus = 10f;

    /// <summary>
    /// Balle à aller prendre : la plus proche (hauteur comptée double), pénalisée si un adversaire en est bien plus près ;
    /// la balle spéciale passe devant (un bot par équipe la dispute, la pénalité d'adversaire ne s'applique pas : c'est une
    /// course). specialOnly : seulement une balle spéciale à portée, sans réserver si aucune.
    /// </summary>
    Ball? PickBall(bool specialOnly = false)
    {
        var w = World!;
        Ball? best = null; float bestD = float.MaxValue;
        var enemy = Nearest(enemy: true);
        foreach (var node in GetTree().GetNodesInGroup("balls"))
        {
            if (node is not Ball b || !Free(b)) continue;
            bool special = b.Kind != Ball.KindNormal;
            if (specialOnly && !special) continue;
            if (w.Claims.TryGetValue(RoamWorld.ClaimKey(b, Hurtbox.Team), out var other) && other != this && IsInstanceValid(other)) continue;
            var p = b.GlobalPosition;
            float d = new Vector2(p.X - roamPos.X, p.Z - roamPos.Y).Length() + Mathf.Abs(p.Y - Feet) * 2f;
            if (special)
            {
                if (d > SpecialReach) continue;
                d = Mathf.Max(0f, d - SpecialBonus);
            }
            // Une balle normale bien plus près d'un adversaire est un piège : pénalité.
            else if (enemy != null && enemy.GlobalPosition.DistanceTo(p) < d * 0.5f) d += 6f;
            if (d < bestD) { bestD = d; best = b; }
        }
        if (best != null) w.Claims[RoamWorld.ClaimKey(best, Hurtbox.Team)] = this;
        return best;
    }

    /// <summary>
    /// Poste sans balle : une case à 9-16 m de l'adversaire le plus proche, de préférence de notre côté et près d'un couvert
    /// (un dessus plus haut d'un mètre à moins de 1,5 m) ; on y attend une balle ou une passe.
    /// </summary>
    Vector3? PickPost()
    {
        var w = World!;
        var enemy = Nearest(enemy: true);
        var fwd = w.Forward(Hurtbox.Team);
        Vector3? best = null; float bestScore = float.MinValue;
        for (int k = 0; k < 28; k++)
        {
            var c = roamPos + new Vector2(rng.RandfRange(-12f, 12f), rng.RandfRange(-12f, 12f));
            int idx = w.Nav.NearestWalkable(c, 1f);
            if (idx < 0) continue;
            var p = w.Nav.PointOf(idx);
            float score = 0f;
            if (enemy != null)
            {
                float d = new Vector2(enemy.GlobalPosition.X - p.X, enemy.GlobalPosition.Z - p.Z).Length();
                score -= Mathf.Abs(d - 12f) * 0.6f;
            }
            score -= Mathf.Max(0f, new Vector2(p.X, p.Z).Dot(fwd)) * 0.25f;   // pas trop loin chez l'adversaire
            if (NearCover(p)) score += 3f;
            score -= new Vector2(p.X - roamPos.X, p.Z - roamPos.Y).Length() * 0.1f;
            if (score > bestScore) { bestScore = score; best = p; }
        }
        return best;
    }

    bool NearCover(Vector3 p)
    {
        var geo = World!.Geo;
        foreach (var o in new[] { new Vector2(1.2f, 0), new Vector2(-1.2f, 0), new Vector2(0, 1.2f), new Vector2(0, -1.2f) })
            if (geo.HighestAt(p.X + o.X, p.Z + o.Y) > p.Y + 1.0f) return true;
        return false;
    }

    /// <summary>Ligne de tir libre de la main à la poitrine de la cible (aucun bloc de l'arène entre les deux).</summary>
    bool LineOfSight(Node3D target)
    {
        var space = GetWorld3D()?.DirectSpaceState;
        if (space == null) return true;
        var from = GlobalPosition + Vector3.Up * 1.4f;
        var to = target.GlobalPosition + Vector3.Up * 1.1f;
        if (target is DesktopPlayer) to = target.GlobalPosition + Vector3.Up * 1.1f;
        var q = PhysicsRayQueryParameters3D.Create(from, to);
        q.CollideWithAreas = false;
        var hit = space.IntersectRay(q);
        return hit.Count == 0 || hit["collider"].AsGodotObject() is not StaticBody3D;
    }

    /// <summary>Esquive en jeu libre : pas de côté perpendiculaire au tir, vers une case praticable au même niveau (jamais le vide).</summary>
    void StartRoamDodge(Ball ball)
    {
        var w = World!;
        var v = new Vector2(ball.LinearVelocity.X, ball.LinearVelocity.Z);
        var side = v.LengthSquared() > 0.01f ? new Vector2(-v.Y, v.X).Normalized() : Vector2.Right;
        foreach (var s in new[] { side, -side })
        {
            // L'esquive parcourt ~2,6 m : toute la ligne doit être praticable au même niveau (pas d'esquive vers le vide).
            var dest = roamPos + s * 2.8f;
            if (w.Nav.StraightWalk(new Vector3(roamPos.X, Feet, roamPos.Y), new Vector3(dest.X, Feet, dest.Y))) { roamDodge = s; dodgeLeft = 0.35; Avatar?.PlayAction("Esquive", 0.4); return; }
        }
    }
}
