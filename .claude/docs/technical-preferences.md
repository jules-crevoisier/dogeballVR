# Technical Preferences

<!-- project.yaml at the repo root is the machine-readable source of truth for
     engine, specialists, naming, performance, platform, and testing.framework.
     This file is the human-readable LEGACY FALLBACK: agents and skills resolve
     each key from project.yaml first and fall back here only when the
     project.yaml key is absent. /setup-engine dual-writes both.
     Forbidden patterns and allowed libraries are NOT migrated — they live only
     in this file. Populated by /setup-engine; updated as decisions are made. -->

<!-- dogeballVR: filled for Godot 4.3 C# on 2026-10-22 (kit /setup-engine done by hand). -->

## Engine & Language

- **Engine**: Godot 4.3 (.NET) — do not use 4.4+ APIs (see docs/engine-reference/godot/VERSION.md)
- **Language**: C# 10 (csproj targets net6.0, net7.0 for Android; the CI uses the .NET 8 SDK), no GDScript in gameplay code. Godot node classes are `partial`.
- **Rendering**: Mobile renderer (Meta Quest), OpenXR enabled
- **Physics**: Godot Physics (no Jolt on 4.3)
- **Code root**: `scripts/` (not `src/`), scenes in `scenes/`, maps as JSON data in `maps/`

## Input & Platform

<!-- Written by /setup-engine. Read by /ux-design, /ux-review, /test-setup, /team-ui, and /dev-story -->
<!-- to scope interaction specs, test helpers, and implementation to the correct input methods. -->

- **Target Platforms**: PC (Windows, Steam, online play) and VR (Meta Quest in EVA free-roam arenas)
- **Input Methods**: Keyboard/Mouse (PC), Quest hand controllers (VR); gamepad later
- **Primary Input**: Keyboard/Mouse on PC; hands (OpenXR) in VR
- **Gamepad Support**: Partial (planned for Steam)
- **Touch Support**: None
- **Platform Notes**: EVA forbids joystick locomotion in VR (players walk physically in 15×8 m zones, ~50 cm drift per half hour: no mechanic may depend on an exact floor position). PC follows the Knockout City format.

## Naming Conventions

- **Classes**: PascalCase, one class per file, file named after the class (`MatchRules.cs`)
- **Variables**: camelCase locals and parameters; private fields camelCase (match the surrounding file)
- **Signals/Events**: C# `event Action<…>` named in the past tense (`PlayerEliminated`, `RoundWon`, `PeerLeft`); Godot `[Signal]` only when the editor needs it
- **Files**: PascalCase `.cs` and `.tscn`; maps in snake_case `.json`
- **Scenes/Prefabs**: PascalCase
- **Constants**: PascalCase `const`
- **Comments and user-facing text**: French

## Performance Budgets

- **Target Framerate**: 72 fps on Meta Quest (VR), 60 fps minimum on PC
- **Frame Budget**: 13.9 ms (Quest)
- **Draw Calls**: keep low on Quest; measure before adding transparent materials per ball
- **Memory Ceiling**: not set yet (measure on Quest)

## Testing

- **Framework**: none yet (no gdUnit4). The CI (`.github/workflows/build.yml`) builds C# then runs headless simulated matches (150 s) and host/client network self-tests; any `SCRIPT ERROR` or unhandled exception fails the job.
- **Minimum Coverage**: not enforced
- **Required Tests**: every change must keep the CI green; pure rules (`scripts/Rules/MatchRules.cs`) and networking (`--net-selftest`) are the critical paths

## Forbidden Patterns

<!-- Add patterns that should never appear in this project's codebase -->
- Godot 4.4+ APIs (the project is pinned to 4.3)
- Game state decided on a client: the host is the only authority on balls, hits and score (`MatchRules` runs on the host)
- Gameplay that depends on an exact physical position on the EVA floor
- Joystick locomotion in VR
- Hard-coded map layouts: maps are JSON data (`maps/*.json`)
- Bots taking a different code path from players

## Allowed Libraries / Addons

<!-- Add approved third-party dependencies here -->
- Godot built-ins only (ENet multiplayer, OpenXR). Godot OpenXR Vendors plugin for the Quest export.
- Any new NuGet package or addon needs an ADR (`/architecture-decision`).

## Architecture Decisions Log

<!-- Quick reference linking to full ADRs in docs/architecture/ -->
- Existing architecture notes: `docs/ARCHITECTURE.md`, `docs/CHOIX-REGLES.md`. No ADR in the kit format yet — use /architecture-decision for new ones.

## Engine Specialists

<!-- Written by /setup-engine when engine is configured. -->
<!-- Read by /code-review, /architecture-decision, /architecture-review, and team skills -->
<!-- to know which specialist to spawn for engine-specific validation. -->

- **Primary**: godot-specialist
- **Language/Code Specialist**: godot-csharp-specialist
- **Shader Specialist**: godot-shader-specialist
- **UI Specialist**: godot-specialist
- **Additional Specialists**: network-programmer (ENet, host authority), gameplay-programmer
- **Routing Notes**: C# files go to godot-csharp-specialist; anything that touches `scripts/Net` also gets network-programmer; VR/OpenXR code goes to godot-specialist.

### File Extension Routing

<!-- Skills use this table to select the right specialist per file type. -->
<!-- If a row says [TO BE CONFIGURED], fall back to Primary for that file type. -->

| File Extension / Type | Specialist to Spawn |
|-----------------------|---------------------|
| Game code (primary language) | godot-csharp-specialist (`.cs`, `.csproj`) |
| Shader / material files | godot-shader-specialist (`.gdshader`, `.tres` materials) |
| UI / screen files | godot-specialist |
| Scene / prefab / level files | godot-specialist (`.tscn`); maps `maps/*.json` → level-designer |
| Native extension / plugin files | godot-gdextension-specialist |
| General architecture review | Primary |
