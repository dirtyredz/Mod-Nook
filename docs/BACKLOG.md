# BACKLOG — Mod Nook

Prioritized trough. P0 = do first, P1 = should, P2 = nice-to-have. Structural items come from the
full review of 2026-08-22 (see [../STRUCTURE.md](../STRUCTURE.md#structural-debt)); none is a
correctness bug — all are shape or capability.

## Structural (from the 2026-08-22 review)

Fixed in that review (safe, compile-verified relocations): `Palette` → `src/Palette.cs`,
`Tags` → `src/Tags.cs`. Everything below was deliberately backlogged as it changes call sites or
control flow and wants its own reviewed, in-game-tested pass.

- [ ] **P1 — Split `Rows.cs` (997 lines).** Extract the pure, UI-free metadata parsing
  (`Label`/`Humanise`/`ExplicitChoices`/`DescriptionChoices`/`SentenceContaining`/`TryRange`/
  `Summarise`/`IsNumeric`/`IsIntegral`/`NiceStep`) into `SettingMetadata`; extract the text-popup
  borrow/restore dance (`Prompt`/`Edit`/`HidePrefix`/`SuspendOverlay`/`RestoreOn`) into
  `TextPopupDialog`. Keep the widget dispatch (`Build`/`BuildBool`/…) in `Rows`. Lowest-risk win:
  `SettingMetadata` (no UI, no shared state).
- [ ] **P1 — Replace the `Rows` static back-channel.** `OverlayRoot`/`OverlayGroup`/`ButtonTemplate`
  are public mutable statics set by `PanelController` and read across row/dialog code; `Confirm`
  reaches straight into `Rows.OverlayGroup`. Thread an explicit `OverlayContext`/`PanelUiContext`
  into `Rows.Build`/`BuildText`/`Prompt` and `Confirm.Ask` instead.
- [ ] **P1 — Introduce a modal-dialog abstraction.** `ColorPicker`/`KeyCapture`/`ListEditor` duplicate
  the singleton lifecycle (`static open`/`IsOpen`/`Open`/`CloseAny`), the dim+panel scaffold, and
  Escape-close — and the assign-`open`-before-`Build` ordering is a correctness contract copied 3×.
  Extract a `ModalDialog` base or a compositional `PanelModalHost` + `ModalShell` builder. **Keep
  `Confirm` out** — it wraps the game's native popup (different shape).
- [ ] **P1 — Dialog registry for close/back.** `PanelController.RequestBack` and `Close` hardcode the
  same three concrete dialog types; a 4th must be added to both or it becomes un-closeable. Have
  dialogs self-register (folds into the modal abstraction) so the panel closes "whatever is open".
- [ ] **P2 — Coarse-split `PanelController.cs` (1133 lines).** Extract the once-per-overlay
  construction block (`EnsureOverlay`…`BuildScroller`) into a `PanelChrome` builder; keep navigation,
  catalog selection, reset/persist, and dynamic rendering in the controller. Do **not** fragment
  header/footer/scroller/heading into micro-files (reviewers + Codex agree that's churn).
- [ ] **P2 — Dedupe UI primitives.** One `UiText.New(...)` TMP builder (currently written 5×) and one
  shared `Stretch(RectTransform)` (2 named + 5 inline copies), in a small `UiPrimitives`/`UiText`
  helper. Resolve the wrap-on inconsistency between the copies while doing it.
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
