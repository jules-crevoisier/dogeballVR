# Succès, stats, niveau et Steam Cloud : ce qu'il faut déclarer dans Steamworks

Tout ce qui suit se fait dans Steamworks une fois l'app créée (étapes 11 et 12 de `ACTIONS-JULES.md`). Les noms API sont ceux du code : les recopier tels quels, sans les changer.

## Ce que fait le jeu

- **Niveau du joueur**, façon rang de Knockout City : de l'XP à chaque fin de match, un niveau de 1 à 100, un titre tous les 10 niveaux (Recrue, Lanceur de rue, Esquiveur, Attrapeur, Cador, Pro du quartier, Vedette, Champion, Légende, Icône du Dogeball).
- **XP d'un match** :
  - match joué : +100 ;
  - victoire : +150 ;
  - par KO : +30 ;
  - par balle attrapée : +10 ;
  - par arrêt parfait : +20 ;
  - par passe : +5, avec un plafond de 20 passes ;
  - meilleur joueur (MVP) : +50.

  Hors ligne, contre des bots faciles, le total est multiplié par 0,5 ; contre des bots normaux, par 0,8. En ligne, ou contre des bots difficiles, l'XP est complète. Un match rapporte en général entre 150 et 600 XP. Il faut 350 XP pour passer du niveau 1 au niveau 2, 800 XP au niveau 10, et 2 800 XP au niveau 50.
- **Écran de fin** : un bandeau en bas de l'écran montre le niveau, la barre qui se remplit, le passage de niveau, l'XP gagnée et son détail.
- **Écran Profil**, depuis le bouton PROFIL du menu : le niveau, le titre et la barre d'XP, les statistiques de carrière, et la liste des succès. Les succès débloqués apparaissent en jaune. Les autres sont grisés, avec leur progression (par exemple « 34 / 50 »).
- **Comptage** : les totaux (KO, attrapes, arrêts parfaits, passes, victoires) sont comptés à la fin du match. Ils viennent du tableau de l'hôte, le même que celui de l'écran de fin, et ce tableau arrive aussi chez les clients en ligne : une partie compte donc de la même façon pour tous les joueurs. Les gestes rares (attrape parfaite, KO chargé, sortie de piste, passe décisive) sont repérés pendant le jeu.
- **Sans Steam**, ou avec un build hors Steam, tout est enregistré dans `progress.cfg`. Au lancement suivant avec Steam, les deux côtés sont fusionnés : chaque stat garde la plus grande des deux valeurs, et un succès débloqué d'un côté est recopié de l'autre.

## 1. Stats (Stats & Achievements > Stats)

Il y a 8 stats, toutes du même type :

- Type : `INT`.
- Set By : `Client`.
- Increment Only : coché.
- Default Value : 0.
- Max Change : vide.

Le nom affiché (Display Name) est en anglais dans Steamworks.

| API name | Display Name | Sens |
|---|---|---|
| STAT_WINS | Wins | victoires |
| STAT_KOS | Knockouts | KO infligés |
| STAT_CATCHES | Catches | tirs adverses attrapés |
| STAT_TACKLES | Tackles | tacles |
| STAT_MATCHES | Matches played | matchs joués jusqu'au bout |
| STAT_PERFECT_CATCHES | Perfect catches | arrêts parfaits |
| STAT_PASSES | Passes | passes à un coéquipier |
| STAT_XP | Total XP | XP totale ; le niveau en est déduit |

## 2. Succès (Stats & Achievements > Achievements)

Il y a 22 succès, listés dans `docs/steam/achievements.csv` avec leurs textes en français et en anglais.

Pour chacun :

- Saisir le nom API, le nom et la description en anglais (langue par défaut), puis en français (onglet de langue).
- Laisser « Hidden » décoché : aucun succès n'est caché.
- Mettre les icônes de `docs/steam/succes-icones/` : `ACH_xxx.png` en « Achieved icon » et `ACH_xxx_gris.png` en « Unachieved icon ». Elles font 256x256. La planche `planche.png` les montre toutes. Ce sont des icônes provisoires, dans le style du jeu, qu'on peut refaire plus tard avec `tools/steam/achievement_icons.py`.
- **Succès à seuil** : ceux qui ont une stat et un seuil dans le CSV. Pour eux, régler « Progress Stat » sur cette stat, avec un minimum de 0 et un maximum égal au seuil. Steam affiche alors la progression dans la bibliothèque, par exemple « 34/50 ». Le jeu débloque déjà ces succès lui-même : ce réglage ne sert qu'à l'affichage.

