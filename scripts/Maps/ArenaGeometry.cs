using System.Collections.Generic;
using Godot;

namespace DogeballVR.Maps;

/// <summary>
/// Bloc plein d'une arène libre : sol, toit, couvert, mur, caisse… Son dessus se marche, ses flancs bloquent.
/// Repère : centre (X, Z), tailles Sx × Sz, du bas Y0 au dessus Y1, tourné de RotY degrés autour de la verticale.
/// </summary>
public class ArenaBlockDef
{
    public string Id { get; set; } = "";
    /// <summary>Rôle et habillage : floor, roof, cover, low, wall, crate, container, ac, tank, rail, bridge, stall, tower.</summary>
    public string Kind { get; set; } = "floor";
    public float X { get; set; }
    public float Z { get; set; }
    public float Sx { get; set; } = 1f;
    public float Sz { get; set; } = 1f;
    public float Y0 { get; set; } = -6f;
    public float Y1 { get; set; }
    public float RotY { get; set; }
    /// <summary>Couleur d'habillage (hex) ; sinon celle du rôle.</summary>
    public string? Color { get; set; }
    /// <summary>Modèle glb à poser à la place de la boîte (assets/models/props) ; la collision reste la boîte.</summary>
    public string? Model { get; set; }
    /// <summary>Les balles le traversent-elles ? (grillage, bâche) : faux par défaut.</summary>
    public bool BallsPass { get; set; }
}

/// <summary>Rampe (ou escalier) alignée sur les axes : la hauteur monte de H0 à H1 dans la direction Dir (+x, -x, +z, -z).</summary>
public class ArenaRampDef
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Sx { get; set; } = 2f;
    public float Sz { get; set; } = 4f;
    public float H0 { get; set; }
    public float H1 { get; set; } = 1f;
    public string Dir { get; set; } = "+z";
    /// <summary>Marches visibles (escalier) plutôt qu'un plan incliné ; la marche se fait pareil.</summary>
    public bool Stairs { get; set; }
}

/// <summary>Tremplin : un joueur qui passe dessus est lancé en cloche vers le point (Tx, Ty, Tz). Recharge par joueur.</summary>
public class ArenaPadDef
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Y { get; set; }
    public float Radius { get; set; } = 0.8f;
    public float Tx { get; set; }
    public float Ty { get; set; }
    public float Tz { get; set; }
    /// <summary>Hauteur du sommet de la cloche au-dessus du point le plus haut (départ ou arrivée).</summary>
    public float Apex { get; set; } = 2.5f;
    public float CooldownSeconds { get; set; } = 2f;
}

/// <summary>Point d'apparition d'un joueur : chaque point est une place (zone) de l'équipe.</summary>
public class ArenaSpawnDef
{
    public int Team { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public float FacingDegrees { get; set; }
}

/// <summary>Décor sans collision (repère lointain, rue en contrebas, enseigne) : modèle glb ou boîte colorée.</summary>
public class ArenaDecorDef
{
    public string? Model { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Sx { get; set; } = 1f;
    public float Sy { get; set; } = 1f;
    public float Sz { get; set; } = 1f;
    public float RotY { get; set; }
    public float Scale { get; set; } = 1f;
    public string Color { get; set; } = "#8a8f98";
    /// <summary>Lumière émise (néon) : 0 = mat.</summary>
    public float Glow { get; set; }
    /// <summary>Motif procédural (KcShaders) : 2 béton, 1 brique, 5 fenêtres, 7 asphalte, 0 uni.</summary>
    public int Pattern { get; set; } = 2;
    public string? Color2 { get; set; }
}

/// <summary>Description d'une arène libre (cartes PC) : tout ce qui se marche, se heurte ou fait tomber.</summary>
public class ArenaDef
{
    /// <summary>Sous cette hauteur (pieds), un joueur est tombé : ring-out.</summary>
    public float KillY { get; set; } = -4f;
    /// <summary>Marche franchie sans sauter (m).</summary>
    public float StepUp { get; set; } = 0.45f;
    /// <summary>Hauteur visuelle du fond (rue, eau) sous le vide.</summary>
    public float FloorBelowY { get; set; } = -12f;
    public string FloorBelowColor { get; set; } = "#2b2f45";
    /// <summary>Multiplie la brume de l'ambiance : une arène de 45 m se lit mal dans la brume d'un toit de 30 m.</summary>
    public float FogScale { get; set; } = 0.45f;
    public List<ArenaBlockDef> Blocks { get; set; } = new();
    public List<ArenaRampDef> Ramps { get; set; } = new();
    public List<ArenaPadDef> Pads { get; set; } = new();
    public List<ArenaSpawnDef> Spawns { get; set; } = new();
    public List<ArenaDecorDef> Decor { get; set; } = new();

