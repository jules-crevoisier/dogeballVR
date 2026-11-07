# Parcours des écrans à la manette (07/11/2026)

Manette simulée par `--pad-walk=…` (`scripts/Tools/PadWalk.cs`) : un appui toutes les 0,4 s, le contrôle qui a le
focus est écrit après chaque appui. Lancé sous xvfb, rendu Vulkan, 1920×1080, en français.

```
godot --path . --rendering-driver vulkan --resolution 1920x1080 -- --menu --lang=fr \
  --pad-walk=pause,s-bas,s-bas,a,b --pad-walk-shots=DOSSIER
```

## Avant

Godot 4.3 ne relie aux actions `ui_*` que la croix : **A ne validait rien, B ne revenait pas**, le stick gauche ne
bougeait pas la sélection. Relevé du premier parcours (A sur « EN LIGNE », A sur « QUITTER ») :

```
[Manette] ui_accept : Enter | Kp Enter | Space
[Manette] ui_cancel : Escape
[Manette] 3 s-bas → « EN LIGNE »
[Manette] 4 a → « EN LIGNE »          (la page En ligne ne s'ouvre pas)
[Manette] 15 a → « QUITTER »          (le jeu ne quitte pas)
```

Autres défauts trouvés en parcourant : aides de bas d'écran toujours au clavier (« Entrée », « Échap »), LB / RB sans
effet dans les options, plus aucun focus après l'aide complète de la pause (la manette ne pouvait plus rien
sélectionner), carte des commandes de la pause coupée par le bord de l'écran à la manette, « Stick G » écrit quatre
fois, B sans effet sur la pause. Captures : `avant-profil-aide-clavier.png`, `avant-pause-carte-coupee.png`.

## Après

| Écran | Parcours | Résultat |
|---|---|---|
| Menu principal | stick ↓ ↑, A, B | chaque item, A ouvre la page, B revient sur l'item d'origine |
| En ligne | A, stick ↓ (salle privée, code, rejoindre, réseau local), B | ok (A sur le champ du code ouvre le clavier de Steam : pas vérifiable sans Steam dans le conteneur) |
| Choix de carte | A sur JOUER, → JOUER DIRECT, A, stick ↓ ×5, B | les 6 cartes, retour au menu |
| Profil | A, RB (vestiaire), LB, B | ok |
| Options | A, RB ×3, LB ×3, stick dans les listes, B | les 4 onglets ; l'onglet des touches s'appelle MANETTE à la manette |
| Crédits | A, B | ok |
| Salon (hôte) | focus sur LANCER, stick ←/→, RB (carte suivante), B | carte changée en gardant le focus, B quitte la salle |
| Pause | Menu, stick ↓, Vue (aide complète), B, A (« MENU PRINCIPAL ? »), B | focus rendu après l'aide, B referme la pause |
| Fin de partie | A (rejouer), stick →, Menu | A = rejouer comme Entrée, Menu = menu principal |

Captures : `menu-a-valider.png`, `cartes-manette.png`, `options-onglet-manette.png`, `salon-manette.png`,
`pause-manette.png`, `fin-manette.png`.
