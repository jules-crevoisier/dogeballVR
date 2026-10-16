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

/// <summary>Description d'une map en données (aucune zone codée en dur dans les scènes).</summary>
public class MapData
{
    public string Name { get; set; } = "";
    public float ZoneWidth { get; set; } = 15f;
    public float ZoneDepth { get; set; } = 8f;
    public float EdgeWarningDistance { get; set; } = 1f;
    public List<ZoneDef> Zones { get; set; } = new();

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