    /// <summary>Une place (zone) par point d'apparition, dans l'ordre du fichier : équipe, position et orientation du point.</summary>
    public List<ZoneDef> ZonesFromSpawns()
    {
        var zones = new List<ZoneDef>();
        for (int i = 0; i < Spawns.Count; i++)
            zones.Add(new ZoneDef { Id = i, Team = Spawns[i].Team, X = Spawns[i].X, Z = Spawns[i].Z, FacingDegrees = Spawns[i].FacingDegrees });
        return zones;
    }
}

/// <summary>Fiche de présentation d'une carte (menus, chargement).</summary>
public class MapInfoDef
{
    public string Title { get; set; } = "";
    public string Tagline { get; set; } = "";
    /// <summary>L'idée de jeu de la carte, en une phrase (écran de choix).</summary>
    public string Idea { get; set; } = "";
    /// <summary>Taille d'équipe conseillée (2, 3 ou 4).</summary>
    public int TeamSize { get; set; } = 3;
}

/// <summary>
/// Calculs purs sur une arène libre (aucun nœud Godot) : hauteur du sol sous un point, sortie des flancs de bloc, vide.
/// Le joueur PC, les bots et l'hôte (validation des poses) s'en servent tous : ils voient le même monde.
/// Limite assumée : pas de passage sous un plancher (un point n'a qu'un sol, le plus haut atteignable).
/// </summary>
public sealed class ArenaGeometry
{
    /// <summary>Solide en repère local : bloc (dessus plat) ou rampe (dessus incliné).</summary>
    sealed class Solid
    {
        public Vector2 C, Half;
        public float Rot;            // radians, même convention que PushOutOfObstacles
        public float Y0, Y1;         // bloc : bas et dessus ; rampe : bas et dessus le plus haut
        public ArenaRampDef? Ramp;
        public float RampLow, RampHigh;

        public Vector2 Local(Vector2 p) => Rot == 0f ? p - C : (p - C).Rotated(Rot);
        public bool Contains(Vector2 local, float margin) => Mathf.Abs(local.X) <= Half.X + margin && Mathf.Abs(local.Y) <= Half.Y + margin;

        /// <summary>Hauteur du dessus au point local (bornée au rectangle).</summary>
        public float TopAt(Vector2 local)
        {
            if (Ramp == null) return Y1;
            float t = Ramp.Dir switch
            {
                "+x" => (local.X + Half.X) / (2f * Half.X),
                "-x" => (Half.X - local.X) / (2f * Half.X),
                "-z" => (Half.Y - local.Y) / (2f * Half.Y),
                _ => (local.Y + Half.Y) / (2f * Half.Y),
            };
            return Mathf.Lerp(RampLow, RampHigh, Mathf.Clamp(t, 0f, 1f));
        }
    }

    readonly List<Solid> solids = new();
    public ArenaDef Def { get; }
    public float StepUp => Def.StepUp;
    public float KillY => Def.KillY;
    /// <summary>Plus haut dessus de l'arène (plafond de tête accepté par l'hôte).</summary>
    public float MaxTop { get; }
    /// <summary>Hauteur maximale atteinte par un tremplin (sommet de cloche).</summary>
    public float MaxPadApex { get; }

    /// <summary>Marge de pied : on tient debout un peu au-delà du bord (le corps a une épaisseur).</summary>
    public const float FootMargin = 0.15f;
    /// <summary>Hauteur du corps qui heurte les flancs (pieds → tête).</summary>
    public const float BodyHeight = 1.75f;

    public ArenaGeometry(ArenaDef def)
    {
        Def = def;
        float maxTop = 0f;
        foreach (var b in def.Blocks)
        {
            solids.Add(new Solid { C = new Vector2(b.X, b.Z), Half = new Vector2(b.Sx, b.Sz) * 0.5f, Rot = Mathf.DegToRad(b.RotY), Y0 = b.Y0, Y1 = b.Y1 });
            maxTop = Mathf.Max(maxTop, b.Y1);
        }
        foreach (var r in def.Ramps)
        {
            float lo = Mathf.Min(r.H0, r.H1), hi = Mathf.Max(r.H0, r.H1);
            // Une rampe qui descend dans la direction Dir : on inverse le sens plutôt que les hauteurs.
            var ramp = r.H1 >= r.H0 ? r : new ArenaRampDef { Id = r.Id, X = r.X, Z = r.Z, Sx = r.Sx, Sz = r.Sz, H0 = r.H1, H1 = r.H0, Dir = Opposite(r.Dir), Stairs = r.Stairs };
            solids.Add(new Solid { C = new Vector2(r.X, r.Z), Half = new Vector2(r.Sx, r.Sz) * 0.5f, Y0 = lo - 6f, Y1 = hi, Ramp = ramp, RampLow = lo, RampHigh = hi });
            maxTop = Mathf.Max(maxTop, hi);
        }
        MaxTop = maxTop;
        float apex = maxTop;
        foreach (var p in def.Pads) apex = Mathf.Max(apex, Mathf.Max(p.Y, p.Ty) + p.Apex);
        MaxPadApex = apex;
    }

