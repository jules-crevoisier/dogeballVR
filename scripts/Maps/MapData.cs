using System.Collections.Generic;
using System.Text.Json;
using Godot;

namespace DogeballVR.Maps;

/// <summary>Une zone de jeu : un joueur, son quart de piste. Position dans l'arène virtuelle (mètres).</summary>
public class ZoneDef
{
    public int Id { get; set; }
    public int Team { get; set; }
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float FacingDegrees { get; set; }
    /// <summary>Lift de la zone (map à niveaux) : le plateau entier monte quand le joueur s'accroupit sur le pad.</summary>
    public LiftDef? Lift { get; set; }

    public Vector3 Center => new(X, Y, Z);
}

public class LiftPadDef
{
    public float X { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 0.8f;
}

public class LiftDef
{
    public LiftPadDef Pad { get; set; } = new();
    public List<float> LevelElevations { get; set; } = new() { 0f, 2.4f };
    public float FadeSeconds { get; set; } = 0.4f;
    public float MaxHighSeconds { get; set; } = 8f;
    public float CooldownSeconds { get; set; } = 12f;
    public float HighElevation => LevelElevations.Count > 0 ? LevelElevations[^1] : 0f;
}

public class LiftRulesDef
{
    public float HoldCrouchSeconds { get; set; } = 1f;
    public bool LockWhenHoldingChargedBall { get; set; } = true;
    public bool DownOnElimination { get; set; } = true;
    public bool OneHighZonePerTeam { get; set; } = true;
    public float RailingHeight { get; set; } = 0.9f;
}

/// <summary>Un élément de décor (greybox) : boîte pleine, centre en x et z, base en y.</summary>
public class PropDef
{
    public string Type { get; set; } = "box";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float SizeX { get; set; } = 1f;
    public float SizeY { get; set; } = 1f;
    public float SizeZ { get; set; } = 1f;
    /// <summary>Couleur « #rrggbb » ou « #rrggbbaa » (alpha = voile de lumière additif).</summary>
    public string Color { get; set; } = "#8a8f98";
    /// <summary>Si faux : purement visuel, les balles passent au travers.</summary>
    public bool Solid { get; set; } = true;
    /// <summary>Modèle du kit de décor (assets/models/props/&lt;nom&gt;.glb) ; la collision suit alors sa boîte englobante.</summary>
    public string? Model { get; set; }
    public float RotY { get; set; }
    public float Scale { get; set; } = 1f;
}

/// <summary>Obstacle du level design (schéma v2) : couvert (cover), plot de rebond (bumper) ou sol de fosse (pit).</summary>
public class ObstacleDef
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "cover";
    public int Zone { get; set; } = -1;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float Sx { get; set; } = 1f;
    public float Sy { get; set; } = 1f;
    public float Sz { get; set; } = 1f;
    public float RotY { get; set; }
}

/// <summary>Emplacement de balle : distributeur d'une zone (dispenser) ou lâcher neutre au centre (neutral_drop).</summary>
public class BallSpawnDef
{
    public string Id { get; set; } = "";
    public string Kind { get; set; } = "dispenser";
    public int Zone { get; set; } = -1;
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float IntervalSeconds { get; set; } = 10f;
    public int MaxStored { get; set; } = 2;
    public int StartBalls { get; set; } = 2;
}

public class BoundsDef
{
    public float MinX { get; set; } = -15f;
    public float MaxX { get; set; } = 15f;
    public float MinZ { get; set; } = -8f;
    public float MaxZ { get; set; } = 8f;
    public float WallHeight { get; set; } = 8f;
    public float CeilingHeight { get; set; } = 8f;
    /// <summary>« rebound » : les balles rebondissent fort sur les murs.</summary>
    public string WallKind { get; set; } = "plain";
}

