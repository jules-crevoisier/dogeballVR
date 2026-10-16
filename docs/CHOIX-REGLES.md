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
- Partie : meilleur des 5 manches, fin dès 3 manches gagnées (revue 3 : 10 min était trop long pour une borne d'arcade).
- Invulnérabilité après un coup : la balle traverse sans être consommée.
- Anti-triche resserré : vitesse de déplacement 8 m/s, vitesse verticale 4 m/s, main à 1 m de la tête, spin plafonné à 12.
- Limites connues : pas de compensation de latence pour les prises (l'hôte garde un léger avantage), pas de validation de la direction des lancers, portée jamais mesurée avec une vraie main sur casque.
