# STRUCTURE — Mod Nook

Where things live in the code. Maps the *shape*; for how the system works see
[docs/ARCHITECTURE.md](docs/ARCHITECTURE.md), for why see [docs/DECISIONS.md](docs/DECISIONS.md).

_Last full review: 2026-08-22_

## Overview

A BepInEx 5 / HarmonyX plugin for the Unity Mono game **Moonlight Peaks** (netstandard2.1). It adds
one **Mod Nook** button to the pause menu; opening it draws a full-screen settings panel built
**out of the game's own widgets** (cloned, not redrawn), populated from every other loaded plugin's
`ConfigEntry` definitions. Discovery is one-directional — no other mod references this assembly.

One assembly, one namespace `ModNook`, ~6.1k lines across 28 files. The source is foldered by
responsibility under `src/` (`game/`, `ui/`, `core/`) with `Plugin.cs` beside the `.csproj` — see
**Layout** below. Folders are for humans only: the namespace stays flat `ModNook` regardless of
folder, and the SDK-style project globs `**/*.cs`, so moving a file needs no `.csproj` change.

## Layout

```
ModNook/
├─ pack.ps1                  workspace-synced packer (must sit at the repo root)
├─ scripts/                  git-hook installer + pre-commit shell scripts
├─ docs/                     living-doc set (ARCHITECTURE, DECISIONS, FEATURES, ROADMAP, BACKLOG, GOTCHAS)
├─ screenshots/              Nexus page imagery
└─ src/
   ├─ ModNook.csproj         SDK-style; globs **/*.cs recursively
   ├─ Plugin.cs              the ONLY .cs at src/ root — BaseUnityPlugin + the 3 Harmony patches
   ├─ game/                  everything touching the LIVE game — clone it, read it, or intercept it
   │  ├─ Templates.cs        find/cache/clone/sanitize the game's Cycle/Slider/Toggle/Button widgets
   │  ├─ BatWingFitter.cs    re-anchors a cloned game button's bat-wing ornaments after relabelling
   │  ├─ GameFonts.cs        locates Gelica + its material presets from already-loaded game assets
   │  ├─ PauseMenu.cs        sources the pause button template, adds ours, grows the pause panel
   │  ├─ InputPrompt.cs      registers/withdraws an entry on the game's shared InputButtonScreen
   │  └─ PopupEscape.cs      guard arming Escape on the game's otherwise inescapable text popup
   ├─ ui/                    the panel we draw. Only the two PORTED primitives sit at this level,
   │  │                      to match the path their (diverged) copies use in sibling mods
   │  ├─ PanelSprite.cs      procedural 9-sliced plate + circle sprites  (ported to 5 mods)
   │  ├─ Palette.cs          the window palette for the few things we draw ourselves  (2 mods)
   │  ├─ chrome/             the panel frame and its reusable furniture
   │  │  ├─ PanelController.cs  MonoBehaviour on PauseScreen — navigation + per-mod content
   │  │  ├─ PanelChrome.cs      once-per-overlay construction; hands the controller its handles
   │  │  ├─ Rows.cs             one ConfigEntryBase → the native widget for its type, bound back
   │  │  ├─ OverlayContext.cs   overlay handles (Root / Group / ButtonTemplate) threaded to dialogs
   │  │  ├─ Tooltip.cs          hover panel + TooltipTrigger
   │  │  ├─ PromptButton.cs     prompt-styled button for an action the game has no binding for
   │  │  └─ UiText.cs           shared TMP-label + full-parent-stretch primitives
   │  └─ dialogs/            modal surfaces and the per-setting editors
   │     ├─ ModalDialog.cs      base for the build-your-own dialogs: singleton, dim shell, Escape
   │     ├─ ColorPicker.cs      ┐
   │     ├─ KeyCapture.cs       │ the four ModalDialog subclasses
   │     ├─ ListEditor.cs       │
   │     ├─ NumberEditor.cs     ┘
   │     ├─ TextPopupDialog.cs  fallback editor — drives the game's TextInputPopupScreen
   │     └─ Confirm.cs          yes/no via the game's GenericPopupScreen
   └─ core/                  the mod's own domain: discovery, config interpretation, diagnostics
      ├─ ModCatalog.cs       discovers loaded plugins with settings → ModEntry / SectionEntry
      ├─ SettingMetadata.cs  UI-free reading of a ConfigEntryBase (label, choices, range, display)
      ├─ Tags.cs             the optional ModNook.* ConfigDescription tag vocabulary
      ├─ ExampleSettings.cs  the mod's own always-on Examples config section
      └─ HierarchyDebug.cs   VerboseLogging dump of the pause/Settings hierarchies
```

