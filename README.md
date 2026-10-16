# DogeballVR

Dodgeball en VR pour les arènes EVA. Démo : 2 contre 2 sur 4 zones de 15 x 8 m, Meta Quest. Évolutions prévues : 4 contre 4, maps verticales, mode compétitif, inter-lieux.

Concept et architecture : voir `docs/ARCHITECTURE.md` et les notes du projet (dossier `masterclasse`).

## Stack
- Godot 4.3 .NET (C#), OpenXR, rendu Mobile.
- Réseau : ENet de Godot, un joueur héberge et fait autorité (serveur dédié plus tard).

## Structure
| Dossier | Rôle |
|---|---|
| `scripts/Zones` | Zones de jeu, distance au bord |
| `scripts/Maps` | Maps décrites en données (`maps/*.json`) |
| `scripts/Gameplay` | Balle, mains VR, joueur PC, bots, hurtbox, chef de partie |
| `scripts/Rules` | Règles pures : vies, manches, score |
| `scripts/Net` | Session réseau |

## Lancer
1. Ouvrir le dossier avec Godot 4.3 .NET, puis *Build* (C#).
2. PC : lance sans casque (OpenXR indisponible) avec le joueur clavier-souris : ZQSD/WASD bouger, Maj accroupi, souris viser, clic droit maintenu attraper, clic gauche maintenu puis relâché lancer, Échap libère la souris.
3. Quest : installer le « Godot OpenXR Vendors » (AssetLib), créer un preset d'export Android avec *XR Mode = OpenXR*, export APK, `adb install`.

## Format de partie (nerveux)
Partie de 5 min = 5 manches de 1 min, 3 manches gagnantes. 2 vies par joueur, retour 3 s après élimination. Un joueur à 0 vie est KO et revient 3 s plus tard. Fin de manche à 5 KO pour une équipe ou à la minute (plus de KO gagne, égalité = manche nulle). Une balle passée à un allié gagne une charge (plus lumineuse, 2 charges ou plus = 2 vies de dégâts). Paramètres dans `scripts/Rules/MatchRules.cs`.

## État
Squelette non compilé ni testé sur casque (écrit sans Godot installé). Premier travail : faire tourner `Main.tscn` sur Quest, puis régler le lancer (`HandThrower`).
