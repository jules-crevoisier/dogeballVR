# Godot Engine — Version Reference (dogeballVR)

| Field | Value |
|-------|-------|
| **Engine Version** | Godot 4.3 (.NET / C#) |
| **Installed at pin time** | 4.3-stable mono — the version the CI downloads (`.github/workflows/build.yml`, `GODOT_VERSION: 4.3-stable`) |
| **Release Date** | August 2024 |
| **Project Pinned** | 2026-10-22 |
| **Last Docs Verified** | 2026-10-22 (pin set to 4.3 for dogeballVR) |
| **LLM Knowledge Cutoff** | Covers Godot 4.3 well |

## dogeballVR pin: write Godot 4.3 code only

This project ships on **Godot 4.3 stable, C#**, with the **Mobile** renderer and
**OpenXR** (Meta Quest), and **Godot Physics** (no Jolt). The CI builds with
`dotnet build` and runs headless matches on 4.3-stable mono.

The other files in this folder (`breaking-changes.md`, `deprecated-apis.md`,
`current-best-practices.md`, `modules/*.md`) were written by Claude Code Game
Studios for Godot **4.4 → 4.7**. Read them as **"what changes if we upgrade"**,
never as APIs to use today:

- Do **not** use anything introduced in 4.4+ (Jolt as built-in physics,
  `AreaLight3D`, AccessKit, variadic GDScript args, `@abstract`, typed-return
  override rules, `VirtualJoystick`, HDR output, the Asset Store, etc.).
  It will not compile or will not exist on 4.3.
- When a doc here says "deprecated since 4.x" for x ≥ 4, the old API is still
  the correct one for us.
- If you are unsure an API exists in 4.3, check the 4.3 class reference:
  https://docs.godotengine.org/en/4.3/ — and prove it with the CI build.

An engine upgrade is a `version_bumps` decision: it needs its own branch, an ADR
(`/architecture-decision`) and jules's go-ahead. Until then this pin wins.

## Post-4.3 timeline (for a future upgrade only)

| Version | Release | Key Theme |
|---------|---------|-----------|
| 4.4 | Mar 2025 | Jolt option, FileAccess return types, typed dictionaries |
| 4.5 | Sep 2025 | Accessibility (AccessKit), variadic args, @abstract, shader baker, SMAA |
| 4.6 | Jan 2026 | Jolt default, glow rework, D3D12 default on Windows, IK restored |
| 4.7 | Jun 2026 | AreaLight3D, HDR output, Asset Store, VirtualJoystick, typed-return overrides |

Sources: https://github.com/godotengine/godot/releases and the official
migration guides listed in `breaking-changes.md` § Sources.
