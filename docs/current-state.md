# Current State

This file describes the current working-tree behavior of `new_dial`, including changes that exist locally but may not be part of a tagged release yet.

## Snapshot

- Package: `com.danilkashulin.newdial.dialogue-editor`
- Display name: `New Dial Dialogue Editor`
- Version: `0.1.0`
- Unity target: `6000.0`
- Main editor entry point: `Window/New Dial/Dialogue Editor`
- Sample creation entry point: `Tools/New Dial/Create Basic Adventure Sample`
- Test assembly: `NewDial.DialogueEditor.Tests.Editor`

## Implemented

### Data model

- `DialogueDatabaseAsset` is the top-level `ScriptableObject` and stores a list of `NpcEntry`.
- `NpcEntry` stores an `Id`, a display `Name`, and a list of `DialogueEntry`.
- `DialogueEntry` stores an `Id`, `Name`, `StartCondition`, and a `DialogueGraphData`.
- `DialogueGraphData` stores `Nodes` through `SerializeReference` plus ordered `Links`.
- `DialogueTextNodeData` is the main playable node type. It carries `BodyText`, `IsStartNode`, and `UseOutputsAsChoices`.
- `CommentNodeData` stores editor-only grouping information: `Area`, `Comment`, and `Tint`.
- `NodeLinkData` connects nodes with ordered outgoing edges and optional `ChoiceText`.
- `ConditionData` plus `ConditionType` represent start conditions and node entry conditions.

### Runtime traversal

- `DialoguePlayer` supports `CanStart`, `Start`, `Next`, and `Choose`.
- `DialogueChoice` exposes the selected `NodeLinkData`, target text node, and resolved display text.
- `IDialogueVariableStore` abstracts variable lookup.
- `DictionaryDialogueVariableStore` is the built-in in-memory implementation.
- `IDialogueConditionEvaluator` abstracts condition evaluation.
- `DefaultDialogueConditionEvaluator` currently supports:
  - pass-through for `ConditionType.None`
  - numeric comparisons for `>`, `<`, `>=`, `<=`
  - string comparisons for `==`, `!=`, and `Contains`
  - a `Truthy` operator for simple boolean-like values
  - `ConditionType.Custom` currently evaluates to `false`

### Editor surfaces

- `DialogueStartWindow` opens from `Window/New Dial/Dialogue Editor`.
- The start window can create a new `DialogueDatabaseAsset` or load an existing asset inside the current Unity project.
- `DialogueEditorWindow` is the main authoring surface. Its toolbar exposes `New`, `Load`, `Save`, `Preview`, `Delete`, and `Dialogue Settings`.
- The palette currently supports `Text Node` and `Comment`. `Function`, `Scene`, and `Debug` are present only as disabled "Not in MVP" affordances.
- `DialoguePreviewWindow` opens from the main editor toolbar for the currently selected dialogue and supports transcript history, advancing, choosing branches, going back, restarting, and jumping to the active node.
- `DialogueEditorAutosaveStore` serializes snapshots as JSON into the consuming Unity project's `Library/DialogueEditorAutosaves` folder.

### Current editor UX behaviors

- Opening another database while the current one has unsaved changes triggers a save/discard prompt before the switch completes.
- Native Unity undo/redo now covers node-scope graph operations: create/delete, drag, comment resize, links, cut/paste, and node/link inspector edits.
- Undo/redo refreshes graph selection, inspector state, preview sessions, and autosave dirty-state against the last saved database snapshot.
- The graph empty-state warning is visible for an empty graph, hides as soon as the first text or comment node is created, and returns when the last node is deleted.
- Comment groups can own both text nodes and nested comment groups.
- Nested ownership prefers the most specific containing comment group when several comment areas overlap.
- Moving a parent comment group moves directly contained text nodes and nested comment groups with it.
- Cutting a selected root comment group removes the full nested hierarchy from the graph after copying it to the clipboard payload.
- Clipboard shortcuts for copy, cut, and paste are implemented in the graph view.

### Sample content

`Basic Adventure Sample` creates one NPC named `Innkeeper` with two dialogues:

- `Greeting`: branching choice dialogue with room, rumor, and trust-gated branches
- `After Hours`: a dialogue gated by a start condition (`tavern_open == false`)

The sample also includes:

- link-level choice text
- a `UseOutputsAsChoices` start node
- a simple numeric trust-level condition
- a comment node used as an in-editor design note

## Runtime API at a glance

- `DialogueDatabaseAsset`: top-level serialized dialogue database asset intended for editor authoring and runtime consumption.
- `NpcEntry`: serializable NPC container that groups dialogues.
- `DialogueEntry`: serializable dialogue record with start condition and graph payload.
- `DialogueGraphData`: serializable graph container for node and link data.
- `DialogueTextNodeData`: playable dialogue node model.
- `CommentNodeData`: editor grouping/annotation node, not a runtime dialogue line.
- `NodeLinkData`: ordered outgoing connection between nodes with optional choice text.
- `ConditionData` and `ConditionType`: lightweight condition metadata for start and node gating.
- `DialoguePlayer`: runtime traversal helper for moving through a dialogue graph.
- `DialogueChoice`: resolved branch option returned by `DialoguePlayer`.
- `IDialogueConditionEvaluator`: extension point for custom condition evaluation.
- `IDialogueVariableStore`: extension point for variable lookup.
- `DictionaryDialogueVariableStore`: minimal built-in variable store implementation.

These types are intended for the current MVP package workflow. They are not yet documented as a long-term stable external schema.

## Experimental

- The graph authoring surface is built on `UnityEditor.Experimental.GraphView`, which remains experimental in Unity 6.x.

## Out of scope for the current MVP

- production in-game dialogue UI
- advanced expression authoring
- undo/redo for NPC, Dialogue, and dialogue-settings edits outside node scope
- Function node execution
- Scene node execution
- Debug node execution

## Tests available

EditMode coverage currently exists for:

- runtime traversal and choice handling
- graph utility deletion and autosave round-tripping
- preview session transcript, end-state, and backtracking behavior
- graph rendering, link order normalization, empty-state behavior, comment-group movement, nested ownership resolution, clipboard cut behavior, and keyboard-pan focus rules
- undo/redo for node creation, link edits, node movement, comment resize, selection restoration, and autosave dirty-state reset
