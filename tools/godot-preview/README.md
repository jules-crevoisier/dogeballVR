# Aperçu moteur de la carte Toit (Godot sans C#, renderer Compatibilité)

Reproduit le rendu du jeu (shader toon, glb, ciel, brume, obstacles de croix_neon.json, avatars) et enregistre 4 captures.
Sert à voir le vrai rendu sans casque ni C#. Projet jetable : copier `assets/` et ce script dans un dossier vide.

```
mkdir /tmp/preview && cp -r assets /tmp/preview/ && cp tools/godot-preview/preview.gd /tmp/preview/test.gd
# project.godot : renderer/rendering_method="gl_compatibility" ; scène principale : un Node3D avec test.gd
godot --headless --path /tmp/preview --import
xvfb-run -a godot --path /tmp/preview --rendering-driver opengl3 --resolution 1280x720
```
