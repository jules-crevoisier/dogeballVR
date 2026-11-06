# Milestone: J4 Finition

## Overview

- **Target Date**: 22/12/2026
- **Type**: Beta
- **Duration**: 2 semaines
- **Number of Sprints**: 2

## Milestone Goal

Faire passer le jeu de « complet » à « agréable et solide » : game feel (impacts, caméra, animations), performance,
accessibilité, Steam Deck et correction de tous les bugs remontés par les testeurs.

## Success Criteria

- [ ] 60 i/s stables sur la configuration minimale annoncée, 60 i/s sur Steam Deck (réglage Deck)
- [ ] Aucun bug S1 ou S2 ouvert
- [ ] Accessibilité : sous-titres, daltonisme (couleurs d'équipe + motifs), réduction des secousses
- [ ] Animations du mannequin (UAL) : course, saut, lancer, attrape, esquive, chute
- [ ] All S1 and S2 bugs resolved
- [ ] Performance within budget on target hardware
- [ ] Build stable for 5 consecutive days

## Feature List

### Must Ship (Milestone Fails Without These)

| Feature | Design Doc | Owner | Sprint Target | Status |
|---------|-----------|-------|--------------|--------|
| Passe de performance | `docs/architecture/` | performance-analyst | S1 | À faire |
| Animations des personnages | `design/art/` | technical-artist | S1 | Mannequin UAL choisi |
| Accessibilité de base | `design/accessibility-requirements.md` | accessibility-specialist | S2 | À faire |
| Correction des bugs des testeurs | — | qa-lead | S2 | À faire |

### Should Ship (Planned but Cuttable)

| Feature | Design Doc | Owner | Sprint Target | Cut Impact | Status |
|---------|-----------|-------|--------------|-----------|--------|
| Vérification Steam Deck | `docs/steam/` | qa-tester | S2 | Pas de badge Deck | À faire |

### Stretch Goals (Only if Ahead of Schedule)

| Feature | Design Doc | Owner | Value Add |
|---------|-----------|-------|----------|
| Rejeu des dernières secondes d'un KO | à écrire | gameplay-programmer | Spectacle |

## Quality Gates

| Gate | Threshold | Measurement Method |
|------|-----------|-------------------|
| Crash rate | < 1 par 10 h de jeu | Essais testeurs |
| Frame rate | > 60 i/s sur config minimale | Profilage |
| Load time | < 5 s | Journal de démarrage |
| Critical bugs | 0 open S1 | Suivi des bugs |
| High-severity bugs | 0 open S2 | Suivi des bugs |
| Test coverage | Tous les jobs CI verts | `.github/workflows/build.yml` |

## Risk Register

| Risk | Probability | Impact | Mitigation | Owner | Status |
|------|------------|--------|-----------|-------|--------|
| Rendu Mobile trop limité pour le PC | Moyenne | Moyen | Passer le PC en Forward+ (ADR et accord de jules), le Quest garde Mobile | technical-director | Ouvert |

## Dependencies

### Internal Dependencies

| Feature | Depends On | Owner of Dependency | Status |
|---------|-----------|-------------------|--------|
| Correction des bugs | Essais de J3 | jules et testeurs | À venir |

### External Dependencies

| Dependency | Provider | Status | Risk if Delayed |
|-----------|---------|--------|----------------|
| Testeurs | jules | À organiser | Bugs découverts après la sortie |

## Review Schedule

| Date | Review Type | Attendees |
|------|-----------|-----------|
| 15/12/2026 | Mid-milestone review | producer, qa-lead |
| 22/12/2026 | Milestone review | jules |
