using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Modèles 3D glb générés par tools/blender (dossier assets/models). Renvoie null si le fichier n'est pas (encore) importé :
/// l'appelant garde alors son rendu procédural, ce qui évite de casser le jeu (CI, import en attente).
/// </summary>
public static class ModelLibrary
{
    /// <param name="toon">Applique le shader toon KC aux matériaux (désactivé : matériaux glb d'origine).</param>
    public static Node3D? Instance(string name, bool toon = true)
    {
        var path = $"res://assets/models/{name}.glb";
        if (!ResourceLoader.Exists(path)) return null;
        var node = ResourceLoader.Load<PackedScene>(path)?.Instantiate<Node3D>();
        if (node != null && toon) KcShaders.Apply(node, withOutline: name.StartsWith("avatar_") || name == "main");
        return node;
    }
}
