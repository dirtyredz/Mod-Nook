---
id: dk-c1b167ea
type: task
created: 2026-10-05
status: done
since: 2026-10-05
area: structure
priority: P2
rank: t
parent:
fixes: []
blocked_by: []
relates: []
---
# Dedupe UI primitives

From docs/BACKLOG.md (Structural (from the 2026-08-22 review))

- [x] **P2 — Dedupe UI primitives.** _Done 2026-08-22._ `NewText` + `Stretch` live in `src/ui/chrome/UiText.cs`;
  the dialogs' three near-identical private `Text(...)` builders were deleted and `ColorPicker`/
  `KeyCapture`/`ListEditor` now call `UiText.NewText`. Every panel/dialog label goes through one
  builder, so the wrap-on inconsistency is gone (uniform TMP default wrapping) — which also removed two
  obsolete-`enableWordWrapping` warnings. Build verified.
