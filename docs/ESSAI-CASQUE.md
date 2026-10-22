# Essai au casque (Meta Quest)

Ce protocole court sert à essayer le build Quest. Il valide ce qu'a ajouté la PR #25 :
- les gants ;
- la vibration au bord de la zone ;
- le voile du garde-corps du lift ;
- le lift, qui s'adapte à la taille ;
- la balle neutre.

Compter 45 minutes la première fois, dont 15 pour le mode développeur.

## 1. Avant de commencer (une seule fois)

1. **Compte développeur Meta.** Sur developers.meta.com, créer une organisation ou en
   rejoindre une. Vérifier le compte par SMS ou avec une carte bancaire (PayPal ne
   marche pas). Il faut avoir 18 ans. Sans cela, l'interrupteur du mode développeur
   n'apparaît pas.
2. **Mode développeur.** Dans l'appli Meta Horizon du téléphone : Menu > Appareils >
   le casque > Paramètres du casque > Mode développeur. **Redémarrer le casque.**
3. **Sur le PC Windows,** installer **Meta Quest Developer Hub** (MQDH, gratuit, sur
   developers.meta.com) ou `adb` (Android platform-tools).
4. **Brancher le casque au PC** avec un câble USB qui transfère des données : celui livré
   avec le casque ne le fait pas toujours, et un câble USB-A vers USB-C convient si le PC
   n'a pas d'USB-C. Dans le casque, accepter « Autoriser le débogage USB » et cocher
   **Toujours autoriser depuis cet ordinateur**.

L'APK déclare les Quest 2, 3 et Pro. Le Quest 3S devrait l'accepter aussi en
installation directe, mais personne ne l'a encore vérifié.

## 2. Récupérer les APK

Dans l'onglet **Actions** du dépôt, ouvrir le dernier run **Build Quest** de la PR #28
(branche `claude/project-thread-otf5l0`).
En bas de la page se trouve l'artifact **DogeballVR-quest-debug** (un zip ; il faut être
connecté à GitHub). Il contient deux APK :

| Fichier | Nom dans le casque | Au lancement |
|---|---|---|
| `DogeballVR-quest.apk` | Dogeball VR | Comme à EVA : Néon court, au centre de la zone 0 (15 × 8 m), contre les bots |
| `DogeballVR-quest-essai.apk` | Dogeball Essai maison | La Fosse, départ **sur le pad du lift**, face au terrain adverse, bots faciles et lents |

Les deux applis s'installent côte à côte. Le keystore de debug de la CI est toujours le
même : un nouvel APK s'installe par-dessus l'ancien.

## 3. Installer et lancer

- **Avec MQDH :** Device Manager > Apps > **Add Build**, puis choisir l'APK.
- **Avec adb** (Android platform-tools : MQDH en contient un, mais pas dans le PATH) :

  ```
  adb install -r DogeballVR-quest-essai.apk
  adb install -r DogeballVR-quest.apk
  ```

