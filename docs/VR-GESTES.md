# Lancer au casque : les gestes

Au casque, on joue comme à EVA : on se déplace en vrai dans sa zone (pas de stick), on
attrape en fermant la main (grip) au bon moment, et on lance avec le vrai geste du bras.
Les effets de balle de Knockout City se font au geste. Le code est dans
`scripts/Gameplay/VrThrowGesture.cs` (lecture du geste) et `scripts/Gameplay/HandThrower.cs`
(la main).

## Les coups

| Coup | Geste | Ce que fait la balle |
|---|---|---|
| **Tir tendu** | Lancer par-dessus l'épaule, comme une balle de baseball. Ouvrir la main (relâcher le grip) au bout du geste | Part dans la direction de la main au lâcher, à la vitesse du **pic** du geste dans les 0,15 s avant le lâcher × 1,4 (20 m/s au plus). Un geste mou mais net part quand même à 5,5 m/s |
| **Effet** | Lancer **de côté**, main sous les épaules, tir à peu près à plat : le bras balaie en arc devant soi | La balle tourne du côté où le bras tournait, jusqu'à 2 m d'écart à 12 m (même effet maximal que le PC). Un lancer par-dessus l'épaule ne part jamais en effet, même quand le tronc tourne |
| **Cloche** | Lancer **par en dessous** : main sous la poitrine, vers le haut (au moins 38° au-dessus de l'horizontale) | Arc haut (3 m au-dessus du lâcher). Le point de chute suit la force du geste (4 à 24 m) et se cale sur l'adversaire visé s'il est à moins de 18° |
| **Passe** | Lancer vers un allié (à moins de 12°), tout adversaire étant au moins deux fois plus loin de l'axe (et 6° de plus) | Arc vers les mains de l'allié, qui gagne un palier de charge en l'attrapant |
| **Tir chargé** | Avec la balle en main, **tenir la gâchette** 0,8 s, puis lancer. Trois petites vibrations, puis un déclic net et l'anneau rose : c'est chargé. Une gâchette déjà serrée à la prise compte. Relâcher la gâchette avant de lancer annule | +15 % de vitesse et de plafond (23 m/s). Le temps de vol minimal passe de 0,5 à 0,43 s. L'hôte vérifie que la balle a bien été tenue 0,8 s |
| **Feinte** | Faire le geste sans ouvrir la main | Rien ne part |

Le nom du coup (« EFFET → », « CLOCHE ! », « PASSE ! », « TIR CHARGÉ ! ») s'affiche
au-dessus de la main au lâcher, pour apprendre les gestes en jouant.

## Le terrain et la zone réelle

- L'origine VR est posée au centre de sa zone de 15 × 8 m et tournée vers l'adversaire.
- **Recalage** : debout au centre de sa zone réelle, regard vers l'adversaire, tenir les
  deux gâchettes 2 s sans balle. Seulement entre deux manches : pendant le décompte et en
  pleine manche, le terrain ne bouge jamais sous les pieds (vrai aussi chez un client : il
  lit la phase de la partie dans le tableau de score de l'hôte). C'est une retouche : le
  terrain ne s'écarte jamais de plus de **0,5 m et 5°** du repère de la salle (espace
  « Stage » du casque), quel que soit le nombre de recalages (pas de dérive d'une pause à
  l'autre). 5° décalent déjà le coin d'une zone de 15 × 8 m de 0,75 m. Le recentrage du
  système (bouton Meta) ne touche pas au terrain. À EVA, le calage de la salle reste
  l'affaire de l'opérateur.
- Au lancement, le jeu lit le contour Guardian (jusqu'à 4 essais si le casque n'est pas
  prêt) et vérifie que les **quatre coins du terrain** sont dedans, à 0,25 m près (le Quest
  donne un rectangle inscrit). Sinon, un message le dit. La seule vraie limite physique
  reste le Guardian.
- Le recalage refuse une tête penchée de plus de 60° (regard au sol : cap indéfini). Chez
  un client, il faut encore au moins 2 s de pause avant la manche suivante.
- Au lâcher, si la main repart en arrière (cap à plus de 60° de celui du pic), la balle
  garde la direction du pic.
- **Pas d'ancre partagée entre casques** : le jeu suppose que l'origine « Stage » de chaque
  casque est le centre de sa zone réelle, tournée comme dans la map. Le jeu ne vérifie pas
  que les casques d'une même salle sont alignés entre eux : c'est le calage de l'opérateur.

## Réglages (map, bloc `rules.vrGestures`)

Tous facultatifs. Valeurs par défaut posées sans casque, à régler aux premiers essais :

| Clé | Défaut | Rôle |
|---|---|---|
| `velocityWindowSeconds` | 0,15 | Fenêtre lue avant le lâcher pour trouver le pic de vitesse |
| `curveDeadzone` / `curveFull` | 3,5 / 10 rad/s | Rotation de la trajectoire de la main : sans effet en dessous, effet maximal au-dessus |
| `curveMaxSpin` | 6 | Effet maximal (même valeur que le PC) |
| `curveHandBelowHead`, `curveMaxElevationDegrees` | 0,3 m ; 25° | Effet réservé au lancer de côté : main sous les épaules, tir à plat (un trois-quarts à hauteur d'épaule n'en donne pas) |
| `lobMinElevationDegrees` | 38 | Angle minimal d'une cloche |
| `lobHandBelowHead` | 0,45 m | La main doit lâcher au moins à cette distance sous la tête |
| `lobDistancePerSpeed`, `lobMinDistance`, `lobMaxDistance` | 2,2 ; 3 ; 24 | Portée de la cloche selon la vitesse de la main |
| `lobAimConeDegrees` | 18 | Cône de visée de la cloche |
| `passConeDegrees` | 12 | Cône de la passe |
| `chargeSeconds` | 0,8 s | Gâchette tenue pour une pleine charge |
| `chargeSpeedBonus` | 0,15 | Gain du tir chargé (borné à 0,5 par l'hôte) |

## Tests sans casque

- `--gesture-test` : 25 cas de gestes synthétiques à 72 images/s (Quest 2), dont la
  comparaison avec l'ancienne lecture (moyenne des 4 dernières images : 8 m/s lus pour un
  pic à 10 m/s).
- Au casque, chaque lancer écrit dans le journal la rotation de la trajectoire de la main
  (`[VR] Lancer … (rotation de la main X rad/s)`) : de quoi régler `curveDeadzone` sur de
  vrais tirs.
- `--vr-sim --vr-sim-throws` : construit la branche casque du jeu sans casque, vérifie que
  le joueur démarre face à l'adversaire et rejoue 5 gestes par la vraie main.
  `--vr-sim-shots=DOSSIER` et `--vr-sim-cam=dos` en tirent des captures (rendu requis).
