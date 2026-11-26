using System.Collections.Generic;
using Godot;

namespace DogeballVR.Maps;

/// <summary>
/// Grille de navigation des bots sur une arène libre (cases de 0,5 m), tirée de <see cref="ArenaGeometry"/> : chaque case
/// praticable a la hauteur de son sol. Liens : marche (marche ≤ StepUp), saut (jusqu'à <see cref="MaxJumpUp"/>), descente
/// (jusqu'à <see cref="MaxDrop"/>) et tremplins. Recherche A* déterministe, pur C# (testable sans moteur).
/// </summary>
public sealed class NavGrid
{
    public const float Cell = 0.5f;
    /// <summary>Hauteur franchie d'un saut de bot (vitesse 5,5 m/s, gravité 12 : sommet 1,26 m, plus la marche).</summary>
    public const float MaxJumpUp = 1.3f;
    public const float MaxDrop = 3.2f;
    /// <summary>Rayon gardé libre autour d'un bot (bords de vide, murs).</summary>
    public const float Clearance = 0.35f;

    public enum Link : byte { Walk, Jump, Drop, Pad }

    readonly ArenaGeometry geo;
    public int W { get; }
    public int H { get; }
    readonly float x0, z0;
    readonly float[] ground;   // NaN : case impraticable
    readonly List<(int to, Link kind, float cost)>[] links;

    public NavGrid(ArenaGeometry geo, BoundsDef bounds)
    {
        this.geo = geo;
        x0 = bounds.MinX; z0 = bounds.MinZ;
        W = Mathf.Max(1, Mathf.CeilToInt((bounds.MaxX - bounds.MinX) / Cell));
        H = Mathf.Max(1, Mathf.CeilToInt((bounds.MaxZ - bounds.MinZ) / Cell));
        ground = new float[W * H];
        links = new List<(int, Link, float)>[W * H];
        for (int j = 0; j < H; j++)
            for (int i = 0; i < W; i++)
                ground[j * W + i] = Sample(CenterOf(i, j));
        BuildLinks();
    }

    public Vector2 CenterOf(int i, int j) => new(x0 + (i + 0.5f) * Cell, z0 + (j + 0.5f) * Cell);
    public bool Walkable(int index) => index >= 0 && index < ground.Length && !float.IsNaN(ground[index]);
    public float GroundOf(int index) => ground[index];
    public int Count => ground.Length;

    /// <summary>Case sous le point, -1 hors grille.</summary>
    public int IndexOf(Vector2 p)
    {
        int i = Mathf.FloorToInt((p.X - x0) / Cell), j = Mathf.FloorToInt((p.Y - z0) / Cell);
        return i < 0 || j < 0 || i >= W || j >= H ? -1 : j * W + i;
    }

    public Vector3 PointOf(int index) { var c = CenterOf(index % W, index / W); return new Vector3(c.X, ground[index], c.Y); }

    /// <summary>
    /// Hauteur praticable au point : le centre et quatre points à <see cref="Clearance"/> doivent tous avoir un sol, sans mur
    /// (dessus plus haut d'une marche) ni bord (dessus plus bas d'une demi-marche). NaN sinon.
    /// </summary>
    float Sample(Vector2 c)
    {
        float h = geo.HighestAt(c.X, c.Y, 0f);
        if (float.IsNegativeInfinity(h)) return float.NaN;
        var d = Clearance;
        foreach (var o in new[] { new Vector2(d, 0), new Vector2(-d, 0), new Vector2(0, d), new Vector2(0, -d), new Vector2(d, d) * 0.7f, new Vector2(-d, d) * 0.7f, new Vector2(d, -d) * 0.7f, new Vector2(-d, -d) * 0.7f })
        {
            float s = geo.HighestAt(c.X + o.X, c.Y + o.Y, 0f);
            if (float.IsNegativeInfinity(s) || s > h + geo.StepUp * 0.6f || s < h - 0.3f) return float.NaN;
        }
        // Plafond : un bloc juste au-dessus (dessous d'un toit) n'est pas praticable.
        return h;
    }

