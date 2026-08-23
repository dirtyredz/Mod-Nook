# BACKLOG — Mod Nook

Prioritized trough. P0 = do first, P1 = should, P2 = nice-to-have. Structural items come from the
full review of 2026-08-22 (see [../STRUCTURE.md](../STRUCTURE.md#structural-debt)); none is a
correctness bug — all are shape or capability.

## Structural (from the 2026-08-22 review)

Fixed so far (compile-verified): `Palette` → `src/Palette.cs` and `Tags` → `src/Tags.cs` (relocations);
`SettingMetadata` and `TextPopupDialog` both extracted from `Rows.cs` — `Rows.cs` is now 550 lines and
its responsibility split is complete; the **modal-dialog abstraction + close/back registry**
(`src/ModalDialog.cs`, the three dialogs now subclass it); and the **overlay-context threading**
(`src/OverlayContext.cs`, the static back-channel is gone); and the **`PanelChrome` split** — the
once-per-overlay construction moved to `src/PanelChrome.cs`, dropping `PanelController` 1136 → 590
lines, with the shared `NewText`/`Stretch` primitives lifted into `src/UiText.cs` (all 2026-08-22, see
below). What remains structurally is finishing the UI-primitive dedupe (the dialogs' own `Text(...)`)
and the optional `PauseMenu` debug-helper move. Anything touching dialog/overlay control flow wants an
in-game play-test on top of the build.

- [x] **P1 — Extract `SettingMetadata` from `Rows.cs`.** _Done 2026-08-22._ Moved the pure, UI-free
  metadata parsing (`Label`/`Humanise`/`ExplicitChoices`/`DescriptionChoices`/`SentenceContaining`/
  `TryRange`/`Summarise`/`IsNumeric`/`IsIntegral`/`NiceStep`) into `src/SettingMetadata.cs`; the three
  dialog call sites now use `SettingMetadata.Label` instead of `Rows.LabelOf`. Brought `Rows.cs` from
  997 → 729 lines, under the God-file cap. Build verified.
- [x] **P1 — Extract `TextPopupDialog` from `Rows.cs`.** _Done 2026-08-22._ Moved the game-text-popup
  borrow/restore dance (`Prompt`/`Edit`/`Brief`/`HidePrefix`/`SuspendOverlay`/`RestoreOn`/`PrefixField`/
  `TextPopup`) into `src/TextPopupDialog.cs`; `Rows.BuildText` and `ListEditor` call it now. `Rows.cs`
  is down to 559 lines. The `OverlayGroup` suspension state was dissolved by the overlay-context item
  below. Build verified — this completes the `Rows.cs` responsibility split.
- [x] **P1 — Replace the `Rows` static back-channel.** _Done 2026-08-22; play-tested._ Added
  `src/OverlayContext.cs` (`Root`/`Group`/`ButtonTemplate`). `PanelController` builds one and threads
  it into `Rows.Build`/`BuildText`; `Rows` passes `Root`/`ButtonTemplate` on to the dialogs and the
  overlay `Group` to `TextPopupDialog.Edit`, `ListEditor.Open` and `Confirm.Ask`. The public mutable
  statics `Rows.OverlayRoot`/`OverlayGroup`/`ButtonTemplate` are deleted — no code reaches back into
  `Rows` for overlay state. This dissolves the last coupling left by the `TextPopupDialog` extraction.
- [x] **P1 — Introduce a modal-dialog abstraction.** _Done 2026-08-22._ Added `src/ModalDialog.cs`, an
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
  once-per-overlay construction block (`EnsureOverlay`…`BuildScroller`) into `src/PanelChrome.cs`, a
  builder that returns the handles the controller drives (`Overlay`/`Context`/`Content`/`Sidebar`/
  `Title`/`ResetButton`/`UsingGamePrompt`); the footer's Reset/Close call back through actions.
  Navigation, catalog selection, reset/persist and per-mod rendering stay in the controller, which
  dropped 1136 → 590 lines (under the cap). Header/footer/scroller were **not** fragmented into
  micro-files.
- [ ] **P2 — Dedupe UI primitives.** _Partly done 2026-08-22:_ the panel's `NewText` + `Stretch` moved
  to `src/UiText.cs` (shared by `PanelChrome` + `PanelController`). Still open: the dialogs' own
  near-identical `Text(...)` TMP builder (written 3× across `ColorPicker`/`KeyCapture`/`ListEditor`)
  could fold into `UiText`. Resolve the wrap-on inconsistency between the copies while doing it.
- [ ] **P2 — Move `PauseMenu.DumpHierarchy`/`Describe`** (~70 lines of debug tooling) out of the
  fit/layout class into a `HierarchyDebug` helper. Optional.

## Capability (from README "Known limits")

- [ ] **P2 — Two-field rows for `Name=Value` pair lists** (`Area scales`-style) — a label + a slider
  for the number, instead of editing the whole `Name=Value` string.
- [ ] **P2 — Better unbounded-number handling** — numbers with no `AcceptableValueRange` fall back to a
  text field; consider an inferred range or a numeric popup.
- [ ] **P2 — Optional per-setting reset / undo** — reset is per-mod only and destructive once written.

## Release hygiene

- [ ] **P1 — Validate the Phase-2 WIP in-game before publishing** (gamepad cancel, Proton fix,
  long-name overflow, prose parser) per [../TESTING.md](../TESTING.md); then bump `<Version>` and pack.
- [ ] **P2 — Robustness of the cancel hook** — it depends on the game's action ids (33, 21); a game
  update could change them. Failure is logged and non-fatal, but worth a periodic re-check.

_Living doc — refresh with /project-docs when it drifts._
