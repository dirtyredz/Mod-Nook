# ARCHITECTURE — Mod Nook

How the system works at runtime. For the code map see [../STRUCTURE.md](../STRUCTURE.md).

## System overview

Mod Nook is a single BepInEx 5 plugin. At load it binds its own two config entries and installs
three HarmonyX patches on the game's pause/settings UI. It owns **no scene of its own**: it grafts a
button onto the game's pause menu and, on demand, builds a full-screen overlay parented to the game's
shared UI canvas. Everything the player sees is either a clone of a live game widget or a small set of
hand-drawn primitives styled to match.

The defining constraint is **one-directional discovery**: Mod Nook reads other plugins' BepInEx
`ConfigEntry` metadata through the Chainloader; no other plugin references or depends on Mod Nook, and
a configured mod keeps working normally when Mod Nook is absent. A mod earns a settings page simply by
calling `Config.Bind` — which it already had to do.

## Data model

- **`ModCatalog.ModEntry`** — one plugin: `Guid`, `Name`, `Version`, `Config` (its `ConfigFile`), and
  ordered `Sections`.
- **`ModCatalog.SectionEntry`** — one `[Section]` from the config file: `Name` + its `ConfigEntryBase`
  list, preserving the author's own grouping.
- Discovery source: `BepInEx.Bootstrap.Chainloader.PluginInfos`. Settings excluded when tagged
  `ModNook.Hidden`. No state of Mod Nook's own is persisted beyond its two config entries; edits are
  written straight into each mod's own config file.

## Widget mapping (the core translation)

`Rows.Build` inspects a `ConfigEntryBase.SettingType` and metadata and picks the native widget that
the game's own Settings screen would use for that shape:

| Setting shape | Widget | Source |
|---|---|---|
| `bool` | `AnimatedToggle` (else Off/On cycle) | bug-report screen |
| `KeyboardShortcut` / `KeyCode` | key-capture dialog | — (checked **before** enum: `KeyCode` is an enum) |
| `enum` | `CycleButton` | Settings › Gameplay |
| `AcceptableValueList` / `ModNook.Values` / prose enumeration | `CycleButton` | — |
| numeric + `AcceptableValueRange` | `SliderButton` | Settings › Video |
| hex-colour `string` | colour picker (palette from `ColorLibrary`) | Settings colours |
| comma-separated `string` (or `ModNook.List`) | list editor | — |
| anything else | game text popup | creature-naming dialog |

Widgets are **cloned from a live instance** (`Templates`), not reconstructed: a cloned `CycleButton`
inherits the right font, anchors, colours, and controller navigation for free and cannot drift when
the game updates. Cloning is followed by sanitizing — stripping the clone's localization, screen
components, hover-select, colour animation, and decorative bat-wings so it behaves as an inert control.

## Key flows / sequences

**Attach & open**
1. `PauseScreen.OnShow` (Harmony postfix) → `PanelController.Attach` — adds one **Mod Nook** pause
   button (once per PauseScreen instance) and schedules a fit at end-of-frame.
2. Button click → `Open` → `EnsureOverlay` builds the overlay once: clone the settings backdrop, clone
   the settings header, build the panel plate, body (sidebar + detail scrollers), and footer.
3. `ModCatalog.Discover` → `ShowModList` fills the sidebar and opens the first mod.
4. `ShowMod` builds one row per `ConfigEntry`; each row's change handler writes and `Persist`s.

**Edit → persist** — a widget event sets `entry.BoxedValue`/`SetSerializedValue`, then
`mod.Config.Save()`. Immediate write, no Save button (BepInEx writes the whole file atomically).

**Dialogs** — colour/key/list editors are children of the overlay. Opening one hides the tooltip;
the text popup path additionally **suspends** the overlay's raycast blocker (`SuspendOverlay`) so the
game's popup, which lives on another canvas, is reachable, and restores it on the popup's `OnScreenHide`.

**Cancel / back** — `PauseMenuState.ProcessContinueInput` (Harmony prefix) is skipped while the panel
is open; instead it reads the game's own cancel action and calls `RequestBack`, which closes an open
dialog first, else the panel. This is what stops one Escape press from leaving both the panel and the
pause menu, and lets gamepad B dismiss dialogs.

## External interfaces

- **BepInEx** — `BaseUnityPlugin`, `ConfigEntry`/`ConfigFile`, `Chainloader.PluginInfos`.
- **HarmonyX** — patches on `PauseScreen.OnShow/OnHide`, `PauseMenuState.ProcessContinueInput`.
- **Game assemblies** (referenced, never shipped): `Vampire.Runtime` (PauseScreen, CycleButton,
  SliderButton, settings screens), `Chicken.UI` (AnimatedButton/Toggle, UIScreen, popups),
  `Rewired_Core` (cancel input), Unity + TextMeshPro. All resolved from the game's `Managed/` folder.
- **Contract for mod authors** — optional `ModNook.*` description tags (`Label=`, `Values=`, `List`,
  `Hidden`) and BepInEx `AcceptableValueRange`/`AcceptableValueList`. All ignored by anyone without
  Mod Nook installed.

## Design notes

- **Overlay is parented to the canvas, not the PauseScreen**, so hiding the pause screen doesn't hide
  it; `OnHide` closes it explicitly. On Linux/Proton the canvas ancestor may still be inactive at
  OnShow, so lookups use `includeInactive: true`.
- **Templates are re-resolved every open** — the PauseScreen is destroyed and rebuilt across sessions,
  so a template captured once becomes a destroyed object and must not be cached across opens.
- **Failure is always contained** — every patch entry point and every per-row build is wrapped so a
  single mod's bad setting costs its own row, never the pause menu (the only way out of the game).

_Living doc — refresh with /project-docs when it drifts._
