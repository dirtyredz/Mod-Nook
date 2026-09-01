# CLAUDE.md — working in Mod Nook

Mod Nook is a **standalone git repo** nested in the Moonlight Peaks workspace. When you're editing
files here, **this** repo is the active project — honor its own structure-review gate and baseline,
not the workspace root's. Orientation lives in the doc set; don't duplicate it here.

- **[README.md](README.md)** — what it is + author-facing quick-start.
- **[STRUCTURE.md](STRUCTURE.md)** — code map + structural debt.
- **[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md)** — how it works at runtime.
- **[docs/DECISIONS.md](docs/DECISIONS.md) · [FEATURES.md](docs/FEATURES.md) ·
  [ROADMAP.md](docs/ROADMAP.md) · [BACKLOG.md](docs/BACKLOG.md) · [GOTCHAS.md](docs/GOTCHAS.md)**

## What it is

A BepInEx 5 / HarmonyX plugin (netstandard2.1) that adds an in-game settings panel to the pause menu,
built from the game's own cloned widgets and populated from other loaded mods' `ConfigEntry`
definitions. Discovery is one-directional: nothing references this assembly.

## Conventions

- **Commit identity:** `dirtyredz <dirtyredz@live.com>` (never the work email).
- **Layout is enforced:** `src/Plugin.cs` (entry point) plus `src/game/` (live-game bridges +
  patches), `src/ui/chrome/` (the panel frame and its reusable furniture), `src/ui/dialogs/` (modal
  surfaces and per-setting editors), `src/core/` (discovery, config interpretation, diagnostics).
  `src/ui/` itself holds ONLY the two vendored primitives (`PanelSprite.cs`, `Palette.cs`), whose
  paths are locked by the byte-sync with sibling mods — new UI goes in `chrome/` or `dialogs/`.
  See STRUCTURE.md `## Layout`. Folders are cosmetic to the compiler — one flat
  namespace `ModNook` everywhere, `internal` by default; never change a namespace when moving a file.
- **Versioning:** bump `<Version>` in `src/ModNook.csproj` only, only when publishing; it flows to
  `[BepInPlugin]` via `ModBuildInfo.Version`. Never hardcode a version in `Plugin.cs`.
- **Synced canonicals — do not edit here:** `Directory.Build.props`, `pack.ps1` (from
  `../../tools/sync-mod-files.ps1`).
- **Never commit:** `save-backup-*/`, `dist/`, `bin/`, `obj/`, decompiled game code.

## Build / run / release

- Build: `dotnet build src/ModNook.csproj -c Release` (needs the game's `Managed/` DLLs; see GOTCHAS).
- Pack: `.\pack.ps1` → `dist/ModNook-<version>.zip` (Nexus layout).
- Publish/update the Nexus page via the workspace **nexus-publish** skill. See [RELEASING.md](RELEASING.md).
- **Do not launch the game** as part of routine work; hand-test per [TESTING.md](TESTING.md) at release.

## Structure-review gate

This repo is gated (pre-push hook, installed 2026-08-22). Edit and debug freely — the review fires
once at **push** on the accumulated change, not per edit or commit. Commit at logical boundaries;
Claude runs the review and pushes (asking first) when work is ready. `/gate status` shows what's
pending. `HEAD` carries a committed-but-unreleased feature on top of live 1.0.0 — that's expected.
