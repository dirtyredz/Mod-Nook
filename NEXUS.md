# Nexus Page Copy

> **Pasting into the upload form? Use [nexus-paste.md](nexus-paste.md), not this file.**
> The BBCode below did render, but `[list=1]` came out unordered and `[font]` left 13 stray
> tags. See [13-nexus-page-standard.md](../../13-nexus-page-standard.md).

Reference copy, one section per field on the Nexus upload form.

**Name:** `Mod Nook`
**Category:** Gameplay — as published
**Tags:** Quality of Life, Utilities for Players
**Nexus page:** [mod 127](https://www.nexusmods.com/moonlightpeaks/mods/127)

## Summary (one line, shows in listings)

Every mod's settings, in the pause menu. Sliders, colour pickers and key binding by pressing
the key — built out of the game's own interface.

> Replaces the live line, which has three errors in it: a missing full stop after "way",
> `config's` for `configs`, and `its` for `it's`.

---

## Field: Description

Most mods keep their settings in a text file. Changing one means leaving the game, finding the
right .cfg, and hoping you typed the value the way the mod expects.

Mod Nook puts them all in the pause menu.

Open [b]Pause > Mod Nook[/b] and every installed mod that has settings is there, listed down the
side, with its options beside it. It finds them on its own — it does not need to know about a mod
in advance, and mods do not need to know about it.

It is built out of the game's own interface rather than made to look like it. The checkboxes,
arrows, sliders, the bat backdrop and the header are Moonlight Peaks' own controls, borrowed. So it
uses the right font at the right size, it reads correctly at any UI scale, and it works with a
controller because the game's controls already do.

Your config files do not change. Mod Nook writes the same values, in the same format, to the same
place — so editing them by hand still works, and nothing breaks if you remove it.

---

## Field: Installation instructions

[b]With Vortex[/b]

Open the Files tab, click the Vortex button, and enable the mod. Done.

[b]Manually[/b]

[list=1]
[*]Install [b]BepInEx 5 (win_x64)[/b] into your Moonlight Peaks folder if you have not already. The
BepInEx folder sits beside Moonlight Peaks.exe.
[*]Launch the game once, then quit. This creates the [font=Courier New]BepInEx\plugins[/font] folder.
[*]Download the archive from the Files tab and extract it over your Moonlight Peaks folder. Check the
file ended up at:
[code]Moonlight Peaks\BepInEx\plugins\ModNook\ModNook.dll[/code]
[*]Start the game, press Escape, and choose [b]Mod Nook[/b].
[/list]

To uninstall, delete the [font=Courier New]BepInEx\plugins\ModNook[/font] folder. Your other mods'
settings are untouched — they stay in their own .cfg files exactly as they were.

---

## Field: Main features

[b]Every mod's settings, in the pause menu[/b]
Mods down the left, the selected mod's settings on the right. Switching between two mods is one
click.

[b]Lists you can actually read[/b]
A setting like [i]GameCanvas,SharedCanvas,MenuCanvas[/i] becomes one row per entry, each with its
own Remove button, plus an Add. Saved back in exactly the same format.

[b]Key bindings you press instead of spell[/b]
Press the key you want. Hold Ctrl, Shift or Alt to include them. Nothing is saved until you confirm,
so hitting the wrong key costs one more press.

[b]A colour picker[/b]
Pick from the game's own palette, or set red, green and blue exactly, with a live preview. Clearing
it leaves the setting empty, which is what mods read as "use my own default".

[b]Sliders instead of typing numbers[/b]
Where a mod says what range a number can take, you get a slider you cannot push out of bounds.

[b]The description is right there[/b]
Every setting keeps the explanation its author wrote, behind a small [b]i[/b] you can hover. A mod
with twenty settings is still a list you can read down.

[b]Reset[/b]
Put any mod back to its defaults from its own page. It asks first.

[b]It also fixes the pause menu[/b]
Every mod that adds a pause-menu button eats into a panel sized for five. Install two and the last
entry sits on the edge. Mod Nook grows the panel to fit whatever is in it — including buttons added
by other mods.

---

## Field: Requirements

[b]Required[/b]

[list]
[*][b]BepInEx 5 (win_x64)[/b], version 5.4.23.5 or newer. Nothing else.
[/list]

Mod Nook does not require any of the mods it configures, and they do not require it. It reads
settings through BepInEx's own config system, so it works with any mod that uses it, including ones
released after this one.

PC/Steam only. The Switch and mobile builds cannot load BepInEx.

[b]Mods of mine it pairs with[/b]

Every one of these has its full settings page in Mod Nook the moment both are installed. None of
them require it, and it does not require them.

[list]
[*][b]Chest Labels[/b] — name your chests. Colour-pick the nameplate tint from the palette.
https://www.nexusmods.com/moonlightpeaks/mods/119
[*][b]Plant Peek[/b] — hover a plant to see how it is doing. Rebind the peek key by pressing it.
https://www.nexusmods.com/moonlightpeaks/mods/120
[*][b]Coffin Break[/b] — the clock stops when you walk away. Nudge the idle timings on sliders.
https://www.nexusmods.com/moonlightpeaks/mods/121
[*][b]Last Swing[/b] — health bars on trees and rocks. Set the colour thresholds with a preview.
https://www.nexusmods.com/moonlightpeaks/mods/122
[*][b]Transplant[/b] — move planted crops without losing growth. Bind the arming key by pressing it.
https://www.nexusmods.com/moonlightpeaks/mods/126
[/list]

[b]Compatibility[/b]

Works alongside [b]Mod Menu[/b] by Elsiabeth — both add their own button and both keep working.

Built and tested against Moonlight Peaks 1.1.45+31 and BepInEx 5.4.23.5. Because it integrates with
the pause screen, a future game update may need a compatibility update.

---

## Field: Shout outs

[b]Little Chicken Game Company[/b] — for a UI clean enough to build on. Nearly every control in this
mod is one of theirs.

[b]Elsiabeth[/b], for [b]Mod Menu[/b] — which made the case that in-game settings were worth having,
and whose pause-menu integration was the reference for this one. Mod Nook is a separate mod, not a
fork, and the two work together.

[b]The BepInEx and HarmonyX teams[/b] — none of this scene exists without them.

[b]My Mate[/b], for being my inspiration.

---

## For mod authors

Post as a sticky comment, or add to the description.

Nothing is required. If your mod calls [font=Courier New]Config.Bind[/font], it already has a page.
Two optional things make it better, neither of which adds a dependency or changes anything when Mod
Nook is not installed:

[b]1. Give numbers a range[/b] — that is what turns a text field into a slider.

[code]Config.Bind("Hover", "HoverHeight", 0.8f,
    new ConfigDescription("Height above the plant.",
        new AcceptableValueRange<float>(0f, 3f)));[/code]

[b]2. Tag the description[/b] — plain strings, ignored by everything else.

[list]
[*][font=Courier New]ModNook.Label=Hover height[/font] — a nicer label than the config key
[*][font=Courier New]ModNook.Values=a|b|c[/font] — render as a cycle over these values
[*][font=Courier New]ModNook.List[/font] — force the list editor on a comma-separated string
[*][font=Courier New]ModNook.Color[/font] — force the colour picker
[*][font=Courier New]ModNook.Hidden[/font] — keep a setting out of the panel
[/list]

Lists and colours are usually detected without any tag — a description mentioning "comma", or a
value that looks like hex, is enough.

---

## Shot list

See [screenshots/README.md](screenshots/README.md).
