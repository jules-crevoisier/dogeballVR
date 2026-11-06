# Milestone: J3 Boucle complète

## Overview

- **Target Date**: 08/12/2026
- **Type**: Alpha
- **Duration**: 2 semaines
- **Number of Sprints**: 2

## Milestone Goal

Tout ce qu'un joueur fait entre le premier lancement et sa dixième partie : tutoriel sur une arène, parties rapides
contre bots, salon en ligne avec invitation d'amis Steam, progression (niveau, récompenses cosmétiques), options
complètes et son de qualité. À la fin, un inconnu peut installer le jeu et y passer une soirée sans aide.

## Success Criteria

- [ ] Tutoriel guidé sur une arène (déplacement, ramasser, lancer, attraper, esquive, tremplin, éjection)
- [ ] Partie en ligne de bout en bout : créer, inviter, rejoindre, jouer, revanche
- [ ] Progression sauvegardée (niveau de joueur, récompenses cosmétiques) et Steam Cloud
- [ ] Options : graphismes, son, contrôles réassignables, sensibilité, langue, accessibilité de base
- [ ] Ambiance sonore et musique par arène, sons d'impact et d'attrape retravaillés
- [ ] All S1 and S2 bugs resolved
- [ ] Performance within budget on target hardware
- [ ] Build stable for 3 consecutive days

## Feature List

### Must Ship (Milestone Fails Without These)

| Feature | Design Doc | Owner | Sprint Target | Status |
|---------|-----------|-------|--------------|--------|
| Tutoriel sur arène | `design/gdd/tutoriel.md` | game-designer | S1 | À faire |
| Salon et invitations Steam | `design/gdd/en-ligne.md` | network-programmer | S1 | Base existante (PR #22) |
| Progression et récompenses | `design/gdd/progression.md` | economy-designer | S2 | À faire |
| Options complètes | `design/ux/options.md` | ui-programmer | S2 | Partiel |
| Son et musique | `design/audio/` | audio-director | S2 | Partiel |

### Should Ship (Planned but Cuttable)

| Feature | Design Doc | Owner | Sprint Target | Cut Impact | Status |
|---------|-----------|-------|--------------|-----------|--------|
| Recherche de partie publique | `design/gdd/en-ligne.md` | network-programmer | S2 | Parties entre amis seulement | À faire |
| Défis quotidiens | `design/gdd/progression.md` | economy-designer | S2 | Moins de rétention | À faire |

### Stretch Goals (Only if Ahead of Schedule)

| Feature | Design Doc | Owner | Value Add |
|---------|-----------|-------|----------|
| Mode entraînement libre sur chaque arène | à écrire | game-designer | Prise en main |

## Quality Gates

| Gate | Threshold | Measurement Method |
|------|-----------|-------------------|
| Crash rate | 0 exception en CI, 0 plantage en 1 h de jeu | CI et essai de jules |
| Frame rate | > 60 i/s | Mesure sur le PC de jules |
| Load time | < 5 s pour rejoindre une partie | Journal réseau |
| Critical bugs | 0 open S1 | PR |
| High-severity bugs | < 3 open S2 | PR |
| Test coverage | Tutoriel, salon et sauvegarde testés en CI | `.github/workflows/build.yml` |

## Risk Register

| Risk | Probability | Impact | Mitigation | Owner | Status |
|------|------------|--------|-----------|-------|--------|
| Réseau Steam non testable sans compte Steamworks | Élevée | Élevé | ENet direct en CI, Steam testé par jules | network-programmer | Ouvert |
| Sons sous licence incertaine | Moyenne | Élevé | Sons CC0 ou faits maison, registre des licences | audio-director | Ouvert |

## Dependencies

### Internal Dependencies

| Feature | Depends On | Owner of Dependency | Status |
|---------|-----------|-------------------|--------|
| Tutoriel | Arènes (J1), interface (J2) | level-designer, ui-programmer | En cours |

### External Dependencies

| Dependency | Provider | Status | Risk if Delayed |
|-----------|---------|--------|----------------|
| AppID Steam | jules (Steamworks) | À faire | Pas d'invitations ni de Cloud |

## Review Schedule

| Date | Review Type | Attendees |
|------|-----------|-----------|
| 01/12/2026 | Mid-milestone review | producer, game-designer |
| 08/12/2026 | Milestone review et essai à plusieurs | jules et ses testeurs |