**Débrancher le câble avant de jouer :** on va marcher, s'accroupir et se retourner.
Dans le casque, ouvrir Bibliothèque, filtre **Sources inconnues**, puis lancer l'appli.
Le jeu démarre directement en VR (il n'y a pas encore de menu en VR).

En cas de plantage, rebrancher le casque et récupérer le journal :

```
adb logcat -d -s godot > journal-quest.txt
```

## 4. Essai maison (appli « Dogeball Essai maison »)

Il faut un espace dégagé d'au moins 2 × 2 m, avec la limite Guardian tracée autour. Se
placer au milieu et lancer l'appli. Une seconde après le lancement, le jeu se recale sur
la tête du joueur :
- on se trouve sur le pad du lift (un disque au sol) ;
- la fosse et l'équipe adverse sont devant soi.

Si l'image n'est pas calée, maintenir le bouton Meta de la manette droite pour
recentrer. Si cela ne suffit pas, quitter l'appli, se replacer au milieu en regardant
vers l'avant, puis relancer. Ouvrir le menu Meta pendant un test ne déplace plus rien,
mais un recentrage, si : ne le faire qu'au milieu de la pièce, face à l'avant.

Un pilier se trouve à 1 m droit devant. On passe à travers sans effet, alors ne pas
avancer : il cache juste une partie de la fosse.

**Les bots tirent, doucement :** 12 à 15 m/s, un tir toutes les 2 à 3,5 s. Rester sur
place et attraper ou encaisser. **Ne pas esquiver en se déplaçant** dans une petite
pièce. Si l'image saccade ou si la nausée vient, retirer le casque.

| # | Test | Ce qui est attendu | À noter |
|---|---|---|---|
| A | **Démarrage** | Image en VR dans les deux yeux, la tête suit sans saccade | Fluide, saccadé, ou plantage ? |
| B | **Gants** | Un gant sur chaque main, posé sur la paume. Manette tenue normalement : paume vers l'intérieur, pouce en haut. Le gant gauche est en miroir. Les cœurs (vies) sont sur le dos de la main gauche | Si la paume est tournée de travers : d'un quart de tour, et dans quel sens ? Si les doigts piquent vers le haut ou vers le bas : de combien ? |
| C | **Lift** | Rester debout immobile 1 s (le jeu mesure la taille), puis s'accroupir et tenir 1 s. Le fondu passe au noir et le plateau monte de 1,6 m. Il redescend seul après 6 s au plus. Il faut ensuite **18 s** avant de pouvoir remonter | Se tenir debout ou sauter ne doit pas le déclencher. Le seuil est à 72 % de la taille debout : se déclenche-t-il trop tôt, ou trop tard ? |
| D | **Voile du garde-corps** | Une fois en haut, regarder vers la fosse. On voit une ligne au sol de la couleur de l'équipe et un voile lumineux très transparent, **sans barre** à laquelle on voudrait s'appuyer | Le bord se voit-il assez ? Le voile gêne-t-il la vue ? Ne pas marcher vers lui : il est à 6 m dans le jeu, donc derrière le mur de la pièce |
| E | **Vibration du bord** | Redescendu, **se retourner, dos à la fosse**, et avancer doucement. Après environ 0,6 m, les deux manettes vibrent par impulsions douces, un peu plus serrées en approchant du bord (dans 2 × 2 m, on n'arrive qu'à 1,1 m du bord : la progression se sent à peine). Un clic grave part toutes les 1,5 s et un mur de lumière apparaît. En revenant sur le pad, tout s'arrête | Se sent-elle sans regarder ? Trop forte, trop faible, agaçante ? Le clic s'entend-il ? |
| F | **Balle neutre** (à l'œil) | La Fosse n'a pas de moyeu. La balle neutre tombe **à la verticale** au-dessus d'une zone de l'équipe menée, hors des couverts. Elle tombe à 3 m ou plus du pad, parfois dans la zone voisine : on ne va pas la chercher | Tombe-t-elle bien à la verticale, sans rebondir jusqu'au mur ? |
| G | **Attraper** | Attraper un tir des bots sans quitter le pad. Pendant qu'un tir arrive, la vibration de tir remplace celle du bord, sauf à moins de 0,3 m du bord. Les balles à ramasser sont trop loin pour un essai à la maison | Limite connue : la balle tenue paraît environ 10 cm devant le gant |

## 5. Jeu comme à EVA (appli « Dogeball VR »)

C'est une manche contre les bots sur Néon court. On démarre au centre de la zone : le
bord est à 4 m devant et derrière, à 7,5 m sur les côtés. À la maison, cette appli ne
sert qu'à vérifier qu'elle démarre et qu'elle tourne bien.

| # | Test | Ce qui est attendu | À noter |
|---|---|---|---|
| H | **Orientation au départ** | Le départ suit l'avant du Guardian. Le jeu ne tourne pas le joueur vers le terrain : la zone 0 est orientée à 180°, donc les adversaires sont **derrière** cet avant. Se retourner | À EVA, le calage de la salle décidera. À confirmer : les adversaires sont-ils bien derrière ? |
| I | **Partie de 2 min** | Une manche jouable du début à la fin, sans plantage | Images saccadées, chauffe du casque, son |
| J | **Échange de plaques** | Ne se produit jamais en VR (coupé par la PR #25) | Toute téléportation de zone est un bug |

## 6. Ce qu'il faut renvoyer

- Une ligne par test (A à J) : OK, KO ou « bof », avec une remarque.
- Pour les gants, le sens et l'angle de la correction suffisent. Ce sont deux constantes
  de `scripts/Main.cs` : `GloveRollDegrees` (90° aujourd'hui) et `GlovePitchDegrees`
  (0°).
- Une vidéo filmée dans le casque aide beaucoup. Bouton Meta > Appareil photo >
  Enregistrer la vidéo : le faire au milieu de la pièce, face à l'avant.
- En cas de plantage, joindre `journal-quest.txt`.

## 7. Limites de ce build

- **APK de debug :** le C# et le moteur tournent avec leurs contrôles de debug, donc un
  peu plus lentement qu'un build de sortie. Ne pas juger la performance au chiffre près.
- **Modèles allégés pour Quest :** les fichiers `*_quest.glb` et `props_quest/` sont
  sur la branche locale du fil Assets 3D, pas encore poussée. Ce build charge les
  modèles PC. Le budget de rendu sera à revoir une fois cette branche fusionnée.
- **Fovéation pas encore réglée :** elle sera à évaluer avec les modèles Quest.
- **Ni icône ni menu en VR :** l'icône est celle de Godot par défaut. Sans menu,
  l'appli ne peut ni héberger ni rejoindre une partie en réseau.
- **La seule vraie limite physique reste le Guardian du Quest.** La vibration du bord
  est une aide, pas une sécurité.

## Refaire le build soi-même

1. Lancer `tools/quest/prepare-android.sh` (sous Linux ou Git Bash). Il installe le
   plugin OpenXR de Meta et règle l'éditeur, ou dit quoi régler si l'éditeur a déjà
   ses réglages.
2. Dans Godot 4.3 .NET : **Projet > Installer le modèle de build Android**.
3. **Projet > Exporter** : exporter les presets **Meta Quest** et **Meta Quest Essai
   maison**.

Il faut aussi les modèles d'export, le JDK 17 et le SDK Android (build-tools 34,
platform 34, NDK 23.2.8568313). Pour que l'APK s'installe par-dessus celui de la CI,
le keystore de debug de l'éditeur doit être `tools/quest/debug.keystore`.

La CI (`.github/workflows/quest.yml`) refait tout cela à chaque push.
