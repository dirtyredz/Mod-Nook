---
id: dk-d4aedfae
type: feature
created: 2026-10-05
status: done
since: 2026-10-05
area: ui
priority: P2
rank: z
parent:
fixes: []
blocked_by: []
relates: []
---
# Better unbounded-number handling

From docs/BACKLOG.md (Capability (from README "Known limits"))

- [x] **P2 — Better unbounded-number handling.** _Done 2026-08-22 (pending in-game test)._ A number with
  no `AcceptableValueRange` now opens `NumberEditor` (`src/ui/dialogs/NumberEditor.cs`, a `ModalDialog`) instead of
  the raw text popup: ±fine/±coarse nudge buttons (step scaled to the value's magnitude), a Type…
  direct-entry path, clamped to the numeric type's own limits, saved via `SetSerializedValue`. Chose a
  dialog over an inferred-range slider (no invented bounds) — see DECISIONS.
