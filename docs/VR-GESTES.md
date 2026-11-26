# Lancer au casque : les gestes

Au casque, on joue comme à EVA : on se déplace en vrai dans sa zone (pas de stick), on
attrape en fermant la main (grip) au bon moment, et on lance avec le vrai geste du bras.
Les effets de balle de Knockout City se font au geste. Le code est dans
`scripts/Gameplay/VrThrowGesture.cs` (lecture du geste) et `scripts/Gameplay/HandThrower.cs`
(la main).

## Les coups

| Coup | Geste | Ce que fait la balle |
|---|---|---|
| **Tir tendu** | Lancer par-dessus l'épaule, comme une balle de baseball. Ouvrir la main (relâcher le grip) au bout du geste | Part dans la direction de la main au lâcher, à la vitesse du **pic** du geste × 1,4 (20 m/s au plus). Un geste mou mais net part quand même à 5,5 m/s |
| **Effet** | Lancer **de côté** : le bras balaie en arc devant soi | La balle tourne du côté où le bras tournait, jusqu'à 2 m d'écart à 12 m (même effet maximal que le PC). Un lancer par-dessus l'épaule a une trajectoire de main droite vue de dessus : il ne part jamais en effet par mégarde |
| **Cloche** | Lancer **par en dessous** : main sous la poitrine, vers le haut (au moins 38° au-dessus de l'horizontale) | Arc haut (3 m au-dessus du lâcher). Le point de chute suit la force du geste (4 à 24 m) et se cale sur l'adversaire visé s'il est à moins de 18° |
| **Passe** | Lancer vers un allié (à moins de 12°), sans adversaire dans l'axe | Arc vers les mains de l'allié, qui gagne un palier de charge en l'attrapant |
| **Tir chargé** | Avec la balle en main, **tenir la gâchette** 0,8 s, puis lancer. Trois petites vibrations, puis un déclic net et l'anneau rose : c'est chargé. Relâcher la gâchette avant de lancer annule | +15 % de vitesse et de plafond (23 m/s). Le temps de vol minimal passe de 0,5 à 0,43 s |
| **Feinte** | Faire le geste sans ouvrir la main | Rien ne part |

Le nom du coup (« EFFET → », « CLOCHE ! », « PASSE ! », « TIR CHARGÉ ! ») s'affiche
au-dessus de la main au lâcher, pour apprendre les gestes en jouant.

## Le terrain et la zone réelle

- L'origine VR est posée au centre de sa zone de 15 × 8 m et tournée vers l'adversaire.
- **Recalage** : debout au centre de sa zone réelle, regard vers l'adversaire, tenir les
  deux gâchettes 2 s sans balle. Seulement hors manche ou pendant le décompte : en pleine
  manche, le terrain ne bouge jamais sous les pieds. Le recentrage du système (bouton
  Meta) ne touche pas au terrain.
- Au lancement, si la zone Guardian est plus petite que la zone du jeu, un message le dit.
  La seule vraie limite physique reste le Guardian.

## Réglages (map, bloc `rules.vrGestures`)

Tous facultatifs. Valeurs par défaut posées sans casque, à régler aux premiers essais :

| Clé | Défaut | Rôle |
|---|---|---|
| `velocityWindowSeconds` | 0,1 | Fenêtre lue avant le lâcher pour trouver le pic de vitesse |
| `curveDeadzone` / `curveFull` | 3,5 / 10 rad/s | Rotation de la trajectoire de la main : sans effet en dessous, effet maximal au-dessus |
| `curveMaxSpin` | 6 | Effet maximal (même valeur que le PC) |
| `lobMinElevationDegrees` | 38 | Angle minimal d'une cloche |
| `lobHandBelowHead` | 0,45 m | La main doit lâcher au moins à cette distance sous la tête |
| `lobDistancePerSpeed`, `lobMinDistance`, `lobMaxDistance` | 2,2 ; 4 ; 24 | Portée de la cloche selon la vitesse de la main |
| `lobAimConeDegrees` | 18 | Cône de visée de la cloche |
| `passConeDegrees` | 12 | Cône de la passe |
| `chargeSeconds` | 0,8 s | Gâchette tenue pour une pleine charge |
| `chargeSpeedBonus` | 0,15 | Gain du tir chargé (borné à 0,5 par l'hôte) |

## Tests sans casque

- `--gesture-test` : 17 cas de gestes synthétiques à 72 images/s (Quest 2), dont la
  comparaison avec l'ancienne lecture (moyenne des 4 dernières images : 8 m/s lus pour un
  pic à 10 m/s).
- `--vr-sim --vr-sim-throws` : construit la branche casque du jeu sans casque, vérifie que
  le joueur démarre face à l'adversaire et rejoue 5 gestes par la vraie main.
  `--vr-sim-shots=DOSSIER` et `--vr-sim-cam=dos` en tirent des captures (rendu requis).
