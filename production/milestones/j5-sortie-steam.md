# Milestone: J5 Sortie Steam

## Overview

- **Target Date**: fin janvier 2027 (dépend des délais Valve, voir `docs/steam/CHECKLIST-SORTIE-STEAM.md` §0)
- **Type**: Gold
- **Duration**: 5 à 6 semaines, en parallèle de J3 et J4 pour la partie administrative
- **Number of Sprints**: 2

## Milestone Goal

Mettre le jeu en vente sur Steam : build envoyé par la CI, page store complète avec captures et bande-annonce tirées
du vrai jeu, succès et Cloud branchés, revue Valve passée.

## Success Criteria

- [ ] Toutes les lignes de `docs/steam/CHECKLIST-SORTIE-STEAM.md` cochées
- [ ] Page « Coming soon » publique au moins 2 semaines avant la sortie
- [ ] Build validé par Valve sur la branche par défaut
- [ ] All S1 and S2 bugs resolved
- [ ] Performance within budget on target hardware
- [ ] Build stable for 7 consecutive days

## Feature List

### Must Ship (Milestone Fails Without These)

| Feature | Design Doc | Owner | Sprint Target | Status |
|---------|-----------|-------|--------------|--------|
| Compte Steamworks, frais, AppID | `docs/steam/ACTIONS-JULES.md` | jules | dès que possible | À faire |
| Capsules et captures | `docs/steam/PAGE-STORE.md` | art-director | S1 | Outils prêts (`tools/steam/`) |
| Bande-annonce | `docs/steam/PAGE-STORE.md` | art-director | S1 | À faire |
| Envoi des builds par la CI | `tools/steam/README.md` | devops-engineer | S1 | Prêt, attend les secrets |

### Should Ship (Planned but Cuttable)

| Feature | Design Doc | Owner | Sprint Target | Cut Impact | Status |
|---------|-----------|-------|--------------|-----------|--------|
| Démo pour le Steam Next Fest | `docs/steam/` | producer | S2 | Moins de listes de souhaits | À décider |

### Stretch Goals (Only if Ahead of Schedule)

| Feature | Design Doc | Owner | Value Add |
|---------|-----------|-------|----------|
| Version Linux | `tools/steam/README.md` | devops-engineer | Steam Deck natif |

## Quality Gates

| Gate | Threshold | Measurement Method |
|------|-----------|-------------------|
| Crash rate | < 1 par 10 h | Essais beta |
| Frame rate | > 60 i/s config minimale | Profilage |
| Load time | < 5 s | Mesure |
| Critical bugs | 0 open S1 | Suivi des bugs |
| High-severity bugs | 0 open S2 | Suivi des bugs |
| Test coverage | CI verte sur le commit envoyé | `.github/workflows/build.yml` |

## Risk Register

| Risk | Probability | Impact | Mitigation | Owner | Status |
|------|------------|--------|-----------|-------|--------|
| Délais Valve (30 jours après les frais) | Certaine | Élevé | Payer les frais dès J2 | jules | Ouvert |
| Déclaration d'usage de l'IA mal remplie | Faible | Élevé | Liste des contenus générés tenue à jour | producer | Ouvert |

## Dependencies

### Internal Dependencies

| Feature | Depends On | Owner of Dependency | Status |
|---------|-----------|-------------------|--------|
| Captures définitives | Interface (J2), arènes habillées (J1) | art-director | En cours |

### External Dependencies

| Dependency | Provider | Status | Risk if Delayed |
|-----------|---------|--------|----------------|
| Steamworks | Valve | Compte à créer | Pas de sortie |

## Review Schedule

| Date | Review Type | Attendees |
|------|-----------|-----------|
| Paiement des frais + 2 semaines | Soumission de la page store | jules |
| Sortie − 2 semaines | Soumission du build | jules, producer |
| Date de sortie | Milestone review | jules |
