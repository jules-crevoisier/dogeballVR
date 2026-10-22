# Assets 3D (Blender, scripts bpy)

Génération 100 % par script, sans fichier .blend : `pip install bpy` (Blender 5.x en module Python, sans GPU ni écran).

```
python3 -I tools/blender/gen_assets.py assets/models        # tous les .glb
python3 -I tools/blender/gen_assets.py assets/models mur    # un seul
python3 -I tools/blender/render.py assets/models /tmp/png   # rendus 3 vues (Cycles CPU) pour la revue
```

Unités : mètres. Palette = DA v3 (`lib.PALETTE`). Matériaux unis (un matériau par couleur), pas de texture. Budget total ≈ 16 000 triangles pour les 28 fichiers ; le plus lourd (avatar) reste sous 1 800.

| Fichier | Usage | État d'intégration |
|---|---|---|
| `balle_normale/bombe/rebond` | bandes, mèche, anneaux (le cœur est gardé en code pour la teinte par type) | intégré (`Ball.cs`) |
| `avatar_{orange,bleu}_{corps,tete}`, `main` | joueurs réels (les bots restent en formes simples) | intégré (`Avatar.cs`) |
| `plot_neutre` | dalle du plot de ramassage | intégré (`PickupBeacon.cs`) |
| `plot_orange/bleu`, `moyeu`, `mur[_orange/_bleu]`, `sol_tuile[_orange/_bleu]`, `abri`, `tremplin`, `bord_fosse`, `tableau_score`, `lampe`, `fx_*`, `avatar_{orange,bleu}` | livrés, pas encore branchés : les cartes JSON fixent les dimensions, il faut les instancier à l'échelle dans `ArenaBuilder` | à faire |

Orientation : l'avant des avatars est -Z dans Godot. `scripts/Visual/ModelLibrary.cs` renvoie null si un glb n'est pas importé : le jeu retombe sur les formes du code.
