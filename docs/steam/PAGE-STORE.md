# Page Steam : textes et contenu

Brouillon de la page store, prêt à coller dans Steamworks (Modifier la page store). Les textes du store sont donnés en anglais (langue de repli obligatoire de Steam) et en français.

Règle de Valve à garder en tête pendant toute la rédaction : **la page ne doit annoncer que ce qui est dans le build au lancement** (« Your store page should only contain features and content that will be available at launch », https://partner.steamgames.com/doc/store/review_process). Chaque paragraphe qui dépend d'un travail en cours est signalé dans le tableau « Conditions » plus bas : le retirer si la fonction n'est pas livrée. Pas de lien externe dans la description, pas de mention d'un autre jeu ni d'une autre marque.

**Décision du 22/10 : sur Steam, on vend le mode écran seulement (PC clavier-souris ou manette, et Steam Deck).** La VR reste pour les arènes EVA et sera distribuée hors Steam. La page ne promet donc rien en VR : pas de texte, pas de tag, pas de case « VR » dans Steamworks, pas de plan VR dans la bande-annonce ni dans les captures. Le build envoyé sur Steam a OpenXR coupé (`.github/workflows/steam-upload.yml`), pour qu'aucun casque ni SteamVR ne se lance.

## 1. Nom

Nom de travail : **DogeballVR**. À remplacer avant la création de l'app dans Steamworks : le nom se change ensuite, mais l'URL de la page, les wishlists et la presse l'auront déjà repris. Défauts du nom actuel : « Doge » évoque le mème Shiba/Dogecoin (risque de confusion et de recherche polluée), et « VR » est faux sur Steam, où le jeu se vend sans la VR (risque de remboursements et d'avis négatifs de joueurs qui attendent un jeu VR).

Trois propositions (à vérifier avant de choisir : recherche dans le store Steam, bases de marques EUIPO / INPI / USPTO en classes 9 et 41, nom de domaine, comptes réseaux sociaux) :

| Nom | Pour | Contre |
|---|---|---|
| **Rooftop Throwdown** | Dit l'univers (toits, quartier) et l'action (lancer, affrontement) ; lisible sur une petite capsule | « Throwdown » est un mot courant, déjà utilisé dans d'autres titres : à vérifier |
| **Hurlstorm** | Un seul mot, inventé, facile à protéger et à trouver ; sonne compétitif | Ne dit pas « dodgeball » : la capsule et l'accroche doivent le montrer |
| **Dodge Circuit** | Le genre est dans le nom (bon pour la recherche Steam) ; « circuit » évoque une ligue de quartier | Plus générique, moins mémorable |

Le jeu affiche aujourd'hui « DOGEBALL » (logo du menu, capsules) : les textes ci-dessous utilisent donc « Dogeball », sans « VR ». Remplacer partout, capsules comprises (`GAME_NAME=… tools/steam/capsules.py`), une fois le nom choisi (et `application/config/name`, `export_presets.cfg`, les noms de fichiers : voir la checklist, le chemin des sauvegardes en dépend).

## 2. Accroche courte (≤ 300 caractères)

Mise à jour du 07/11 : le jeu vendu sur Steam est celui de la PR #34 (trois arènes en 3 contre 3, interface « Affiche », anglais et français). Les anciennes cartes néon (Croix Néon, Fosse croisée, Tour 4v4) restent dans le dépôt pour la VR d'EVA et n'apparaissent plus sur la page.

EN (244 caractères) :

> Team dodgeball, 3 vs 3, for keyboard & mouse or gamepad. Catch a throw at the last instant and send it back faster, pass to charge your shot, tackle rivals off the rooftops. Three arenas, quick matches, play online with friends or against bots.

FR (271 caractères) :

> Du dodgeball en équipe, 3 contre 3, au clavier-souris ou à la manette. Attrape un tir au dernier moment et renvoie-le plus fort, fais des passes pour charger ton tir, pousse tes rivaux dans le vide. Trois arènes, des matchs courts, en ligne entre amis ou contre des bots.

## 3. Description longue (BBCode Steam)

### EN

