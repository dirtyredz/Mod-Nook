# Mod Nook

An in-game settings panel for BepInEx mods, built out of the game's own settings widgets.

**Status:** v1.0.0 — packaged and ready to publish, pending the manual checks in
[RELEASING.md](RELEASING.md).

Build the archive with `.\pack.ps1` → `dist/ModNook-1.0.0.zip`.

## What it does

Adds **Mod Nook** to the pause menu. It lists every loaded BepInEx plugin that has settings, and
gives each one a page built from its existing `ConfigEntry` definitions.

Nothing is required of the mods it configures. Discovery is one-directional: Mod Nook reads other
plugins' config, and no plugin references this assembly. A mod gets a page by doing what it already
had to do — call `Config.Bind` — and keeps working normally when this is not installed.

> Name confirmed 2026-08-04. **Mod Nook** is the display name; `ModNook` is the assembly,
> namespace and directory, and `com.dirtyredz.moonlightpeaks.modnook` is the plugin GUID — which
> also names the config file, so it should not change now.

## Why not just use Mod Menu

[Mod Menu](https://www.nexusmods.com/moonlightpeaks/mods/102) by Elsiabeth already does this, and
does it well. This exists because of three things it cannot do from where it sits:

- **It does not grow with the font.** Its settings rows are fixed-height cards, so raising the UI
  text size — with Bigger UI, or a large display scale — clips them.
- **Everything unbounded is a text box.** A number with no declared range, a key binding and a
  comma-separated list all render as free-form fields.
- **A comma-separated list is unreadable in a single line.** `GameCanvas,SharedCanvas,MenuCanvas`
  in a box narrower than the value is proofreading, not editing.

Mod Menu is under an all-rights-reserved licence — *"No permission is granted to distribute
modified builds or derivative releases"* — so a fixed build of it could not be published even if
the changes were made. This is a separate mod, not a fork: it shares no code with Mod Menu, and the
two can be installed together.

## How it looks native

Every control is the game's own, cloned from a live instance rather than redrawn in its likeness:

| Setting type | Widget | Sourced from |
|---|---|---|
| `bool` | `AnimatedToggle` checkbox | the bug-report screen |
| `enum`, `AcceptableValueList` | `CycleButton` | Settings › Gameplay |
| bounded number | `SliderButton` | Settings › Video |
| `KeyboardShortcut`, `KeyCode` | key-capture dialog | — |
| comma-separated `string` | list editor | — |
| anything else | the game's text popup | — |

The backdrop, the decorated header bar and the corner **Close** prompt are cloned or registered
from the game's Settings screen too. This is deliberate and it is the whole design: a cloned
`CycleButton` inherits the right font, anchors, colours and controller navigation for free, and
cannot drift when the game is updated. See
[10-visual-integration.md](https://github.com/dirtyredz/chest-labels/blob/main/10-visual-integration.md).

## For mod authors

Two things make a mod's page better, both optional and neither creating a dependency.

**Declare a range.** A number with an `AcceptableValueRange` becomes a slider; without one there is
no honest way to draw a track, so it falls back to a text field:

```csharp
HoverHeight = Config.Bind(
    "Hover", "HoverHeight", 0.8f,
    new ConfigDescription(
        "Height above the plant, in world units.",
        new AcceptableValueRange<float>(0f, 3f)));
```

**Tag the description.** Tags are plain strings, ignored by BepInEx and by anyone without this
installed:

| Tag | Effect |
|---|---|
| `ModNook.Label=Hover height` | Overrides the label, which otherwise comes from the config key |
| `ModNook.Values=a\|b\|c` | Renders as a cycle over those values |
| `ModNook.List` | Forces the list editor on a comma-separated string |
| `ModNook.Hidden` | Keeps the setting out of the panel |

A comma-separated setting is detected without any tag if its description mentions "comma" — which
is why Bigger UI's canvas list works untouched.

## Settings

| Setting | Default | Does |
|---|---|---|
| `Display.ShowDescriptions` | `true` | Show the info icon carrying each setting's description |
| `Diagnostics.VerboseLogging` | `false` | Dump the pause and settings hierarchies at startup |

## Requirements

- BepInEx 5.x
- Moonlight Peaks for Windows

Built against Moonlight Peaks `1.1.45+31`, Unity `6000.3.6f1`, BepInEx `5.4.23.5`.

## Known limits

- **Only bounded numbers get sliders.** Most mods, including several of mine, bind numbers with no
  range at all, so they appear as text fields until their authors declare one.
- **`Area scales`-style pair lists edit as whole `Name=Value` strings.** Better than one long line,
  but a two-field row with a slider for the number would suit them properly.
- **No per-setting reset or undo.** Reset is per-mod and, once written, the previous values are
  gone.
- **The cancel-input hook uses the game's own action ids.** A game update could change them; the
  failure would be cancel quietly not working inside the panel, and it is logged.

## Related

- [TESTING.md](TESTING.md) — what to check by hand
- [RELEASING.md](RELEASING.md) — the publish checklist
- [CHANGELOG.md](CHANGELOG.md)
