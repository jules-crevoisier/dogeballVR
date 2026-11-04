using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Modèles 3D glb générés par tools/blender (dossier assets/models). Renvoie null si le fichier n'est pas (encore) importé :
/// l'appelant garde alors son rendu procédural, ce qui évite de casser le jeu (CI, import en attente).
/// </summary>
public static class ModelLibrary
{
    /// <summary>Casque autonome (Quest) : on charge les versions allégées quand elles existent (`nom_quest.glb`,
    /// `props_quest/nom.glb`). Activé au démarrage en VR, ou par `--quest` pour tester sur PC.</summary>
    public static bool Quest { get; set; }

    /// <param name="toon">Applique le shader toon KC aux matériaux (désactivé : matériaux glb d'origine).</param>
    public static Node3D? Instance(string name, bool toon = true)
    {
        var path = $"res://assets/models/{name}.glb";
        if (Quest)
        {
            var light = name.StartsWith("props/") ? $"res://assets/models/props_quest/{name[6..]}.glb" : $"res://assets/models/{name}_quest.glb";
            if (ResourceLoader.Exists(light)) path = light;
        }
        if (!ResourceLoader.Exists(path)) return null;
        var node = ResourceLoader.Load<PackedScene>(path)?.Instantiate<Node3D>();
        if (node != null && toon) KcShaders.Apply(node, withOutline: name.StartsWith("avatar_") || name == "main");
        return node;
    }
}
