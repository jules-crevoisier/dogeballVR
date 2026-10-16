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

/// <summary>Un élément de décor / de level design (greybox) : boîte pleine posée dans l'arène, centre en mètres.</summary>
public class PropDef
{
    public string Type { get; set; } = "box";
    public float X { get; set; }
    public float Y { get; set; }
    public float Z { get; set; }
    public float SizeX { get; set; } = 1f;
    public float SizeY { get; set; } = 1f;
    public float SizeZ { get; set; } = 1f;
    /// <summary>Couleur « #rrggbb » (gris greybox par défaut).</summary>
    public string Color { get; set; } = "#8a8f98";
    /// <summary>Si faux : purement visuel, les balles passent au travers.</summary>
    public bool Solid { get; set; } = true;
}

/// <summary>Description d'une map en données (aucune zone codée en dur dans les scènes).</summary>
public class MapData
{
    public string Name { get; set; } = "";
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public float EdgeWarningDistance { get; set; } = 1f;
    /// <summary>Volume de l'arène virtuelle (murs et plafond invisibles autour).</summary>
    public float ArenaWidth { get; set; } = 30f;
    public float ArenaDepth { get; set; } = 16f;
    public float ArenaHeight { get; set; } = 8f;
    public List<ZoneDef> Zones { get; set; } = new();
    public List<PropDef> Props { get; set; } = new();

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
