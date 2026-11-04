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
2. PC : lance sans casque (OpenXR indisponible) avec le joueur clavier-souris : ZQSD/WASD bouger, Maj sprinter, Espace sauter, Ctrl/C s'accroupir, E esquive, souris viser, clic gauche (maintenu = plus fort) lancer, clic droit attraper (fenêtre de 0,5 s), les balles lentes près des pieds se ramassent seules, PageHaut/PageBas règlent la sensibilité, F1 aide, Échap libère la souris. `-- --pc-autoplay` fait jouer le joueur PC tout seul (tests).
3. Quest : installer le « Godot OpenXR Vendors » (AssetLib), créer un preset d'export Android avec *XR Mode = OpenXR*, export APK, `adb install`.

## Jouer à deux (réseau local)
- Hôte : `DogeballVR.exe --host` (ou `godot --path . -- --host`). Il joue la zone 0 ; les autres zones sont des bots.
- Client : `DogeballVR.exe --join=ADRESSE_IP_DE_L_HOTE` (port UDP 7777, `--port=N` pour changer). Il prend la zone 2 (équipe adverse) à la place d'un bot ; `--zone=1|3` pour une autre.
- Map : `--map=neon_court` (par défaut) ou `--map=demo_2v2`, la même chez l'hôte et le client.
- L'hôte est seul maître des balles, des touches et du score. Le client envoie sa pose et ses demandes d'attraper / lancer ; il reçoit l'état à 30 Hz.
- Test automatique (CI) : `--net-selftest` sur le client le fait jouer seul.

### Limites connues du réseau (revue indépendante)
- Pas de prédiction ni de rembobinage : attraper et lancer passent par un aller-retour vers l'hôte. Sans conséquence sur un réseau local, à revoir pour de l'inter-lieux (référence : Knockout City fait du rollback).
- Pas d'interpolation entre snapshots (lissage + extrapolation par la vitesse de la balle).
- Allocations à chaque snapshot, matériaux transparents sur chaque balle : à mesurer sur Quest.
- Rien de testé sur casque : en particulier le lancer VR côté client (fenêtre de 4 images pour la vitesse de main).

## Format de partie (nerveux)
Partie de 5 min minimum, visée autour de 10 min : 5 manches de 2 min, toutes jouées ; une manche s'arrête dès qu'une équipe atteint 3 KO. 2 vies par joueur, un joueur à 0 vie est KO et revient 3 s plus tard. La manche revient à l'équipe avec le plus de KO. Égalité : prolongation de 30 s à mort subite (le premier KO gagne), sinon manche nulle. Si les manches sont à égalité après la 5e, une manche décisive est jouée. Une balle passée à un allié gagne une charge (plus lumineuse, 2 charges ou plus = 2 vies de dégâts). Paramètres dans `scripts/Rules/MatchRules.cs`.

## Sortie Steam
- Checklist complète : `docs/steam/CHECKLIST-SORTIE-STEAM.md` ; actions de jules (compte, frais, page) : `docs/steam/ACTIONS-JULES.md` ; textes du store : `docs/steam/PAGE-STORE.md`.
- Envoi des builds : `tools/steam/README.md` et le workflow manuel « Envoi Steam » (`.github/workflows/steam-upload.yml`).
- Le zip de test contient `steam_api64.dll` et `steam_appid.txt` (480, Spacewar) pour essayer les fonctions Steam sans compte Steamworks.

## État
- Le projet compile (`dotnet build`) et une partie complète tourne en simulation avec les bots (Godot 4.3 .NET, mode headless, sans casque) : manches, KO, prolongation.
- Un workflow GitHub Actions (`.github/workflows/build.yml`) fait ce build et cette simulation à chaque PR.
- **Jamais testé sur casque ni à la main** : le ressenti du lancer, de l'attrapé et des vitesses reste à régler (`HandThrower`, `DesktopPlayer`, `Bot`).