```
[h2]Catch it. Charge it. Send it back.[/h2]
Dogeball is a fast, team-based dodgeball game played 3 vs 3 in third person: run, dash, jump, aim and throw with keyboard and mouse or a gamepad. Every ball that comes at you is a chance: time your catch and it's yours to throw back.

[h2]Catching is everything[/h2]
[list]
[*][b]Catch[/b] incoming throws, or dodge them if you can't. Nail the timing for a [b]perfect catch[/b]: the ball keeps its charge and goes up one level in your hands.
[*][b]Pass to power up[/b]: a pass from a teammate charges the ball. Every charge level makes the next throw faster and harder to catch.
[*][b]Trick shots[/b]: curve your throw around cover, lob it over a wall, or fake a throw to bait a catch into thin air.
[*][b]Two hearts each[/b]: a hit costs one, the second one sends you out for a few seconds. Catching never heals, so every hit counts.
[/list]

[h2]Three arenas, with nothing to hold on to[/h2]
[list]
[*][b]Market Rooftops[/b]: two rooftops above a street. Hold the bridges, because one tackle and you fall.
[*][b]The Pool[/b]: an empty pool and a diving board. Low ground against high ground, edges open to the drop.
[*][b]The Depot[/b]: two pits, two trams, three lanes. The tram roof rules, one tackle away from the drop.
[/list]
Charge into a rival to knock the ball out of their hands. Near an edge, they fall: [b]ring out[/b], and the KO is yours.

[h2]Special balls[/h2]
Each match draws one special ball type, Bomb or Bounce. Every 45 seconds one appears on a pink plinth in the middle of the arena, on the side of the team that's behind, and both teams race for it:
[list]
[*][b]Bomb[/b]: explodes on impact and hits every rival in the blast.
[*][b]Bounce[/b]: ricochets off the walls, so bank it off one near your target.
[/list]

[h2]Short, intense matches[/h2]
[list]
[*]First team to 10 KOs wins the round, best of three rounds, sudden death on a tie.
[*]Bots at three difficulty levels fill every empty slot, so a match always starts.
[*]A guided training mode teaches every move in a few minutes.
[/list]

[h2]Play your way[/h2]
[list]
[*][b]Keyboard & mouse[/b]: over-the-shoulder camera with an optional first-person view, dash, jump, tackle and fully rebindable keys.
[*][b]Gamepad[/b]: matches and menus play on a controller, with rebindable buttons (room codes use Steam's on-screen keyboard).
[*][b]English and French[/b], adjustable text size and team symbols for colour-blind players.
[/list]

[h2]Online with friends[/h2]
[list]
[*]Quick match, or a private room your friends join with a code.
[*]Online matches are hosted by a player; bots take any empty seat.
[/list]

[h2]Level up your look[/h2]
Every match earns XP. Climb from level 1 to 100 and unlock [b]17 hats and masks[/b], from a cap to a black crown, and wear them online.
```

### FR

```
[h2]Attrape. Charge. Renvoie.[/h2]
Dogeball est un dodgeball en équipe, rapide, en 3 contre 3 à la troisième personne : cours, dash, saute, vise et lance au clavier-souris ou à la manette. Chaque balle qui arrive sur toi est une occasion : attrape-la au bon moment et renvoie-la.

[h2]Tout se joue à l'attrape[/h2]
[list]
[*][b]Attrape[/b] les tirs, ou esquive-les si tu ne peux pas. Au bon moment, c'est une [b]attrape parfaite[/b] : la balle garde sa charge et monte d'un palier dans tes mains.
[*][b]Passe pour charger[/b] : une passe d'un coéquipier charge la balle. Chaque palier rend le tir suivant plus rapide et plus dur à attraper.
[*][b]Coups spéciaux[/b] : courbe ton tir autour d'un abri, lobe par-dessus un mur, ou feinte pour faire attraper le vide.
[*][b]Deux cœurs chacun[/b] : une touche en coûte un, la deuxième te sort quelques secondes. Attraper ne soigne jamais : chaque touche compte.
[/list]

[h2]Trois arènes, rien pour se retenir[/h2]
[list]
[*][b]Toits du Marché[/b] : deux toits au-dessus d'une rue. Tiens les ponts, un tacle et c'est la chute.
[*][b]La Piscine[/b] : un bassin vide et un plongeoir. Le bas contre le haut, les bords ouverts sur le vide.
[*][b]Le Dépôt[/b] : deux fosses, deux trams, trois couloirs. Le toit du tram domine, à un tacle de la chute.
[/list]
Fonce sur un rival pour lui faire lâcher sa balle. Au bord du vide, il tombe : [b]éjecté[/b], le KO est pour toi.

[h2]Balles spéciales[/h2]
Chaque match tire au sort un type de balle spéciale, bombe ou rebond. Toutes les 45 secondes, une balle apparaît sur un socle rose au milieu de l'arène, du côté de l'équipe menée, et les deux équipes se la disputent :
[list]
[*][b]Bombe[/b] : explose à l'impact et touche tous les adversaires dans le souffle.
[*][b]Rebond[/b] : ricoche sur les murs, vise à côté d'eux.
[/list]

[h2]Des matchs courts et intenses[/h2]
[list]
[*]La première équipe à 10 KO gagne la manche, en deux manches gagnantes, mort subite en cas d'égalité.
[*]Des bots à trois niveaux complètent les places vides : un match démarre toujours.
[*]Un entraînement guidé apprend chaque geste en quelques minutes.
[/list]

[h2]Joue comme tu veux[/h2]
[list]
[*][b]Clavier-souris[/b] : caméra à l'épaule, vue à la première personne en option, dash, saut, tacle et touches entièrement réassignables.
[*][b]Manette[/b] : matchs et menus se jouent à la manette, boutons réassignables (le code de salle passe par le clavier virtuel de Steam).
[*][b]Français et anglais[/b], taille du texte réglable et symboles d'équipe pour les daltoniens.
[/list]

[h2]En ligne entre amis[/h2]
[list]
[*]Partie rapide, ou salle privée que tes amis rejoignent avec un code.
[*]Les matchs en ligne sont hébergés par un joueur ; des bots prennent les places libres.
[/list]

[h2]Soigne ton look[/h2]
Chaque match rapporte de l'XP. Monte du niveau 1 au niveau 100 et débloque [b]17 couvre-chefs et masques[/b], de la casquette à la couronne noire, à porter en ligne.
```

