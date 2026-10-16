# Choix de règles (écarts code / JSON du level design)

Règle appliquée : en cas de doute, l'option la plus nerveuse.

- Vies : 2 vies, respawn 3 s (valeurs du code conservées).
- Balles en jeu : 6 sur la map A (le JSON du LD en prévoyait moins), réglable via `maxBallsInPlay`.
- Gravité des balles : `ballGravityFactor` 0.3, car une balle plate tombait de 1,2 m en ~0,64 s à 0,6 g et ne touchait plus au-delà de 12 m.
- Amortissement : `LinearDamp` 0,1, portée réelle d'environ 15 m.
- Invulnérabilité après un coup (`invulnerableAfterHitSeconds`) : adoptée.
- Récompense de rattrapage parfait (`RegisterPerfectCatch`) : adoptée.
- Non implémentés : traversée des couvertures, bonus de hauteur (high ground).
- `arenaBounds` est lu depuis la map chargée ; la fosse est dans le sol, seuil de recyclage des balles sous y=-4.
- Partie : 5 manches de 2 min toutes jouées (Jules : 5 min minimum, manches plus longues et prolongation). Le raccourcissement à 3 manches de 75 s décidé après la revue 3 est annulé.
- Invulnérabilité après un coup : la balle traverse sans être consommée.
- Anti-triche resserré : vitesse de déplacement 8 m/s, vitesse verticale 4 m/s, main à 1 m de la tête, spin plafonné à 12.
- Limites connues : pas de compensation de latence pour les prises (l'hôte garde un léger avantage), pas de validation de la direction des lancers, portée jamais mesurée avec une vraie main sur casque.
- Revue 4 : manche finie à 3 KO (durée de partie inchangée, ~10 min).
- Bots : un tir ennemi rapide n'est attrapé qu'une fois sur trois ; visée à 1,3 m.
- Prises côté hôte : tolérance de latence = vitesse de la balle x 0,12 s (2 m maximum) ; tête client au minimum à 0,8 m.
- Revue 5 : la mort subite compte maintenant les KO depuis le début de la prolongation ; bots plus lents à l'esquive (3 m/s), visée à 1,1 m.
- Connus et non résolus : bots peu létaux (7 à 14 % de touches), tolérance de latence large pour un réseau ouvert, pas de test unitaire de MatchRules, aucune mesure sur casque.

## La Fosse (map B)
- JSON du level design lu tel quel, sauf : 6 balles en jeu (le LD en met 4) et gravité 0,3, comme sur Néon Court.
- Implémenté : plateaux qui montent (lift), fosse dans le sol, garde-corps de 0,9 m au bord avant, une seule zone haute par équipe, bonus de hauteur (première balle lancée du haut chargée +1, une fois par montée).
- Seuil de recyclage des balles à y = -3 (la fosse est à y = -4), -2 sous un plateau.
- Non implémenté : remplacement des couverts par le garde-corps une fois en haut (les couverts montent avec le plateau), disparition des couverts « phasing » du level design.
- Revue La Fosse 1 : le lift est validé par l'hôte (tête sur le pad, hors recharge de 18 s, pas de coéquipier en haut, 6 s maximum, repris à l'élimination) ; le client ne fait que demander. Test automatique en CI (triche hors pad refusée, altitude accordée puis reprise).
- Garde-corps visible et solide seulement en haut. Bonus de hauteur : première balle lancée du haut à charge 2 (dégâts doubles), une fois par montée.
- Non fait : réinjection des balles sous le plateau au distributeur, seuil d'accroupi relatif à la taille du joueur, noir tenu 0,1 s, couverts remplacés en haut, bots qui utilisent le lift.
- Revue La Fosse 2 : la durée du lift se contrôle au tick de l'hôte (un client silencieux redescend aussi), plafond de tête à 2,3 m, et un client qui n'est pas confirmé en haut par l'hôte pendant 1 s redescend.
- Connus : une fenêtre d'environ 0,2 s où deux coéquipiers peuvent monter ensemble (pas de réservation côté hôte), l'accroupi d'1 s et le verrou de balle chargée ne sont vérifiés que côté client, rien ne réinitialise le lift entre manches, les bots n'utilisent pas le lift.
- Revue La Fosse 3 : le pad est testé sur la tête acceptée (après zone et vitesse), l'état du lift ne change plus sur un paquet de pose ignoré (anti-inondation), cas « tête spoofée sur le pad » ajouté au test CI.
