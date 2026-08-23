# Changelog

## 1.1.0

New editors and reach, plus a built-in reference — all still read-only against other mods' configs,
no gameplay patches, nothing written to the save.

**New**

- **Unbounded numbers get a real editor.** A number with no `AcceptableValueRange` used to fall back
  to a text box; it now opens a number editor — nudge buttons (a fine and a coarse step, scaled to the
  value) around the current value, plus **Type…** for an exact one, clamped to the type's own limits.
  This closes the 1.0.0 "unbounded numbers fall back to text" limit.
- **Built-in Examples section.** Mod Nook's own page now carries a live gallery — one setting for every
  control it can draw (every widget, every dialog, and each `ModNook.*` author tag) — so you can see
  the whole vocabulary at a glance without installing anything else.
- **Prose choices.** A plain-string setting that lists its valid values in its *description*
  (e.g. "… DARK MOON, BLOOD VELVET or ROSE QUARTZ") now renders as a cycle, anchored on the current
  value so ordinary prose isn't mistaken for a list.

**Fixed**

- **Gamepad / Steam Deck cancel** now dismisses the colour, key and list dialogs (they had no Escape
  key to press).
- **Long third-party mod names** ellipsize instead of overflowing the sidebar.
- **Proton / Linux** overlay parenting fix (`includeInactive` canvas lookup), so the panel lands on
  the right canvas when ancestors are still inactive as the pause menu opens.

**Internal**

- Large structural cleanup with no behaviour change: the row factory, the modal dialogs, the overlay
  context, and the panel's chrome construction were split into focused files. No user-facing effect.

**Known limits at 1.1.0**

- `Name=Value` pair lists edit as whole strings
- No per-setting reset and no undo

## 1.0.0

First release.

An in-game settings panel for BepInEx mods, reached from **Pause › Mod Nook**. Reads other
plugins' `ConfigEntry` definitions and nothing else — no Harmony patches on gameplay, nothing
written to the save.

**Built from the game's own UI**

Every control is cloned from a live instance rather than redrawn:

- `AnimatedToggle` for booleans, `CycleButton` for enums and value lists, `SliderButton` for
  bounded numbers
- The Settings screen's backdrop, its decorated header bar, and its corner **Close** prompt, which
  is registered with the game's shared input bar so it shows whatever key the player actually has
  bound
- Confirmation uses the game's own popup, so its buttons are in the player's language

**Layout**

- Mods down the left, the selected mod's settings on the right, both scrolling independently —
  so comparing two mods costs one click rather than backing out and in again

**Editors for the types a text box handles badly**

- **Colours** — a picker with the game's own palette for one-click choices and RGB sliders for
  anything else, with a live preview. Clear leaves the setting empty, which is what mods read as
  "use my default", and the hex format is preserved exactly as it was
- **Key bindings** — a capture dialog showing the current binding, waiting for a press, with Save
  and Try again. Modifiers are held provisionally, so Alt binds Alt but Ctrl+S binds Ctrl+S
- **Comma-separated lists** — one row per entry with its own Remove, plus Add. The stored format is
  unchanged, so hand-editing still works
- Everything else opens the game's text popup, writing through `SetSerializedValue` so a value the
  mod would reject from its config file is rejected here too

**Other**

- Descriptions live behind a hover info icon, so a page of settings stays scannable
- Per-mod **Reset**, behind a confirmation
- The pause panel grows to fit extra buttons, rather than letting them overflow it — which fixes
  the overflow caused by any mod adding a pause entry, not only this one

**Known limits at 1.0.0**

- Only numbers with an `AcceptableValueRange` get sliders; unbounded ones fall back to text
- `Name=Value` pair lists edit as whole strings
- No per-setting reset and no undo