À ajouter avant publication : 2 ou 3 GIF courts dans la description (attrape parfaite, éjection, bombe), tirés de la bande-annonce.

Noms anglais des arènes : ceux affichés en jeu (`assets/i18n/textes.csv`). Balles spéciales : une famille tirée au sort par match, posée toutes les 45 s sur le socle central le plus proche de l'équipe menée (`MatchController.PlaceOnSpecialSpot`, socles `special` de `tools/maps/arenas.py`).

### Conditions (retirer la ligne si ce n'est pas livré)

| Passage | Dépend de | État au 07/11 |
|---|---|---|
| « fully rebindable keys » / « touches entièrement réassignables » | Options > Clavier et Contrôles | fait |
| « Gamepad » / « Manette », « rebindable buttons » | Manette en mode écran, boutons réassignables | fait ; à essayer sur une vraie manette et sur Steam Deck avant de l'annoncer |
| « Quick match, or a private room your friends join with a code » | Salons Steam (code dans `scripts/Online/Steam/`), relais P2P | écrit, jamais lancé avec un vrai Steam : premier essai avec l'AppID 480 (`ACTIONS-JULES.md`) ; sans Steam, le code demande le port UDP 7777 ouvert |
| « team symbols for colour-blind players » | Option « Symboles d'équipe (daltonisme) » | fait |
| « 17 hats and masks » / « 17 couvre-chefs et masques », « from a cap to a black crown » | Catalogue `scripts/Online/Cosmetics.cs` | fait ; recompter si le catalogue change |
| « English and French » | Option Langue, table `assets/i18n/textes.csv` | fait ; cocher les deux langues (interface) dans Steamworks |
| Tag « Online PvP », case Online PvP (section 6), carton « 3 VS 3 · ONLINE » de la bande-annonce | Jeu en ligne par Steam (salons, relais) | même état que la ligne « Quick match » : à garder seulement après un vrai match en ligne par Steam |
| Toute mention de la VR | Hors Steam : la VR est pour les arènes EVA (décision du 22/10) | **ne jamais l'annoncer sur Steam** |
| Achievements, Stats, Cloud (cases à cocher) | 22 succès et 8 stats (`achievements.csv`), Auto-Cloud | écrits ; à vérifier avec un vrai Steam |

## 4. Tags suggérés (20)

À choisir dans la liste de Steam (Modifier la page store > Tags) ; les 5 premiers pèsent le plus, l'ordre compte.

1. Sports
2. Action
3. Online PvP
4. Team-Based
5. Multiplayer
6. PvP
7. Competitive
8. Fast-Paced
9. Arcade
10. Third Person
11. Physics
12. Controller
13. Stylized
14. Colorful
15. Cartoony
16. Bots
17. 3D
18. Party Game
19. Casual
20. Indie

Pas de tag « VR » ni « First-Person » : la version Steam se joue sur écran, à la troisième personne.