/// <summary>Aide au lancer VR : multiplicateur de la vitesse de main et vitesse minimale d'un vrai lancer.</summary>
public class ThrowAssistDef
{
    public float SpeedMultiplier { get; set; } = 1.4f;
    public float MinLaunchSpeed { get; set; } = 5.5f;
    /// <summary>Plafond de vitesse d'un lancer VR (m/s).</summary>
    public float MaxLaunchSpeedVR { get; set; } = 20f;
    /// <summary>Attrape parfaite : rapport minimal entre la vitesse de la balle et sa vitesse de lancer.</summary>
    public float PerfectCatchFactor { get; set; } = 0.8f;
    /// <summary>Attrape parfaite : délai maximal entre le grip et la prise (s).</summary>
    public float PerfectCatchWindowSeconds { get; set; } = 0.15f;
    /// <summary>Prise : le grip doit avoir été pressé dans ce délai avant le contact (s).</summary>
    public float GripWindowSeconds { get; set; } = 0.35f;
    /// <summary>Volume de prise de la main (m).</summary>
    public float CatchRadius { get; set; } = 0.3f;
}

/// <summary>Règles de partie lues dans la map. Seules celles qui sont codées sont lues ; le reste du bloc « rules » est ignoré.</summary>
public class RulesDef
{
    /// <summary>Après une touche, la cible ne peut plus être blessée pendant ce temps (0 = pas d'invulnérabilité).</summary>
    public float InvulnerableAfterHitSeconds { get; set; }
    /// <summary>Un cœur perdu revient après ce délai sans être touché (0 = jamais).</summary>
    public float HeartRegenSeconds { get; set; }
    /// <summary>KO d'équipe qui met fin à la manche (0 = valeur par défaut des règles).</summary>
    public int KoTarget { get; set; }
}

/// <summary>Règles de balle de la map : plafond de balles en jeu, durée de vol maximale, gravité réduite pour tendre les trajectoires.</summary>
public class BallRulesDef
{
    public int MaxBallsInPlay { get; set; } = 0;          // 0 = pas de plafond
    public float MaxFlightSeconds { get; set; } = 0f;      // 0 = illimité
    public float BallGravityFactor { get; set; } = 0.6f;
    /// <summary>Aimant de la main sur une balle qui s'approche (m).</summary>
    public float BallMagnetRadius { get; set; } = 0.5f;
    public ThrowAssistDef ThrowAssist { get; set; } = new();
}

public class BallReturnDef
{
    public float Seconds { get; set; } = 3f;
}

/// <summary>Lanceur et pylône du moyeu central (maps en moulinet, schéma v3).</summary>
public class HubDef
{
    public float X { get; set; }
    public float Z { get; set; }
    public HubPylonDef Pylon { get; set; } = new();
    public HubLauncherDef Launcher { get; set; } = new();
    public HubRotorDef? Rotor { get; set; }
}

public class HubRotorDef
{
    public float ArmLength { get; set; } = 4f;
    public float Thickness { get; set; } = 0.4f;
    public float YMin { get; set; } = 0.6f;
    public float YMax { get; set; } = 3f;
    public float DegPerSecond { get; set; } = 60f;
}

public class HubPylonDef
{
    public float Size { get; set; } = 1.6f;
    public float TopY { get; set; } = 3.4f;
}

public class HubLauncherDef
{
    public float Y { get; set; } = 4f;
    public float IntervalSeconds { get; set; } = 6f;
}

