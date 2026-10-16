# Architecture

Principes (voir `05-architecture-evolutive-eva.md` dans les notes du projet) :
1. Le jeu raisonne en **zones**, pas en piste entière : démo = 4 zones (`maps/demo_2v2.json`), EVA = autres définitions.
2. Les **maps sont des données** (JSON) : zones, équipes, plus tard murets et niveaux.
3. **Autorité unique** (hôte) sur balles, touches, scores : `MatchRules` est du C# pur, exécuté côté hôte.
4. **Bots** sur le même chemin que les joueurs.
5. Aucun mécanisme ne dépend d'une position précise sur la piste (dérive de ~50 cm par demi-heure).
6. Pas de déplacement au joystick (interdit en production chez EVA).

## Étapes
1. APK sur Quest : mains + balle (`Main.cs`, `HandThrower.cs`).
2. Régler lancer / courbe / attrapé parfait.
3. Réseau à 2 joueurs (`NetSession`), balle synchronisée.
4. 4 zones, avatars, règles 2v2 (`MatchRules`).
5. Balles spéciales, DA néon, lumière, bots, test sur piste.