## 5. Genres

Action, Sports, Indie. Ajouter **Early Access** si le jeu sort en accès anticipé (recommandé, voir `ACTIONS-JULES.md`) : il faut alors répondre aux questions Early Access de Valve sur la page.

## 6. Fonctionnalités Steam à cocher

| Case Steamworks | Cocher ? | Condition |
|---|---|---|
| Online PvP | oui | relais Steam / lobbies livrés (sinon le jeu ne marche en ligne que par IP) |
| LAN PvP | oui | déjà fait (`--host`, `--join=IP`) ; à garder si le menu le propose |
| Shared/Split Screen PvP | non | pas d'écran partagé |
| Remote Play Together | **non** | ne sert qu'aux jeux multijoueurs en local (même machine) ; le jeu n'en a pas |
| Remote Play on TV / Phone / Tablet | à tester | clavier-souris ou manette en streaming : ça marche en théorie, à vérifier |
| Steam Achievements | oui | succès livrés (fil réseau, `docs/steam/achievements.csv`) |
| Stats | oui | si les stats Steam sont livrées |
| Steam Cloud | oui | Auto-Cloud configuré (`ACTIONS-JULES.md`) |
| Steam Trading Cards | plus tard | Valve les ouvre selon les ventes ; rien à faire au lancement |
| Steam Workshop / Level editor | non | |
| In-App Purchases | non | |
| VR Supported / VR Only / Tracked Motion Controllers | **non** | la VR n'est pas vendue sur Steam (arènes EVA, hors Steam) : ne rien cocher ni remplir dans la partie VR de Steamworks |
| Full Controller Support | à décider après essai | manette faite en mode écran, menus compris ; le code de salle passe par le clavier virtuel de Steam (`MainMenu.PadKeyboard`), donc seulement quand Steam tourne. Après essai sur une vraie manette et sur Steam Deck : « Full » |
| Partial Controller Support | voir ligne au-dessus | le questionnaire « controller support » de Steamworks fixe ensuite l'étiquette |
| Captions available | non | pas de dialogues |
| Cross-Platform Multiplayer | non | la version VR d'EVA est hors Steam |
| Steam Timeline / Leaderboards | non | pas prévu au lancement |

## 7. Configuration requise

