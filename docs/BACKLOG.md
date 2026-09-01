# BACKLOG — Mod Nook

Prioritized trough. P0 = do first, P1 = should, P2 = nice-to-have. Structural items come from the
full review of 2026-08-22 (see [../STRUCTURE.md](../STRUCTURE.md#structural-debt)); none is a
correctness bug — all are shape or capability.

## Structural (from the 2026-08-22 review)

Fixed so far (compile-verified): `Palette` → `src/ui/Palette.cs` and `Tags` → `src/core/Tags.cs` (relocations);
`SettingMetadata` and `TextPopupDialog` both extracted from `Rows.cs` — `Rows.cs` is now 550 lines and
its responsibility split is complete; the **modal-dialog abstraction + close/back registry**
(`src/ui/dialogs/ModalDialog.cs`, the three dialogs now subclass it); and the **overlay-context threading**
(`src/ui/chrome/OverlayContext.cs`, the static back-channel is gone); and the **`PanelChrome` split** — the
once-per-overlay construction moved to `src/ui/chrome/PanelChrome.cs`, dropping `PanelController` 1136 → 590
lines, with the shared `NewText`/`Stretch` primitives lifted into `src/ui/chrome/UiText.cs` (all 2026-08-22, see
below); and the **UI-primitive dedupe** (`src/ui/chrome/UiText.cs`, the dialogs' `Text(...)` folded in). The only
structural item left is the optional `PauseMenu` debug-helper move. Anything touching dialog/overlay
control flow wants an in-game play-test on top of the build.

- [x] **P1 — Extract `SettingMetadata` from `Rows.cs`.** _Done 2026-08-22._ Moved the pure, UI-free
  metadata parsing (`Label`/`Humanise`/`ExplicitChoices`/`DescriptionChoices`/`SentenceContaining`/
  `TryRange`/`Summarise`/`IsNumeric`/`IsIntegral`/`NiceStep`) into `src/core/SettingMetadata.cs`; the three
  dialog call sites now use `SettingMetadata.Label` instead of `Rows.LabelOf`. Brought `Rows.cs` from
  997 → 729 lines, under the God-file cap. Build verified.
- [x] **P1 — Extract `TextPopupDialog` from `Rows.cs`.** _Done 2026-08-22._ Moved the game-text-popup
  borrow/restore dance (`Prompt`/`Edit`/`Brief`/`HidePrefix`/`SuspendOverlay`/`RestoreOn`/`PrefixField`/
  `TextPopup`) into `src/ui/dialogs/TextPopupDialog.cs`; `Rows.BuildText` and `ListEditor` call it now. `Rows.cs`
  is down to 559 lines. The `OverlayGroup` suspension state was dissolved by the overlay-context item
  below. Build verified — this completes the `Rows.cs` responsibility split.
- [x] **P1 — Replace the `Rows` static back-channel.** _Done 2026-08-22; play-tested._ Added
  `src/ui/chrome/OverlayContext.cs` (`Root`/`Group`/`ButtonTemplate`). `PanelController` builds one and threads
  it into `Rows.Build`/`BuildText`; `Rows` passes `Root`/`ButtonTemplate` on to the dialogs and the
  overlay `Group` to `TextPopupDialog.Edit`, `ListEditor.Open` and `Confirm.Ask`. The public mutable
  statics `Rows.OverlayRoot`/`OverlayGroup`/`ButtonTemplate` are deleted — no code reaches back into
  `Rows` for overlay state. This dissolves the last coupling left by the `TextPopupDialog` extraction.
- [x] **P1 — Introduce a modal-dialog abstraction.** _Done 2026-08-22._ Added `src/ui/dialogs/ModalDialog.cs`, an
  `abstract ModalDialog : MonoBehaviour` base that owns the one-at-a-time singleton lifecycle, the
  dim+centered-panel shell (`BuildShell(width, padding, spacing, …)`), Escape-close, and the
  register-before-`Build` contract. `ColorPicker`/`KeyCapture`/`ListEditor` now subclass it and
  implement only `Build`; each shed ~70–85 lines. Fixed a latent bug: `KeyCapture` and `ListEditor`
  used to assign the singleton *after* `Build`, so a build that threw left an un-closeable half-built
  dialog — now assigned once, before `Build`, in the base. `Confirm` stays out (native popup). Build
  verified; wants an in-game play-test of the three dialogs.
- [x] **P1 — Dialog registry for close/back.** _Done 2026-08-22 (folded into the modal abstraction)._
  A single `ModalDialog.current` replaced the three per-type statics; `PanelController.RequestBack`
  and `Close` now call `ModalDialog.CloseCurrent()`/read `ModalDialog.IsAnyOpen` instead of three
  hardcoded `if (X.IsOpen)` checks. A new dialog kind is closeable the moment it subclasses
  `ModalDialog` — no third list to update.
- [x] **P2 — Coarse-split `PanelController.cs`.** _Done 2026-08-22; play-tested._ Moved the
  once-per-overlay construction block (`EnsureOverlay`…`BuildScroller`) into `src/ui/chrome/PanelChrome.cs`, a
  builder that returns the handles the controller drives (`Overlay`/`Context`/`Content`/`Sidebar`/
  `Title`/`ResetButton`/`UsingGamePrompt`); the footer's Reset/Close call back through actions.
  Navigation, catalog selection, reset/persist and per-mod rendering stay in the controller, which
  dropped 1136 → 590 lines (under the cap). Header/footer/scroller were **not** fragmented into
  micro-files.
- [x] **P2 — Dedupe UI primitives.** _Done 2026-08-22._ `NewText` + `Stretch` live in `src/ui/chrome/UiText.cs`;
  the dialogs' three near-identical private `Text(...)` builders were deleted and `ColorPicker`/
  `KeyCapture`/`ListEditor` now call `UiText.NewText`. Every panel/dialog label goes through one
  builder, so the wrap-on inconsistency is gone (uniform TMP default wrapping) — which also removed two
  obsolete-`enableWordWrapping` warnings. Build verified.
- [x] **P2 — Move `PauseMenu.DumpHierarchy`/`Describe`.** _Done 2026-08-22._ The ~70 lines of
  `VerboseLogging` hierarchy tooling moved into `src/core/HierarchyDebug.cs` (`HierarchyDebug.Dump`); the
  caller in `PanelController` was repointed, and `PauseMenu` is now purely button-sourcing + panel-fit
  (280 → 204 lines). This clears the last documented structural item from the 2026-08-22 review.
- [ ] **P3 — Row summary shows large floats in scientific notation.** `SettingMetadata.Summarise`
  uses `BoxedValue.ToString()`, so a big `float`/`double` renders as e.g. `1.234568E+08` in the row's
  value label. Pre-existing; surfaced by the Examples "Growth rate". Format float/double without the
  exponent (and invariant). Cosmetic — the stored value is correct and round-trips.
- [ ] **P3 — Fold the dialogs' button rows onto `ModalDialog.ButtonRow`.** A shared `ButtonRow` helper
  now lives on `ModalDialog` and `NumberEditor` uses it, but `ColorPicker`/`KeyCapture`/`ListEditor`
  still build their own near-identical button-row block (spacing 20, height 72). Convert the three to
  the helper. Low value; only re-touch when next editing those dialogs.

## Capability (from README "Known limits")

- [ ] **P2 — Two-field rows for `Name=Value` pair lists** (`Area scales`-style) — a label + a slider
  for the number, instead of editing the whole `Name=Value` string.
- [x] **P2 — Better unbounded-number handling.** _Done 2026-08-22 (pending in-game test)._ A number with
  no `AcceptableValueRange` now opens `NumberEditor` (`src/ui/dialogs/NumberEditor.cs`, a `ModalDialog`) instead of
  the raw text popup: ±fine/±coarse nudge buttons (step scaled to the value's magnitude), a Type…
  direct-entry path, clamped to the numeric type's own limits, saved via `SetSerializedValue`. Chose a
  dialog over an inferred-range slider (no invented bounds) — see DECISIONS.
- [ ] **P2 — Optional per-setting reset / undo** — reset is per-mod only and destructive once written.

## Release hygiene

- [ ] **P1 — Validate the Phase-2 WIP in-game before publishing** (gamepad cancel, Proton fix,
  long-name overflow, prose parser) per [../TESTING.md](../TESTING.md); then bump `<Version>` and pack.
- [ ] **P2 — Robustness of the cancel hook** — it depends on the game's action ids (33, 21); a game
  update could change them. Failure is logged and non-fatal, but worth a periodic re-check.

_Living doc — refresh with /project-docs when it drifts._

## Placement follow-ups (from the 2026-09-01 structure review)

Raised by the review of the `src/` regrouping. All are placement opinions, not defects — the build is
green and nothing is broken. Recorded rather than acted on, so the call stays yours.

- **P2 — `core/HierarchyDebug.cs` may belong in `game/`.** It is the only `core/` file that
  references game types: its one method takes a `PauseScreen` and walks `SettingsMenuScreen` /
  `SettingsGameplayScreen` via `Chicken.UI`, and its only caller is `ui/chrome/PanelController.cs`.
  Every other `core/` file has zero game/ui references. Either move it, or narrow the `core/`
  charter so "diagnostics" stops licensing game-internals readers.
- **P2 — `ui/dialogs/TextPopupDialog.cs` and `ui/dialogs/Confirm.cs` are native-popup adapters, not
  drawn UI.** Both merely locate and drive a game screen (`TextInputPopupScreen`,
  `GenericPopupScreen`); neither builds any UI. That is the same category as `game/PopupEscape.cs`,
  which arms Escape on the very same popup — yet the three are split across two folders. STRUCTURE.md's
  own component table already groups all three as one component. Either move these two to `game/`, or
  move `PopupEscape.cs` into `dialogs/` — currently the same test gives different answers.
