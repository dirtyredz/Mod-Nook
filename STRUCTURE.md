# STRUCTURE — Mod Nook

Where things live in the code. Maps the *shape*; for how the system works see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), for why see [docs/DECISIONS.md](docs/DECISIONS.md).

_Last full review: 2026-08-22_

## Overview

A BepInEx 5 / HarmonyX plugin for the Unity Mono game **Moonlight Peaks** (netstandard2.1). It adds
one **Mod Nook** button to the pause menu; opening it draws a full-screen settings panel built
**out of the game's own widgets** (cloned, not redrawn), populated from every other loaded plugin's
`ConfigEntry` definitions. Discovery is one-directional — no other mod references this assembly.

Plugin source is flat in `src/*.cs` (no `src/ModNook/` nesting). One assembly, one namespace
`ModNook`, ~5.6k lines across 19 files.

## Architecture at a glance

```
Harmony patches (Plugin.cs)
   └─ PauseScreen.OnShow ─▶ PanelController.Attach   (adds the pause button, builds the overlay)
   └─ PauseMenuState.ProcessContinueInput ─▶ PanelController.RequestBack  (cancel steps back)
   └─ PauseScreen.OnHide ─▶ PanelController.CloseFor

PanelController  (the panel: overlay + sidebar + page)
   ├─ ModCatalog.Discover ───────▶ ModEntry / SectionEntry   (what to show)
   ├─ PauseMenu ─────────────────▶ add button, size the pause panel
   ├─ Rows.Build / BuildText ────▶ one native widget per ConfigEntry
   │     ├─ Templates ───────────▶ clone + sanitize game Cycle/Slider/Toggle/Button
   │     └─ dialogs: ColorPicker · KeyCapture · ListEditor · (text popup) · Confirm
   └─ chrome: PanelSprite · GameFonts · Palette · Tooltip · InputPrompt · PromptButton
```

## Components

| Component | Files | Responsibility | Depends on | Seam |
|---|---|---|---|---|
| **Entry & patches** | `Plugin.cs` | `ModNookPlugin` binds its own config and installs 3 Harmony patches; the patch classes forward OnShow/OnHide/cancel to `PanelController`. | PanelController | Add a patch / config entry here |
| **Panel** ⚠️ | `PanelController.cs` (1133) | MonoBehaviour on the PauseScreen. Navigation state machine (open/close/back/show-mod/reset/persist) **+** one-time overlay & chrome construction **+** per-mod content rendering. God-file — see debt. | Rows, ModCatalog, PauseMenu, Templates, InputPrompt, Tooltip, Confirm, PanelSprite, all dialogs | Panel layout / navigation |
| **Row factory** | `Rows.cs` (559) | Maps one `ConfigEntryBase` → the native widget for its type and binds it back **+** routes text settings to the right editor (colour/list/key/text popup) **+** builds the info icon. Also holds the panel-set overlay statics (`OverlayRoot`/`OverlayGroup`/`ButtonTemplate`). | Templates, dialogs, TextPopupDialog, SettingMetadata, Tooltip, PanelSprite, GameFonts, Tags, Palette | Add a setting-type → widget mapping |
| **Setting metadata** | `SettingMetadata.cs` (275) | Pure, UI-free reading of a `ConfigEntryBase`: label (camel-case humanise + `ModNook.Label`), explicit/prose choices, numeric range, display summary, slider step. No UI, no shared state — reusable by rows and dialogs. | Tags | Label / choice / range rules |
| **Modal dialogs** | `ModalDialog.cs` (170), `ColorPicker.cs` (510), `KeyCapture.cs` (273), `ListEditor.cs` (269) | `ModalDialog` base owns the one-at-a-time singleton lifecycle, the dim+centered-panel shell (`BuildShell`), Escape-close and the register-before-`Build` contract; the three subclasses (hex colour, key binding, comma-list) implement only their own `Build`. | ModalDialog, Templates, PanelSprite, GameFonts, Palette, Rows (label/overlay) | A new editor kind (subclass `ModalDialog`) |
| **Native popup adapters** | `TextPopupDialog.cs` (192), `Confirm.cs` (92), `PopupEscape.cs` (52) | `TextPopupDialog` opens the game's `TextInputPopupScreen` for free-form/text settings, borrowing and restoring the overlay's raycast blocker and the popup's "Name:" prefix; `Confirm` drives `GenericPopupScreen` for reset; `PopupEscape` arms Escape on the popup. Distinct shape from the custom build-your-own dialogs. | Rows.OverlayGroup, SettingMetadata, game screens | Text-popup borrow/restore |
| **Widget templating** | `Templates.cs` (546), `BatWingFitter.cs` (89) | Find & cache the game's Cycle/Slider/Toggle/Button templates; clone; sanitize a clone (strip localization/decorations/hover-select/colour-freeze/bat-wings); relabel. `BatWingFitter` repositions wing ornaments a frame after layout. | game UI assemblies | Adjust how cloned widgets are tamed |
| **Pause-menu integration** | `PauseMenu.cs` (280) | Source the pause button template, add ours, grow the pause panel to fit — plus a `VerboseLogging` hierarchy dump. | game PauseScreen | Button placement / fit |
| **Catalog** | `ModCatalog.cs` (170), `Tags.cs` (46) | Discover loaded plugins that expose settings; group into `ModEntry`/`SectionEntry`; honour `ModNook.Hidden`. `Tags` reads the optional `ModNook.*` description tags. | BepInEx Chainloader | Discovery / tag vocabulary |
| **Drawing & theming** | `PanelSprite.cs` (182), `GameFonts.cs` (178), `Palette.cs` (10), `Tooltip.cs` (184) | Procedural sliced/circle sprites; find & apply the game font; shared colours; hover tooltip + `TooltipTrigger`. | game UI/TMP | Look & feel primitives |
| **Corner prompt** | `InputPrompt.cs` (144), `PromptButton.cs` (128) | Register/withdraw the game's real corner **Close** prompt (drawing the player's bound key cap); build a prompt-style button when the real one is unavailable. | game input screens | Prompt bar |

