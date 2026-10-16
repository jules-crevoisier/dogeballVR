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

    public Vector3 Center => new(X, Y, Z);
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
    public float MinLaunchSpeed { get; set; } = 0f;
}

/// <summary>Règles de partie lues dans la map. Seules celles qui sont codées sont lues ; le reste du bloc « rules » est ignoré.</summary>
public class RulesDef
{
    /// <summary>Après une touche, la cible ne peut plus être blessée pendant ce temps (0 = pas d'invulnérabilité).</summary>
    public float InvulnerableAfterHitSeconds { get; set; }
}

/// <summary>Règles de balle de la map : plafond de balles en jeu, durée de vol maximale, gravité réduite pour tendre les trajectoires.</summary>
public class BallRulesDef
{
    public int MaxBallsInPlay { get; set; } = 0;          // 0 = pas de plafond
    public float MaxFlightSeconds { get; set; } = 0f;      // 0 = illimité
    public float BallGravityFactor { get; set; } = 0.6f;
    public ThrowAssistDef ThrowAssist { get; set; } = new();
}

public class BallReturnDef
{
    public float Seconds { get; set; } = 3f;
}

/// <summary>Description d'une map en données (aucune zone codée en dur dans les scènes).</summary>
public class MapData
{
    public string Name { get; set; } = "";
    public int SchemaVersion { get; set; } = 1;
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public float EdgeWarningDistance { get; set; } = 1f;
    /// <summary>Ancien format (v1) : volume de l'arène centré sur l'origine. Le v2 utilise ArenaBounds.</summary>
    public float ArenaWidth { get; set; } = 30f;
    public float ArenaDepth { get; set; } = 16f;
    public float ArenaHeight { get; set; } = 8f;
    public BoundsDef? ArenaBounds { get; set; }
    public BallReturnDef BallReturn { get; set; } = new();
    public BallRulesDef BallRules { get; set; } = new();
    public RulesDef Rules { get; set; } = new();
    public List<ZoneDef> Zones { get; set; } = new();
    public List<PropDef> Props { get; set; } = new();
    public List<ObstacleDef> Obstacles { get; set; } = new();
    public List<BallSpawnDef> BallSpawns { get; set; } = new();

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
