using Godot;

namespace DogeballVR.Audio;

/// <summary>Parent des voix de Sfx, sous la racine : il ne quitte l'arbre qu'à la fermeture du jeu, et libère alors les sons (voir Sfx.Release).</summary>
public partial class SfxHolder : Node3D
{
    public override void _ExitTree() => Sfx.Release();
}
