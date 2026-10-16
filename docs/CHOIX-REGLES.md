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
- Revue La Fosse 4 : la recharge du lift est tenue par zone (une reconnexion ne la remet pas à zéro), l'hôte exige un accroupi tenu 0,8 s sur le pad avant de monter, marge de durée de 1 s.
- Toujours non fait : verrou « balle chargée » côté hôte, seuil d'accroupi relatif à la taille du joueur (borne d'arcade : un enfant peut déclencher le lift debout), remise à zéro du lift entre manches, bots qui utilisent le lift, mesure sur casque.
- Revue La Fosse 5 : la recharge du lift démarre aussi si le joueur part en haut. L'accroupi est lu sur la hauteur de tête envoyée par le client : il protège d'un bug, pas d'un client modifié (limite assumée, bornée à 6 s toutes les 18 s sur le pad).

## Clavier-souris (joueur PC)
- Inspiré des commandes de Knockout City : ZQSD/WASD, Maj sprint (7,5 m/s contre 5,5 de marche, 3,8 en armant un lancer), Espace saut (~0,5 m), Ctrl/C accroupi, E esquive (10 m/s pendant 0,18 s, recharge 1,2 s), souris pour viser. Accélération courte (pas de glissade).
- Lancer : clic gauche maintenu puis relâché. Un tap part à 14 m/s, 0,7 s de charge donnent 30 m/s. Aide à la visée légère : un adversaire à moins de 7° du regard attire le tir de moitié.
- Attrape : clic droit ouvre une fenêtre de 0,5 s (main tendue à 0,95 m, portée 1,1 m), puis 0,55 s de recharge si elle est ratée ; réussie, pas de recharge. Les balles lentes (< 3,5 m/s) à moins de 0,9 m des pieds sont ramassées automatiquement (la main se baisse dans la pose envoyée pour que l'hôte valide).
- L'hôte garde la même validation : vitesse de déplacement plafonnée à 11 m/s (était 8) pour laisser passer l'esquive, saut sous le plafond vertical de 4 m/s, main à 1 m de la tête.
- Les couverts et plots sont solides pour le joueur PC (il glisse le long). Sensibilité réglable (PageHaut/PageBas), enregistrée dans `user://settings.cfg`.
- HUD : viseur rouge sur un adversaire, marqueur de touche (hôte et hors ligne seulement), flash rouge quand on perd une vie, indicateurs d'attrape et d'esquive, aide des touches (F1).
- Test : `--pc-autoplay` pilote le joueur PC avec les mêmes actions que le clavier-souris (solo et client réseau, en CI).
- Limites : aucun test avec une vraie souris ni une vraie manette ; le marqueur de touche n'existe pas côté client ; les valeurs de feeling (vitesses, fenêtre d'attrape) sont des estimations à régler à la main.
- Revue clavier-souris 1 (6/10) corrigée : aide F1 et messages à l'écran (ancrage), mur de bord de zone masqué pour le joueur PC (il aveuglait l'écran), recapture de la souris sans lancer ni attrape, Échap en pleine charge annule le lancer, couverts solides aussi sur un plateau monté, saut qui relève la hurtbox (on passe sous une balle basse), pas d'accroupi en l'air (vitesse verticale sous 4 m/s), tir visé depuis la main avec compensation de la chute de la balle.
- Connus et non corrigés : la fenêtre d'attrape (0,5 s / recharge 0,55 s) n'est contrôlée que côté client, l'hôte n'impose que 0,15 s entre deux essais (vrai aussi en VR) ; ZQSD et WASD sont actifs ensemble ; côté client la fenêtre d'attrape se ferme à l'envoi de la demande ; pas de lancer courbe, lob, passe ni feinte comme dans Knockout City.
- Revue clavier-souris 2 (6,5/10) corrigée : les 4 murs de bord sont masqués (groupe), champ de vision horizontal 100° (cadré sur la largeur, plus de fisheye), score/manche/chrono dans le HUD 2D (tableau flottant caché pour le joueur PC), message « clique pour reprendre » quand la souris est libre, jauge d'attrape grisée quand on tient une balle, log du bord de zone limité à un message par 2 s.
- Connu : le saut d'un client PC est cosmétique pour l'hôte (la hurtbox distante ne se relève pas, la capsule s'allonge) ; l'aide à la visée ignore la ligne de vue ; pas d'inversion d'axe Y ni de rebind.
- Revue clavier-souris 3 (6,5/10) corrigée : balle tenue réduite à 55 % (elle bouchait le bas de l'écran ; elle reprend sa taille au lancer), tir visé à la distance réelle du décor touché par le regard (plus de tir trop haut de près), deux jauges séparées (attrape, esquive) en 16 px, texte d'aide limité aux 12 premières secondes, un clic déjà enfoncé au ramassage n'arme pas de lancer.
- Revue clavier-souris 4 (6/10) corrigée : côté client réseau la balle tenue suit la main locale sans attendre les snapshots et garde sa taille réduite, message KO sous le score (plus de chevauchement), étiquettes des jauges à côté des jauges, texte d'astuce décalé hors de la balle tenue, aide F1 limitée à 12 s.
- Connus : l'aide à la visée ignore la ligne de vue ; le rayon d'aide part de l'œil, pas de la main ; aucun test avec une vraie souris. Plafond atteint : 4 relectures (6, 6,5, 6,5, 6) sans atteindre 8/10 faute de pouvoir jouer à la main.
- Revue clavier-souris 5 (7/10, dernier tour) : balle fantôme dans la main d'un client après un lancer masquée, vue qui suit la souris à chaque image. La Fosse avec un client PC : 18 prises et 18 lancers validés par l'hôte. Verdict final de la boucle : 6, 6,5, 6,5, 6, 7 ; 8/10 non atteint, les relecteurs ne peuvent juger le feeling sans souris réelle.

## Croix Néon : jeu libre au clavier-souris (phase 1 de la refonte Knockout City)

Source : `masterclasse/level-design/03-game-design-knockout-pc-vr.md` et `v3/maps/map_a_croix_neon.json`.

- **Carte** : `maps/croix_neon.json` (4 plateaux N/E/S/W autour d'un moyeu central, `layout: "ring"`). Les cartes en zones (`neon_court`, `la_fosse`) gardent la vue à la 1re personne.
- **PC libre** : caméra à la 3e personne (épaule droite, 3 m), course constante 7,5 m/s sans sprint, saut d'environ 1 m, déplacement sur toute l'arène (bornes ±0,5 m), pylône central plein.
- **Dash** : Maj, 4 m en 0,2 s, 2 charges, 1,6 s de recharge par charge, invulnérable pendant l'impulsion (`Hurtbox.DashInvulnerable`).
- **Moyeu** : un lanceur envoie une balle neutre toutes les 6 s vers le plateau qui en a le moins (l'équipe menée gagne les égalités).
- **Réseau** : l'hôte valide la pose avec les bornes de l'arène entière, course 9 m/s (26 m/s pendant un dash validé : 2 charges tenues par l’hôte, 1,6 s chacune, compteur de dashs dans la pose, invulnérabilité côté hôte 0,3 s), vitesse verticale 5,5 m/s, tête sous 2,9 m ; retour de KO : l’hôte remet la copie au point d’apparition et ignore les poses du client éloignées de plus de 3 m pendant 1,5 s au plus (`NetSession.FreeRoam`). Les valeurs VR gardent leurs plafonds.
- **Pas encore fait** (phases suivantes) : passes, feinte, effet, lob, attrape parfaite, KO au palier 2, gouffre et tremplins (cartes B et C), rotor, échanges de plateaux, balles spéciales. Les bots restent des tireurs fixes sur leur plateau.
- **Test** : `--map=croix_neon --pc-autoplay` en solo et en réseau (étape CI « jeu libre Croix Néon »).
- **Limites connues (phase 1)** : pas de lobby mixte VR/PC sur les cartes en anneau (le mode libre suit la plateforme de l'hôte, conformément au cahier des charges) ; pas de collision joueur-joueur ; tir 3e personne sans raycast main→cible (collé à un couvert, la balle peut partir dans l'abri) ; le ressenti n'a pas été testé à la souris ni au casque.
- **Choix assumés** : un joueur KO est immobilisé en jeu libre seulement ; la caméra 3e personne suit le pas physique (60 Hz) sans interpolation, à vérifier sur écran 144 Hz ; accroupi 0,9 m et attrape 0,5 s (le cahier des charges dit 1,0 m et 0,45 s) ne sont pas encore alignés.
- **Limites réseau connues** (revue 5) : l'hôte ne contrôle pas les obstacles (un client modifié pourrait se tenir dans le pylône) ; un client modifié peut réclamer une invulnérabilité de 0,3 s par dash sans bouger (budget de 2 charges) ; la fenêtre d'invulnérabilité de l'hôte démarre à la réception du compteur, donc décalée de la latence ; pas de test CI de retour de KO.

## Croix Néon : coups spéciaux (phase 2 de la refonte Knockout City)

- **Passe** (molette ou X) : cloche vers l'allié le mieux aligné (35°, sinon le plus proche), gravité pleine, sommet à 4,2 m, sans frottement pour tomber là où on a visé. La passe ne blesse personne et ne s'intercepte pas (bots compris). L'allié la prend tout seul (1,3 m) et elle gagne +1 palier ; 2 paliers = KO direct (déjà le cas : 2 cœurs, dégâts doubles).
- **Cloche** (R) : lob blessant vers l'adversaire visé ou le point visé, sommet 2,2 m au-dessus des deux extrémités.
- **Effet** (E maintenu + gauche/droite au lâcher) : balle qui dévie de 3 à 4 m sur 20 m (spin 6 rad/s, force de Magnus existante).
- **Feinte** (F) : le bras part sans lâcher (visible des autres via la pose), charge annulée, il faut relâcher le clic pour réarmer.
- **Attrape parfaite** : clic droit dans les 0,18 s avant l'arrivée d'un tir adverse rapide : +1 cœur (règle existante) et la balle repart avec un palier. Fenêtre d'attrape 0,45 s, blocage de 0,6 s après un appui raté. Le client déclare la perfection, l'hôte ne la valide que sur un tir adverse rapide.
- **Règles** de la carte : un cœur revient après 10 s sans être touché, la manche s'arrête à 6 KO.
- **Flèches** : une balle ennemie rapide (≥ 12 m/s) à moins de 25 m et hors de l'écran est signalée par une flèche au bord.
- **Limites** : les passes ne sont pas interceptables (même par une attrape parfaite) ; les bots ne passent pas ; verrouillage de l'aide à la visée absent sur l'effet ; ressenti non testé à la souris.

## Fosse croisée : fosse, tremplins, terrasses, tacle (phase 3a de la refonte Knockout City)

Source : `v3/maps/map_b_fosse_croisee.json` (copie : `maps/fosse_croisee.json`, avec les règles de Croix Néon : cœur après 10 s, manche à 6 KO).

- **Fosse** : trou carré de 9 m au centre (le pylône en sort), rebord clair de 0,3 m sans collision. Les balles qui y tombent reviennent par la règle des balles perdues.
- **Chute (ring-out)** : l'hôte juge la hauteur de tête sous 0,5 m au-dessus de la fosse. Poussé par un adversaire dans les 3 s : KO pour la victime, point pour l'équipe du pousseur. Tombé seul : un cœur en moins, retour au point d'apparition après 1,5 s avec 1,5 s d'invulnérabilité (à 0 cœur c'est un KO normal, point pour l'adversaire).
- **Dash** : s'arrête à 0,8 m du vide ; on tombe en courant dedans ou en étant poussé.
- **Tacle** : un dash qui passe à moins de 1,1 m d'un adversaire le tacle. Le client l'annonce, l'hôte exige un dash validé, une distance de 3,2 m au plus (marge de latence), une victime vulnérable ; la victime lâche sa balle et est poussée de 2 m en 0,25 s ; un tacle par dash, 0,8 s entre deux tacles.
- **Tremplin + terrasse** (PC) : le tremplin de son camp (disque lumineux) lance un vol guidé de 1,8 s, sommet 3 m, qui se pose au centre de la terrasse de la plaque (bloc de 1,6 m, parapet de 1 m pour les balles seulement). On y reste 8 s au plus puis on est ramené vers la plaque ; recharge du tremplin 10 s. Un tir parti dans les 3 s après l'arrivée, avec une balle au palier 0, gagne +1 palier.
- **Réseau** : sur une carte à tremplins l'hôte laisse 6,5 m/s en vertical et 5 m de hauteur de tête ; la tête peut descendre jusqu'à −8 m au-dessus de la fosse ; l'altitude de terrasse n'est accordée que tête au-dessus du bloc et assez haute (arrivée en vol), reprise après 9 s.
- **VR** : lift et plaques inchangés, la fosse est visible ; terrasses et tremplins n'existent pas au casque.
- **Test** : `--map=fosse_croisee --pc-autoplay` (l'hôte, équipe 0, sert d'appât à 1,5 m du bord ; le client `--zone=1`, équipe 1, le tacle vers la fosse par le réseau ; puis chacun dash vers la fosse et chute volontairement). Étape CI « Test Fosse croisée ».
- **Triche** : le tacle exige un dash validé, une victime humaine à 3,2 m (marge de latence) et ±1,5 m de haut, une direction à moins de 70° (de chaque côté) de la victime, le tacle pouvant arriver jusqu'à 0,3 s avant la pose qui annonce le dash ; un client qui garde la tête haute au-dessus de la fosse est jugé tombé après 0,9 s, et on ne peut pas dasher en l'air près de la fosse ; le plafond de tête est de 2,9 m sauf pendant un vol de tremplin accordé par l'hôte (2,2 s, tête au-dessus d'un tremplin de son camp) ou sur une terrasse ; la terrasse n'est accordée qu'à la fin d'un vol de tremplin et seulement celle de ce tremplin ; un compteur de dashs truqué donne un tacle toutes les 1,6 s (budget de dashs de l'hôte) ; un joueur tombé hors manche remonte seul après 1,5 s ; la recharge de terrasse est de 7,5 s côté hôte (10 s côté client depuis le lancer).
- **Limites** : le parapet ne bloque pas le joueur (seulement les balles) ; l'hôte ne vérifie pas qu'un client arrive vraiment par le tremplin (il vérifie la hauteur de tête) ; le joueur local ne tombe pas si l'hôte est lent à juger (il remonte tout seul à −2,5 m une fois déclaré KO) ; les bots ne tacleront ni ne tomberont (ils restent sur leur plaque) ; pas de playtest à la souris.

## Tour 4v4 : huit plaques autour d'une fosse de 16×10 m (phase 3b)

- **Carte** : `maps/tour_4v4.json` (v3, carte C du level design). Huit plaques en anneau (équipes A,B,A,B…), fosse rectangulaire de 16×10 m avec le pylône au centre, un tremplin et une terrasse par plaque, 4 contre 4, seuil de KO à 10, régénération de cœur 10 s. Les règles de la Fosse croisée s'appliquent telles quelles (tacle, ring-out, chute, tremplins) ; le code lit déjà une fosse rectangulaire quelconque.
- **Pas encore fait** : les échanges de plateaux (phase 4c) ; le rotor est décrit plus bas.
- **Test** : `--map=tour_4v4 --pc-autoplay` à deux humains (hôte zone 0 = appât, client `--zone=1` = pousseur) ; l'appât se place à 0,5 à 1,8 m du bord (décalé le long du bord s'il faut éviter les couverts, sa place et celle du pousseur 2 m derrière étant libres) ; les autres zones sont des bots, que le scénario ne vise pas (un tacle sur un bot est ignoré par l'hôte). Étape CI « Test Tour 4v4 », 30 ms de latence, 4 tentatives. Le plafond de joueurs réseau passe à 8 (la zone demandée au Hello doit exister dans la carte) ; un vrai 4v4 à huit humains n'a pas été joué.

## Rotor du moyeu (phase 4a)

- **Règle** : deux bras de 4 m (une barre de 8 m, 0,4 m d'épaisseur) tournent à 60°/s entre 0,6 et 3 m de haut autour du pylône, sur les cartes en moulinet qui ont `hub.rotor` (Croix Néon, Fosse croisée, Tour 4v4). Un tir tendu qui croise un bras est arrêté ou renvoyé ; les passes montent à 4,2 m, au-dessus ; une cloche (sommet 3,0 m) frôle le dessus de la barre et peut être arrêtée. Un joueur PC touché par un bras est repoussé sans dégât. Au casque, le rotor existe pour les balles seulement (le joueur reste sur sa plaque).
- **Réseau** : l'hôte fait tourner le rotor ; l'angle est le dernier flottant de chaque snapshot, le client avance à la même vitesse et se recale doucement. La collision du joueur PC est locale (même limite que les couverts : l'hôte ne la contrôle pas).
- **Test** : `--rotor-test` lance 30 balles tendues à 22 m/s sur le moyeu, à 1,5 m de l'axe (hors du pylône), et juge chacune 1,3 s plus tard (mesuré : 16 à 20 arrêtées ; témoin `--rotor-off` sans collision : 0 à 4 ; la CI exige ≥ 10 et ≤ 8) ; en réseau le scénario de Croix Néon poste les deux joueurs à 2,6 m du centre de 8 à 20 s et l'étape CI exige au moins un « Poussé par le rotor ». L'étape Croix Néon est reprise jusqu'à 3 fois (le scénario de coups spéciaux est aléatoire).
- **Limites** : jamais ressenti en jeu ; les balles très rapides (30 m/s) peuvent traverser un bras au pas de 60 Hz malgré la détection continue ; sur les cartes à fosse, le rotor est au-dessus du vide et n'affecte que les balles (la collision du joueur PC n'est active que sur les cartes sans fosse) ; le rayon de visée et la caméra traversent le rotor.
- Une balle qui retomberait sur le dessus de la barre (y = 3 m) serait emportée sans être recyclée tant qu'elle bouge à plus de 0,4 m/s : non observé, non traité.

## Balles spéciales : bombe et rebond (phase 4b)

Une seule famille de balle spéciale par match, tirée au sort au début (comme dans Knockout City). Toutes les 45 s de manche (45 s et 90 s, jamais en prolongation), le moyeu lance en lob une balle spéciale vers une plaque de l'équipe menée aux KO (à égalité, l'équipe alterne). Le tableau annonce « BALLE SPÉCIALE : … DANS 5 s » à 40 s et 85 s. La balle est neutre à l'atterrissage : n'importe qui peut la ramasser, elle garde sa couleur (jaune pour la bombe, violet pour le rebond) et son halo prend cette couleur tant qu'elle est neutre.

- **Bombe** : explose au contact d'un adversaire ou du décor une fois lancée (0,12 s après le lâcher). Souffle de 3 m : 1 cœur à chaque adversaire dans le rayon, sans tir allié ni cumul avec le coup direct (la cible touchée est exclue du souffle). Les joueurs en dash et ceux protégés par l'invulnérabilité après une touche ne sont pas touchés. Le souffle ignore la charge (1 cœur, contre 2 pour un coup direct chargé) et les murs. L'hôte envoie la bombe explosée (type 3) pendant 0,15 s : c'est ce qui déclenche l'onde de choc chez les clients. Les balles spéciales ne comptent pas dans la limite de balles en jeu.
- **Rebond** : rebonds à 0,95 (au lieu de 0,55) et vol létal (tir tendu) 2,5 fois plus long : elle revient après avoir ricoché sur les murs.
- **Sonar et cage** : listés dans les maps (`specialBalls.types`) mais pas encore implémentés ; seuls bombe et rebond sont tirés.

Limites connues : une balle spéciale tombée dans la fosse ou recyclée est remplacée par une balle normale ; `types`, l'intervalle et l'annonce sont codés en dur (le JSON des maps est descriptif) ; le ressenti n'a pas été testé à la main. Test CI : `--special-test` lâche une bombe à 1,2 m d'un adversaire et vérifie le souffle, plus l'annonce et le lancer à 45 s.