⚠️ = over the ~800-line God-file cap; see **Structural debt**.

## Key flows

- **Open:** `PauseScreen.OnShow` → `Attach` adds the pause button (once) and, on click, `Open` →
  `EnsureOverlay` builds the overlay/backdrop/header/body once → `ModCatalog.Discover` → `ShowModList`
  fills the sidebar and opens the first mod → `ShowMod` builds a row per `ConfigEntry` via `Rows`.
- **Edit:** a widget's `OnValueChanged` writes `entry.BoxedValue` (or `SetSerializedValue`) and calls
  `Persist(mod)` → `mod.Config.Save()`. No separate Save button; the mod's own file is written.
- **Cancel/back:** the `ProcessContinueInput` patch routes cancel to `RequestBack`, which closes an
  open dialog first, else closes the panel. Lets gamepad B dismiss dialogs that have no Escape key.

## Conventions

- Plugin `.cs` flat in `src/`. One namespace `ModNook`. `internal` by default.
- Version is single-sourced from `<Version>` in `src/ModNook.csproj` → `ModBuildInfo.Version` via the
  `GenerateModBuildInfo` target in `Directory.Build.props`. Never hardcode a version in `Plugin.cs`.
- `Directory.Build.props` and `pack.ps1` are **workspace-synced canonicals** — do not edit here; they
  come from `../../tools/sync-mod-files.ps1`.
- Build: `dotnet build src/ModNook.csproj -c Release`. Pack: `.\pack.ps1` → `dist/ModNook-<ver>.zip`.

## Where to find things

- A setting renders wrong / new setting type → `Rows.cs` (`Build` dispatch) + `Templates.cs`.
- Panel layout / sidebar / scrolling → `PanelController.cs` (`BuildBody`/`BuildScroller`).
- A cloned widget looks off (colour, wings, hover) → `Templates.cs` sanitizers.
- Which mods/settings appear → `ModCatalog.cs`; tag behaviour → `Tags.cs`.
- Cancel/Escape/gamepad behaviour → `Plugin.cs` patch + `PanelController.RequestBack`.

## Structural debt

Documented by the full review of 2026-08-22 (componentization + abstraction lenses + Codex).
Two safe relocations were fixed then (`Palette`, `Tags` → own files); the rest is backlogged in
[docs/BACKLOG.md](docs/BACKLOG.md). None is a correctness bug; all are shape.

- **P1 · `Rows.cs` responsibility split — done (2026-08-22).** Extracted the pure metadata parsing
  → `SettingMetadata.cs` and the game-text-popup borrow/restore dance
  (`Prompt`/`Edit`/`Brief`/`HidePrefix`/`SuspendOverlay`/`RestoreOn`) → `TextPopupDialog.cs`. `Rows.cs`
  went 997 → 559 lines and is now a focused widget-dispatch + row-chassis class. One coupling remains:
  `TextPopupDialog` and `Confirm` still read `Rows.OverlayGroup` — dissolved by the overlay-context item.
- **P1 · `Rows.OverlayRoot` / `OverlayGroup` / `ButtonTemplate` are a leaky back-channel** — public
  mutable statics set by `PanelController`, read deep in row/dialog code; `Confirm` reaches straight
  into `Rows.OverlayGroup`. Wants an explicit `OverlayContext`/`PanelUiContext` threaded in. *(backlog)*
- **P1 · Modal-dialog abstraction — done (2026-08-22).** `ColorPicker`/`KeyCapture`/`ListEditor` now
  subclass `ModalDialog` (`src/ModalDialog.cs`), which owns the singleton lifecycle, the dim/panel
  shell (`BuildShell`) and Escape-close, and fixes the assign-before-`Build` contract in one place —
  two of the three subclasses used to assign it *after* `Build`, leaving an un-closeable half-built
  dialog if the build threw. `Confirm` stays out (it wraps a native popup).
- **P1 · Dialog close/back registry — done (2026-08-22, folded into the modal abstraction).** A single
  `ModalDialog.current` replaced the three per-type statics; `PanelController.RequestBack`/`Close` now
  call `ModalDialog.CloseCurrent()`, so a new dialog kind is closeable the moment it subclasses
  `ModalDialog` — no third place to update.
- **P2 · `PanelController.cs` God-file** — one coarse extraction warranted: the once-per-overlay
  construction block (`EnsureOverlay`…`BuildScroller`, ~408–943) → a `PanelChrome` builder. Do **not**
  fragment header/footer/scroller into micro-files (reviewer + Codex agree that's churn). *(backlog)*
- **P2 · Duplicated UI primitives** — `NewText`/`AddText`/`Text` TMP builder is written 5×; `Stretch`
  exists as 2 named copies + 5 inline. Wants a small `UiText`/`UiPrimitives` helper. *(backlog)*
- **P2 · `PauseMenu.DumpHierarchy`/`Describe`** (~70 lines) is debug tooling inside a fit/layout class
  — could move to a `HierarchyDebug` helper. *(backlog, optional)*
- **Not debt (checked):** `Templates.cs` (546) is coherent — one job, many small tools; leave whole.
  `ModEntry`/`SectionEntry` are fine colocated with `ModCatalog`. Setting-type dispatch in `Rows.Build`
  should stay explicit (no `ISettingRowProvider` registry — premature). `Confirm` should not join the
  modal base. Harmony patch classes / `Tooltip`+`TooltipTrigger` are fine paired in one file.

_Living doc — refresh with /project-docs when it drifts._
