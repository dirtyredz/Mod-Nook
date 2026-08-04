# Testing

There is no automated suite. Every code path either reads another mod's BepInEx config or clones a
live Unity object out of the running game, so a console runner could not exercise anything real.
This is the manual pass instead.

## Before a release

- [ ] **Fresh config** — delete `BepInEx/config/com.dirtyredz.moonlightpeaks.modnook.cfg`, launch,
      confirm defaults and that `VerboseLogging` is `false`
- [ ] **The log is quiet** — no warnings from Mod Nook during a normal open/close cycle
- [ ] **Pause menu** — Mod Nook appears below Settings, and the panel grows so no entry is clipped
      or overlapping its edge
- [ ] **With Mod Menu installed as well** — both buttons present, both menus still work
- [ ] **Without Mod Menu installed** — nothing changes

## The panel

- [ ] Mod list shows every plugin with settings, sorted, with its setting count
- [ ] Opening a mod shows its sections, each heading underlined
- [ ] Scrolling works over rows *and* over the gaps between them
- [ ] Scrolling does not change the value of whatever the pointer passes over
- [ ] Cancel steps back one page, and from the list closes the panel
- [ ] Cancel does **not** also close the pause menu
- [ ] Clicking the corner **Close** prompt does the same as pressing the key

## Widgets

- [ ] Booleans are checkboxes, with the label on the left like every other row
- [ ] Enums cycle, showing readable names
- [ ] Bounded numbers get a slider with sensible steps — check Bigger UI's `Scale`
- [ ] Unbounded numbers open the text popup and reject nonsense without corrupting the value
- [ ] Info icons appear only where the author wrote a description, and the tooltip stays on screen
      near the edges

## Key bindings

- [ ] `KeyboardShortcut` (Plant Peek's `ExpandKey`) opens the capture dialog
- [ ] `KeyCode` (Minimap, Save Anywhere) opens it too, rather than cycling every key on the keyboard
- [ ] Pressing **Left Alt** alone binds Left Alt
- [ ] **Ctrl+S** binds Ctrl+S
- [ ] **Right Alt** binds Right Alt, not "AltGr + RightAlt"
- [ ] Clicking Save or Try again does not bind the mouse button
- [ ] Escape leaves without binding
- [ ] The dialog cannot be left open behind a closed panel

## List editor

- [ ] Bigger UI's `Canvases` opens as rows, one per canvas
- [ ] Add, Remove and Cancel behave; Save writes a comma-separated line
- [ ] The resulting config file is byte-identical in format to a hand-written one
- [ ] Bigger UI still parses it after a restart

## Reset

- [ ] Reset appears only on a mod's page, never over the list
- [ ] It asks first, in the game's own popup
- [ ] Confirming restores defaults, and the page redraws with the new values
- [ ] Cancelling changes nothing

## Regressions worth re-checking

Each of these was a real bug during development and each has a specific cause worth re-testing:

- [ ] **Bat wings** still animate on the game's own pause buttons after opening and closing the
      panel several times — clones sharing the selection marker broke this
- [ ] **Every mod is not called "Settings"** — cloned buttons reverting to the template's localized
      caption
- [ ] **A mod's page is not missing settings partway down** — one throwing row used to end the loop
- [ ] **The text popup is escapable when the value is empty** — it has no cancel button of its own
