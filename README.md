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
Partie de 10 minutes minimum : 5 manches de 2 min, toutes jouées en entier. 2 vies par joueur, un joueur à 0 vie est KO et revient 3 s plus tard. La manche revient à l'équipe avec le plus de KO. Égalité : prolongation de 30 s à mort subite (le premier KO gagne), sinon manche nulle. Si les manches sont à égalité après la 5e, une manche décisive est jouée. Une balle passée à un allié gagne une charge (plus lumineuse, 2 charges ou plus = 2 vies de dégâts). Paramètres dans `scripts/Rules/MatchRules.cs`.

## État
- Le projet compile (`dotnet build`) et une partie complète tourne en simulation avec les bots (Godot 4.3 .NET, mode headless, sans casque) : manches, KO, prolongation.
- Un workflow GitHub Actions (`.github/workflows/build.yml`) fait ce build et cette simulation à chaque PR.
- **Jamais testé sur casque ni à la main** : le ressenti du lancer, de l'attrapé et des vitesses reste à régler (`HandThrower`, `DesktopPlayer`, `Bot`).