**Enforced homes:**

- `src/game/` — live-game bridges, and Harmony patches other than the entry trio (the three that
  wire up `PanelController` stay in `Plugin.cs`, which installs them)
- `src/ui/` — the two ported drawing primitives ONLY (`PanelSprite`, `Palette`); see the note below
- `src/ui/chrome/` — the panel frame and its reusable furniture
- `src/ui/dialogs/` — modal surfaces and the per-setting editors
- `src/core/` — the mod's own domain logic, config interpretation, state and diagnostics
- `src/Plugin.cs` — BepInEx entry point; must sit beside the `.csproj`
- `scripts/` — repo tooling (git-hook installer, pre-commit)
- `pack.ps1` — workspace-synced packer; `../../tools/sync-mod-files.ps1` writes it at the repo root

New code goes in one of those; something that fits none of them is a signal the taxonomy is wrong,
not a licence for another folder.

**Why `ui/` is split and why two files stay above the split.** The panel is what this mod *is*, so
`ui/` reached 16 files and tripped the gate's flat-bucket cap of 12 — it is now `ui/chrome/` (the
frame and its reusable furniture) and `ui/dialogs/` (modal surfaces and per-setting editors), 7 each.

`PanelSprite.cs` and `Palette.cs` stay at `src/ui/` itself for **cross-mod path symmetry only**:
5 and 2 sibling mods respectively carry a port of the same file, each at `src/ui/<file>`, and keeping
this one there means a port or a diff between mods lines up on the same path. Be precise about what
that is and isn't — these are **ports that have since diverged**, not synced copies. Every copy of
`PanelSprite.cs` currently hashes differently, and `tools/sync-mod-files.ps1` syncs only
`pack.ps1` and `Directory.Build.props`; it has never touched a `.cs` file. So this is a convenience,
not an invariant, and it is fair to overrule if `ui/` root ever earns real content. If a third
non-port file lands at `src/ui/` root, it belongs in `chrome/` or `dialogs/` instead.

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
   │     └─ dialogs: ColorPicker · KeyCapture · ListEditor · NumberEditor · (text popup) · Confirm
   └─ chrome: PanelSprite · GameFonts · Palette · Tooltip · InputPrompt · PromptButton
