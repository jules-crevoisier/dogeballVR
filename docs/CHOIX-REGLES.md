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
