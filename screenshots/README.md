Artwork and screenshots for the Nexus page.

| File | What it is |
|---|---|
| `banner.png` | Mod page banner, 2358×667 |
| `thumbnail.png` | Mod page thumbnail |

Both should use the game's plum and gold so they sit alongside the in-game panel rather than
against it — see
[10-visual-integration.md](https://github.com/dirtyredz/chest-labels/blob/main/10-visual-integration.md).

## Shot list

Numbered in the order they should appear on the page. Capture on a **fresh config**, since that is
what a new install looks like.

| # | Shot | File | Why it earns a slot | Status |
|---|---|---|---|---|
| 1 | Sidebar + a mod's settings, full screen on the bat backdrop | `01-mod-list.png` | The first thing anyone sees, and it shows the panel is not a floating box | ⚠️ recapture — predates the sidebar |
| 2 | A mod page with mixed row types — checkbox, cycle, slider, text | `02-settings.png` | Proves the controls are the game's own | ⚠️ recapture — predates the sidebar |
| 3 | An info icon hovered, tooltip open | `03-description.png` | Answers "where did the descriptions go" before it is asked | ⚠️ verify |
| 4 | The colour picker on Plant Peek's nameplate tint | `04-color-picker.png` | Game palette and RGB sliders, neither of which a text box can do | ✅ current |
| 5 | The key-capture dialog mid-press | `05-key-binding.png` | The other thing a text box handles badly | ⚠️ verify |
| 6 | A text setting's popup | `06-text-input.png` | The general fallback | ⚠️ verify |
| 7 | Sidebar with a long mod selected | `07-in-game.png` | Shows the layout carrying a real mod's worth of settings | ⚠️ recapture — shows the fixed label bug |
| 8 | **The list editor on Bigger UI's `Canvases`** | `08-list-editor.png` | The feature this mod was started for; one row per entry instead of a comma-separated line | ❌ missing |

**Worth capturing at a raised UI scale as well.** Not fitting the larger font is the specific
complaint this mod exists to answer, so a side-by-side at 1.5× scale would carry more weight on the
page than any description of it.