    static readonly (int di, int dj)[] Dirs = { (1, 0), (-1, 0), (0, 1), (0, -1), (1, 1), (1, -1), (-1, 1), (-1, -1) };

    void BuildLinks()
    {
        for (int idx = 0; idx < ground.Length; idx++)
        {
            if (!Walkable(idx)) continue;
            var list = links[idx] = new List<(int, Link, float)>(8);
            int i = idx % W, j = idx / W;
            float h = ground[idx];
            foreach (var (di, dj) in Dirs)
            {
                float stepLen = (di != 0 && dj != 0 ? 1.4142f : 1f) * Cell;
                // Case voisine directe, puis jusqu'à 4 cases plus loin si l'entre-deux est un rebord (sol présent mais trop
                // près d'un mur ou d'une marche) : c'est ainsi qu'on relie le haut et le bas d'une marche ou d'un muret.
                float midTop = float.NegativeInfinity;
                for (int k = 1; k <= 4; k++)
                {
                    int ni = i + di * k, nj = j + dj * k;
                    if (ni < 0 || nj < 0 || ni >= W || nj >= H) break;
                    int n = nj * W + ni;
                    if (!Walkable(n))
                    {
                        var c = CenterOf(ni, nj);
                        float top = geo.HighestAt(c.X, c.Y, 0f);
                        if (float.IsNegativeInfinity(top)) break;   // pas de saut par-dessus le vide
                        midTop = Mathf.Max(midTop, top);
                        continue;
                    }
                    if (k > 1 && di != 0 && dj != 0) break;   // longs liens : seulement en ligne droite
                    if (midTop > Mathf.Max(h, ground[n]) + 0.1f) break;   // un mur entre les deux : pas de lien
                    float dh = ground[n] - h;
                    float len = stepLen * k;
                    if (dh <= geo.StepUp && dh >= -geo.StepUp) { if (k == 1 || Mathf.Abs(dh) > 0.05f) list.Add((n, Link.Walk, len)); }
                    else if (dh > 0 && dh <= MaxJumpUp) list.Add((n, Link.Jump, len + 1.5f));
                    else if (dh < 0 && -dh <= MaxDrop) list.Add((n, Link.Drop, len + 0.5f));
                    break;
                }
            }
        }
        foreach (var pad in geo.Def.Pads)
        {
            int from = NearestWalkable(new Vector2(pad.X, pad.Z), 2f), to = NearestWalkable(new Vector2(pad.Tx, pad.Tz), 2.5f);
            if (from < 0 || to < 0) continue;
            float len = new Vector2(pad.Tx - pad.X, pad.Tz - pad.Z).Length();
            links[from].Add((to, Link.Pad, len * 0.5f + 1f));
        }
    }

    /// <summary>Case praticable la plus proche du point, dans un rayon donné (m) ; -1 si aucune.</summary>
    public int NearestWalkable(Vector2 p, float radius = 3f)
    {
        int c = IndexOf(p);
        if (Walkable(c)) return c;
        int best = -1; float bestD = float.MaxValue;
        int r = Mathf.CeilToInt(radius / Cell);
        int ci = Mathf.FloorToInt((p.X - x0) / Cell), cj = Mathf.FloorToInt((p.Y - z0) / Cell);
        for (int j = cj - r; j <= cj + r; j++)
            for (int i = ci - r; i <= ci + r; i++)
            {
                if (i < 0 || j < 0 || i >= W || j >= H) continue;
                int n = j * W + i;
                if (!Walkable(n)) continue;
                float d = CenterOf(i, j).DistanceSquaredTo(p);
                if (d < bestD) { bestD = d; best = n; }
            }
        return best;
    }

    /// <summary>Liens sortants d'une case (pour les tests et le débogage).</summary>
    public IReadOnlyList<(int to, Link kind, float cost)> LinksOf(int index) => (IReadOnlyList<(int, Link, float)>?)links[index] ?? System.Array.Empty<(int, Link, float)>();

