# Plan de production jusqu'à la 1.0

Feuille de route de dogeballVR de l'état du 06/11/2026 à la sortie Steam. Cinq jalons, chacun livré en une ou
plusieurs PR à CI verte, avec un zip Windows de la branche et des captures avant/après. Rien n'entre dans `main`
sans l'accord de jules.

| Jalon | Fichier | Type | But en une phrase |
|---|---|---|---|
| J1 Arènes | `j1-arenes.md` | Vertical Slice | De vraies cartes continues à étages, jouables par le joueur PC, les bots et en ligne. |
| J2 Interface | `j2-interface.md` | Alpha | Une interface entièrement neuve, cohérente du menu au tableau des scores. |
| J3 Boucle complète | `j3-boucle.md` | Alpha | Tout ce qu'un joueur fait entre le lancement et la 10e partie : tutoriel, salon, progression, options, son. |
| J4 Finition | `j4-finition.md` | Beta | Game feel, performance, accessibilité, Steam Deck, zéro bug bloquant. |
| J5 Sortie Steam | `j5-sortie-steam.md` | Gold | Build Steam, page store, captures, bande-annonce, revue Valve. |

## Principes

- **Le jeu PC en ligne passe d'abord.** La VR EVA garde ses cartes (Croix Néon, Tour 4v4…) et n'est pas cassée,
  mais les nouveaux contenus visent d'abord le PC. Les arènes libres sont PC seulement (`VrAllowed=false`).
- **Inspiration, pas copie.** Le dodgeball de rue arcade est la référence de rythme ; aucun nom, personnage, carte
  ni élément visuel repris d'un jeu existant.
- **Données plutôt que code.** Les cartes sont des JSON générés par `tools/maps/arenas.py` ; les règles restent dans
  les JSON de cartes et `scripts/Rules/`.
- **Preuve par la CI.** Chaque système a son job de test headless (`.github/workflows/build.yml`) ; les captures sont
  prises dans le vrai jeu (rendu Vulkan logiciel en conteneur, rendu complet sur le PC de jules).
- **Revue indépendante.** Tout livrable visible est noté par un agent indépendant jusqu'à 8/10 ou 5 tours ; les notes
  vont dans la PR, pas dans les messages à jules.

## Décisions qui restent à jules

- Fusion de chaque PR dans `main` (et de la PR #33 des interfaces, dont J2 reprend l'infrastructure).
- Nom commercial définitif, prix, accès anticipé ou 1.0 (voir `docs/steam/ACTIONS-JULES.md`).
- Compte Steamworks, frais Steam Direct, secrets de la CI Steam.
