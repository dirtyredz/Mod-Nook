# ROADMAP — Mod Nook

Phase-by-phase trajectory. Detailed tasks live in [BACKLOG.md](BACKLOG.md).

## ✅ Phase 1 — 1.0.0 (published)

Live on Nexus as [mod 127](https://www.nexusmods.com/moonlightpeaks/mods/127). Auto-discovery,
sidebar layout, the full native-widget set (toggle/cycle/slider/key/colour/list/text), info icons,
per-mod reset, immediate persistence, cloned native chrome, and the cancel-steps-back hook.

## 🧪 Phase 2 — next release (committed, unreleased)

On top of 1.0.0 in `HEAD` (commit `3f5f65f`, marked WIP) — do **not** publish until validated in-game:
- Gamepad cancel dismisses the custom dialogs.
- Proton/Linux overlay-parenting fix.
- Long-name overflow → ellipsis in the sidebar.
- Prose choice-parser (values read from a string setting's description).
- Refreshed page copy.

**To ship:** hand-test per [../TESTING.md](../TESTING.md), bump `<Version>` in `src/ModNook.csproj`,
`.\pack.ps1`, publish via the nexus-publish skill (see [../RELEASING.md](../RELEASING.md)).

## 📋 Phase 3 — structural cleanup (no user-facing change)

Pay down the debt recorded in [../STRUCTURE.md](../STRUCTURE.md) / [BACKLOG.md](BACKLOG.md), one
independently reviewable and in-game-testable step at a time. **Done (2026-08-22):** the `Rows.cs`
responsibility split (`SettingMetadata` + `TextPopupDialog`), the modal-dialog abstraction + close/back
registry (`ModalDialog`), and the overlay-context threading (`OverlayContext`, static back-channel
removed). **Remaining:** coarse-split the `PanelController` God-file (`PanelChrome`) and dedupe the UI
primitives (`UiText`/`Stretch`).

## 📋 Phase 4 — capability gaps (author-facing)

From the README "Known limits": a two-field row (label + slider) for `Name=Value` pair lists; better
handling for unbounded numbers (they fall back to text until an author declares a range); optional
per-setting reset/undo. All depend on demand from configured mods.

_Living doc — refresh with /project-docs when it drifts._
