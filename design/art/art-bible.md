# Art Bible: Dogeball

> **Status**: Approved (choix de jules du 22/10, point 4 de l'audit : « une seule DA KC, avec une variante Quest »)
> **Owned By**: art-director
> **Last Updated**: 2026-10-22
> **Art Director Sign-Off (AD-ART-BIBLE)**: relecture indépendante du 22/10, voir la PR

Cette bible remplace toutes les DA précédentes. L'« arène sombre » v3 (`masterclasse/da/01-direction-artistique.md`), la v2 « craie » et la v1 « néon » sont archivées : on ne s'y réfère plus. Une seule DA vaut pour le PC (Steam) et pour la VR (EVA, Meta Quest). La VR en reprend une variante allégée (section 8), jamais une autre ambiance.

Sources : étude Knockout City (`masterclasse/knockout-city/01-analyse-visuelle.md`), revue 3D et DA du 22/10 (`audit-kit/constats2/3d-da.md`), fiche des modeleurs (`masterclasse/assets3d/refs-persos/FICHE-MODELEURS.md`), code (`scripts/Visual/Palette.cs`, `scripts/Maps/ArenaBuilder.cs`, `assets/shaders/kc_pbr.gdshader`).

## 1. Visual Identity Statement

**Règle unique : « Dodgeball de rue sur les toits, en plein jour : tout ce qui peut te toucher brille et est rond, tout le reste est mat et posé. »**

- **La balle d'abord.** Quand on hésite sur une couleur ou un effet, on choisit ce qui garde la balle comme l'objet le plus vif de l'image. Test : sur une capture en niveaux de gris, on trouve la balle en moins d'une seconde. Pilier servi : lisibilité du duel.
- **Une équipe, une teinte.** Quand un objet n'appartient à aucune équipe, il ne prend ni l'orange ni le bleu d'équipe, même en néon. Test : un abri neutre ne doit jamais faire croire qu'il « appartient » à une équipe. Pilier servi : lecture tactique à 20 m.
- **Un seul monde.** Quand un support (Quest) ne peut pas tout afficher, on retire des effets (ombres, SSAO, glow), jamais l'ambiance, la palette ou le décor. Test : une capture Quest et une capture PC de la même carte se reconnaissent comme la même carte. Pilier servi : la démo EVA vend le même jeu que la page Steam.

## 2. Mood & Atmosphere

| État | Cible émotionnelle | Lumière | Descripteurs | Énergie |
|---|---|---|---|---|
| Match (jour) | défi entre potes, joyeux et tendu | soleil haut et chaud (`FFE6C2`, 1,9), ciel bleu, brume chaude `FFE2C4`, ACES, saturation 1,15 | ensoleillé, urbain, aéré, vertical | haute |
| Match (soir, variante de carte) | même défi, plus dramatique | soleil rasant orange, ciel dégradé vers le violet, néons d'enseigne allumés | doré, contrasté, néon | haute |
| Fin de match | célébration du gagnant, respect du perdant | même lumière que le match ; podium éclairé, le reste baisse de 30 % | podium, confettis aux couleurs du vainqueur | retombée |
| Menus | invitation, calme | toit de Croix Néon en fond (`MenuBackdrop`), flou léger | posé, accueillant | basse |
| Tutoriel | confiance | lumière de match, mannequins neutres gris | clair, sans danger | moyenne |

Chaque état se distingue du suivant par l'énergie et le cadrage, pas par une autre palette.

## 3. Shape Language

- **Personnages** : triangle inversé (épaules larges, taille fine), mains ×1,3 à ×1,5 au plus et pieds ×1,4 (fiche §1). Une grande forme par héros, qui suffit à le reconnaître en silhouette noire à 20 m : Riko (capuche XXL), Bo (carrure costaude, épaulettes), Nova (queue de cheval haute), Kai (casquette à l'envers et dreads). Rien de fin ni de filaire (aliasing en VR).
- **Danger = rond et lumineux** : balles, balles spéciales, faisceau vertical des distributeurs. Aucun élément de décor rond et lumineux à hauteur de jeu.
- **Décor = rectangles posés** : toits, acrotères, conteneurs, cheminées, caisses ; arêtes chanfreinées (2 à 5 cm), jamais vives.
- **Repères à grande échelle** : chaque carte a une silhouette unique visible à plus de 30 m (château d'eau, enseigne, grue). Ils servent à s'orienter.
- **Interface** : formes de BD inclinées (bandeaux à 6–8°), pastilles rondes pour les équipes, cœurs pleins. Pas de coins arrondis « appli ».

## 4. Color System

| Rôle | Hex | Règle |
|---|---|---|
| Balle (cœur) | `#D6FF2E` | **réservée** : aucun autre objet ne porte ce vert-jaune, ni le jaune pur `#F2C230`. |
| Équipe 1 (orange) | `#FF6A1A` | personnages, traînée, étiquette, interface de l'équipe. |
| Équipe 2 (bleu clair) | `#6CC8FF` | idem. Vestes entre `#4FA8F0` et `#6CC8FF` ; sur le sol de toit, le contraste de luminance n'est que de 2,4 à 3,3 (l'orange : 2,1), la lecture vient de la teinte saturée et du contour anthracite, pas de la luminosité ; jamais de bleu marine pour une équipe. |
| Distributeurs | `#5FE0C8` (turquoise) | faisceau et socle des distributeurs seulement. |
| Neutre clair | `#F2F4F8` | lignes, texte, gants des héros, bandes de danger. |
| Anthracite | `#101830` | contours, panneaux d'interface, ombres d'interface. |
| Sol de toit | luminance ≤ 0,2 (par exemple `#5E6178`) | contraste balle/sol ≥ 5. |

- **Sémantique** : orange et bleu = équipes ; vert-jaune = balle ; turquoise = « ici tu prends une balle » ; blanc = règle ou limite (bord de zone, ligne au sol).
- **Décor** : couleurs posées et désaturées (brique, béton, bois, tôle peinte). Les néons d'enseigne sont blanc chaud, rose ou turquoise, jamais dans la teinte d'une équipe, jamais vert-jaune. Le jaune de chantier passe en blanc et anthracite.
- **Température** : décor chaud au soleil, ombres teintées bleu-violet, jamais noires.
- **Daltonisme** : orange et bleu se distinguent par la teinte et la luminance (protanopie et deutéranopie). Chaque signal d'équipe a un doublon de forme : disque blanc sur le torse (équipe 1), losange (équipe 2), étiquette au-dessus de la tête. La charge de la balle se lit aussi par la taille du halo et le son, pas seulement par la couleur.

À appliquer (constats ouverts de la revue du 22/10) : bandes de danger jaunes et `neon_jaune` du toit (fil Assets 3D), veste de Nova `#2F7DE1` et liseré cyan `#7FE8FF` des abris (fils Scenario et Assets 3D).

## 5. Character Design Direction

- **Archétype** : héros stylisés « sport urbain », 1,75 m, 6,5 têtes, streetwear à grandes zones de couleur (bomber, hoodie, survêt à bandes, sneakers montantes). Nos personnages, nos crews, nos logos : rien de reconnaissable de Knockout City.
- **Quatre héros jouables dans les deux équipes** (Riko, Bo, Nova, Kai). La couleur d'équipe est portée par un masque `team_mask` que le shader teinte (60 % équipe, 30 % neutre, 10 % accent). Pas de modèle par équipe : en 4v4, pas de clones.
- **Gants blancs à manchette d'équipe** : la main est le signal le plus lu du jeu (attrape, parade, passe), en VR comme au PC.
- **Visages** : grands yeux, formes nettes, expression par la forme ; trois expressions au minimum (neutre, cri, sourire).
- **Rig** : squelette humanoïde Godot (`SkeletonProfileHumanoid`, environ 55 os, clavicules, trois vertèbres, deux doigts au minimum) pour recibler des animations du marché. Le rig actuel à 16 os est un prototype.
- **Héros de référence** : un héros fait par un modeleur ou par une génération 3D payante, que les autres copient pour le niveau de finition. Décision de budget pour jules.
- **Détail à distance** : ce qui doit rester lisible à 20 m, ce sont la silhouette, la couleur d'équipe, les gants et la balle tenue. Le reste peut disparaître au LOD2.

## 6. Environment Design Language

- **Monde** : toits d'une ville rétro-moderne fictive, fin d'après-midi par défaut. Chaque carte = un quartier (toits et enseignes, chantier, ruelle industrielle, cour d'immeuble).
- **Matériaux** : PBR stylisé avec `kc_pbr` (rugosité dans l'alpha des couleurs de sommet), aplats, AO cuite dans les couleurs de sommet, pas de bruit photo. Le toon avec contour est abandonné (décision du 21/10).
- **Densité de props** : dense sur le pourtour (hors zone de jeu), clairsemée sur les plaques. Sur une plaque, seuls comptent les couverts et les distributeurs : aucun prop décoratif à moins de 1,5 m d'un bord de zone ni dans une bande neutre.
- **Zones VR** : la bande neutre entre zones adverses (au moins 1,5 m) est un sol neutre sans prop. Le bord d'une zone VR est marqué au sol par une ligne blanche discrète (`LigneDeBord`), plus un voile translucide à la couleur de l'équipe quand on s'en approche.
- **Récit** : enseignes, graffitis et objets racontent un quartier où l'on joue au dodgeball sur les toits (paniers, filets, bancs, glacières), sans texte lisible qui ferait une marque.

## 7. UI/HUD Visual Direction

- **PC** : HUD écran façon Knockout City (cœurs et pseudo en bas à gauche, chrono, objectif et score en bas à droite, ronds de manches, pastilles d'équipe). Réticule de verrouillage à la couleur de l'équipe de la cible.
- **VR** : HUD diégétique seulement. Cœurs au poignet gauche, panneau de score mural, fin de match sur un podium dans le casque. Aucun texte collé au regard.
- **Typographie** : linéale grasse et condensée pour les titres (ton BD, pas la police de Knockout City), linéale lisible pour le texte courant. Taille minimale 24 px à l'écran en 1080p ; en VR, 1° d'angle visuel au moins à la distance de lecture.
- **Icônes** : pleines, contour anthracite de 2 px, deux couleurs au plus.
- **Animation** : arrivées en 0,15–0,25 s, rebond léger, jamais de clignotement au-dessus de 3 Hz (option « Réduire les flashs » respectée partout).
- **Conflit tranché** : les bandeaux inclinés de BD sont gardés pour les annonces, mais les chiffres (score, chrono) restent droits pour se lire d'un coup d'œil.

## 8. Asset Standards

| Catégorie | PC (LOD0) | Quest (LOD0) | Remarques |
|---|---|---|---|
| Héros | 12 000 à 15 000 triangles | ≤ 8 000 | LOD1 6 000, LOD2 2 500 ; 1 matériau, 2 au plus |
| Textures héros | atlas 2048 (albédo, normal, ORM, masque d'équipe) | atlas 1024, ASTC | ou couleurs de sommet + normal cuite tant que le héros de référence n'existe pas |
| Props | ≤ 800 triangles (cône ≤ 300) | idem | 1 primitive, 1 matériau, AO cuite dans les sommets |
| Décor de carte (toit et ville) | ≤ 60 000 triangles visibles | ≤ 40 000 | primitives fusionnées par matériau, matériaux en cache |
| Scène entière visible | < 100 000 triangles | < 100 000 | mesuré avec `--perf-log` |

- **Formats** : glTF 2.0 binaire (`.glb`), +Y en haut, 1 unité = 1 m, animations en actions séparées, sans root motion.
- **Nommage** : préfixe de catégorie, `char_` (héros), `env_` (décor de carte), `prop_` (accessoires), `fx_` (effets), en minuscules et sans accents : `char_riko_rig.glb`, `env_toit.glb`, `prop_cone.glb`. À appliquer aux nouveaux fichiers ; les anciens sont renommés à l'occasion.
- **Variante Quest (même DA, effets en moins)** : pas d'ombre temps réel, ni SSAO, ni glow (`pcProfile = false` dans `ArenaBuilder`). Elles sont remplacées par l'AO cuite dans les couleurs de sommet du toit, de la ville et des props, et, **cible pas encore en place**, par un disque d'ombre sous chaque personnage (0,5 m, alpha 0,35). MSAA 4x en VR (`XrBootstrap`). Même ciel, même brume, même palette, mêmes décors que le PC.
- **Moteur** : Godot 4.3, rendu Mobile, 72 i/s sur Quest (13,9 ms par image), 60 i/s sur PC.
- **Arbitrage enregistré** : l'art voudrait des ombres réelles sur Quest, la cible des 72 i/s ne le permet pas ; on garde l'AO cuite et le disque d'ombre (à faire).

## 9. Reference Direction

| Référence | Ce qu'on prend | Ce qu'on évite |
|---|---|---|
| Knockout City (Velan, 2021) | lisibilité : lueur = danger, faisceau vertical = objectif, une couleur par équipe, toits de jour, texte de BD | nom, logo, personnages, police, gants et patins néon, balle rouge/rose |
| Splatoon (Nintendo) | couleurs d'équipe franches sur un décor posé, personnages streetwear lisibles | encre et peinture au sol, ton enfantin |
| Jet Set Radio Future (Sega) | toits urbains, enseignes, graffitis, énergie de rue | cel-shading à contour épais (décision du 21/10 : PBR stylisé) |
| Spider-Verse (films Sony) | bandeaux de texte BD et cadrages dynamiques pour les annonces et la fin de match | trame et décalage chromatique (gênants en VR) |
| Rocket League (Psyonix) | fin de match et podium lisibles, compteurs droits | interface chargée en menus |