Liste des succès à seuil :

| Succès | Stat | Seuil |
|---|---|---|
| ACH_FIRST_WIN | STAT_WINS | 1 |
| ACH_WINS_10 | STAT_WINS | 10 |
| ACH_WINS_50 | STAT_WINS | 50 |
| ACH_KO_10 | STAT_KOS | 10 |
| ACH_KO_100 | STAT_KOS | 100 |
| ACH_CATCH_50 | STAT_CATCHES | 50 |
| ACH_TACKLE | STAT_TACKLES | 1 |
| ACH_MATCHES_25 | STAT_MATCHES | 25 |
| ACH_PERFECT_25 | STAT_PERFECT_CATCHES | 25 |
| ACH_PASS_50 | STAT_PASSES | 50 |

Ensuite :

1. Cocher « Steam Achievements » dans les fonctionnalités de l'app.
2. Cliquer sur « Publish » (Publier). Sans publication, rien n'existe pour les joueurs.

## 3. Steam Cloud (Auto-Cloud, rien à coder)

La configuration est celle de l'étape 11 de `ACTIONS-JULES.md`, sans changement. Elle synchronise deux fichiers du dossier `%APPDATA%/DogeballVR/` :

- **`settings.cfg`** : les options qui suivent le joueur (commandes et touches, son, accessibilité, difficulté des bots, tutoriel fait).
- **`progress.cfg`** : les succès, les stats et l'XP, donc le niveau.

**Nouveau : `machine.cfg` n'est pas synchronisé, exprès.** Il contient les réglages propres à l'ordinateur :

- plein écran ;
- synchronisation verticale ;
- images par seconde ;
- qualité ;
- échelle de rendu ;
- taille de l'interface.

Sans cette séparation, un Steam Deck recevrait la qualité haute du PC, et un PC hériterait de l'interface agrandie du Deck. Les anciennes installations sont migrées seules : au premier lancement, ces réglages sont lus dans `settings.cfg`, puis rangés dans `machine.cfg`.

Quotas : 1 Mo et 10 fichiers par joueur suffisent. Les deux fichiers font moins de 4 Ko.

## 4. Ce que donne l'AppID 480 (Spacewar), avant la création de l'app

Le zip de test se lance avec l'AppID 480. Ce que ça donne :

- **Niveau, XP, écran Profil, succès dans le jeu** : tout marche. Tout est enregistré en local dans `progress.cfg`.
- **Succès et stats sur Steam** : rien ne s'affiche. Spacewar a ses propres succès, et les nôtres n'y existent pas. Le jeu le voit au lancement et garde la progression en local : aucune erreur, aucun plantage. Le journal indique `[Succès] Stats Steam reçues : 0 envoyé(s)…`.
- **Steam Cloud** : rien n'est synchronisé, puisque l'Auto-Cloud de Spacewar ne connaît pas nos fichiers.
- **Au passage sur le vrai AppID**, une fois les stats et succès déclarés et publiés : au premier lancement, la progression locale est envoyée à Steam. Les succès déjà gagnés pendant les tests apparaissent alors d'un coup, sans rien perdre.

## 5. Vérifier après publication

Sur la branche beta, avec le vrai AppID :

1. Jouer un match.
2. Ouvrir l'overlay Steam (Maj+Tab), onglet Succès : « Première victoire » doit y être si le match est gagné.
3. Dans la page du jeu de la bibliothèque, regarder les succès à seuil : la progression s'affiche, par exemple « 1/10 victoires ».
4. Passer sur un second PC ou sur un Deck : les touches et le niveau doivent suivre, mais pas la qualité graphique.
5. En cas de doute, le journal `%APPDATA%/DogeballVR/logs/godot.log` montre les lignes `[Succès]` et `[Profil]`.
