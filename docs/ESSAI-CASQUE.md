# Essai au casque (Meta Quest)

Protocole court pour essayer le build Quest et valider ce qu'a ajouté la PR #25 :
gants, vibration au bord de la zone, voile du garde-corps du lift, lift relatif à la
taille et balle neutre. Compter 30 minutes.

## 1. Ce qu'il faut

- Un Quest 2, 3 ou Pro en **mode développeur**. Dans l'appli Meta Horizon du
  téléphone : Menu > Appareils > le casque > Paramètres du casque > Mode développeur.
- Le câble USB-C du casque et le PC Windows.
- **Meta Quest Developer Hub** (MQDH, gratuit, site developers.meta.com) ou `adb`
  (Android platform-tools).
- Pour l'essai maison : 2 m × 2 m dégagés, limite Guardian tracée.

## 2. Récupérer les APK

Onglet **Actions** du dépôt > dernier run **Build Quest** de la branche > en bas,
artifact **DogeballVR-quest-debug** (zip, il faut être connecté à GitHub). Le zip contient :

| Fichier | Nom dans le casque | Lancement |
|---|---|---|
| `DogeballVR-quest.apk` | Dogeball VR | Comme à EVA : Néon court, au centre de la zone 0 (15 × 8 m), contre les bots |
| `DogeballVR-quest-essai.apk` | Dogeball Essai maison | La Fosse, départ **sur le pad du lift**, face au terrain adverse : tout le protocole tient en 2 m |

Les deux applis s'installent côte à côte. Le keystore de debug est fixe : un nouvel
APK s'installe par-dessus l'ancien.

## 3. Installer et lancer

**Avec MQDH** : brancher le casque et accepter « Autoriser le débogage USB » dans le
casque. Dans MQDH, Device Manager > Apps > **Add Build**, puis choisir l'APK.

**Avec adb** :

```
adb install -r DogeballVR-quest-essai.apk
adb install -r DogeballVR-quest.apk
```

Dans le casque : Bibliothèque > filtre **Sources inconnues** > lancer l'appli. Le jeu
démarre directement en VR (pas de menu en VR pour l'instant).

Pour garder le journal du moteur pendant l'essai (utile en cas de plantage) :
`adb logcat -s godot > journal-quest.txt`

## 4. Essai maison (appli « Dogeball Essai maison »)

Se placer au milieu de l'espace dégagé, regarder droit devant, lancer l'appli.
On démarre sur le pad du lift (disque au sol), la fosse et l'équipe adverse devant soi.

| # | Test | Ce qui est attendu | À noter |
|---|---|---|---|
| A | **Démarrage** | Image en VR dans les deux yeux, la tête suit sans saccade | Fluide, saccadé, ou plantage ? |
| B | **Gants** | Un gant sur chaque main, posé sur la paume. Manette tenue normalement : paume vers l'intérieur, pouce en haut. Gant gauche en miroir. Les cœurs (vies) sont sur le dos de la main gauche | Si la paume est tournée de travers : d'un quart de tour, dans quel sens ? Si les doigts piquent vers le haut ou le bas : de combien ? |
| C | **Lift** | Debout sur le pad, s'accroupir et tenir 1 s : fondu au noir, le plateau monte de 1,6 m. Il redescend seul après 6 s au plus | Se tenir debout ou sauter ne doit pas le déclencher. Le seuil est à 72 % de la taille debout : se déclenche-t-il trop tôt ou trop tard ? |
| D | **Voile du garde-corps** | En haut, vers la fosse : une ligne au sol de la couleur de l'équipe et un voile lumineux très transparent, **sans barre** à laquelle on voudrait s'appuyer | Le bord se voit-il assez ? Le voile gêne-t-il la vue ? *Ne pas marcher vers le voile : il est à 6 m dans le jeu, donc derrière le mur de la pièce* |
| E | **Vibration du bord** | Redescendu, marcher doucement **vers la gauche**. Après 1,4 m environ, les deux manettes vibrent par impulsions douces, de plus en plus serrées vers le bord, et un clic grave part toutes les 1,5 s. Un mur de lumière apparaît. En reculant, tout s'arrête | Se sent-elle sans regarder ? Trop forte, trop faible, agaçante ? Le clic s'entend-il en pleine partie ? |
| F | **Balle neutre** | La Fosse n'a pas de moyeu : la balle neutre tombe **à la verticale** au-dessus d'une zone de l'équipe menée, hors des couverts | Tombe-t-elle à portée, dans la zone, sans rebondir jusqu'au mur ? |
| G | **Lancer et attraper** | Prendre une balle, la lancer sur un bot, attraper un tir. Pendant un tir entrant, la vibration de tir remplace celle du bord, sauf à moins de 0,3 m du bord | Limite connue : la balle tenue paraît environ 10 cm devant le gant |

## 5. Jeu comme à EVA (appli « Dogeball VR »)

Manche contre les bots sur Néon court. On démarre au centre de la zone : le bord
de la zone est à 4 m devant ou derrière soi et à 7,5 m sur les côtés. À la maison, il
ne se teste donc qu'avec l'essai maison.

| # | Test | Ce qui est attendu | À noter |
|---|---|---|---|
| H | **Orientation au départ** | Les adversaires devant soi | **À vérifier** : la zone 0 est tournée de 180° et, hors essai maison, le jeu ne tourne pas l'origine VR. Il se peut que l'on démarre dos aux adversaires. À EVA, c'est le calage de la salle qui décide |
| I | **Partie de 2 min** | Une manche jouable du début à la fin, sans plantage | Images saccadées, chaleur du casque, son |
| J | **Échange de plaques** | Il ne se produit jamais en VR (coupé par la PR #25) | Toute téléportation de zone est un bug |

## 6. Ce qu'il faut renvoyer

Une ligne par test (A à J) : OK, KO ou « bof », avec la remarque. Pour les gants,
le sens et l'angle de la correction suffisent. Ce sont deux constantes dans
`scripts/Main.cs` : `GloveRollDegrees`, 90° aujourd'hui, et `GlovePitchDegrees`, 0°.
Une vidéo du casque aide beaucoup : bouton Meta > Appareil photo > Enregistrer la
vidéo. Joindre aussi `journal-quest.txt` en cas de plantage.

## 7. Limites de ce build

- **APK de debug** : le C# et le moteur tournent avec les contrôles de debug, un peu
  plus lents qu'un build de sortie. Ne pas juger la performance au chiffre près.
- **Modèles allégés pour Quest** : les `*_quest.glb` et `props_quest/` sont sur la
  branche locale du fil Assets 3D, pas encore poussée. Ce build charge les modèles
  PC. Le budget de rendu sera à revoir une fois cette branche fusionnée.
- Pas d'icône (icône Godot par défaut) ni de menu en VR. Pas de jeu en réseau
  depuis le casque : sans menu, l'appli ne peut ni héberger ni rejoindre.
- Le Guardian du Quest reste la seule vraie limite physique. La vibration du bord
  est une aide, pas une sécurité.

## Refaire le build soi-même

`tools/quest/prepare-android.sh` installe le plugin OpenXR de Meta et règle
l'éditeur. Il faut ensuite exporter les presets **Meta Quest** et **Meta Quest Essai
maison** (Projet > Exporter), avec Godot 4.3 .NET, les modèles d'export, le JDK 17
et le SDK Android : build-tools 34, platform 34, NDK 23.2.8568313. La CI
(`.github/workflows/quest.yml`) fait la même chose à chaque push.
