# FEATURES — Mod Nook

What Mod Nook does. Status: ✅ shipped (in 1.0.0) · 🧪 committed, unreleased (on top of 1.0.0) · 📋 planned.

## Discovery & pages
- ✅ Adds one **Mod Nook** button to the pause menu.
- ✅ Auto-discovers every loaded BepInEx plugin that binds settings; no registration or dependency.
- ✅ Sidebar of mods (left) + selected mod's settings (right), preserving the author's config sections.
- ✅ Works when configuring mods that don't know Mod Nook exists (one-directional discovery).

## Native widgets (per setting type)
- ✅ `bool` → `AnimatedToggle` checkbox (falls back to an Off/On cycle if the template is missing).
- ✅ `enum` → `CycleButton` (humanised names).
- ✅ `AcceptableValueList` / `ModNook.Values=a|b|c` → `CycleButton`.
- ✅ Bounded number (`AcceptableValueRange`) → `SliderButton` with a sensible step (`NiceStep`).
- ✅ `KeyboardShortcut` / `KeyCode` → key-capture dialog (checked before enum so a `KeyCode` isn't a
  full-keyboard cycle).
- ✅ Hex-colour `string` → colour picker with a palette from the game's `ColorLibrary` + RGBA sliders.
- ✅ Comma-separated `string` (auto-detected, or `ModNook.List`) → multi-row list editor.
- ✅ Anything else → the game's own text popup, validated through `SetSerializedValue`.
- 🧪 Prose choice-parser: reads a fixed value set out of a plain-string setting's **description**
  (e.g. "… DARK MOON, BLOOD VELVET or ROSE QUARTZ"), anchored on the current value to avoid false hits.

## Author controls (optional tags)
- ✅ `ModNook.Label=` (override the label), `ModNook.Values=` (force a cycle), `ModNook.List`
  (force the list editor), `ModNook.Hidden` (keep a setting off the panel).
- ✅ Per-setting info icon (ⓘ) carrying the author's description on hover (toggle:
  `Display.ShowDescriptions`).

## Panel behaviour
- ✅ Immediate persistence — every edit writes the mod's config file at once; no Save button.
- ✅ Per-mod **Reset to defaults**, behind a confirmation (no undo).
- ✅ Scrolls when a mod has more settings than fit; grows with the UI font instead of clipping.
- ✅ Native chrome — cloned settings backdrop, decorated header bar, and the real corner **Close**
  prompt drawing the player's bound key cap.
- ✅ Cancel steps back one level (closes an open dialog, else the panel) via the game's cancel action.
- 🧪 Gamepad cancel (B / Steam Deck) dismisses the colour/key/list dialogs.
- 🧪 Long third-party mod names ellipsize instead of overflowing the sidebar.
- 🧪 Proton/Linux overlay-parenting fix (`includeInactive` canvas lookup).

## Diagnostics
- ✅ `Diagnostics.VerboseLogging` dumps the pause and settings hierarchies at startup.
- ✅ Every patch and per-row build is exception-contained so a bad setting never takes down the pause menu.

_Living doc — refresh with /project-docs when it drifts._
