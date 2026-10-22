# Checklist de sortie Steam

État au 22/10/2026, branche `claude/project-thread-9jmtq8` (PR #22). Légende :

- **[x]** fait dans le dépôt ; **[~]** en cours cette nuit (fil indiqué) ; **[ ]** à faire.
- **Qui** : `code` (dépôt, agents), `jules` (compte Steamworks, décisions, argent), `art` (modèles, images, vidéo, son).
- Les liens `partner.steamgames.com/doc/...` sont la doc Valve. Les chiffres marqués « vérifié » ont été relus sur cette doc le 21/10/2026 ; « à revérifier » = pas trouvé tel quel dans la doc, à confirmer dans l'interface Steamworks.

Documents liés : `docs/steam/ACTIONS-JULES.md` (ce que seul jules peut faire, dans l'ordre), `docs/steam/PAGE-STORE.md` (textes, tags, config, bande-annonce, captures), `tools/steam/README.md` (envoi des builds).

## 0. Calendrier imposé par Valve (vérifié)

| Règle | Valeur | Source |
|---|---|---|
| Frais Steam Direct | 100 $ par jeu, récupérés sur le versement qui suit 1 000 $ de revenu brut ajusté | partner.steamgames.com/steamdirect |
| Délai entre le paiement des frais et la sortie | **30 jours** (page Steam Direct). La page d'onboarding dit 21 jours : prévoir 30 | partner.steamgames.com/steamdirect, /doc/gettingstarted/onboarding |
| Vérification banque et fiscalité | jusqu'à 30 jours (15 à 30 selon la page Steam Direct) | /doc/gettingstarted/onboarding |
| Page « Coming soon » publique avant la sortie | **au moins 2 semaines** | /doc/store/coming_soon |
| Revue de la page store | 3 à 5 jours ouvrés ; la soumettre au moins 7 jours ouvrés avant la date voulue | /doc/store/review_process, /doc/store/coming_soon |
| Revue du build | 3 à 5 jours ouvrés, prévoir 7 ; une seule revue, ensuite les mises à jour sont libres | /doc/store/review_process |
| Branche `default` | ne peut pas être activée automatiquement par steamcmd (à la main dans Steamworks) | /doc/sdk/uploading |

Conséquence : **aucune sortie Steam n'est possible avant J+30 après le paiement des frais**, et en pratique 5 à 6 semaines après le début de l'inscription (paperasse + revue page + 2 semaines de « Coming soon » + revue du build). Ce qu'on peut avoir rapidement : un build qui tourne sur une branche beta privée et une page prête à soumettre.

## 1. Steamworks et compte

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [ ] | Compte Steam de jules avec Steam Guard mobile | jules | store.steampowered.com/join |
| [ ] | Inscription Steamworks (nom légal exact, particulier ou société), NDA et Steam Distribution Agreement signés | jules | partner.steamgames.com/steamdirect, /doc/gettingstarted/onboarding |
| [ ] | Coordonnées bancaires (titulaire = nom déclaré) | jules | /doc/gettingstarted/onboarding |
| [ ] | Questionnaire fiscal (W-8BEN pour un particulier français, W-8BEN-E pour une société) et vérification d'identité | jules | /doc/gettingstarted/onboarding |
| [ ] | Paiement des frais Steam Direct (100 $) : démarre les 30 jours | jules | partner.steamgames.com/steamdirect |
| [ ] | Création de l'app, nom définitif choisi (voir PAGE-STORE.md §1), AppID noté | jules | partner.steamgames.com/dashboard |
| [ ] | Depot Windows (AppID + 1) ; depot Linux facultatif ; publier | jules | /doc/sdk/uploading, /doc/store/application/depots |
| [ ] | Options de lancement (au moins une option VR, sinon la VR n'est pas activée) : voir section 10 | jules | /doc/features/steamvr/settings |
| [ ] | Compte builder dédié (permissions Edit App Metadata + Publish App Changes) | jules | tools/steam/README.md §2 |
| [ ] | Secrets GitHub `STEAM_USERNAME`, `STEAM_CONFIG_VDF`, `STEAM_APP_ID`, `STEAM_DEPOT_ID_WINDOWS` (+ `STEAM_DEPOT_ID_LINUX`) | jules | tools/steam/README.md §3-4 |
| [ ] | Branche beta (avec mot de passe pour les testeurs) | jules | tools/steam/README.md §6 |
| [ ] | Auto-Cloud : quotas et chemins (voir ACTIONS-JULES.md) | jules | /doc/features/cloud |
| [ ] | Succès saisis depuis `docs/steam/achievements.csv` (icônes 256x256, couleur + grisée), publiés | jules + art | /doc/features/achievements/ach_guide |
| [ ] | Fichier de rich presence `docs/steam/rich_presence_fr_en.vdf` envoyé (Communauté > Rich Presence) | jules | /doc/api/ISteamFriends (Rich Presence Localization) |
| [ ] | Questionnaire de contenu (Content Survey) dont la déclaration d'usage de l'IA (contenu pré-généré : textes, modèles, code produits avec de l'IA) | jules | Steamworks > page store > Content Survey ; règles IA à revérifier |
| [ ] | Classification IARC (questionnaire gratuit dans Steamworks) | jules | Steamworks > Content Survey / Age ratings (à revérifier) |
| [ ] | Prix et prix régionaux (recommandation Valve) | jules | /doc/store/pricing |
| [ ] | Accès anticipé ou version 1.0 : décision (recommandé : Early Access, voir ACTIONS-JULES.md) | jules | /doc/store/earlyaccess |

## 2. Page store et assets marketing

Tailles vérifiées sur /doc/store/assets/standard, /doc/store/assets/libraryassets et /doc/store/assets/community. Toutes les capsules doivent porter le nom ou le logo lisible ; pas de texte promotionnel (« -50 % », récompenses) sur les capsules.

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | Textes : accroche FR/EN ≤ 300 caractères, description longue BBCode FR/EN, tags, genres, fonctionnalités | code | docs/steam/PAGE-STORE.md |
| [ ] | Relecture des textes par jules, nom définitif, retrait des passages non livrés (tableau « Conditions ») | jules | docs/steam/PAGE-STORE.md §3 |
| [~] | Outil de captures et capsules : `tools/steam/shots.sh` prend les captures dans le vrai jeu (`--shots`, `--shots-clean` sans HUD) et `tools/steam/capsules.py` sort toutes les capsules aux tailles ci-dessous avec le logo du menu. Jeu provisoire du 22/10 (rendu logiciel, nom de travail) dans le dossier du projet `steam/captures-provisoires/` : à refaire sur le PC de jules en rendu complet, et à remplacer par une vraie illustration pour les capsules | code (fait) + art | tools/steam/ |
| [ ] | Header capsule **920x430** (obligatoire) | art | /doc/store/assets/standard |
| [ ] | Small capsule **462x174** (obligatoire ; Steam en tire 120x45 et 184x69 : le logo doit rester lisible en tout petit) | art | /doc/store/assets/standard |
| [ ] | Main capsule **1232x706** (obligatoire) | art | /doc/store/assets/standard |
| [ ] | Vertical capsule **748x896** (obligatoire) | art | /doc/store/assets/standard |
| [ ] | Page background **1438x810** (facultatif ; sinon généré depuis la dernière capture) | art | /doc/store/assets/standard |
| [ ] | Captures **1920x1080 minimum, 16:9, au moins 5** (8 prévues), au moins 4 « tout public », gameplay uniquement | art + code | /doc/store/assets/standard ; liste dans PAGE-STORE.md §9 |
| [ ] | Library capsule **600x900** (texte limité au titre) | art | /doc/store/assets/libraryassets |
| [ ] | Library header **920x430** (sinon reprend le header capsule) | art | /doc/store/assets/libraryassets |
| [ ] | Library hero **3840x1240**, **aucun texte**, zone sûre centrale 860x380 | art | /doc/store/assets/libraryassets |
| [ ] | Library logo **PNG à fond transparent, 1280 de large et/ou 720 de haut**, logotype seul ; position choisie dans l'outil d'aperçu | art | /doc/store/assets/libraryassets |
| [ ] | Icône d'app (communauté) **184x184 JPG** | art | /doc/store/assets/community |
| [ ] | Icône de raccourci **256x256 ou 512x512, ICO ou PNG** | art | /doc/store/assets/community |
| [ ] | Icône de l'exe Windows (`application/icon` dans `export_presets.cfg`, .ico) | art + code | export_presets.cfg ; voir section 4 |
| [ ] | Bande-annonce : 1920x1080 max, 30/60 i/s, ≥ 5 000 kbit/s, H.264 + AAC (.mp4/.mov), 16:9, la première = gameplay ; plan 75 s | art | /doc/store/trailer ; PAGE-STORE.md §8 |
| [ ] | Langues déclarées : interface FR + EN (l'anglais est obligatoire comme langue de repli) ; ni audio ni sous-titres (pas de voix) | jules + code | /doc/store/localization/languages |
| [ ] | Configuration minimale / recommandée PC et VR (estimations à mesurer) | code + jules | PAGE-STORE.md §7 |
| [ ] | Contrôleurs : questionnaire « controller support » ; aujourd'hui rien à la manette en mode écran, donc ni Full ni Partial | jules | Steamworks > page store (questionnaire) ; à revérifier |
| [ ] | Page soumise en revue ≥ 7 jours ouvrés avant le « Coming soon » visé, puis bouton « Post as Coming Soon » | jules | /doc/store/coming_soon |
| [ ] | Date de sortie (ou « à venir ») affichée sur la page | jules | /doc/store/coming_soon |

## 3. Build et dépôts

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | Export Windows automatique à chaque merge (zip de test, release `build-latest`) | code | .github/workflows/export.yml |
| [x] | `steam_api64.dll` (Steamworks.NET 2024.8.0, somme SHA-256 vérifiée) à côté de l'exe et dans `data_DogeballVR_windows_x86_64` | code | export.yml, steam-upload.yml |
| [x] | `steam_appid.txt` = 480 (Spacewar) dans le zip de test seulement, avec note dans le lisez-moi | code | export.yml, docs/LISEZ-MOI-test.txt |
| [x] | Modèles SteamPipe (app, depot Windows, depot Linux) ; `steam_appid.txt`, `.pdb`, console exclus | code | tools/steam/*.vdf.template |
| [x] | Workflow d'envoi manuel (`workflow_dispatch`, branche beta par défaut, aperçu, arrêt net si secrets manquants, actions épinglées par SHA) | code | .github/workflows/steam-upload.yml |
| [x] | Version 0.1.0 (`config/version` ; `file_version` / `product_version` = 0.1.0.0, Godot exige 4 nombres) | code | project.godot, export_presets.cfg |
| [x] | Preset d'export Linux | code | export_presets.cfg |
| [ ] | Premier envoi réel sur la branche beta (nécessite AppID + secrets) | jules | tools/steam/README.md §5 |
| [ ] | Build installé depuis Steam et testé sur un PC propre (sans Godot ni .NET installés) | jules | |
| [ ] | `company_name`, `copyright` (« © 2026 … »), `trademarks` dans `export_presets.cfg` : à choisir par jules (nom légal ou studio) ; laissés vides | jules | export_presets.cfg |
| [ ] | `application/modify_resources` = true pour que l'icône et les infos de version soient écrites dans l'exe (demande rcedit, et wine sur le runner Linux) | code | export_presets.cfg, docs Godot « Exporting for Windows » |
| [ ] | `product_name`, `application/config/name`, nom de l'exe et du dossier data alignés sur le nom définitif (attention : changer `config/name` change le dossier des sauvegardes, voir section 7) | code | project.godot, export_presets.cfg |
| [ ] | Signature de code de l'exe (facultatif sur Steam, évite l'alerte SmartScreen hors Steam) | jules | |
| [ ] | Build Linux natif (facultatif ; sinon Proton) et test sur Steam Deck | code | steam-upload.yml (secret `STEAM_DEPOT_ID_LINUX`) |
| [ ] | Retirer les options de test de la version publique (`--pc-autoplay`, `--net-selftest`, `--rotor-test`… inoffensives mais à cacher) | code | scripts/ |

## 4. Intégration du SDK Steamworks

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | Initialisation Steamworks.NET, `RestartAppIfNecessary(AppID)` dès que `ShippedAppId` est renseigné, repli propre sans Steam (une ligne « [Steam] indisponible », salle en LAN / code IP) | code | scripts/Online/Steam/SteamPlatform.cs ; /doc/sdk/api#initialization_and_shutdown |
| [x] | Lobbies Steam : partie rapide (recherche filtrée par version puis création), salon privé (amis), invitations (overlay, « Rejoindre la partie », `+connect_lobby` au lancement) ; **jamais testé avec un vrai client Steam** : à valider avec l'AppID 480 sur deux PC | code + jules | scripts/Online/SteamBridge.cs ; /doc/features/multiplayer/matchmaking |
| [x] | Transport P2P par relais Steam (ISteamNetworkingMessages, relais SDR de Valve, pas de port à ouvrir) ; même code testé en CI sur UDP local avec 10 % de perte | code | scripts/Online/Steam/SteamMultiplayerPeer.cs ; /doc/features/multiplayer/steamdatagramrelay |
| [x] | Succès (`docs/steam/achievements.csv`), suivis sur l'hôte et sur les clients (touches relayées par l'hôte), repli local sans Steam | code | scripts/Online/Steam/AchievementTracker.cs ; /doc/features/achievements |
| [x] | Rich presence FR/EN (`docs/steam/rich_presence_fr_en.vdf`, clé `steam_display`) : menu, salle, en partie, « Rejoindre » | code | scripts/Online/Steam/RichPresence.cs ; /doc/api/ISteamFriends#richpresencelocalization |
| [~] | Steam Cloud pour `user://settings.cfg` et `user://progress.cfg` (Auto-Cloud, rien à coder : chemins à saisir par jules, voir ACTIONS-JULES.md) ; `identity.cfg`, `sessions/`, `crash/` restent locaux | jules | /doc/features/cloud |
| [x] | Journaux de crash : au lancement suivant, le journal d'une session morte est copié dans `user://crash` (10 gardés), exceptions .NET non gérées comprises ; pas d'envoi (pas de politique de confidentialité à écrire) | code | scripts/Online/CrashReporter.cs ; |
| [x] | Compatibilité vérifiée à la connexion : version du jeu et empreinte des messages réseau, refus clair « mettez le jeu à jour » | code | scripts/Net/NetHandshake.cs |
| [x] | Ping affiché et bannière « connexion instable » / « reconnexion » | code | scripts/Online/NetStatusOverlay.cs |
| [ ] | Overlay Steam : vérifier qu'il s'affiche (Maj+Tab) en mode écran avec le renderer Vulkan, et la pause quand il s'ouvre | code | /doc/features/overlay |
| [~] | Nom Steam du joueur dans la salle (fait) ; avatar Steam et nom au-dessus des personnages (à faire) | code | ISteamFriends |
| [ ] | Langue du jeu par défaut = langue de Steam (`GetCurrentGameLanguage`), FR sinon EN | code | /doc/store/localization/languages |
| [ ] | Steam Input / glyphes manette (seulement si on vise le support manette) | code | /doc/features/steam_controller/getting_started_for_devs |

## 5. Contenu du jeu

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | Boucle de match complète : manches, KO, prolongation, score (testé en CI) | code | scripts/Rules/MatchRules.cs, .github/workflows/build.yml |
| [x] | Deux formats : VR d'EVA (5 manches de 2 min) et PC en ligne façon KC (premier à 7 / 15 KO en 2c2 / 4c4, chrono 5:00, 2 manches gagnantes, retour 5 s, pause 12 s), choisi par la salle : format VR dès qu'un casque est présent | code | scripts/Rules/MatchRules.cs (`UseOnlineFormat`), scripts/Online/OnlineLobby.cs |
| [x] | Fin de partie en ligne : retour de tous dans la salle, revanche automatique en 10 s si chacun reste prêt | code | scripts/Online/OnlineLobby.cs (`ReturnToLobby`) |
| [x] | 6 cartes (Croix Néon, Fosse croisée, Tour 4v4, Néon Court, La Fosse, démo 2v2) | code | maps/*.json |
| [x] | Coups : passe, cloche, effet, feinte, attrape parfaite, dash, tacle, ring-out, balles spéciales (bombe, rebond) | code | docs/CHOIX-REGLES.md |
| [x] | Bots qui complètent les places vides | code | scripts/Gameplay/Bot.cs |
| [~] | Modèles 3D et rig des personnages (remplacent les capsules) | art (fil 3D) | |
| [~] | Carte continue (décor 3D fini) | art (fil 3D) | |
| [~] | Menu principal, salon, sélection de carte | code (fil interfaces) | |
| [~] | Pause, écran de fin de partie (stats, rejouer, quitter) | code (fil interfaces) | |
| [~] | Tutoriel (attrape, lancer, passe, dash) | code (fil interfaces) | |
| [~] | Game feel : impacts, ralentis, secousses, particules, sons | code (fil interfaces) | |
| [ ] | Musique et sons définitifs (aujourd'hui sons synthétisés, `scripts/Audio/Sfx.cs`) | art | |
| [ ] | Progression (niveau de compte, cosmétiques débloqués) stockée dans `progress.cfg` | code | |
| [ ] | Écran de crédits avec les mentions de licences (section 8) | code | |
| [ ] | Bots qui utilisent lift, tremplins, passes (aujourd'hui tireurs fixes) | code | docs/CHOIX-REGLES.md |
| [ ] | Sonar et cage (balles spéciales listées dans les cartes mais non faites) : les retirer des cartes ou les faire | code | docs/CHOIX-REGLES.md |

## 6. Options et accessibilité

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | Sensibilité souris enregistrée (`user://settings.cfg`) | code | scripts/Gameplay/DesktopPlayer.cs |
| [~] | Options vidéo (plein écran / fenêtré, résolution, V-Sync, limite d'i/s, qualité, champ de vision) | code (fil interfaces) | |
| [~] | Options audio (général, musique, effets) | code (fil interfaces) | |
| [~] | Touches réassignables (aujourd'hui ZQSD et WASD actifs ensemble, pas de rebind) | code (fil interfaces) | |
| [ ] | Inversion de l'axe Y | code | docs/CHOIX-REGLES.md (noté manquant) |
| [ ] | Taille du texte / HUD, daltonisme (équipes distinguées autrement que par la couleur) | code + art | |
| [ ] | Réduction des secousses d'écran et des flashs (photosensibilité) | code | |
| [ ] | Confort VR : rotation par crans ou continue, vignette, hauteur, main dominante | code | voir section 10 |
| [ ] | Support manette en mode écran (nécessaire pour Steam Deck « Vérifié » et pour cocher Full Controller Support) | code | /doc/steamdeck/compat |

## 7. Technique et qualité

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | CI : compilation C#, parties simulées, tests réseau hôte + client | code | .github/workflows/build.yml |
| [x] | Hôte qui fait autorité (anti-triche de base : vitesses, portée, validation des prises) | code | docs/CHOIX-REGLES.md |
| [x] | Dossier des sauvegardes fixé, indépendant du nom du jeu : `%APPDATA%/DogeballVR/` (Windows), `~/.local/share/DogeballVR/` (Linux) ; chemins Auto-Cloud alignés dans ACTIONS-JULES.md | code | project.godot (`config/use_custom_user_dir`) |
| [ ] | Pas d'alerte OpenXR au démarrage sans casque (réglage `xr/openxr/startup_alert` à vérifier dans Godot 4.3) et pas de lancement involontaire de SteamVR en mode écran | code | project.godot, scripts/Xr/XrBootstrap.cs |
| [ ] | Performance mesurée : 60 i/s stables en mode écran sur la config minimale ; 90 i/s en VR (72 sur Quest Link) | code | |
| [~] | Interpolation des autres joueurs sur l'horloge de l'hôte avec retard adaptatif à la gigue (fait, testé en CI à 80 ms et 5 % de perte) ; rembobinage côté hôte et prédiction du lancer côté client (à faire) | code | scripts/Net/ClientWorld.cs |
| [x] | Déconnexions : client qui revient dans sa zone (coupure ou crash, secret émis par l'hôte, 45 s), bot qui reprend la place en attendant ; hôte qui ferme : les clients sont prévenus et reviennent au menu (pas de migration d'hôte) ; testé en CI | code | scripts/Net/NetSession.cs, scripts/Online/OnlineLobby.cs |
| [x] | Test de bout en bout en CI : salle à 4 (hôte + 3 clients + 4 bots, Tour 4v4), deux parties enchaînées par la revanche, zones distinctes, chaque client joue | code | .github/workflows/build.yml |
| [ ] | Test de charge 4c4 avec 8 humains et latence réelle (100–150 ms) | code + jules | |
| [ ] | Tests sur plusieurs PC (NVIDIA, AMD, Intel ; Windows 10 et 11 ; écran 144 Hz) | jules | |
| [ ] | Test sur casques réels : Quest via Link / Air Link / Steam Link, Index ou Vive | jules | |
| [ ] | Écrans 16:10, 21:9, 4K ; Alt+Tab ; perte de focus | code | |

## 8. Légal

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | Les textes du store ne citent aucun autre jeu ni aucune marque | code | docs/steam/PAGE-STORE.md |
| [ ] | Nom définitif vérifié (Steam, EUIPO/INPI/USPTO classes 9 et 41, domaine) | jules | PAGE-STORE.md §1 |
| [~] | Mentions d'un autre jeu retirées du contenu livré (`maps/*.json`, fait) ; restent les commentaires de `scripts/` et `README.md` / `docs/`, à nettoyer avant de rendre le dépôt public | code | grep -ri knockout |
| [ ] | Mentions de licences dans les crédits : Godot (MIT, texte de licence obligatoire), bibliothèques tierces de Godot (FreeType, ENet, mbedTLS… via `Engine.GetLicenseText`), Steamworks.NET (MIT) | code | docs Godot « Complying with licenses » |
| [ ] | Accord Steamworks SDK accepté (couvre la redistribution de `steam_api64.dll`) | jules | partner.steamgames.com (accords) |
| [ ] | Licences des polices, sons, musiques, modèles (garder les preuves) | art + jules | |
| [ ] | Politique de confidentialité si les journaux de crash ou des statistiques sont envoyés (RGPD) ; lien sur la page | jules + code | |
| [ ] | CLUF : l'accord d'abonné Steam suffit, CLUF propre facultatif | jules | |
| [ ] | Statut fiscal et social en France pour encaisser (micro-entreprise ou société), TVA : à voir avec un comptable | jules | |

## 9. Lancement et marketing

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [ ] | Page « Coming soon » publiée au plus tôt (les wishlists s'accumulent dès ce moment) | jules | /doc/store/coming_soon |
| [ ] | Démo jouable (app démo gratuite rattachée) et inscription au Steam Next Fest (calendrier et dates limites sur Steamworks) | jules + code | /doc/marketing/upcoming_events/nextfest (à revérifier) |
| [ ] | Playtest Steam fermé (outil « Steam Playtest ») pour remplir les serveurs et mesurer le ressenti | jules | /doc/features/playtest |
| [ ] | Kit presse (captures, logo, GIF, fiche), liste de créateurs VR et de jeux de sport | art + jules | |
| [ ] | Discord ou forum de la communauté, lien depuis le jeu | jules | |
| [ ] | Réduction de lancement (10 à 20 %) et date de sortie hors des gros soldes Steam | jules | /doc/marketing/discounts |
| [ ] | Notes de version et événement « Sortie » dans le centre d'événements Steam | jules | /doc/marketing/event_tools |

## 10. VR spécifique (SteamVR / OpenXR)

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [x] | OpenXR activé, carte d'actions avec profils Touch, Index, Vive, Cosmos, WMR, Pico, HP Reverb | code | project.godot, openxr_action_map.tres |
| [x] | Démarrage sans casque en mode écran si OpenXR est indisponible | code | scripts/Xr/XrBootstrap.cs |
| [ ] | Choix **« VR Supported »** (le jeu se joue aussi sans casque) et non « VR Only » | jules | /doc/features/steamvr/settings ; libellés à revérifier |
| [ ] | Options de lancement Steamworks (Installation > Général) : « Jouer en VR » (`--xr-mode on`) et « Jouer sur écran » (`--xr-mode off`, arguments natifs de Godot 4) ; au moins une option VR, sinon la VR n'est pas activée | jules + code | /doc/features/steamvr/settings |
| [ ] | Page store : SDK (OpenXR), casques, espace (debout, échelle de la pièce), entrées (manettes suivies) | jules | /doc/features/steamvr/settings ; PAGE-STORE.md §7 |
| [ ] | Liaisons SteamVR suggérées pour Index, Vive, WMR (Valve demande des liaisons dédiées à ces manettes) | code | /doc/features/steamvr/settings |
| [ ] | **Déplacement artificiel pour la VR à domicile** (stick, rotation par crans, vignette) : la VR actuelle est conçue pour les arènes EVA où l'on marche (déplacement au stick interdit chez EVA) ; sur Steam les joueurs n'ont que 2 à 3 m. À désactiver dans le build arène | code | docs/ARCHITECTURE.md (principe 6) |
| [ ] | Mesure sur casque : lancer, attrape, vibrations, lisibilité du texte (jamais testé, README) | jules | README.md |
| [ ] | Rendu : vérifier le renderer Mobile sur PC VR (ou passer en Forward+ pour le build PC) | code | project.godot |
| [ ] | Salon mixte VR + clavier-souris sur les cartes en anneau (pas fait) : ne pas l'annoncer avant | code | docs/CHOIX-REGLES.md |
| [ ] | Build Quest autonome / EVA : distribution hors Steam (Meta Horizon Store ou APK arène), à séparer de la version Steam | jules | |

## 11. Steam Deck

Critères vérifiés sur /doc/steamdeck/compat. La VR ne marche pas sur Deck : seul le mode écran compte.

| Statut | Élément | Qui | Référence |
|---|---|---|---|
| [ ] | Toutes les fonctions accessibles avec la configuration manette par défaut (support manette en mode écran à faire) | code | /doc/steamdeck/compat |
| [ ] | Glyphes de boutons qui suivent l'entrée active | code | /doc/steamdeck/compat |
| [ ] | Texte d'au moins 9 px de haut à 1280x800 (12 px conseillés) | code | /doc/steamdeck/compat |
| [ ] | 30 i/s au moins en 800p avec les réglages par défaut | code | /doc/steamdeck/compat |
| [ ] | Pas d'alerte OpenXR ni de lancement de SteamVR sur Deck (option de lancement écran par défaut) | code | section 7 |
| [ ] | Saisie de texte (nom de salon) à la manette (clavier virtuel Steam) | code | /doc/steamdeck/compat |
| [ ] | Demande de revue Deck (après la revue du build) | jules | Steamworks > Technical Tools > Steam Hardware Compatibility Review |
