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

## Deuxième tour (après revue : 6/10)

Corrigé après la revue indépendante :

- fin de partie : A valide le bouton sélectionné (plus de raccourci A caché vers REJOUER), et les appuis des 0,8 premières
  secondes du podium et du tableau sont avalés : enchaîner les sauts au coup de sifflet ne relance plus la partie ;
- salon : B (ou Échap) sélectionne d'abord QUITTER LA SALLE, le second appui quitte ;
- pause : en sortant, un bouton encore tenu ne compte pas comme un appui (B qui referme la pause ne fait plus esquiver) ;
  aide complète sur Y (Vue reste le tableau des scores) ;
- options : le focus revient sur la ligne après une réassignation ; barre « A Valider · B Retour » ;
- stick ou croix maintenus : la sélection défile (0,4 s puis toutes les 0,09 s), une rafale de stick ne saute qu'une ligne :

```
[Manette] 2 flot-bas → « ENTRAÎNEMENT »      (rafale 0,3 → 1,0 : une seule ligne)
[Manette] 2 s-bas+ → « ENTRAÎNEMENT »        (stick tenu…)
[Manette] 3 pause → « OPTIONS »              (… la sélection descend)
[Manette] 1 pause → « LANCER LA PARTIE »
[Manette] 2 b → « QUITTER LA SALLE »         (premier B : sélection)
[Lobby] Salle quittée                        (second B)
[Manette] 17 y → « Y »  /  19 s-bas → « L3 »  (réassignation : le focus reste dans la liste)
```

- Steam Deck : les mouvements du pavé tactile ne repassent plus les aides au clavier.

## Troisième tour (après revue : 7/10)

- réassignation : pendant l'attente du nouveau bouton, ni la répétition du stick ni un nouveau mouvement de stick ne
  bougent la sélection (plus de « Croix bas » enregistré par erreur) ; A pressé stick tenu valide la ligne visée ;
- salon : à la manette, B ne fait que sélectionner QUITTER LA SALLE, il faut A pour partir (B ×3 : on reste dans la salle) ;
- pause : Y ouvre et referme l'aide complète ; fin de partie : Entrée valide le bouton sélectionné, comme A ;
- Steam Deck : à la fin de partie, le tableau passait sous la barre d'XP et cachait la moitié des boutons
  (`deck/avant-fin-1280x800.png`) ; la colonne laisse la place de la barre et se réduit si besoin (`deck/fin-1280x800.png`) ;
- couvert translucide : verre gris-bleu (ni voile laiteux, ni presque invisible la nuit),
  `couvert-transparent-avant-apres.png` (avant, deuxième tour, maintenant).

```
[Manette] 9 s-bas+ → « Stick G »        (stick tenu…)
[Manette] 11 a → « Appuie… »            (A stick tenu : la ligne Sauter attend)
[Manette] 13 pause → « Appuie… »        (stick toujours tenu : rien ne bouge)
[Manette] 17 s-bas → « Appuie… »        (nouveau mouvement : avalé)
[Manette] 3 b → « QUITTER LA SALLE »  /  5 b, 7 b → « QUITTER LA SALLE »   (on reste dans la salle)
[Manette] 7 y → « RETOUR »  /  10 y → « REPRENDRE »                         (aide ouverte puis refermée)
[Manette] 52 s-droite → « Menu »  /  entree → menu principal                (Entrée suit la sélection)
```

Captures au format Deck : `deck/pause-1280x800.png`, `deck/pause-aide-1280x800.png`, `deck/fin-1280x800.png`,
`deck/hud-1280x800.png`, `deck/options-reassignation-1280x800.png`.

## Quatrième tour (après revue : 7,5/10)

- réassignation : la direction tenue est oubliée pendant l'attente et le retour du stick au centre n'est plus avalé, la
  sélection ne repart plus seule ensuite (`19 pause → « X »` : le focus reste sur la ligne réassignée) ;
- salon : aide « B Sortir » (B sélectionne QUITTER LA SALLE, A confirme) ; les appuis des 0,8 premières secondes dans la
  salle, à l'arrivée ou au retour d'un match, sont avalés (vérifié à la lecture : pas simulable au bon instant) ;
- fin de partie : « A Valider » sous les boutons ; aide complète de la pause : « B / Y Refermer » ;
- couvert translucide : alpha 0,22, le joueur garde ses accents orange.

Restent à vérifier sur un vrai Deck : stick physique, pavé tactile, clavier de Steam pour le code de salle.
