# Plan d'adoption du kit (CCGS) — dogeballVR

> **Généré** : 2026-10-22 (`/adopt full`, mode autonome, rigor standard)
> **Phase** : Production (`project.yaml`) — jeu jouable, mais sans aucun document du kit
> **Moteur** : Godot 4.3 .NET (C#), `engine.code_root: scripts` (pas `src/`)
> **Config résolue** : review_mode `lean`, workflow `standard`, automation `autonomous` (seule question : `file_deletions`)

Objectif : que chaque prochaine session fasse avancer le jeu avec les skills du kit.
Travailler dans l'ordre, cocher au fur et à mesure, relancer `/adopt` après l'étape 3.
Sources hors dépôt (lecture seule, à convertir, jamais à copier) : `/mnt/project-files/masterclasse/` (noté `MC/` ci-dessous).
Si ce dossier n'est pas accessible dans la session, partir de `README.md`, `docs/ARCHITECTURE.md`, `docs/CHOIX-REGLES.md` et du code, puis le signaler dans la PR.
Règle de priorité : le travail demandé au fil passe avant ce plan (voir `CLAUDE.md`). `/gate-check` est toujours soumis à jules.
Travail en parallèle : avant de prendre une étape, lister les PR ouvertes du dépôt (outil GitHub `list_pull_requests`) ; prendre la première case qu'aucune PR ne couvre, une étape par PR, et dire dans le titre de la PR quelle étape elle traite. Si l'étape dont elle dépend est encore dans une PR ouverte, partir de cette branche (y fusionner la branche de la PR) ou prendre une étape indépendante (1.3, 1.4, 4.3).

## Résumé de l'audit

| Élément | État |
|---|---|
| Config (`project.yaml`, migration v1) | OK, rien à migrer (`migrate-v1-config.sh --dry-run` : « Nothing to do ») |
| Référence moteur `docs/engine-reference/godot/` | OK (pin 4.3) |
| Concept, piliers, systems-index, GDD | 0 (design/gdd/ n'existe pas) |
| ADR | 0 |
| `tr-registry.yaml` | présent mais `requirements: []` |
| Control manifest, epics, stories, sprints, milestones | 0 |
| Docs hors format | `README.md`, `docs/ARCHITECTURE.md`, `docs/CHOIX-REGLES.md` (conservés, source des GDD) |

Écarts : **BLOCKING 0** — **HIGH 6** (concept, systems-index, GDD, ADR, manifest, stories) — **MEDIUM 5** — **LOW 3**.
Aucun artefact existant n'est mal formaté : tout est à créer, rien à corriger.

---

## Étape 1 : Fondations de conception (HIGH, ~1 session)

- [ ] **1.1 Concept du jeu** — `/brainstorm` en lui donnant `MC/04-concept-dodgeball-vr.md`, `MC/knockout-city/02-adaptation-vr-eva.md` et `README.md` comme matière (ne pas repartir de zéro : valider, structurer).
  Agents : `creative-director`, `game-designer`. Produit : `design/gdd/game-concept.md`. Passer le nom « BALL BRAWL VR » (provisoire) en décision ouverte.
- [ ] **1.2 Piliers** — inclus dans 1.1 (sortie `design/gdd/game-pillars.md`). Pistes tirées des sources : nerveux, lisible en VR sans joystick, attraper/renvoyer, original (pas une copie de Knockout City).
- [ ] **1.3 Direction artistique** — `/art-bible` à partir de `MC/da/01-direction-artistique.md` et `MC/da/02-revue-da.md` (les `98-*` et `99-*` sont des archives : ne pas les suivre). Code existant à aligner : `scripts/Visual/Palette.cs`, `Materials.cs`. Agent : `art-director`. Produit : `design/art/art-bible.md`.
- [ ] **1.4 Accessibilité** — `design/accessibility-requirements.md` (modèle `.claude/docs/templates/accessibility-requirements.md`), agent `accessibility-specialist` : inconfort VR, lisibilité des balles chargées, daltonisme.

## Étape 2 : Systèmes et GDD recopiés depuis le code (HIGH, 1 à 2 sessions)

- [ ] **2.1 Index des systèmes** — `/map-systems` sur concept + code. Systèmes attendus : Balle et charge (`Ball.cs`), Mains VR / lancer (`HandThrower.cs`), Joueur PC (`DesktopPlayer.cs`), Règles de partie (`MatchRules.cs`), Zones (`ZoneManager.cs`), Maps JSON (`ArenaBuilder.cs`, `MapData.cs`), Lift et plateaux (`LiftController.cs`, `PlateSwap.cs`), Bots (`Bot.cs`), Réseau hôte-autorité (`scripts/Net/*`), Avatars et visuels (`scripts/Visual/*`), HUD (`DesktopHud.cs`), Bootstrap XR (`scripts/Xr/XrBootstrap.cs`). Produit : `design/gdd/systems-index.md` (statuts exacts, sans parenthèses).
- [ ] **2.2 GDD par système, d'abord le noyau** — `/reverse-document design scripts/Rules` (puis `scripts/Gameplay`, `scripts/Net`, `scripts/Zones`, `scripts/Maps`) : en standard, 5 sections (+ Formulas). **Passer `scripts/…` en argument** : l'exemple `src/…` de la skill est obsolète ici.
  Sources : `docs/CHOIX-REGLES.md` (valeurs et écarts de règles), `MC/knockout-city/04-gameplay-complet.md` (comportements de référence), `MC/level-design/01-level-design-vr-eva.md`, `MC/level-design/03-game-design-knockout-pc-vr.md`. Produit : `design/gdd/<systeme>.md`, statut `Designed`.
  Ordre : MatchRules → Ball/charge/attraper → Réseau → Zones+Maps → Lift → Bots → Joueurs (PC, VR).
- [ ] **2.3 Cohérence** — `/review-all-gdds`, puis `/consistency-check`. Agents : `game-designer`, `systems-designer`. Produit : `design/gdd/gdd-cross-review-*.md` ; complète `design/registry/entities.yaml`.
- [ ] **2.4 Points à trancher (laissés en « Open Questions » des GDD)** : compensation de latence des prises, validation de la direction des lancers, bots trop peu létaux (7 à 14 % de touches), traversée des couverts, bonus de hauteur, portée jamais mesurée sur casque.

## Étape 3 : Architecture et infrastructure (HIGH, ~1 session)

Ordre imposé : ADR → `/architecture-review` → manifest → sprint.

- [ ] **3.1 ADR** — `/architecture-decision` (une par décision, chacune avec Status, Engine Compatibility Godot 4.3, GDD Requirements Addressed). Agent : `technical-director`, avec `godot-csharp-specialist` et `network-programmer`.
  Sources : `docs/ARCHITECTURE.md`, `README.md` (section réseau), `MC/05-architecture-evolutive-eva.md`.
  ADR à écrire : 0001 hôte autorité unique (ENet), 0002 maps en JSON et raisonnement par zones, 0003 `MatchRules` en C# pur testable, 0004 rendu Mobile + Godot Physics 4.3 (pas de Jolt), 0005 pas de déplacement au joystick en VR, 0006 bots sur le chemin des joueurs, 0007 tests = parties simulées en CI.
  Produit : `docs/architecture/adr-NNNN-*.md`.
- [ ] **3.2 Vue d'ensemble** — `/create-architecture` (reprend `docs/ARCHITECTURE.md` en l'élargissant). Produit : `docs/architecture/architecture.md`.
- [ ] **3.3 Registre d'exigences** — `/architecture-review` : remplit `docs/architecture/tr-registry.yaml` (aujourd'hui vide) et `requirements-traceability.md`.
- [ ] **3.4 Manifeste de contrôle** — `/create-control-manifest`. Produit : `docs/architecture/control-manifest.md` (règles : C# 4.3, `scripts/`, PascalCase, interdits Godot 4.4+).
- [ ] **3.5 Stage** — ne pas lancer `/gate-check` dans un fil (la skill attend toujours un humain). Écrire dans la PR le rapport de ce qui manque encore pour chaque étape (voir `.claude/docs/effects-map.md` § project.stage) ; jules lance `/gate-check` lui-même. Ne pas modifier `project.yaml` à la main.

Relancer `/adopt` ici : BLOCKING et HIGH doivent être à zéro.

## Étape 4 : Interface, tests, planification (MEDIUM, 1 à 2 sessions)

- [ ] **4.1 HUD et menus (PC puis VR)** — `/ux-design` à partir de `MC/knockout-city/05-interfaces.md`, `MC/knockout-city/01-analyse-visuelle.md`, l'art bible et `scripts/Gameplay/DesktopHud.cs`. Agents : `ux-designer`, `ui-programmer`. Produit : `design/ux/*.md` ; ensuite `/ux-review`.
- [ ] **4.2 Parcours joueur** — `design/player-journey.md` (lobby, partie, résultat ; hôte/client ; VR et PC).
- [ ] **4.3 Tests** — ne pas lancer `/test-setup` (il génère du gdUnit4 en GDScript). Créer un projet de test C# séparé (`tests/DogeballVR.Tests/*.csproj`, xUnit ou NUnit, net6.0) pour `MatchRules` et les règles pures, l'exclure du csproj du jeu (`<Compile Remove="tests/**" />`) et le lancer depuis `.github/workflows/build.yml` ; documenter `--net-selftest` et `--pc-autoplay` dans `production/qa/`. Agent : `qa-lead`, `godot-csharp-specialist`.
- [ ] **4.4 Épopées et stories** — `/create-epics` puis `/create-stories` (une épopée par système de l'index) ; `/story-readiness` avant de coder. Produit : `production/epics/<epopee>/EPIC.md`, `story-NNN-*.md`.
- [ ] **4.5 Sprint** — `/sprint-plan` (1 sprint = stories prioritaires ci-dessous), puis `/sprint-status`. Produit : `production/sprints/`, `production/sprint-status.yaml`.
  Priorités déjà connues (README « État » et CHOIX-REGLES) : essai sur casque Quest (jamais fait), réglage du lancer/attrapé, latence réseau, bots plus létaux, interpolation des snapshots.
- [ ] **4.6 Jalon** — `/milestone-review` avec un jalon « Démo EVA 2v2 sur casque » (`production/milestones/`). Source : `MC/00-cadrage-et-plan.md`.

## Étape 5 : Boucle de travail courante (à répéter à chaque session)

1. `/help` ou `/sprint-status` pour savoir où on en est.
2. Une story : `/story-readiness` → `/dev-story` (agents `godot-csharp-specialist`, `gameplay-programmer`, `network-programmer`) → `/code-review` → `/story-done`.
   `/dev-story` et `/story-done` ne se lancent que si un humain tape la commande : en session autonome, lire `.claude/skills/dev-story/SKILL.md` (puis `story-done`) et suivre leurs étapes (voir `docs/STUDIO-CLAUDE.md` § 5).
3. Petit réglage de valeur : `/quick-design` + `/balance-check`. Changement de règle : `/propagate-design-change`.
4. Avant de livrer : pousser et attendre la CI verte (pas de Godot ni de .NET dans les conteneurs) ; jules fusionne.
5. Après un essai : `/playtest-report` (sur casque, noter inconfort et portée réelle) ; `/bug-report` / `/bug-triage`.
6. Fin de sprint : `/retrospective`, `/scope-check`.

## Étape 6 : Vers Steam et Quest (LOW, plus tard)

- [ ] **6.1** `/perf-profile` sur Quest (72 i/s, 13,9 ms ; allocations par snapshot, matériaux transparents sur les balles) — `performance-analyst`.
- [ ] **6.2** `/security-audit` du réseau (tolérance de latence large, anti-triche, hôte-autorité) — `security-engineer`.
- [ ] **6.3** `/vertical-slice` puis rapport de gate pour jules (il lance `/gate-check`) ; `/release-checklist` et `/launch-checklist` en s'appuyant sur `MC/plan/plan-steam.md` et le gabarit `.claude/docs/templates/certification-steam.md` ; `/localize` si le jeu sort en plusieurs langues.

---

## Notes d'adoption

- Les documents existants ne sont pas réécrits : `README.md`, `ARCHITECTURE.md` et `CHOIX-REGLES.md` restent la mémoire du projet ; les GDD et ADR les citent comme sources.
- Les études `MC/` restent hors dépôt. Chaque document produit les cite par leur chemin, sans les recopier.
- Pas de stories à migrer (aucune n'existe) : toutes seront créées avec TR-ID, ADR et Manifest Version.
- Review mode : `lean` (vient de `modes.rigor: standard`). Pour changer de profondeur, modifier `modes.rigor` ou `/settings --local modes.review_mode=<full|lean|solo>` ; ne pas l'écrire dans `project.yaml`.
- Décisions prises en mode autonome : workflow `standard` conservé (5 sections de GDD) ; aucune migration v1 (déjà faite) ; ordre ADR → registre → manifest respecté ; stage Production gardé tel quel jusqu'au verdict de `/gate-check`.

## Réexécution

Relancer `/adopt` après l'étape 3 pour vérifier qu'il ne reste ni écart BLOCKING ni HIGH.