Valeurs **estimées, à mesurer** sur de vraies machines (le rendu est le renderer Mobile de Godot 4.3, Vulkan ; la taille finale du build est à relever dans l'artefact du workflow). Dans Steamworks, chaque ligne a son champ. Aucune configuration VR : la partie VR des systèmes et plateformes reste vide.

| | Minimum | Recommandé |
|---|---|---|
| OS | Windows 10 64 bits | Windows 11 64 bits |
| Processeur | Intel Core i3-6100 / AMD Ryzen 3 1200 | Intel Core i5-8400 / AMD Ryzen 5 2600 |
| Mémoire | 8 Go | 16 Go |
| Graphismes | GeForce GTX 950 2 Go / Radeon RX 460, Vulkan 1.1 | GeForce GTX 1060 6 Go / Radeon RX 580 |
| Réseau | Connexion haut débit | idem |
| Stockage | 1 Go (à mesurer) | 1 Go |
| Notes | Clavier-souris ou manette | |

Steam Deck : visé (voir la checklist, section Steam Deck), mais rien à écrire sur la page tant que Valve n'a pas donné son verdict de compatibilité.

## 8. Bande-annonce (60 à 90 s)

Contraintes Valve (https://partner.steamgames.com/doc/store/trailer) : 1920x1080 au plus, 30 ou 60 i/s, 5 000 kbit/s ou plus, H.264 + AAC en .mp4/.mov, 16:9. Les deux premières bandes-annonces passent avant les captures, la première doit montrer surtout du gameplay. Beaucoup la regardent sans le son : tout doit se comprendre à l'image. Captation en jeu, à 60 i/s, HUD visible, interface en anglais.

Durée visée : 70 s. Musique de match du jeu, coupes sur les temps.

| # | Temps | Plan | Ce qu'on voit | Texte à l'écran |
|---|---|---|---|---|
| 1 | 0:00–0:04 | Gros plan à l'épaule | Une balle arrive vers la caméra, le joueur l'attrape au dernier moment (anneau d'attrape parfaite, son) | — |
| 2 | 0:04–0:09 | Plan large | Toits du Marché : les deux toits, la rue, les trois ponts, six joueurs en mouvement | logo du jeu |
| 3 | 0:09–0:15 | 3e personne | Course, dash, tir chargé qui touche un adversaire (flash, cœur perdu) | — |
| 4 | 0:15–0:21 | 3e personne, caméra proche | Attrape parfaite, la balle monte d'un palier, renvoi immédiat qui touche | CATCH. |
| 5 | 0:21–0:27 | 3e personne | Passe d'un allié (palier 1), la balle s'illumine | CHARGE. |
| 6 | 0:27–0:33 | Plan latéral | Le tir chargé part et met KO ; puis tir à effet autour d'une cheminée | SEND IT BACK. |
| 7 | 0:33–0:39 | Toits du Marché | Tacle sur un pont, l'adversaire tombe dans la rue (éjecté) | — |
| 8 | 0:39–0:45 | La Piscine | Saut du plongeoir, tir plongeant sur le bassin | — |
| 9 | 0:45–0:51 | Le Dépôt | Annonce « SPECIAL BALL: BOMB », course vers le socle rose, la bombe explose sur deux adversaires | — |
| 10 | 0:51–0:56 | Feinte | Le joueur feinte, l'adversaire attrape le vide, le vrai tir le met KO | KEYBOARD & MOUSE OR GAMEPAD |
| 11 | 0:56–1:02 | Montage | Vestiaire : trois couvre-chefs essayés, puis podium de fin avec le MVP | 17 HATS TO UNLOCK |
| 12 | 1:02–1:06 | Montage rapide | Trois KO enchaînés, un par arène | 3 VS 3 · ONLINE |
| 13 | 1:06–1:10 | Carton final | Logo, « Wishlist now on Steam » (puis « Available now » à la sortie) | — |

Tourner une version sans aucun texte pour la presse et les réseaux. N'utiliser que des plans réellement jouables dans le build (règle du store) : aucun plan VR.

## 9. Captures d'écran

Exigences : 1920x1080 au moins, 16:9, au moins 5 sur la page, au moins 4 marquées « tout public » ; uniquement du jeu (pas d'illustration, pas de texte ajouté par-dessus, https://partner.steamgames.com/doc/store/assets/standard et /doc/store/review_process). HUD autorisé et conseillé. Captures en 3840x2160 si possible (Steam réduit).

Les captures se refont d'une commande dans le vrai jeu, joueur automatique, interface en anglais : `GODOT=godot tools/steam/shots.sh DOSSIER` (puis `capsules.py` pour les capsules). Jeu du 07/11 : `/mnt/project-files/steam/captures-0711/` (rendu logiciel lavapipe sans carte graphique : les ombres et l'anticrénelage seront meilleurs sur une vraie machine, refaire la série sur le PC de jules avant de publier).

Sélection du 07/11 (dans `/mnt/project-files/steam/captures-0711/`, ordre conseillé pour la page) :

1. `01-toits-pont` : Toits du Marché, trois joueurs se disputent la bombe sur le pont central (caméra de plateau, sans HUD).
2. `02-toits-touche` : le joueur se fait toucher (« BAM! », « HIT! BY MISTRAL », un cœur perdu), trois joueurs dans le cadre.
3. `03-piscine-02` : La Piscine, le bassin vide et le plongeoir vus du bord.
4. `04-depot-03` : Le Dépôt, trams et plaque tournante, faisceaux des plots.
5. `05-piscine-feinte` : feinte d'un bot (« FEINT! ») au bord du bassin.
6. `06-balle-speciale` : la bombe posée sur son socle rose au fond du bassin, faisceau allumé.
7. `07-fin-de-partie` : écran de fin, MVP, tableau, XP gagnée.
8. `08-menu` : menu « Affiche » (à garder pour la fin, ou pour une capture marketing hors page).

Toutes datent de la première manche : refaire la série sur une partie entière (score serré, prolongation) sur le PC de jules.

Manquent encore, à capturer à la main sur le PC de jules (l'instant est trop court pour le joueur automatique) : une **attrape parfaite** en gros plan et un **tacle au bord du vide**. Le **vestiaire** (profil, un couvre-chef porté : `--profile-wardrobe`) peut remplacer le menu.

La première capture sert aussi de fond de page si on ne fournit pas de « page background ». Les capsules (fond : vues d'ensemble à la caméra de plateau, logo du menu sur un aplat d'encre) sont dans `captures-0711/capsules/` ; une illustration dessinée (personnages en gros plan) les remplacera avantageusement avant la sortie.
