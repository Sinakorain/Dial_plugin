# Changelog

## Unreleased

### Added

- Added generic `Function`, `Scene`, and `Debug` executable node data with primitive argument support.
- Added execution extension points for project-provided function/scene metadata and executors.
- Added runtime traversal support for immediate executable-node execution, pending resume, failure policy handling, and automatic next-link traversal.
- Added editor palette, graph rendering, inspectors, validation, and autosave support for executable nodes.
- Added an editor EN/RU language switcher with localized toolbar, palette, inspector, preview, diagnostics, prompts, and node summaries.
- Added optional `VoiceKey` metadata on text nodes for future project-side voiceover/audio lookup.

### Changed

- Dialogue editor can now be opened from `Tools/New Dial/Dialogue Editor` while retaining the existing `Window/New Dial/Dialogue Editor` entry point.
- NPC, dialogue, and node identifiers can now be edited explicitly in the editor, with guarded generation actions and immediate empty/duplicate warnings.
- Node identifier changes now update internal graph links that reference the old node id.
- Choice-mode nodes now surface authoring diagnostics for missing outputs, broken targets, fallback labels, order conflicts, and unreachable choice targets.
- Condition editing now uses guided operator choices, type-specific hints, and optional project-provided key suggestions.
- Dialogue preview now includes test variables and explains blocked starts, unavailable choices, missing targets, branch ends, and fallback labels.
- Added Where Used sections with internal references and a project-extensible external reference resolver registry.
- Prompt to save or discard unsaved changes before opening another dialogue database in the editor.
- Graph empty-state visibility now updates when the first node is created and when the last node is removed.
- Nested comment-group ownership and movement behavior now follow the most specific containing comment group.
- Cutting a selected root comment group removes the full nested hierarchy after copying it to the clipboard payload.
- Native Unity undo/redo now covers node-scope graph edits, comment resize, link edits, and node inspector changes on both macOS (`Cmd+Z`) and Windows (`Ctrl+Z`).
- Undo/redo now refreshes graph selection, preview sessions, and autosave dirty-state against the last saved database snapshot.
- Text and executable nodes now select from any non-button part of the node while preserving lower-half link dragging.
- Scene node inspectors now write the first available Known Scene into an empty `SceneKey` instead of only showing it as the dropdown default.
- The editor left dock now keeps the NPC/dialogue project area at a fixed height and shows the full compact node palette without palette scrolling.

### Tests

- Expanded `DialogueGraphViewTests` to cover empty-state visibility, nested comment ownership, direct parent resolution, nested group cutting, and focus-based keyboard pan behavior.
- Added undo/redo coverage for node creation, link changes, node movement, comment-group movement, comment resize, and grouped cut behavior.
- Added `DialogueEditorWindowTests` for selection restoration, inspector refresh, and dirty/autosave reset after undo.
- Added text-node voice-key clone and inspector editing coverage.
- Added choice-flow diagnostic coverage for choice nodes, fallback labels, broken targets, order conflicts, and inspector warnings.
- Added coverage for guided condition fields, preview test-variable gating, blocked-state reasons, and Where Used external resolver results.
- Added coverage for editor language switching, localized node summaries, and full-node runtime selection.

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
