---
id: dk-ebb7ae3a
type: task
created: 2026-10-05
status: todo
since: 2026-10-05
area: structure
priority: P2
rank: zy
parent:
fixes: []
blocked_by: []
relates: []
---
# `ui/dialogs/TextPopupDialog.cs` and `ui/dialogs/Confirm.cs` are native-popup adapters, not drawn UI

From docs/BACKLOG.md (Placement follow-ups (from the 2026-09-01 structure review))

- **P2 — `ui/dialogs/TextPopupDialog.cs` and `ui/dialogs/Confirm.cs` are native-popup adapters, not
  drawn UI.** Both merely locate and drive a game screen (`TextInputPopupScreen`,
  `GenericPopupScreen`); neither builds any UI. That is the same category as `game/PopupEscape.cs`,
  which arms Escape on the very same popup — yet the three are split across two folders. STRUCTURE.md's
  own component table already groups all three as one component. Either move these two to `game/`, or
  move `PopupEscape.cs` into `dialogs/` — currently the same test gives different answers.
