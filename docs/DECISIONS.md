# DECISIONS — Mod Nook

Decisions worth not re-litigating. Newest first. Rationale is drawn from the code, README, and git
history; where a rationale is inferred rather than recorded, it says so.

## 2026-08-22 — Ship a built-in Examples section as a live gallery + test surface

`ExampleSettings.Bind` adds an always-on **Examples** section to Mod Nook's own config: one inert
setting per render path (toggle, cycle, both slider bounds, the number editor, key capture, colour,
list, text popup, prose choices, and each `ModNook.*` tag incl. a `Hidden` one that must not appear).
Because Mod Nook lists its own config like any other mod, this shows on its own page. **Why:** the
render paths were previously only testable if some installed mod happened to exercise them (e.g. an
unbounded number needs a mod that binds one) — this makes every path reachable in one place, always,
and doubles as authors' reference for what a given `Config.Bind`/tag produces. **Always-on, not
gated:** chosen deliberately (over a toggle or a debug-only build) so the gallery is a permanent,
zero-setup reference; the values are inert and confined to one clearly-labelled section. Lives in its
own file, not `Plugin.cs`, to keep the entry point to binding-its-own-config + patches.

## 2026-08-22 — Unbounded numbers get a nudge-and-type dialog, not an inferred range

A numeric setting with no `AcceptableValueRange` used to fall through to the free-form text popup.
Added `NumberEditor` (`src/ui/dialogs/NumberEditor.cs`, a `ModalDialog` subclass): a value display with ±fine/
±coarse nudge buttons (step scaled once to the value's magnitude — 2.5 nudges by 0.1, 5000 by 100), a
Type… path via the game popup for far-off values, clamped to the numeric type's own min/max, saved as
an invariant string through `SetSerializedValue`. **The working value is a `decimal`, and nothing is
written unless it's changed** — chosen after review caught that a `double` working value silently
corrupted values (a `long` past 2^53, a `double` past six decimals, or a near-max `long`/`ulong` that
`double` rounds out of range). `decimal` represents every integral type exactly and carries more
precision than any `float`/`double` setting holds; the edited-guard means opening the editor and
pressing Save leaves the setting byte-for-byte unchanged, even one too large to load into the editor. **Why a dialog, not an inferred-range slider:** a
guessed 0…N ceiling is wrong the moment a value is meant to exceed it, and a slider can't represent
negatives or very large numbers; nudge-and-type is honest about having no bounds while still beating a
raw text box. **Why not just a numeric-validated popup:** it adds nothing over today beyond rejecting
non-numbers (which `SetSerializedValue` already does) — no stepping. Reuses the `ModalDialog` base and
routes from `Rows.BuildText` (via `NumberEditor.Suits`) ahead of the text-popup fallback. `Confirm`
and the bounded-slider path are untouched.

## 2026-08-22 — Split `PanelChrome` out of `PanelController`; share `UiText`

Moved the once-per-overlay chrome construction (`EnsureOverlay` and its `AddBackdrop`/`BuildPanel`/
`BuildHeader`/`CloneSettingsHeader`/`BuildFooter`/`BuildBody`/`BuildScroller` helpers) into a new
`src/ui/chrome/PanelChrome.cs` builder. `PanelChrome.Build(pauseScreen, onClose, onReset)` builds everything and
returns the handles the controller drives (`Overlay`/`Context`/`Content`/`Sidebar`/`Title`/
`ResetButton`/`UsingGamePrompt`); `PanelController.EnsureOverlay` shrank to delegating + unpacking those
into its existing fields. `PanelController` went 1136 → 590 lines, back under the ~800 God-file cap.
**Why:** it was the last God-file, mixing three concerns (navigation, chrome construction, content
rendering); construction is the self-contained, once-per-overlay concern and lifts out cleanly.
**Chosen shape:** a builder that returns handles, with the controller keeping its fields — smallest
blast radius, so the navigation/content code is untouched; the footer's Reset/Close call back through
`Action`s rather than the chrome knowing the controller. **Rejected:** fragmenting header/footer/
scroller/heading into their own micro-files — the prior full review + Codex agreed that's churn, not
clarity; and pushing the whole `OverlayContext` into every dialog (kept from the overlay-context ADR).
The two shared UI primitives `NewText` + `Stretch` moved to `src/ui/chrome/UiText.cs` (a neutral home both the
chrome and the controller's content use), which also starts the separate UI-primitive dedupe. No
behaviour change; Release build verified and play-tested (open, mod list, per-mod pages, scrolling,
dialogs, reset, Close/Esc).

## 2026-08-22 — Thread an explicit `OverlayContext`; drop the `Rows` overlay statics

Replaced the public mutable statics `Rows.OverlayRoot`/`OverlayGroup`/`ButtonTemplate` with a small
`OverlayContext` (`src/ui/chrome/OverlayContext.cs`) that `PanelController` builds once per overlay and threads
into `Rows.Build`/`BuildText`; `Rows` hands `Root`/`ButtonTemplate` to the colour/key/list dialogs and
the overlay `Group` to `TextPopupDialog`, `ListEditor` and `Confirm.Ask`. **Why:** the statics were a
back-channel — set in one class, read deep in three others — that hid the panel→dialog dependency and
left shared mutable state; an explicit parameter makes the dependency visible and removes the coupling.
**Chosen shape:** thread the context into `Rows` and pass primitives onward, rather than into every
dialog `Open` — this keeps `ColorPicker`/`KeyCapture` signatures unchanged (only `ListEditor` gained
the overlay `Group`, which it needs for its add-entry popup). No behaviour change; Release build
verified and play-tested (colour/key/list dialogs, the text popup, and the reset confirmation).

## 2026-08-22 — Extract `TextPopupDialog` from `Rows`

Moved the game-text-popup borrow/restore plumbing (`Prompt`/`Edit`/`Brief`/`HidePrefix`/
`SuspendOverlay`/`RestoreOn`/`TextPopup`) out of `Rows.cs` into `TextPopupDialog.cs`, completing the
`Rows` responsibility split (997 → 559 lines; now widget-dispatch + row-chassis only). **Why:** opening
and taming the game's `TextInputPopupScreen` is a self-contained concern distinct from building rows.
**Rejected:** moving the shared overlay statics (`OverlayRoot`/`OverlayGroup`/`ButtonTemplate`) in the
same pass — they're used by the colour/key/list paths too, so relocating them is the separate
overlay-context change. `TextPopupDialog` therefore still read `Rows.OverlayGroup` at the time of this
extraction — a coupling resolved the same day by the overlay-context change above. No behaviour change;
Release build verified.

## 2026-08-22 — Extract `SettingMetadata` from `Rows`

Moved the pure, UI-free config-metadata reading (label / humanise / explicit & prose choices / range /
summary / slider step) out of `Rows.cs` into `SettingMetadata.cs`. **Why:** `Rows` was a God-file (997
lines) conflating widget construction with metadata interpretation; the parsing touches no
`GameObject`/`Transform`, so it's the cleanest seam. The move brought `Rows` to 729 lines — under the
800-line cap — with no behaviour change (Release build verified). Dialogs now call
`SettingMetadata.Label` directly instead of the removed `Rows.LabelOf`. **Rejected:** splitting the
text-popup plumbing in the same pass (deferred so each extraction is independently reviewable); a
per-setting-type strategy/registry (premature for a 6-branch dispatch).

## 2026-08-22 — Two safe structural extractions; the rest backlogged

Moved `Palette` (out of `Rows.cs`) and `Tags` (out of `ModCatalog.cs`) into their own files.
**Why:** both are shared helpers used across many files but were buried at the bottom of an unrelated
one; relocating within the same namespace is a zero-risk, compile-verified move. **Rejected:** the
larger splits surfaced by the full review (God-file decomposition, a modal-dialog base, an overlay
context) — deferred to [BACKLOG.md](BACKLOG.md) because they change call sites or control flow and
deserve their own reviewed pass, not a drive-by edit.

## 2026-08 — Sidebar layout replaces the list→page navigation

Mods run down a left sidebar with the selected mod's settings filling the right, instead of a mod-list
page you enter and back out of. **Why:** both are always on screen, comparing two mods costs one click
not three, and the header stops changing under the player. **Rejected:** the earlier list-then-detail
page model. (Commit `af72d3a`.)

## 2026-08 — Persist immediately; no Save button

Every edit writes through to the mod's config file at once via `mod.Config.Save()`. **Why:** a panel
that needs a separate Save is one people lose work in, and BepInEx already writes the whole file
atomically. **Rejected:** a staged/apply model. **Cost:** no undo — reset is per-mod and behind a
confirmation because the previous values are gone once written.

## 2026-08 — Clone the game's own widgets rather than redraw them

Every control is cloned from a live game instance and sanitized, not rebuilt in the game's likeness.
**Why:** a cloned `CycleButton`/`SliderButton`/`AnimatedToggle` inherits the correct font, anchors,
colours, and controller navigation for free, and cannot drift when the game updates. **Rejected:**
hand-drawn look-alikes (font/scale/nav would drift). See `10`/`16`/`17` workspace guides. **Cost:** a
sanitize step (strip localization, screen components, hover-select, colour animation, bat-wings).

## 2026-08 — One-directional discovery; a separate mod, not a fork of Mod Menu

Mod Nook reads other plugins' `ConfigEntry` metadata; nothing references Mod Nook. **Why:** a mod
gets a page for free and keeps working when Mod Nook is absent; it also sidesteps Mod Menu's
all-rights-reserved licence (no derivative build could be published). **Rejected:** forking/patching
Mod Menu (licence forbids distributing modified builds) and requiring mods to register with an API.

## 2026-08 — Version single-sourced from the csproj `<Version>`

`ModBuildInfo.Version` is generated at compile time from `<Version>` by the `GenerateModBuildInfo`
target in `Directory.Build.props`; `[BepInPlugin]` reads it. **Why:** the archive name (`pack.ps1`)
and the BepInEx-reported version can never drift from a hand-typed string. **Rejected:** a hardcoded
version constant in `Plugin.cs` (removed in `798ac3c`/`7f99776`).

## 2026-08 — Cancel handled through the game's own action ids

The `ProcessContinueInput` patch reads the game's own cancel condition/action ids rather than watching
the Escape key. **Why:** the corner prompt fires `SimulateActionForAll`, not a click, so watching the
key misses it; using the game's action means the panel answers to whatever cancel is bound to, on
keyboard or gamepad (B on Steam Deck). **Rejected:** watching `KeyCode.Escape` directly. **Cost:** a
game update could change the action ids; the failure mode is cancel quietly not working, and is logged.

## 2026-08-04 — Name: "Mod Nook" display, `ModNook` everything else

Display name **Mod Nook**; `ModNook` is the assembly/namespace/directory; GUID
`com.dirtyredz.moonlightpeaks.modnook`. **Why:** the GUID also names the config file, so it must not
change after release. **Rejected:** renaming post-1.0.0.

_Living doc — refresh with /project-docs when it drifts._