public class PcSpawnDef
{
    public int Zone { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
}

public class PcJointDef
{
    public string Id { get; set; } = "";
    public float X { get; set; }
    public float Z { get; set; }
    public float Sx { get; set; } = 1f;
    public float Sz { get; set; } = 1f;
}

/// <summary>Tremplin (profil PC) : pousse le joueur jusqu'à la terrasse de sa plaque.</summary>
public class JumpPadDef
{
    public string Id { get; set; } = "";
    public int Zone { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public float Radius { get; set; } = 0.6f;
    public float LaunchToY { get; set; } = 3f;
    public float MaxStaySeconds { get; set; } = 8f;
    public float CooldownSeconds { get; set; } = 10f;
}

/// <summary>Terrasse (profil PC) : bloc haut au fond d'une plaque, entouré d'un parapet. On y monte par le tremplin.</summary>
public class TerraceDef
{
    public string Id { get; set; } = "";
    public int Zone { get; set; }
    public float X { get; set; }
    public float Z { get; set; }
    public float Sx { get; set; } = 4f;
    public float Sy { get; set; } = 1.6f;
    public float Sz { get; set; } = 2f;
    public float ParapetHeight { get; set; } = 1f;
}

/// <summary>Profil clavier-souris (schéma v3) : apparitions, joints entre plaques, tremplins et terrasses. Ignoré en VR.</summary>
public class PcDef
{
    public List<PcSpawnDef> Spawns { get; set; } = new();
    public List<PcJointDef> Joints { get; set; } = new();
    public List<JumpPadDef> JumpPads { get; set; } = new();
    public List<TerraceDef> Terraces { get; set; } = new();
}

/// <summary>Description d'une map en données (aucune zone codée en dur dans les scènes).</summary>
public class MapData
{
    public string Name { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public float EdgeWarningDistance { get; set; } = 1f;
    /// <summary>« ring » : plaques en moulinet autour d'un moyeu (schéma v3) ; le joueur PC y circule librement.</summary>
    public string Layout { get; set; } = "zones";
    public HubDef? Hub { get; set; }
    public PcDef? Pc { get; set; }
    /// <summary>Ancien format (v1) : volume de l'arène centré sur l'origine. Le v2 utilise ArenaBounds.</summary>
    public float ArenaWidth { get; set; } = 30f;
    public float ArenaDepth { get; set; } = 16f;
    public float ArenaHeight { get; set; } = 8f;
    public BoundsDef? ArenaBounds { get; set; }
    public BallReturnDef BallReturn { get; set; } = new();
    public BallRulesDef BallRules { get; set; } = new();
    public RulesDef Rules { get; set; } = new();
    public LiftRulesDef LiftRules { get; set; } = new();
    public List<ZoneDef> Zones { get; set; } = new();
    /// <summary>Décor visuel optionnel (assets/models/&lt;decor&gt;.glb et &lt;decor&gt;_ville.glb) : toit, skyline et ciel de la DA Knockout City. Sans collision.</summary>
    public string? Decor { get; set; }
    public List<PropDef> Props { get; set; } = new();
    public List<ObstacleDef> Obstacles { get; set; } = new();
    public List<BallSpawnDef> BallSpawns { get; set; } = new();

    public bool IsRing => Layout == "ring";

    /// <summary>Fosse carrée au centre (obstacle « pit » qui ne coupe pas toute l'arène) : y tomber est un ring-out. Null sinon.</summary>
    public ObstacleDef? PitRect => IsRing ? Obstacles.Find(o => o.Kind == "pit") : null;

    /// <summary>Vrai si le point (x, z) est au-dessus de la fosse de ring-out, `margin` mètres de marge comprise (négative = à l'intérieur).</summary>
    public bool OverPit(float x, float z, float margin = 0f)
    {
        var p = PitRect;
        return p != null && Mathf.Abs(x - p.X) < p.Sx / 2f + margin && Mathf.Abs(z - p.Z) < p.Sz / 2f + margin;
    }

    /// <summary>Terrasse sous le point (x, z), `margin` comprise, sinon null.</summary>
    public TerraceDef? TerraceAt(float x, float z, float margin = 0f) =>
        Pc?.Terraces.Find(t => Mathf.Abs(x - t.X) < t.Sx / 2f + margin && Mathf.Abs(z - t.Z) < t.Sz / 2f + margin);

    public bool HasLifts => Zones.Exists(z => z.Lift != null);

    /// <summary>Altitude maximale d'un joueur (plateau haut) : borne les poses acceptées par l'hôte.</summary>
    public float MaxElevation
    {
        get
        {
            float m = 0f;
            foreach (var z in Zones) if (z.Lift != null) m = Mathf.Max(m, z.Lift.HighElevation);
            return m;
        }
    }

    /// <summary>Limites de l'arène, quel que soit le format du fichier.</summary>
    public BoundsDef Bounds => ArenaBounds ?? new BoundsDef
    {
        MinX = -ArenaWidth / 2, MaxX = ArenaWidth / 2, MinZ = -ArenaDepth / 2, MaxZ = ArenaDepth / 2,
        WallHeight = ArenaHeight, CeilingHeight = ArenaHeight,
    };

    static readonly JsonSerializerOptions Options = new() { PropertyNameCaseInsensitive = true };

    public static MapData Load(string resPath)
    {
        string json = FileAccess.GetFileAsString(resPath);
        var map = JsonSerializer.Deserialize<MapData>(json, Options);
        if (map == null || map.Zones.Count == 0)
            throw new System.InvalidOperationException($"Map invalide : {resPath}");
        return map;
    }
}