```

## Components

| Component | Files | Responsibility | Depends on | Seam |
|---|---|---|---|---|
| **Entry & patches** | `Plugin.cs`, `core/ExampleSettings.cs` (106) | `ModNookPlugin` binds its own config (Display/Diagnostics) and installs 3 Harmony patches forwarding OnShow/OnHide/cancel to `PanelController`. `ExampleSettings` binds an always-on **Examples** section — one inert setting per render path — so Mod Nook's own page is a live gallery + manual-test surface. | PanelController | Add a patch / config entry / example |
| **Panel** | `ui/chrome/PanelController.cs` (590) | MonoBehaviour on the PauseScreen. Navigation state machine (open/close/back/show-mod/reset/persist) **+** per-mod content rendering. Delegates the once-per-overlay chrome construction to `PanelChrome` and keeps the handles it returns. | PanelChrome, Rows, ModCatalog, PauseMenu, Templates, InputPrompt, Tooltip, Confirm, PanelSprite, UiText | Panel navigation / content |
| **Panel chrome** | `ui/chrome/PanelChrome.cs` (619) | Builds the once-per-overlay chrome — overlay, cloned settings backdrop + header, panel plate, mods/detail body with scrollers, footer — and returns the handles the controller drives (`Overlay`/`Context`/`Content`/`Sidebar`/`Title`/`ResetButton`/`UsingGamePrompt`). Construction only; the footer's Reset/Close call back through actions. | PauseMenu, Templates, PanelSprite, Tooltip, InputPrompt, PromptButton, OverlayContext, UiText, game screens | Panel layout / chrome |
| **Row factory** | `ui/chrome/Rows.cs` (565) | Maps one `ConfigEntryBase` → the native widget for its type and binds it back **+** routes text settings to the right editor (colour/list/key/text popup) **+** builds the info icon. Takes an explicit `OverlayContext` for the dialogs it routes to — no shared statics. | Templates, dialogs, TextPopupDialog, SettingMetadata, OverlayContext, Tooltip, PanelSprite, GameFonts, Tags, Palette | Add a setting-type → widget mapping |
| **Setting metadata** | `core/SettingMetadata.cs` (276) | Pure, UI-free reading of a `ConfigEntryBase`: label (camel-case humanise + `ModNook.Label`), explicit/prose choices, numeric range, display summary, slider step. No UI, no shared state — reusable by rows and dialogs. | Tags | Label / choice / range rules |
| **Modal dialogs** | `ui/dialogs/ModalDialog.cs` (194), `ui/dialogs/ColorPicker.cs` (493), `ui/dialogs/KeyCapture.cs` (256), `ui/dialogs/ListEditor.cs` (254), `ui/dialogs/NumberEditor.cs` (267) | `ModalDialog` base owns the one-at-a-time singleton lifecycle, the dim+centered-panel shell (`BuildShell`), a shared `ButtonRow`, Escape-close and the register-before-`Build` contract; the four subclasses (hex colour, key binding, comma-list, unbounded number) implement only their own `Build`. They no longer read anything off `Rows` — parent/template/overlay group arrive as call args from the `OverlayContext` — and build labels through the shared `UiText.NewText`. | ModalDialog, Templates, PanelSprite, UiText, Palette, SettingMetadata, TextPopupDialog | A new editor kind (subclass `ModalDialog`) |
| **Native popup adapters** | `ui/dialogs/TextPopupDialog.cs` (200), `ui/dialogs/Confirm.cs` (92), `game/PopupEscape.cs` (52) | `TextPopupDialog` opens the game's `TextInputPopupScreen` for free-form/text settings, borrowing and restoring the overlay's raycast blocker and the popup's "Name:" prefix; `Confirm` drives `GenericPopupScreen` for reset; `PopupEscape` arms Escape on the popup. Distinct shape from the custom build-your-own dialogs. | OverlayContext.Group, SettingMetadata, game screens | Text-popup borrow/restore |
| **Widget templating** | `game/Templates.cs` (546), `game/BatWingFitter.cs` (89) | Find & cache the game's Cycle/Slider/Toggle/Button templates; clone; sanitize a clone (strip localization/decorations/hover-select/colour-freeze/bat-wings); relabel. `BatWingFitter` repositions wing ornaments a frame after layout. | game UI assemblies | Adjust how cloned widgets are tamed |
| **Pause-menu integration** | `game/PauseMenu.cs` (204) | Source the pause button template, add ours, grow the pause panel to fit. | game PauseScreen | Button placement / fit |
| **Diagnostics** | `core/HierarchyDebug.cs` (88) | A one-off `VerboseLogging` dump of the pause + Settings screen hierarchies — the scaffolding the panel resize and the header/prompt clones were built against. Off unless `Diagnostics.VerboseLogging`. | game screens | Hierarchy logging |
| **Catalog** | `core/ModCatalog.cs` (126), `core/Tags.cs` (51) | Discover loaded plugins that expose settings; group into `ModEntry`/`SectionEntry`; honour `ModNook.Hidden`. `Tags` reads the optional `ModNook.*` description tags. | BepInEx Chainloader | Discovery / tag vocabulary |
| **Drawing & theming** | `ui/PanelSprite.cs` (182), `game/GameFonts.cs` (178), `ui/Palette.cs` (11), `ui/chrome/Tooltip.cs` (184), `ui/chrome/UiText.cs` (40) | Procedural sliced/circle sprites; find & apply the game font; shared colours; hover tooltip + `TooltipTrigger`; `UiText` is the shared TMP-label + full-parent-stretch pair used by the panel chrome and content. | game UI/TMP | Look & feel primitives |
| **Corner prompt** | `game/InputPrompt.cs` (144), `ui/chrome/PromptButton.cs` (128) | Register/withdraw the game's real corner **Close** prompt (drawing the player's bound key cap); build a prompt-style button when the real one is unavailable. | game input screens | Prompt bar |

No file is currently over the ~800-line God-file cap. (⚠️ would mark one; see **Structural debt**.)

## Key flows

- **Open:** `PauseScreen.OnShow` → `Attach` adds the pause button (once) and, on click, `Open` →
  `EnsureOverlay` builds the overlay/backdrop/header/body once → `ModCatalog.Discover` → `ShowModList`
  fills the sidebar and opens the first mod → `ShowMod` builds a row per `ConfigEntry` via `Rows`.
- **Edit:** a widget's `OnValueChanged` writes `entry.BoxedValue` (or `SetSerializedValue`) and calls
  `Persist(mod)` → `mod.Config.Save()`. No separate Save button; the mod's own file is written.
- **Cancel/back:** the `ProcessContinueInput` patch routes cancel to `RequestBack`, which closes an
  open dialog first, else closes the panel. Lets gamepad B dismiss dialogs that have no Escape key.

## Conventions

- Source foldered by responsibility (`src/game/`, `src/ui/`, `src/core/`) with `Plugin.cs` at
  `src/` root — see **Layout**. Folders never affect namespaces: one flat namespace `ModNook`
  everywhere, `internal` by default, and no `using` changes when a file moves.
- Version is single-sourced from `<Version>` in `src/ModNook.csproj` → `ModBuildInfo.Version` via the
  `GenerateModBuildInfo` target in `Directory.Build.props`. Never hardcode a version in `Plugin.cs`.
- `Directory.Build.props` and `pack.ps1` are **workspace-synced canonicals** — do not edit here; they
  come from `../../tools/sync-mod-files.ps1`.
- Build: `dotnet build src/ModNook.csproj -c Release`. Pack: `.\pack.ps1` → `dist/ModNook-<ver>.zip`.

## Where to find things

- A setting renders wrong / new setting type → `ui/chrome/Rows.cs` (`Build` dispatch) + `game/Templates.cs`.
- Panel layout / sidebar / scrolling / header / footer → `ui/chrome/PanelChrome.cs` (`BuildBody`/`BuildScroller`).
- Panel navigation / per-mod content → `ui/chrome/PanelController.cs` (`ShowMod`/`AddModButton`/`AddHeading`).
- A cloned widget looks off (colour, wings, hover) → `game/Templates.cs` sanitizers.
- Which mods/settings appear → `core/ModCatalog.cs`; tag behaviour → `core/Tags.cs`.
- Cancel/Escape/gamepad behaviour → `Plugin.cs` patch + `PanelController.RequestBack`.

## Structural debt

Documented by the full review of 2026-08-22 (componentization + abstraction lenses + Codex).
Two safe relocations were fixed then (`Palette`, `Tags` → own files); the rest is backlogged in
[docs/BACKLOG.md](docs/BACKLOG.md). None is a correctness bug; all are shape.

- **P1 · `ui/chrome/Rows.cs` responsibility split — done (2026-08-22).** Extracted the pure metadata parsing
  → `core/SettingMetadata.cs` and the game-text-popup borrow/restore dance
  (`Prompt`/`Edit`/`Brief`/`HidePrefix`/`SuspendOverlay`/`RestoreOn`) → `ui/dialogs/TextPopupDialog.cs`. `ui/chrome/Rows.cs`
  went 997 → 559 lines and is now a focused widget-dispatch + row-chassis class.
- **P1 · Overlay back-channel — done (2026-08-22).** The public mutable statics `Rows.OverlayRoot`/
  `OverlayGroup`/`ButtonTemplate` are gone. `PanelController` builds one `OverlayContext`
  (`src/ui/chrome/OverlayContext.cs`) and threads it into `Rows.Build`/`BuildText`, which pass it on to the
  dialogs; `TextPopupDialog` and `Confirm.Ask` now take the overlay `CanvasGroup` as a parameter. No
  code reaches back into `Rows` for overlay state any more.
- **P1 · Modal-dialog abstraction — done (2026-08-22).** `ColorPicker`/`KeyCapture`/`ListEditor` now
  subclass `ModalDialog` (`src/ui/dialogs/ModalDialog.cs`), which owns the singleton lifecycle, the dim/panel
  shell (`BuildShell`) and Escape-close, and fixes the assign-before-`Build` contract in one place —
  two of the three subclasses used to assign it *after* `Build`, leaving an un-closeable half-built
  dialog if the build threw. `Confirm` stays out (it wraps a native popup).
- **P1 · Dialog close/back registry — done (2026-08-22, folded into the modal abstraction).** A single
  `ModalDialog.current` replaced the three per-type statics; `PanelController.RequestBack`/`Close` now
  call `ModalDialog.CloseCurrent()`, so a new dialog kind is closeable the moment it subclasses
  `ModalDialog` — no third place to update.
- **P2 · `ui/chrome/PanelController.cs` God-file — done (2026-08-22).** The once-per-overlay construction block
  (`EnsureOverlay`…`BuildScroller`) moved to a `PanelChrome` builder (`src/ui/chrome/PanelChrome.cs`), which
  returns the handles the controller drives; `PanelController` dropped 1136 → 590 lines, back under the
  cap. Navigation, catalog selection, reset/persist and per-mod rendering stay in the controller;
  header/footer/scroller were **not** fragmented into micro-files (reviewer + Codex agreed that's churn).
- **P2 · Duplicated UI primitives — done (2026-08-22).** `NewText` + `Stretch` live in `src/ui/chrome/UiText.cs`,
  and the dialogs' three near-identical private `Text(...)` builders were deleted — `ColorPicker`/
  `KeyCapture`/`ListEditor` now call `UiText.NewText` too. The wrap-on inconsistency is gone (one
  builder, uniform TMP default wrapping), which also dropped two obsolete-`enableWordWrapping` warnings.
- **P2 · `PauseMenu.DumpHierarchy`/`Describe` — done (2026-08-22).** The ~70 lines of `VerboseLogging`
  hierarchy tooling moved out of the fit/layout class into `src/core/HierarchyDebug.cs` (`HierarchyDebug.Dump`);
  `PauseMenu` is now purely button-sourcing + panel-fit (280 → 204 lines).
- **Not debt (checked):** `game/Templates.cs` (546) is coherent — one job, many small tools; leave whole.
  `ModEntry`/`SectionEntry` are fine colocated with `ModCatalog`. Setting-type dispatch in `Rows.Build`
  should stay explicit (no `ISettingRowProvider` registry — premature). `Confirm` should not join the
  modal base. Harmony patch classes / `Tooltip`+`TooltipTrigger` are fine paired in one file.

_Living doc — refresh with /project-docs when it drifts._