    /// <summary>Étape d'un chemin : point à atteindre et façon d'y aller.</summary>
    public readonly record struct Step(Vector3 Point, Link Kind);

    /// <summary>Chemin A* entre deux cases praticables (liste vide si aucun). Le départ n'est pas inclus.</summary>
    public List<Step> FindPath(int start, int goal, int maxExpanded = 6000)
    {
        var result = new List<Step>();
        if (!Walkable(start) || !Walkable(goal)) return result;
        if (start == goal) { result.Add(new Step(PointOf(goal), Link.Walk)); return result; }
        var g = new Dictionary<int, float> { [start] = 0f };
        var came = new Dictionary<int, (int from, Link kind)>();
        var open = new PriorityQueue<int, float>();
        var goalP = PointOf(goal);
        open.Enqueue(start, Heuristic(start, goalP));
        int expanded = 0;
        while (open.Count > 0 && expanded++ < maxExpanded)
        {
            int cur = open.Dequeue();
            if (cur == goal) break;
            float gc = g[cur];
            foreach (var (to, kind, cost) in LinksOf(cur))
            {
                float ng = gc + cost;
                if (g.TryGetValue(to, out float old) && old <= ng) continue;
                g[to] = ng;
                came[to] = (cur, kind);
                open.Enqueue(to, ng + Heuristic(to, goalP));
            }
        }
        if (!came.ContainsKey(goal)) return result;
        for (int n = goal; n != start; n = came[n].from) result.Add(new Step(PointOf(n), came[n].kind));
        result.Reverse();
        return Smooth(result, PointOf(start));
    }

    float Heuristic(int index, Vector3 goal)
    {
        var p = PointOf(index);
        return new Vector2(p.X - goal.X, p.Z - goal.Z).Length();
    }

    /// <summary>Retire les points intermédiaires d'une ligne droite praticable au même niveau (le bot ne zigzague pas de case en case).</summary>
    List<Step> Smooth(List<Step> path, Vector3 from)
    {
        if (path.Count < 3) return path;
        var outp = new List<Step>();
        var anchor = from;
        for (int k = 0; k < path.Count; k++)
        {
            bool last = k == path.Count - 1;
            if (!last && path[k].Kind == Link.Walk && path[k + 1].Kind == Link.Walk && StraightWalk(anchor, path[k + 1].Point)) continue;
            outp.Add(path[k]);
            anchor = path[k].Point;
        }
        return outp;
    }

    /// <summary>Ligne droite praticable sans saut : toutes les cases traversées sont praticables, à moins d'une marche l'une de l'autre.</summary>
    public bool StraightWalk(Vector3 a, Vector3 b)
    {
        var a2 = new Vector2(a.X, a.Z); var b2 = new Vector2(b.X, b.Z);
        float len = a2.DistanceTo(b2);
        int n = Mathf.Max(1, Mathf.CeilToInt(len / (Cell * 0.5f)));
        float prev = a.Y;
        for (int s = 1; s <= n; s++)
        {
            int idx = IndexOf(a2.Lerp(b2, s / (float)n));
            if (!Walkable(idx)) return false;
            float h = ground[idx];
            if (Mathf.Abs(h - prev) > geo.StepUp) return false;
            prev = h;
        }
        return true;
    }

    /// <summary>Nombre de cases praticables (journal de construction, tests).</summary>
    public int WalkableCount()
    {
        int n = 0;
        for (int k = 0; k < ground.Length; k++) if (Walkable(k)) n++;
        return n;
    }

    /// <summary>Case praticable atteignable depuis <paramref name="from"/> ? (parcours en largeur, pour vérifier une carte.)</summary>
    public HashSet<int> Reachable(int from)
    {
        var seen = new HashSet<int>();
        if (!Walkable(from)) return seen;
        var q = new Queue<int>();
        q.Enqueue(from); seen.Add(from);
        while (q.Count > 0)
        {
            int c = q.Dequeue();
            foreach (var (to, _, _) in LinksOf(c)) if (seen.Add(to)) q.Enqueue(to);
        }
        return seen;
    }
}
