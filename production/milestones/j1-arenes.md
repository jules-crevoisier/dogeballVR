# Milestone: J1 Arènes

## Overview

- **Target Date**: 13/11/2026
- **Type**: Vertical Slice
- **Duration**: 1 semaine
- **Number of Sprints**: 1

## Milestone Goal

Remplacer les cartes « plateau plat » du PC par de vraies arènes continues : toits, pont, passerelles, bassin,
tremplins et vide qui éjecte. Le joueur PC, les bots et le jeu en ligne y fonctionnent ensemble. À la fin, on peut
jouer une partie solo et une partie en ligne complètes sur deux arènes, et elles se lisent bien en capture.

## Success Criteria

- [x] Géométrie d'arène partagée par le joueur, les bots et l'hôte (`scripts/Maps/ArenaGeometry.cs`)
- [x] Bots qui naviguent sur toute la carte sans tomber seuls (`NavGrid`, `BotRoam`)
- [x] Toits du Marché et La Piscine jouables en solo et en ligne
- [ ] Job CI « arenes » vert (solo des deux cartes, réseau sur Toits du Marché)
- [ ] Revue indépendante des cartes ≥ 8/10
- [ ] Troisième arène (dépôt de tramways, couverts mobiles)
- [ ] All S1 and S2 bugs resolved
- [ ] Performance within budget on target hardware (60 i/s sur PC milieu de gamme)
- [ ] Build stable for 2 consecutive days

## Feature List

### Must Ship (Milestone Fails Without These)

| Feature | Design Doc | Owner | Sprint Target | Status |
|---------|-----------|-------|--------------|--------|
| Géométrie d'arène (sol, murs, vide, tremplins) | `scripts/Maps/ArenaGeometry.cs` | gameplay-programmer | S1 | Fait |
| Bots itinérants | `scripts/Gameplay/BotRoam.cs` | ai-programmer | S1 | Fait |
| Validation réseau des arènes | `scripts/Net/NetSession.cs` | network-programmer | S1 | Fait |
| Toits du Marché | `tools/maps/arenas.py` | level-designer | S1 | Blockout habillé |
| La Piscine | `tools/maps/arenas.py` | level-designer | S1 | Blockout habillé |

### Should Ship (Planned but Cuttable)

| Feature | Design Doc | Owner | Sprint Target | Cut Impact | Status |
|---------|-----------|-------|--------------|-----------|--------|
| Dépôt de tramways (3e arène, couverts mobiles) | à écrire dans `design/levels/` | level-designer | S1 | Deux cartes seulement au lancement | À faire |
| Bots qui prennent les tremplins | `scripts/Gameplay/BotRoam.cs` | ai-programmer | S1 | Bots moins mobiles en hauteur | Liens prêts dans la grille |
| Tutoriel sur une arène | `scripts/UI/Tutorial.cs` | game-designer | J3 | Tutoriel sur l'ancienne carte | Reporté à J3 |

### Stretch Goals (Only if Ahead of Schedule)

| Feature | Design Doc | Owner | Value Add |
|---------|-----------|-------|----------|
| Modèles Blender des toits (château d'eau, clim, étals) | `design/art/` | technical-artist | Habillage final |
| Événement de carte (vent, jet d'eau) | à écrire | game-designer | Variété des parties |

## Quality Gates

| Gate | Threshold | Measurement Method |
|------|-----------|-------------------|
| Crash rate | 0 exception dans les journaux | Job CI « arenes » |
| Frame rate | > 60 i/s sur PC milieu de gamme | Mesure sur le PC de jules |
| Load time | < 5 s pour construire une arène | Journal `[Arène]` |
| Critical bugs | 0 open S1 | PR et retours de jules |
| High-severity bugs | 0 chute de bot sans poussée | Journal « Chute dans la fosse » en CI (≤ 3 par run) |
| Test coverage | Les deux arènes en solo, une en réseau | `.github/workflows/build.yml` |

## Risk Register

| Risk | Probability | Impact | Mitigation | Owner | Status |
|------|------------|--------|-----------|-------|--------|
| Bots bloqués ou qui tombent sur une nouvelle géométrie | Moyenne | Élevé | Grille de navigation testée en CI, garde-fou de vide | ai-programmer | Surveillé |
| Triche de hauteur en ligne | Faible | Élevé | Bornes de hauteur et de vitesse côté hôte | network-programmer | Couvert |
| Cartes jugées « boîtes grises » | Moyenne | Élevé | Habillage par type de bloc, revue indépendante, captures | level-designer | En cours |

## Dependencies

### Internal Dependencies

| Feature | Depends On | Owner of Dependency | Status |
|---------|-----------|-------------------|--------|
| Arènes en ligne | Validation hôte des poses | network-programmer | Fait |
| Habillage | Modèles glb existants (`assets/models/`) | technical-artist | Disponible |

### External Dependencies

| Dependency | Provider | Status | Risk if Delayed |
|-----------|---------|--------|----------------|
| Godot 4.3 mono | Godot | Épinglé | Aucun |

## Review Schedule

| Date | Review Type | Attendees |
|------|-----------|-----------|
| 07/11/2026 | Revue indépendante des cartes | level-designer, art-director (agents) |
| 10/11/2026 | Essai de jules sur le zip Windows | jules |
| 13/11/2026 | Milestone review | jules, producer |
