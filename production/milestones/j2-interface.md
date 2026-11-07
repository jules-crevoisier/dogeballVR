# Milestone: J2 Interface

## Overview

- **Target Date**: 24/11/2026
- **Type**: Alpha
- **Duration**: 1,5 semaine
- **Number of Sprints**: 2

## Milestone Goal

Une interface entièrement neuve et cohérente, du premier écran au tableau des scores : menu principal avec une
arène en fond, choix de carte illustré, salon en ligne, HUD de match, fin de partie et pause. Elle reprend
l'infrastructure de la PR #33 (police Rubik, `ScorePanel`, `MenuHero`, `MapPlan`) mais pas forcément sa direction
artistique, que jules n'aime pas en l'état.

## Success Criteria

- [ ] Direction artistique de l'interface validée sur planches (captures du vrai jeu) avant intégration
- [ ] Tous les écrans du PC refaits : titre, menu, solo, en ligne, salon, options, HUD, pause, fin de manche, fin de partie
- [ ] Navigation complète à la souris et à la manette
- [x] Textes FR et EN (aucune chaîne en dur hors tables de traduction) : `assets/i18n/textes.csv`, langue dans les options, contrôle en CI (`tools/i18n/extract.py --check`) ; les journaux restent en français
- [ ] Revue indépendante ≥ 8/10 sur chaque écran
- [ ] All S1 and S2 bugs resolved
- [ ] Performance within budget on target hardware
- [ ] Build stable for 2 consecutive days

## Feature List

### Must Ship (Milestone Fails Without These)

| Feature | Design Doc | Owner | Sprint Target | Status |
|---------|-----------|-------|--------------|--------|
| Charte de l'interface (couleurs, typo, composants) | `design/ux/` | ux-designer, art-director | S1 | À faire |
| Menu principal avec arène vivante en fond | `design/ux/` | ui-programmer | S1 | À faire |
| HUD de match (balles, cœurs, score, chrono, indicateurs hors champ) | `design/ux/hud.md` | ui-programmer | S1 | À faire |
| Écrans de fin de manche et de partie | `design/ux/` | ui-programmer | S2 | À faire |
| Salon en ligne refait | `design/ux/` | ui-programmer | S2 | À faire |

### Should Ship (Planned but Cuttable)

| Feature | Design Doc | Owner | Sprint Target | Cut Impact | Status |
|---------|-----------|-------|--------------|-----------|--------|
| Transitions animées entre écrans | `design/ux/` | ui-programmer | S2 | Interface plus sèche | À faire |
| Panneaux VR alignés sur la nouvelle charte | `design/ux/` | ui-programmer | S2 | VR garde l'ancienne interface | À faire |

### Stretch Goals (Only if Ahead of Schedule)

| Feature | Design Doc | Owner | Value Add |
|---------|-----------|-------|----------|
| Écran de personnalisation de l'avatar | à écrire | ux-designer | Rétention |

## Quality Gates

| Gate | Threshold | Measurement Method |
|------|-----------|-------------------|
| Crash rate | 0 exception | Jobs CI « lobby » et « solo » |
| Frame rate | > 60 i/s au menu | Mesure sur le PC de jules |
| Load time | < 3 s du lancement au menu | Journal de démarrage |
| Critical bugs | 0 open S1 | PR |
| High-severity bugs | 0 écran sans retour possible | Parcours manuel et CI |
| Test coverage | Captures de chaque écran dans `production/qa/evidence/` | Outil `--shots` |

## Risk Register

| Risk | Probability | Impact | Mitigation | Owner | Status |
|------|------------|--------|-----------|-------|--------|
| Direction artistique encore rejetée | Moyenne | Élevé | Planches en captures réelles avant d'intégrer | art-director | Ouvert |
| Conflits avec la PR #33 non fusionnée | Élevée | Moyen | Partir de #33 si jules la fusionne, sinon en reprendre les briques | lead-programmer | Ouvert |

## Dependencies

### Internal Dependencies

| Feature | Depends On | Owner of Dependency | Status |
|---------|-----------|-------------------|--------|
| Fond du menu | Arènes de J1 | level-designer | En cours |
| Briques d'interface | PR #33 | jules (fusion) | En attente |

### External Dependencies

| Dependency | Provider | Status | Risk if Delayed |
|-----------|---------|--------|----------------|
| Police Rubik (OFL) | Google Fonts | Dans le dépôt via #33 | Faible |

## Review Schedule

| Date | Review Type | Attendees |
|------|-----------|-----------|
| 17/11/2026 | Planches de la charte | jules |
| 20/11/2026 | Mid-milestone review | producer, ux-designer |
| 24/11/2026 | Milestone review | jules |
