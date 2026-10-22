# Studio Claude : mode d'emploi du kit dans dogeballVR

Ce dépôt embarque **Claude Code Game Studios** (CCGS, v1.2.0, licence MIT,
© 2026 Donchitos, texte dans `.claude/LICENSE-CCGS`,
github.com/Donchitos/Claude-Code-Game-Studios) : 49 agents spécialisés
(directeurs, leads, programmeurs, spécialistes Godot), 74 skills (`/commande`)
et des hooks. Ce document dit comment chaque session Claude s'en sert pour
faire avancer le jeu.

## 1. Quand le kit est actif

Claude Code charge `CLAUDE.md`, `.claude/agents/`, `.claude/skills/` et les hooks
**au démarrage d'une session ouverte à la racine du dépôt**. Une session qui part
de `main` n'a donc le kit que lorsque la PR d'installation (#23) est fusionnée.
Tant qu'elle ne l'est pas, une session peut lire les fichiers du kit sur la
branche de cette PR (`git show origin/<branche de la PR #23>:<chemin>`) et
suivre leurs consignes à la main. Il ne faut pas les committer dans une PR de
travail : agents, skills et hooks ne s'activent qu'au démarrage d'une session
sur une branche qui les contient.

Au démarrage, le hook `session-start.sh` affiche la branche, les derniers
commits, le mode de revue et l'état de session
(`production/session-state/active.md`).

## 2. Réglages (project.yaml)

| Clé | Valeur | Effet |
|---|---|---|
| `engine` | Godot 4.3, C#, Mobile, Godot Physics | Les agents écrivent du C# Godot 4.3, sans API 4.4 ou plus récente |
| `engine.code_root` | `scripts` | Ajout propre à dogeballVR : les hooks et scripts du kit cherchent le code dans `scripts/`. Les skills écrivent encore `src/…` dans leurs exemples : il faut lire `scripts/…` (c'est dit dans `code-root-resolution.md` et `directory-structure.md`) |
| `modes.automation` | `autonomous` | Pas de « May I write… ? ». Le kit prend l'option recommandée et la note |
| `modes.automation_always_ask` | `file_deletions` | La seule question qui reste : avant de supprimer des fichiers |
| `modes.rigor` | `standard` | Revues « lean » par les directeurs, docs équilibrées, QA standard |
| `project.stage` | `Production` | Le jeu tourne déjà, même si les documents du kit manquent. Seul jules lance `/gate-check` |
| `commands.*` | build `dotnet build`, test = partie headless de 150 s | Mêmes vérifications que la CI |
| `updates.check` | `false` | Pas d'appel à l'API GitHub du kit à chaque session |
| `testing.strict.*` | `false` | Pas de Godot local : une CI verte, citée dans la PR, sert de preuve |

Pour changer un réglage : `/settings clé=valeur`, ou modifier `project.yaml`
dans une PR.

## 3. Règles de travail (rappel de CLAUDE.md)

- Une branche et une PR par travail, CI verte. **Jamais de push ni de fusion
  dans `main`** : jules relit et fusionne.
- Autonomie : on décide et on continue. Les choix sont notés dans la
  description de la PR (section « Choix faits »). Le journal du kit
  (`production/session-logs/`) disparaît avec le conteneur. On s'arrête
  seulement avant une suppression ou une action hors du dépôt.
- Le travail demandé passe d'abord. Les documents du plan d'adoption ne
  s'écrivent que si c'est le sujet du fil, ou en fin de PR pour le système
  touché.
- Le hook `validate-push.sh` refuse tout push sur `main` ou `master`. En
  revanche, aucun outil n'empêche de fusionner une PR : seule la règle
  l'interdit. Une protection de branche sur GitHub (Settings › Branches) le
  rendrait sûr.
- Pas de Godot ni de .NET dans les conteneurs cloud. Le build, les tests et le
  smoke check passent par la CI : on pousse, puis on lit son résultat.
- Revue en boucle avant livraison : au moins un agent indépendant note le
  travail sur 10. On corrige jusqu'à 8/10, au plus 5 tours.
- Code et commentaires en français, Godot 4.3, autorité unique de l'hôte,
  cartes en JSON, aucune mécanique liée à une position exacte sur la piste EVA.

## 4. Quel outil pour quel travail

Les skills se lancent avec l'outil Skill (`/nom`). Les agents se lancent avec
l'outil Agent (`subagent_type: "nom-de-l-agent"`).

| Besoin | Skills | Agents à appeler |
|---|---|---|
| Où en est le projet, quoi faire ensuite | `/help`, `/project-stage-detect`, `/sprint-status` | producer |
| Concevoir ou modifier une mécanique | `/quick-design` (petit), `/design-system` (GDD complet), `/balance-check` | game-designer, systems-designer |
| Documenter ce qui existe déjà dans le code | `/reverse-document` | game-designer, lead-programmer |
| Décision d'architecture | `/architecture-decision` (ADR dans `docs/architecture/`) | technical-director, lead-programmer |
| Découper le travail | `/create-epics`, `/create-stories`, `/sprint-plan`, `/estimate` | producer |
| Coder une story | `.claude/skills/dev-story/SKILL.md` (voir § 5) | godot-csharp-specialist, gameplay-programmer, network-programmer |
| Relire du code | `/code-review`, `/architecture-review` | lead-programmer, godot-csharp-specialist |
| Combat, lancer, attraper | `/team-combat` | gameplay-programmer, game-designer |
| Cartes et niveaux | `/team-level` | level-designer |
| Interfaces, menus, HUD | `/team-ui`, `/ux-design`, `/ux-review` | ui-programmer, ux-designer, art-director |
| Direction artistique | `/art-bible`, `/asset-spec`, `/asset-audit` | art-director, technical-artist |
| Son | `/team-audio` | audio-director, sound-designer |
| Game feel, finitions | `/team-polish`, `/perf-profile` | technical-artist, performance-analyst |
| Tests et QA | `/qa-plan`, `/smoke-check`, `/regression-suite`, `/bug-report`, `/bug-triage` | qa-lead, qa-tester |
| Jeu en ligne | `/security-audit` | network-programmer, security-engineer |
| Sortie Steam | `/release-checklist`, `/team-release`, `/patch-notes`, `/changelog` | release-manager, devops-engineer |
| Vérifier une étape du projet | `/gate-check` (toujours soumis à jules : le fil prépare le rapport) | creative-director, technical-director, producer |

La liste complète est dans `.claude/docs/skills-reference.md` et
`.claude/docs/agent-roster.md`. Le parcours type est décrit dans
`docs/WORKFLOW-GUIDE.md`.

## 5. Skills réservées à l'humain

Cinq skills ne peuvent pas être lancées par un modèle : elles ne démarrent que
si un humain tape la commande. Ce sont `/dev-story`, `/story-done`, `/hotfix`,
`/day-one-patch` et `/launch-checklist`. Dans un fil autonome, il faut lire
`.claude/skills/<nom>/SKILL.md` et suivre ses étapes à la main. Ses lignes
`` !`bash "${CLAUDE_SKILL_DIR}/…" resolve_config …` `` ne s'exécutent pas dans ce
cas : il faut lancer soi-même `bash .claude/hooks/yaml-helper.sh resolve_config`. jules peut aussi
taper la commande lui-même dans une session Claude Code.

## 6. Premier chantier : rattraper les documents

Le kit attend des GDD, des ADR et des stories. Le jeu les a précédés. Le plan
de rattrapage est dans `docs/adoption-plan-2026-10-22.md`, produit par l'audit
`/adopt` à l'installation. Il faut le suivre dans l'ordre : chaque étape dit
quelle skill utiliser et quel document existant convertir. Une fois les docs en
place, les fils travaillent par story (`/create-stories` → dev-story →
`/code-review` → story-done).

Tant qu'il n'y a ni story ni ADR, dev-story s'arrête : elle exige un ADR
accepté. Pour un travail demandé directement (« ajoute X ») :

1. `/quick-design` si une règle du jeu change (sinon rien).
2. Faire coder l'agent spécialiste : `godot-csharp-specialist`, plus
   `network-programmer` si `scripts/Net` est touché.
3. Faire relire avec `/code-review`, puis par un agent indépendant (note ≥ 8/10).
4. Pousser, attendre la CI verte et la citer dans la PR, avec une section
   « Choix faits ».

## 7. Ce qui a été adapté dans le kit

Toutes les modifications portent la mention `dogeballVR`
(`grep -rn dogeballVR .claude`). Il faut les refaire après une mise à jour du kit.

- `.claude/hooks/yaml-helper.sh`, `.claude/scripts/project-state.sh`,
  `write-guard.sh`, `asset-scan.sh`, `project-coherence.sh` : lecture de
  `engine.code_root`. Le kit codait `src/` en dur pour Godot.
- `.claude/hooks/detect-gaps.sh` : `scripts/` compte comme dossier de code.
- `.claude/hooks/validate-push.sh` : refuse un push sur `main` ou `master`,
  `gh pr merge` et `git push --mirror` ou `--all`.
- `.claude/hooks/validate-commit.sh` et `validate-assets.sh` : les cartes
  `maps/*.json` sont vérifiées comme données (un JSON invalide bloque).
- `.claude/docs/coding-standards.md` : commits en français, en texte libre
  comme l'historique (le kit imposait les Conventional Commits).
- `.claude/agents/*.md` (les 49) : un encadré en tête du « Collaboration
  Protocol » qui remplace le « demander avant d'écrire » par le mode autonome.
- `.claude/rules/*.md` : les règles de code s'appliquent aussi à nos dossiers
  (`scripts/Net/**` pour le réseau, `scripts/Gameplay/**` pour le gameplay,
  `maps/*.json` pour les données, etc.).
- `.claude/docs/technical-preferences.md` : rempli pour Godot 4.3 C#, avec les
  motifs interdits du projet.
- `docs/engine-reference/godot/VERSION.md` : épinglé sur 4.3. Les autres fichiers
  de ce dossier décrivent 4.4 → 4.7 et ne servent que pour une future mise à
  jour du moteur.
- `CLAUDE.md` : réécrit pour notre pile et notre protocole autonome.
- `.gitignore` : on ajoute seulement les lignes du kit pour l'état local. Celui
  du kit ignorait `*.csproj`, `*.sln` et `export_presets.cfg`, dont nous avons
  besoin.

Non repris : `studio-tests/` (tests du kit lui-même), `CHANGELOG.md`, `README.md`
et `.github/` du kit. `src/` est inutile chez nous.

`.claude/settings.json` est celui du kit, sans modification. Il ajoute des
interdictions (`rm -rf`, `git push --force`, `git reset --hard`,
`git clean -f`, `sudo`, lecture des `.env`), une barre d'état et les hooks.
Les hooks bloquent quatre cas :
- un commit avec un JSON de données invalide sous `assets/**/data/` ou dans
  `maps/` ;
- la lecture d'un seul tenant d'un document de design de plus de 40 Ko (il
  faut alors lire par plages) ;
- un push sur `main` ou `master` ;
- `gh pr merge` et `git push --mirror` ou `--all`.

Les outils GitHub MCP (`merge_pull_request`, `enable_pr_auto_merge`, et
`push_files` ou `create_or_update_file` vers `main`) échappent aux hooks : seule
la règle les interdit.

## 8. Mettre le kit à jour

1. Cloner la nouvelle version du kit à côté.
2. Remplacer `.claude/` par le nouveau, puis refaire les adaptations du § 7.
   Repérer les anciennes avec `grep -rn dogeballVR .claude` sur la branche
   actuelle.
3. Fusionner `project.yaml` à la main, sans l'écraser.
4. Faire passer la CI, puis ouvrir une PR.
