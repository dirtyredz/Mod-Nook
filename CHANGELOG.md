# Changelog

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
