# DogeballVR — Claude Code Game Studios

Dodgeball multijoueur façon Knockout City : VR free-roam pour les arènes EVA
(Meta Quest) et PC clavier-souris en ligne, sortie Steam visée. Ce dépôt embarque
le kit **Claude Code Game Studios** (49 agents, 74 skills, hooks) adapté à notre
projet. Mode d'emploi pour chaque session : `docs/STUDIO-CLAUDE.md`.

**Répondre et écrire en français** (code, commentaires, commits, docs). Les
fichiers du kit restent en anglais.

## Technology Stack

- **Engine**: Godot 4.3 (.NET) — pinned, no 4.4+ API (see the import below)
- **Language**: C# 10 — the csproj targets `net6.0` (net7.0 for Android): no C# 11/12 features (primary constructors, collection expressions `[..]`, raw strings, `required`)
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
  pick the recommended option and keep going.
- **Decisions go in the PR description** (a "Choix faits" section). The kit's
  `production/session-logs/decision-log.md` is gitignored and disappears with
  the container: it is a scratch copy, never the record.
- Work on a branch, commit, push, open a PR and drive the CI to green. **Never
  push to `main`, never merge a PR**: jules reviews and merges. Nothing in the
  tooling blocks it, so this rule is on you.
- Stop and ask only for irreversible actions or anything outside the project
  (deleting files or data, production, messages to other people) —
  `modes.automation_always_ask: [file_deletions]`.
- A question with no answer is not an answer: take the default, write it in
  the PR, and say so in the final report.
- **The task you were given comes first.** Kit documents (GDD, ADR, stories
  from `docs/adoption-plan-2026-10-22.md`) are written only when they are the
  thread's subject, or at the end of a PR for the system it touched. Tick the
  plan's boxes in your own PR.
- `/gate-check` and `/setup-engine` always ask a human (kit exemption): prepare
  their report, then leave the decision to jules.
- **No Godot or .NET in the cloud containers**: `commands.*` cannot run there.
  Push and read the CI (`.github/workflows/build.yml`) — it is the build, the
  test and the smoke check.
- `project.stage` is `Production` because the game runs, although the kit's
  documents are still missing.
- The CI is the test evidence: `/story-done` and `/smoke-check` checks that
  need a local run or a screenshot are satisfied by a green CI run, quoted in
  the PR (`testing.strict` is off in `project.yaml`).
- Every `**/*.cs` under the repo is compiled by `DogeballVR.csproj`. A test or
  prototype project in `tests/` or `prototypes/` must be excluded with
  `<Compile Remove="tests/**;prototypes/**" />` in the same PR. Do not run
  `/test-setup` as is: it scaffolds gdUnit4 GDScript tests, we write C#.
- Kit documents: section headings exactly as in the kit templates (English,
  the scripts check them), content in French.
- `git rm` inside your own branch is fine (reversible in the PR). Deleting
  anything outside the repo, data, or remote branches needs jules.
- The hooks print reminders on `git merge` and on pushes: merging `origin/main`
  into your own branch is normal, ignore that reminder. Force pushes are denied
  (`--force-with-lease` too): resolve with merge commits, never rebase a pushed
  branch.
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
