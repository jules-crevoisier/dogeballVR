# DogeballVR — Claude Code Game Studios

Dodgeball multijoueur façon Knockout City : VR free-roam pour les arènes EVA
(Meta Quest) et PC clavier-souris en ligne, sortie Steam visée. Ce dépôt embarque
le kit **Claude Code Game Studios** (49 agents, 74 skills, hooks) adapté à notre
projet. Mode d'emploi pour chaque session : `docs/STUDIO-CLAUDE.md`.

**Répondre et écrire en français** (code, commentaires, commits, docs). Les
fichiers du kit restent en anglais.

## Technology Stack

- **Engine**: Godot 4.3 (.NET) — pinned, no 4.4+ API (see the import below)
- **Language**: C# (.NET 8)
- **Rendering**: Mobile renderer, OpenXR (Meta Quest)
- **Physics**: Godot Physics
- **Networking**: Godot ENet, the host is the only authority (balls, hits, score)
- **Version Control**: Git, one branch + one PR per piece of work, never push to `main`
- **Build System**: `dotnet build DogeballVR.csproj` then Godot 4.3-stable mono headless (CI: `.github/workflows/build.yml`)
- **Asset Pipeline**: Godot import system; 3D assets from Blender (glb)

> Code root is **`scripts/`** (not `src/`): `project.yaml` → `engine.code_root`.
> Architecture and rules of play: `docs/ARCHITECTURE.md`, `docs/CHOIX-REGLES.md`,
> `README.md`.

## Project Structure

@.claude/docs/directory-structure.md

dogeballVR differs from the kit layout above: C# in `scripts/<Domain>/`
(Gameplay, Maps, Net, Rules, Visual, Xr, Zones), scenes in `scenes/`, maps as
JSON data in `maps/`, assets in `assets/`. Kit design docs go in `design/`,
production tracking in `production/`, ADRs in `docs/architecture/`.

## Engine Version Reference

@docs/engine-reference/godot/VERSION.md

## Technical Preferences

`project.yaml` at the repo root is the primary config store — engine, specialists,
naming, platform, performance, modes. Skills resolve it via `resolve_config`
(see `.claude/docs/config-resolution.md`). `.claude/docs/technical-preferences.md`
holds the forbidden patterns and allowed libraries.

## Coordination Rules

@.claude/docs/coordination-rules.md

## Collaboration Protocol (dogeballVR override)

The kit's default is "ask before every write". **This project runs in
autonomous mode** (`modes.automation: autonomous` in `project.yaml`): sessions
usually have nobody to answer questions, and jules wants full autonomy.

- Do not ask "May I write…?" and do not wait for approval on reversible work:
  pick the recommended option, log it in
  `production/session-logs/decision-log.md`, and keep going.
- Work on a branch, commit, push, open a PR and drive the CI to green. **Never
  push or merge into `main`**: jules reviews and merges.
- Stop and ask only for irreversible actions or anything outside the project
  (deleting files or data, production, messages to other people) —
  `modes.automation_always_ask: [file_deletions]`.
- A question with no answer is not an answer: log the default you chose and say
  so in the final report.
- Quality loop before delivering anything visible: get it critiqued by at least
  one independent agent (score out of 10), fix, re-review, until ≥ 8/10 or 5
  rounds; report the scores.

See `docs/COLLABORATIVE-DESIGN-PRINCIPLE.md` for the kit's original protocol
(still useful for how to present options and drafts).

## Coding Standards

@.claude/docs/coding-standards.md

## Context Management

Read `.claude/docs/context-management.md` on demand — it is a reference, not
session context. Two of its conventions are load-bearing:

- **`production/session-state/active.md` is the session checkpoint.** The file is
  the memory, not the conversation. Read it first after any compaction, crash, or
  `/clear`.
- **Helpers in `.claude/scripts/` emit observations, never verdicts.**
