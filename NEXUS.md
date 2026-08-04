# Nexus Page Copy

Paste-ready copy for the mod page, plus the shot list.

**Category:** User Interface
**Tags:** Quality of Life, Utilities for Players
**Requirements:** BepInEx 5.4.23.5

---

## Summary (one line, shows in listings)

Change any mod's settings from the pause menu, in a panel built out of the game's own controls.

---

# Paste-ready page copy

Nexus splits the page into named fields. Each heading below maps to one of them.

## Field: Description

Most mods keep their settings in a text file. Changing one means alt-tabbing, finding the right
`.cfg`, and hoping you typed the value the way the mod expects.

**Mod Nook** puts them all in the pause menu.

Pick **Mod Nook**, choose a mod, and change what you want. It finds every installed mod that has
settings — it does not need to know about them in advance, and they do not need to know about it.

[b]It looks like part of the game, because it is[/b]

The checkboxes, the arrows, the sliders, the backdrop, the header, the prompt in the corner — none
of it is drawn to look like Moonlight Peaks. It is Moonlight Peaks' own interface, borrowed. That
means it uses the right font at the right size, it reads correctly at any UI scale, and it works
with a controller because the game's controls already do.

[b]Settings that a text box handles badly[/b]

[list]
[*][b]Lists[/b] — a setting like [i]GameCanvas,SharedCanvas,MenuCanvas[/i] becomes one row per
entry, each with its own Remove button, plus an Add. It is saved back in exactly the same format,
so nothing else has to change.
[*][b]Key bindings[/b] — press the key you want instead of typing its name. Hold Ctrl, Shift or Alt
to include them. Nothing is saved until you say so, so a mis-hit costs one more press.
[*][b]Numbers[/b] — a slider where the mod declares a range, so you cannot type a value it will
reject.
[/list]

[b]The description is right there[/b]

Every setting keeps the explanation its author wrote. It sits behind a small [b]i[/b] you can hover,
so a mod with twenty settings is still a list you can read down.

[b]Reset[/b]

Any mod can be put back to its defaults from its own page. It asks first.

[b]It also fixes the pause menu[/b]

Every mod that adds a pause-menu button eats into a panel that was sized for five. Install two and
the last entry ends up on the edge. Mod Nook grows the panel to fit whatever is actually in it —
including buttons added by other mods.

## Field: Requirements

[list]
[*][b]BepInEx 5[/b] — required
[/list]

Nothing else. Mod Nook does not require the mods it configures, and they do not require it.

## Field: Installation

Extract the archive over your Moonlight Peaks folder. The DLL should end up at:

[code]Moonlight Peaks\BepInEx\plugins\ModNook\ModNook.dll[/code]

Start the game and open the pause menu.

## Field: Compatibility

Works alongside [b]Mod Menu[/b] by Elsiabeth — both add their own button and both keep working.

Mod Nook reads other mods' settings through BepInEx's own config system, so it supports any mod
that uses it, including ones released after this. It changes nothing about how those settings are
stored: the config files stay exactly as they were, and editing them by hand still works.

Because it integrates with the pause screen, a future game update may need a compatibility update.

## Field: Credits

[list]
[*][b]Little Chicken Game Company[/b] — for a UI clean enough to build on
[*][b]Elsiabeth[/b], for [b]Mod Menu[/b] — which showed in-game settings were worth having, and
whose pause-menu integration was the reference for this one
[*][b]BepInEx[/b] and [b]HarmonyX[/b]
[/list]

## Field: Development disclosure

This mod was created with the use of generative AI tools.

---

## For mod authors (post as a sticky comment, or a second page section)

Nothing is required. If your mod calls [font=Courier New]Config.Bind[/font], it already has a page.

Two optional things make it better, neither of which adds a dependency or changes anything when
Mod Nook is not installed:

[b]1. Give numbers a range[/b] — that is what turns a text field into a slider.

[code]Config.Bind("Hover", "HoverHeight", 0.8f,
    new ConfigDescription("Height above the plant.",
        new AcceptableValueRange<float>(0f, 3f)));[/code]

[b]2. Tag the description[/b] — plain strings, ignored by everything else.

[list]
[*][font=Courier New]ModNook.Label=Hover height[/font] — a nicer label than the config key
[*][font=Courier New]ModNook.Values=a|b|c[/font] — render as a cycle over these values
[*][font=Courier New]ModNook.List[/font] — force the list editor on a comma-separated string
[*][font=Courier New]ModNook.Hidden[/font] — keep a setting out of the panel
[/list]

A comma-separated setting is detected automatically if its description mentions "comma", so most
list settings work with no changes at all.

---

## Shot list

See [screenshots/README.md](screenshots/README.md).

## Before publishing

- Category **User Interface**; list BepInEx as the only requirement.
- Mention Mod Menu compatibility explicitly — anyone reading this page probably has it installed.
- The AI development disclosure is required by Nexus policy and is included above.
