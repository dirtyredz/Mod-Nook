---
id: dk-4e68c4ae
type: task
created: 2026-10-05
status: done
since: 2026-10-05
area: structure
priority: P1
rank: y
parent:
fixes: []
blocked_by: []
relates: []
---
# Introduce a modal-dialog abstraction

From docs/BACKLOG.md (Structural (from the 2026-08-22 review))

- [x] **P1 — Introduce a modal-dialog abstraction.** _Done 2026-08-22._ Added `src/ui/dialogs/ModalDialog.cs`, an
  `abstract ModalDialog : MonoBehaviour` base that owns the one-at-a-time singleton lifecycle, the
  dim+centered-panel shell (`BuildShell(width, padding, spacing, …)`), Escape-close, and the
  register-before-`Build` contract. `ColorPicker`/`KeyCapture`/`ListEditor` now subclass it and
  implement only `Build`; each shed ~70–85 lines. Fixed a latent bug: `KeyCapture` and `ListEditor`
  used to assign the singleton *after* `Build`, so a build that threw left an un-closeable half-built
  dialog — now assigned once, before `Build`, in the base. `Confirm` stays out (native popup). Build
  verified; wants an in-game play-test of the three dialogs.
