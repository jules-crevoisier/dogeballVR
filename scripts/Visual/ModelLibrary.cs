using Godot;

namespace DogeballVR.Visual;

/// <summary>
/// Modèles 3D glb générés par tools/blender (dossier assets/models). Renvoie null si le fichier n'est pas (encore) importé :
/// l'appelant garde alors son rendu procédural, ce qui évite de casser le jeu (CI, import en attente).
/// </summary>
public static class ModelLibrary
{
    public static Node3D? Instance(string name)
    {
        var path = $"res://assets/models/{name}.glb";
        if (!ResourceLoader.Exists(path)) return null;
        return ResourceLoader.Load<PackedScene>(path)?.Instantiate<Node3D>();
    }
}
