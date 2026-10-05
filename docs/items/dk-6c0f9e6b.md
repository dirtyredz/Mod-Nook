---
id: dk-6c0f9e6b
type: bug
created: 2026-10-05
status: todo
since: 2026-10-05
area: structure
priority: P3
rank: n
parent:
fixes: []
blocked_by: []
relates: []
---
# Row summary shows large floats in scientific notation

From docs/BACKLOG.md (Structural (from the 2026-08-22 review))

- [ ] **P3 — Row summary shows large floats in scientific notation.** `SettingMetadata.Summarise`
  uses `BoxedValue.ToString()`, so a big `float`/`double` renders as e.g. `1.234568E+08` in the row's
  value label. Pre-existing; surfaced by the Examples "Growth rate". Format float/double without the
  exponent (and invariant). Cosmetic — the stored value is correct and round-trips.
