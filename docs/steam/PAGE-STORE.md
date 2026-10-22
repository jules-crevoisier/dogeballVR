# Page Steam : textes et contenu

Brouillon de la page store, prêt à coller dans Steamworks (Modifier la page store). Les textes du store sont donnés en anglais (langue de repli obligatoire de Steam) et en français.

Règle de Valve à garder en tête pendant toute la rédaction : **la page ne doit annoncer que ce qui est dans le build au lancement** (« Your store page should only contain features and content that will be available at launch », https://partner.steamgames.com/doc/store/review_process). Chaque paragraphe qui dépend d'un travail en cours est signalé dans le tableau « Conditions » plus bas : le retirer si la fonction n'est pas livrée. Pas de lien externe dans la description, pas de mention d'un autre jeu ni d'une autre marque.

## 1. Nom

Nom de travail : **DogeballVR**. À remplacer avant la création de l'app dans Steamworks : le nom se change ensuite, mais l'URL de la page, les wishlists et la presse l'auront déjà repris. Défauts du nom actuel : « Doge » évoque le mème Shiba/Dogecoin (risque de confusion et de recherche polluée), et « VR » enferme le jeu alors qu'il se joue aussi au clavier-souris.

Trois propositions (à vérifier avant de choisir : recherche dans le store Steam, bases de marques EUIPO / INPI / USPTO en classes 9 et 41, nom de domaine, comptes réseaux sociaux) :

| Nom | Pour | Contre |
|---|---|---|
| **Neon Throwdown** | Dit l'univers (arènes néon) et l'action (lancer, affrontement) ; lisible sur une petite capsule | « Throwdown » est un mot courant, déjà utilisé dans d'autres titres : à vérifier |
| **Hurlstorm** | Un seul mot, inventé, facile à protéger et à trouver ; sonne compétitif | Ne dit pas « dodgeball » : la capsule et l'accroche doivent le montrer |
| **Dodge Circuit** | Le genre est dans le nom (bon pour la recherche Steam) ; « circuit » évoque une ligue et les arènes en anneau | Plus générique, moins mémorable |

Les textes ci-dessous utilisent « DogeballVR » ; remplacer partout une fois le nom choisi (et `application/config/name`, `export_presets.cfg`, les noms de fichiers : voir la checklist, le chemin des sauvegardes en dépend).

## 2. Accroche courte (≤ 300 caractères)

EN (237 caractères) :

> Fast-paced team dodgeball for VR and keyboard & mouse. Catch, charge and fling glowing balls across neon arenas, pass to power up your shot, dash and tackle rivals into the pit. Play 2v2 or 4v4 online, with bots filling every empty slot.

FR (277 caractères) :

> Du dodgeball en équipe, nerveux, en VR ou au clavier-souris. Attrape, charge et renvoie des balles lumineuses dans des arènes néon, fais des passes pour charger ton tir, dash et pousse tes rivaux dans la fosse. Joue en 2c2 ou 4c4 en ligne, des bots complètent les places vides.

## 3. Description longue (BBCode Steam)

### EN

```
[h2]Catch it. Charge it. Send it back.[/h2]
DogeballVR is a fast, team-based dodgeball game built for two ways to play: throw with your real arms in VR, or dash and aim in third person with keyboard and mouse. Every ball that comes at you is a chance: time your catch and it's yours to throw back.

[h2]Catching is everything[/h2]
[list]
[*][b]Catch[/b] incoming throws instead of dodging them. Nail the timing for a [b]perfect catch[/b]: you get a heart back and the ball leaves your hand already charged.
[*][b]Pass and catch to power up[/b]: a pass from a teammate charges the ball, and a perfect catch of an enemy throw adds one more level. From level 2, the ball knocks out in one hit.
[*][b]Trick shots[/b]: curve your throw around cover, lob it over a wall, or fake a throw to bait the catch.
[*]In VR, block a weak shot with an open palm parry.
[/list]

[h2]Arenas that fight back[/h2]
[list]
[*][b]Neon Cross[/b]: four plates around a central hub, with a rotor whose arms swat flat throws out of the air.
[*][b]Crossed Pit[/b]: a gaping pit in the middle. Dash, tackle and knock rivals over the edge for a ring-out, or take a jump pad to a sniper terrace.
[*][b]4v4 Tower[/b]: eight plates around a 16 x 10 m pit for full team chaos.
[/list]

[h2]Special balls[/h2]
Every match draws one special ball family. The hub launches it toward the team that's behind:
[list]
[*][b]Bomb[/b]: explodes on contact and hits every rival in the blast.
[*][b]Bounce[/b]: ricochets off the walls and keeps flying long after you thought you were safe.
[/list]

[h2]Play your way[/h2]
[list]
[*][b]VR[/b]: natural throwing and catching with tracked controllers, threat alerts with haptics, plate swaps that reshuffle the arena mid-round.
[*][b]Keyboard & mouse[/b]: third-person movement with dash charges, jump, tackle, aim assist and fully rebindable keys.
[*]Short, intense matches: best of three online, five two-minute rounds against bots and in VR, sudden-death overtime on a tie.
[/list]

[h2]Online with friends[/h2]
[list]
[*]2v2 and 4v4 online matches, hosted by a player.
[*]Create a lobby and invite your Steam friends, or jump into a public match.
[*]Bots fill every empty slot, so a match always starts.
[/list]
```

### FR

```
[h2]Attrape. Charge. Renvoie.[/h2]
DogeballVR est un dodgeball en équipe, rapide, pensé pour deux façons de jouer : lance avec tes vrais bras en VR, ou dash et vise à la troisième personne au clavier-souris. Chaque balle qui arrive sur toi est une occasion : attrape-la au bon moment et renvoie-la.

[h2]Tout se joue à l'attrape[/h2]
[list]
[*][b]Attrape[/b] les tirs au lieu de les esquiver. Au bon moment, c'est une [b]attrape parfaite[/b] : tu regagnes un cœur et la balle repart déjà chargée.
[*][b]Passe et attrape pour charger[/b] : une passe d'un coéquipier charge la balle, et une attrape parfaite d'un tir adverse lui ajoute un palier. Dès le palier 2, elle met KO en un seul coup.
[*][b]Coups spéciaux[/b] : courbe ton tir autour d'un abri, lobe par-dessus un mur, ou feinte pour piéger l'attrape adverse.
[*]En VR, dévie un tir faible d'une parade de la paume.
[/list]

[h2]Des arènes qui se défendent[/h2]
[list]
[*][b]Croix Néon[/b] : quatre plaques autour d'un moyeu central, dont le rotor balaie les tirs tendus.
[*][b]Fosse croisée[/b] : une fosse béante au centre. Dash, tacle et pousse tes rivaux dans le vide, ou prends un tremplin vers une terrasse de tir.
[*][b]Tour 4v4[/b] : huit plaques autour d'une fosse de 16 x 10 m, pour le chaos à quatre contre quatre.
[/list]

[h2]Balles spéciales[/h2]
Chaque match tire au sort une famille de balles spéciales. Le moyeu l'envoie vers l'équipe menée :
[list]
[*][b]Bombe[/b] : explose au contact et touche tous les adversaires dans le souffle.
[*][b]Rebond[/b] : ricoche sur les murs et revient quand on ne l'attend plus.
[/list]

[h2]Joue comme tu veux[/h2]
[list]
[*][b]VR[/b] : lancers et attrapes naturels avec les manettes, alertes de menace avec vibrations, échanges de plaques qui rebattent l'arène en pleine manche.
[*][b]Clavier-souris[/b] : déplacement à la troisième personne avec charges de dash, saut, tacle, aide à la visée et touches entièrement réassignables.
[*]Des matchs courts et intenses : 2 manches gagnantes en ligne, 5 manches de 2 minutes contre les bots et en VR, prolongation à mort subite en cas d'égalité.
[/list]

[h2]En ligne entre amis[/h2]
[list]
[*]Matchs en ligne en 2c2 et 4c4, hébergés par un joueur.
[*]Crée un salon et invite tes amis Steam, ou rejoins une partie publique.
[*]Des bots complètent les places vides : un match démarre toujours.
[/list]
```

Les noms de cartes anglais (Neon Cross, Crossed Pit, 4v4 Tower) sont des traductions proposées des noms du dépôt (`maps/croix_neon.json`, `fosse_croisee.json`, `tour_4v4.json`) ; à aligner avec les noms affichés en jeu.

### Conditions (retirer la ligne si ce n'est pas livré)

| Passage | Dépend de | État au 21/10 |
|---|---|---|
| « fully rebindable keys » / « touches entièrement réassignables » | Options, touches réassignables (fil interfaces) | en cours |
| « Create a lobby and invite your Steam friends, or jump into a public match » | Lobbies Steam, invitations, relais P2P (fil réseau) | en cours ; aujourd'hui : IP directe ou LAN seulement |
| « 2v2 and 4v4 online » | Fait (ENet, hôte qui fait autorité) ; en ligne hors LAN dépend du relais Steam | en cours |
| Jouer VR et clavier-souris dans le même match | Pas de salon mixte VR/PC sur les cartes en anneau (`docs/CHOIX-REGLES.md`) | **ne pas l'annoncer** tant que ce n'est pas fait |
| « plate swaps » | Fait, VR seulement, jamais testé sur casque | à vérifier sur casque |
| VR en général | Jamais testé sur casque (README) ; la VR « à domicile » demande un déplacement artificiel (voir checklist, section VR) | à faire |
| Achievements, Cloud (cases à cocher) | Fil réseau | en cours |

## 4. Tags suggérés (20)

À choisir dans la liste de Steam (Modifier la page store > Tags) ; les 5 premiers pèsent le plus, l'ordre compte.

1. Sports
2. Action
3. Online PvP
4. Team-Based
5. VR
6. Multiplayer
7. PvP
8. Competitive
9. Fast-Paced
10. Arcade
11. Third Person
12. First-Person
13. Physics
14. Stylized
15. Colorful
16. Futuristic
17. 3D
18. Party Game
19. Casual
20. Indie

## 5. Genres

Action, Sports, Indie. Ajouter **Early Access** si le jeu sort en accès anticipé (recommandé, voir `ACTIONS-JULES.md`) : il faut alors répondre aux questions Early Access de Valve sur la page.

## 6. Fonctionnalités Steam à cocher

| Case Steamworks | Cocher ? | Condition |
|---|---|---|
| Online PvP | oui | relais Steam / lobbies livrés (sinon le jeu ne marche en ligne que par IP) |
| LAN PvP | oui | déjà fait (`--host`, `--join=IP`) ; à garder si le menu le propose |
| Shared/Split Screen PvP | non | pas d'écran partagé |
| Remote Play Together | **non** | ne sert qu'aux jeux multijoueurs en local (même machine) ; DogeballVR n'en a pas |
| Remote Play on TV / Phone / Tablet | à tester | clavier-souris en streaming : ça marche en théorie, à vérifier ; pas pour la VR |
| Steam Achievements | oui | succès livrés (fil réseau, `docs/steam/achievements.csv`) |
| Stats | oui | si les stats Steam sont livrées |
| Steam Cloud | oui | Auto-Cloud configuré (`ACTIONS-JULES.md`) |
| Steam Trading Cards | plus tard | Valve les ouvre selon les ventes ; rien à faire au lancement |
| Steam Workshop / Level editor | non | |
| In-App Purchases | non | |
| VR Supported (et non VR Only) | oui | le jeu se joue sans casque ; nécessite une option de lancement VR dans Steamworks |
| Tracked Motion Controller Support | oui | OpenXR, profils Touch / Index / Vive / WMR / Pico dans `openxr_action_map.tres` |
| Full Controller Support | **non** | pas de support manette en mode bureau aujourd'hui ; à cocher seulement si menus + jeu passent entièrement à la manette |
| Partial Controller Support | non | idem ; le questionnaire « controller support » de Steamworks fixe ensuite l'étiquette |
| Captions available | non | pas de dialogues |
| Cross-Platform Multiplayer | non | sauf si la version Quest autonome joue avec la version Steam (à décider) |
| Steam Timeline / Leaderboards | non | pas prévu au lancement |

## 7. Configuration requise

Valeurs **estimées, à mesurer** sur de vraies machines (le rendu est le renderer Mobile de Godot 4.3, Vulkan ; la taille finale du build est à relever dans l'artefact du workflow). Dans Steamworks, chaque ligne a son champ ; la VR se décrit dans la partie VR des systèmes et plateformes.

| | Minimum (écran) | Recommandé (écran) | Minimum VR | Recommandé VR |
|---|---|---|---|---|
| OS | Windows 10 64 bits | Windows 11 64 bits | Windows 10 64 bits | Windows 11 64 bits |
| Processeur | Intel Core i3-6100 / AMD Ryzen 3 1200 | Intel Core i5-8400 / AMD Ryzen 5 2600 | Intel Core i5-4590 / AMD Ryzen 5 1500X | Intel Core i5-9600K / AMD Ryzen 5 3600 |
| Mémoire | 8 Go | 16 Go | 8 Go | 16 Go |
| Graphismes | GeForce GTX 950 2 Go / Radeon RX 460, Vulkan 1.1 | GeForce GTX 1060 6 Go / Radeon RX 580 | GeForce GTX 1060 6 Go / Radeon RX 580 | GeForce RTX 2060 / Radeon RX 5700 |
| Réseau | Connexion haut débit | idem | idem | idem |
| Stockage | 1 Go (à mesurer) | 1 Go | 1 Go | 1 Go |
| Notes | Clavier-souris | | Casque compatible SteamVR/OpenXR, debout, espace de 2 x 1,5 m | Port USB 3 (Quest Link) ou Wi-Fi 6 (Air Link / Steam Link) |

Partie VR à déclarer :
- SDK : OpenXR (via SteamVR ou le runtime Meta).
- Casques : Valve Index, HTC Vive / Vive Pro, Meta Quest 2 / 3 / 3S / Pro (Link, Air Link ou Steam Link), Pico 4 (streaming). Ne pas lister Windows Mixed Reality (retiré de Windows 11 24H2).
- Espace : debout et à l'échelle de la pièce (« Standing », « Room-Scale ») ; pas assis.
- Entrées : manettes VR suivies (« Tracked Motion Controllers ») ; clavier-souris en mode écran.

Les libellés exacts des champs VR de Steamworks sont à revérifier dans l'interface (la doc Valve ne les cite pas toutes).

## 8. Bande-annonce (60 à 90 s)

Contraintes Valve (https://partner.steamgames.com/doc/store/trailer) : 1920x1080 au plus, 30 ou 60 i/s, 5 000 kbit/s ou plus, H.264 + AAC en .mp4/.mov, 16:9. Les deux premières bandes-annonces passent avant les captures, la première doit montrer surtout du gameplay. Beaucoup la regardent sans le son : tout doit se comprendre à l'image. Captation en jeu, à 60 i/s, HUD visible.

Durée visée : 75 s. Musique électro rythmée, coupes sur les temps.

| # | Temps | Plan | Ce qu'on voit | Texte à l'écran |
|---|---|---|---|---|
| 1 | 0:00–0:04 | Gros plan | Une balle lumineuse fonce vers la caméra, une main VR l'attrape au dernier moment (son « PRISE ! ») | — |
| 2 | 0:04–0:08 | Plan large qui tourne | Croix Néon de haut, 4 plaques, le rotor tourne, 8 balles en vol | logo du jeu |
| 3 | 0:08–0:14 | 3e personne, clavier-souris | Course, dash, tir chargé qui touche un adversaire (flash, cœur perdu) | CATCH. |
| 4 | 0:14–0:20 | Vue VR | Attrape parfaite (anneau, son), renvoi immédiat, KO | CHARGE. |
| 5 | 0:20–0:26 | 3e personne | Passe à un allié (palier 1), attrape parfaite d'un tir adverse : palier 2 (double halo), tir KO en un coup | SEND IT BACK. |
| 6 | 0:26–0:32 | Plan latéral | Tir à effet qui contourne un couvert ; cloche par-dessus un mur | — |
| 7 | 0:32–0:38 | Fosse croisée | Tacle en dash, l'adversaire tombe dans la fosse (ring-out) | — |
| 8 | 0:38–0:43 | Tremplin | Vol guidé vers la terrasse, tir plongeant depuis le haut | — |
| 9 | 0:43–0:49 | Moyeu | Annonce « BALLE SPÉCIALE », la bombe part, explosion qui touche deux adversaires | — |
| 10 | 0:49–0:54 | Rebond | Balle rebond qui ricoche sur trois murs et touche par-derrière | — |
| 11 | 0:54–0:60 | VR | Échange de plaques (fondu au noir), le joueur se retrouve face à un tir et pare de la paume | VR OR KEYBOARD & MOUSE |
| 12 | 0:60–0:66 | Tour 4v4 | Plan large d'un 4c4 complet, six balles en l'air | 2v2 · 4v4 ONLINE |
| 13 | 0:66–0:71 | Montage rapide | Quatre KO enchaînés, un par carte | — |
| 14 | 0:71–0:75 | Carton final | Logo, « Wishlist now on Steam » (puis « Available now » à la sortie) | — |

Tourner une version sans aucun texte pour la presse et les réseaux. N'utiliser que des plans réellement jouables dans le build (règle du store).

## 9. Captures d'écran (8 à produire)

Exigences : 1920x1080 au moins, 16:9, au moins 5 sur la page, au moins 4 marquées « tout public » ; uniquement du jeu (pas d'illustration, pas de texte ajouté par-dessus, https://partner.steamgames.com/doc/store/assets/standard et /doc/store/review_process). HUD autorisé et conseillé. Captures en 3840x2160 si possible (Steam réduit).

1. **Attrape parfaite en VR** : vue casque, main qui se referme sur une balle lumineuse, anneau d'attrape parfaite, adversaire flou derrière.
2. **Plan large Croix Néon** : 4 plaques, moyeu et rotor, 6 balles en vol avec leurs traînées, éclairage néon.
3. **Clavier-souris 3e personne** : joueur en pleine charge de tir, viseur rouge sur un adversaire, jauges de dash visibles.
4. **Ring-out sur la Fosse croisée** : adversaire poussé au bord de la fosse par un tacle, traînée de dash.
5. **Explosion de bombe** : souffle qui touche deux adversaires, cœurs qui sautent.
6. **Balle chargée** : balle au palier 2 (double halo et anneau) juste après une attrape parfaite.
7. **Tour 4v4** : vue d'ensemble des huit plaques en plein match, tableau de score visible.
8. **Menu / salon** : écran de salon avec amis Steam et sélection de carte (une fois le menu du fil interfaces livré) ; sinon terrasse et tremplin en vol.

Ordre conseillé sur la page : 2, 1, 3, 4, 5, 6, 7, 8 (la première capture sert aussi de fond de page si on ne fournit pas de « page background »).
