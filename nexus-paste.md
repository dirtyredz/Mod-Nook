> ⚠️ **Superseded — do not paste from this file.**
> The live pages were restyled on 2026-08-04 and this BBCode is the *pre-style* version.
> The live page is now the source of truth; pull its BBCode from the edit form's description
> field. Structure: [14-description-review.md](../../14-description-review.md). Look:
> [15-page-style.md](../../15-page-style.md). Mechanics: [13-nexus-page-standard.md](../../13-nexus-page-standard.md).

# Mod Nook — Nexus page source

**Nexus page:** [mod 127](https://www.nexusmods.com/moonlightpeaks/mods/127)

The description field is **SCEditor with a BBCode source**, so the block below is the literal
value that gets set. Structure per [14-description-review.md](../../14-description-review.md).

Description prose and Main features wording are **yours, unchanged**.

This page inverts the Configuration section: instead of pointing at Mod Nook, it lists the
five mods Mod Nook configures. It also fixes paths that were in code blocks mid-sentence —
`<pre>` is a block element, so *"To uninstall, delete the [box] folder."* was landing on three
lines with "folder." orphaned underneath.

## Other fields

| Field | Change |
|---|---|
| Name | `Mod Nook` — no change |
| Category | Gameplay → **User Interface** ✅ applied. It is a settings interface; Gameplay is where people browse for mods that change how the game plays |
| Tags | User Interface, Quality of Life — no change |
| Short description | replace, see below |

**Short description** (replaces *"Configure your mods your way An In-Game tool for changing
other mods config's. Designed to feel like its part of the game."* — a missing full stop after
"way", `config's` for `configs`, and `its` for `it's`, in the one line that shows on every
listing tile):

```
Every mod's settings, in the pause menu. Sliders, colour pickers and key binding by pressing the key — built out of the game's own interface.
```

## Description source

```bbcode
[size=4][b]Description[/b][/size]
[color=#D4D4D8]Most mods keep their settings in a text file. Changing one means leaving the game, finding the right .cfg, and hoping you typed the value the way the mod expects.

Mod Nook puts them all in the pause menu.

Open [b]Pause > Mod Nook[/b] and every installed mod that has settings is there, listed down the side, with its options beside it. It finds them on its own — it does not need to know about a mod in advance, and mods do not need to know about it.

It is built out of the game's own interface rather than made to look like it. The checkboxes, arrows, sliders, the bat backdrop and the header are Moonlight Peaks' own controls, borrowed. So it uses the right font at the right size, it reads correctly at any UI scale, and it works with a controller because the game's controls already do.

Your config files do not change. Mod Nook writes the same values, in the same format, to the same place — so editing them by hand still works, and nothing breaks if you remove it.[/color]

[size=4][b]Main features[/b][/size]
[list]
[*][b]Every mod's settings, in the pause menu[/b] — mods down the left, the selected mod's settings on the right. Switching between two is one click.
[*][b]Lists you can actually read[/b] — a setting like [i]GameCanvas,SharedCanvas,MenuCanvas[/i] becomes one row per entry, each with its own Remove, plus an Add. Saved back in exactly the same format.
[*][b]Key bindings you press instead of spell[/b] — press the key you want. Hold Ctrl, Shift or Alt to include them. Nothing saves until you confirm.
[*][b]A colour picker[/b] — the game's own palette, or red/green/blue exactly, with a live preview.
[*][b]Sliders instead of typing numbers[/b] — where a mod declares a range, you get a slider you can't push out of bounds.
[*][b]The description is right there[/b] — behind a small [b]i[/b] you can hover, so twenty settings is still a list you can read down.
[*][b]Reset[/b] — put any mod back to its defaults. It asks first.
[*][b]It also fixes the pause menu[/b] — every mod that adds a button eats into a panel sized for five. Mod Nook grows it to fit whatever's in it, including other mods' buttons.
[/list]

[size=4][b]Requirements[/b][/size]
[list]
[*][b]BepInEx 5 (win_x64)[/b], version 5.4.23.5 or newer — the only thing this mod needs
[/list]
[color=#D4D4D8]Mod Nook does not require any of the mods it configures, and they do not require it. It reads settings through BepInEx's own config system, so it works with any mod that uses it, including ones released after this one.

PC/Steam only. The Switch and mobile builds cannot load BepInEx.[/color]

[size=4][b]Installation[/b][/size]
[b]With Vortex[/b]
[color=#D4D4D8]Open the Files tab, click the Vortex button, and enable the mod. Done.[/color]

[b]Manually[/b]
[list=1]
[*]Install [b]BepInEx 5 (win_x64)[/b] into your Moonlight Peaks folder if you have not already. The BepInEx folder sits beside Moonlight Peaks.exe.
[*]Launch the game once, then quit. This creates the BepInEx\plugins folder.
[*]Download the archive from the Files tab and extract it over your Moonlight Peaks folder, so the file ends up at Moonlight Peaks\BepInEx\plugins\ModNook\ModNook.dll
[*]Start the game, press Escape, and choose [b]Mod Nook[/b].
[/list]
[color=#D4D4D8]To uninstall, delete the BepInEx\plugins\ModNook folder. Your other mods' settings are untouched — they stay in their own .cfg files exactly as they were.[/color]

[size=4][b]Mods it configures[/b][/size]
[color=#D4D4D8]Anything that uses BepInEx's config system, which is very nearly everything. Mine all show up in it the moment both are installed:[/color]
[list]
[*][url=https://www.nexusmods.com/moonlightpeaks/mods/119][b]Chest Labels[/b][/url] — name your chests. Set the nameplate tint with the colour picker.
[*][url=https://www.nexusmods.com/moonlightpeaks/mods/120][b]Plant Peek[/b][/url] — hover a plant to see how it is doing. Rebind the peek key by pressing it.
[*][url=https://www.nexusmods.com/moonlightpeaks/mods/121][b]Coffin Break[/b][/url] — the clock stops when you walk away. Nudge the idle timings on sliders.
[*][url=https://www.nexusmods.com/moonlightpeaks/mods/122][b]Last Swing[/b][/url] — health bars on trees and rocks. Set the colour thresholds with a live preview.
[*][url=https://www.nexusmods.com/moonlightpeaks/mods/126][b]Transplant[/b][/url] — move planted crops without losing growth. Bind the arming key by pressing it.
[/list]

[size=4][b]Compatibility[/b][/size]
[color=#D4D4D8]Built and tested against Moonlight Peaks 1.1.45+31 and BepInEx 5.4.23.5. Because it integrates with the pause screen, a future game update may need a compatibility update.[/color]

[size=4][b]For mod authors[/b][/size]
[color=#D4D4D8]Nothing is required. If your mod calls Config.Bind, it already has a page. Two optional things make it better, neither of which adds a dependency or changes anything when Mod Nook is not installed.

[b]1. Give numbers a range[/b] — that is what turns a text field into a slider, via AcceptableValueRange.

[b]2. Tag the description[/b] — plain strings, ignored by everything else:[/color]
[list]
[*]ModNook.Label=Hover height — a nicer label than the config key
[*]ModNook.Values=a|b|c — render as a cycle over these values
[*]ModNook.List — force the list editor on a comma-separated string
[*]ModNook.Color — force the colour picker
[*]ModNook.Hidden — keep a setting out of the panel
[/list]
[color=#D4D4D8]Lists and colours are usually detected without any tag — a description mentioning "comma", or a value that looks like hex, is enough.[/color]

[size=4][b]Shout outs[/b][/size]
[list]
[*][b]Little Chicken Game Company[/b] — for a UI clean enough to build on. Nearly every control in this mod is one of theirs.
[*]The [b]BepInEx[/b] and [b]HarmonyX[/b] teams — none of this scene exists without them.
[*][b]My Mate[/b], for being my inspiration.
[/list]
```