    static string Opposite(string dir) => dir switch { "+x" => "-x", "-x" => "+x", "-z" => "+z", _ => "-z" };

    /// <summary>
    /// Sol sous le point (x, z) pour des pieds à la hauteur <paramref name="feet"/> : le plus haut dessus atteignable
    /// (au plus <see cref="StepUp"/> au-dessus des pieds). <see cref="float.NegativeInfinity"/> au-dessus du vide.
    /// </summary>
    public float GroundAt(float x, float z, float feet, float margin = FootMargin)
    {
        var p = new Vector2(x, z);
        float best = float.NegativeInfinity;
        foreach (var s in solids)
        {
            var l = s.Local(p);
            if (!s.Contains(l, margin)) continue;
            float top = s.TopAt(l);
            if (top <= feet + StepUp + 0.001f && top > best) best = top;
        }
        return best;
    }

    /// <summary>Plus haut dessus sous le point, sans limite de marche (navigation, captures).</summary>
    public float HighestAt(float x, float z, float margin = 0f) => GroundAt(x, z, float.PositiveInfinity, margin);

    /// <summary>Aucun solide sous le point : y être, c'est tomber.</summary>
    public bool IsVoid(float x, float z, float margin = FootMargin) => float.IsNegativeInfinity(HighestAt(x, z, margin));

    /// <summary>
    /// Sort le disque du joueur (centre <paramref name="p"/>, rayon <paramref name="radius"/>) des flancs plus hauts qu'une marche.
    /// Vrai si le point a bougé.
    /// </summary>
    public bool Collide(ref Vector2 p, float feet, float radius)
    {
        bool moved = false;
        for (int pass = 0; pass < 2; pass++)
            foreach (var s in solids)
            {
                if (s.Y0 >= feet + BodyHeight) continue;   // le bloc est au-dessus de la tête
                var rel = s.Local(p);
                var closest = new Vector2(Mathf.Clamp(rel.X, -s.Half.X, s.Half.X), Mathf.Clamp(rel.Y, -s.Half.Y, s.Half.Y));
                if (s.TopAt(closest) <= feet + StepUp + 0.001f) continue;   // on peut monter dessus : ce n'est pas un mur
                var diff = rel - closest;
                float d = diff.Length();
                if (d >= radius) continue;
                Vector2 push;
                if (d > 1e-4f) push = diff / d * (radius - d);
                else
                {
                    float ox = s.Half.X - Mathf.Abs(rel.X), oz = s.Half.Y - Mathf.Abs(rel.Y);
                    push = ox < oz ? new Vector2(Mathf.Sign(rel.X == 0 ? 1 : rel.X) * (ox + radius), 0) : new Vector2(0, Mathf.Sign(rel.Y == 0 ? 1 : rel.Y) * (oz + radius));
                }
                p += s.Rot == 0f ? push : push.Rotated(-s.Rot);
                moved = true;
            }
        return moved;
    }

    /// <summary>Tremplin sous le point (pieds posés à sa hauteur), sinon null.</summary>
    public ArenaPadDef? PadAt(float x, float z, float feet)
    {
        foreach (var pad in Def.Pads)
            if (Mathf.Abs(feet - pad.Y) < 0.4f && new Vector2(x - pad.X, z - pad.Z).Length() <= pad.Radius) return pad;
        return null;
    }

    /// <summary>
    /// Vol de tremplin : vitesse de départ (horizontale x/z, verticale) pour arriver sur la cible avec la gravité donnée,
    /// et durée du vol.
    /// </summary>
    public static (Vector2 horizontal, float vertical, float time) PadLaunch(ArenaPadDef pad, Vector2 from, float fromY, float gravity)
    {
        float apexY = Mathf.Max(fromY, pad.Ty) + pad.Apex;
        float vy = Mathf.Sqrt(2f * gravity * (apexY - fromY));
        float tUp = vy / gravity;
        float tDown = Mathf.Sqrt(2f * (apexY - pad.Ty) / gravity);
        float t = tUp + tDown;
        return ((new Vector2(pad.Tx, pad.Tz) - from) / t, vy, t);
    }
}
