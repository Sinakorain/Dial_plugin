# Changelog

## Unreleased

### Changed

- Prompt to save or discard unsaved changes before opening another dialogue database in the editor.
- Graph empty-state visibility now updates when the first node is created and when the last node is removed.
- Nested comment-group ownership and movement behavior now follow the most specific containing comment group.
- Cutting a selected root comment group removes the full nested hierarchy after copying it to the clipboard payload.
- Native Unity undo/redo now covers node-scope graph edits, comment resize, link edits, and node inspector changes on both macOS (`Cmd+Z`) and Windows (`Ctrl+Z`).
- Undo/redo now refreshes graph selection, preview sessions, and autosave dirty-state against the last saved database snapshot.

### Tests

- Expanded `DialogueGraphViewTests` to cover empty-state visibility, nested comment ownership, direct parent resolution, nested group cutting, and focus-based keyboard pan behavior.
- Added undo/redo coverage for node creation, link changes, node movement, comment-group movement, comment resize, and grouped cut behavior.
- Added `DialogueEditorWindowTests` for selection restoration, inspector refresh, and dirty/autosave reset after undo.

### Docs

- Added repository-level `README.md`, `AGENTS.md`, and `CONTRIBUTING.md`.
- Expanded package documentation and sample documentation to reflect current package behavior.
- Added `docs/current-state.md` as a working-tree implementation snapshot.
- Documented node-scope undo/redo support and current out-of-scope limits for project-tree edits.

## 0.1.0

- Initial MVP package structure
- Runtime data model and traversal API
- Editor windows, graph canvas, preview, and autosave
- Basic adventure sample bootstrap
- EditMode test coverage for traversal and editor utilities
