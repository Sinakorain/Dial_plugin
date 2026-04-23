# New Dial Dialogue Editor

`New Dial Dialogue Editor` is a Unity UPM package for graph-based dialogue and cutscene authoring.

- Package name: `com.danilkashulin.newdial.dialogue-editor`
- Version: `0.1.0`
- Unity: `6000.0`

The package is intentionally scoped as an MVP. It already includes a reusable runtime dialogue data model, editor authoring tools, preview support, autosave, sample content, and EditMode tests, but it is not yet a full production narrative stack.

## What ships today

- dialogue database asset model built around NPCs, dialogues, graphs, nodes, and links
- graph editor for text nodes, comment groups, ordered links, conditions, and choice-style branches
- preview window with branching playback, transcript history, backtracking, restart, and active-node jump
- autosave snapshots stored outside tracked assets
- package sample that creates a small branching adventure database
- EditMode coverage for traversal, preview, autosave, and graph behavior

## Installation

Add the package to a Unity 6.x project as a local or embedded UPM package.

### Local package

1. Open Unity Package Manager.
2. Choose `Add package from disk...`.
3. Select `new_dial/package.json` from this repository checkout.

### From a Git host

If you publish this repository to a Git remote, configure the dependency so Unity resolves the `new_dial` subfolder that contains `package.json`.

## Opening the editor

- Main entry point: `Window/New Dial/Dialogue Editor`
- Sample creation: `Tools/New Dial/Create Basic Adventure Sample`
- Asset menu support: `New Dial/Dialogue Database`

The start window can create a new `DialogueDatabaseAsset` or load an existing one from the current Unity project.

## Package layout

- `Runtime`: public data model, condition contracts, variable store contracts, and traversal player
- `Editor`: start window, main graph editor, preview UI, autosave store, and editor styling
- `Samples~`: importable package samples and sample usage notes
- `Tests`: EditMode tests for runtime and editor behavior

## Feature matrix

| Status | Capability | Notes |
| --- | --- | --- |
| Implemented | Runtime dialogue database model | `DialogueDatabaseAsset`, NPC/dialogue records, graph nodes and links |
| Implemented | Traversal helper | `DialoguePlayer` supports start, linear advance, and choice selection |
| Implemented | Conditions | Lightweight start/node gating through `ConditionData` and evaluator interfaces |
| Implemented | Graph authoring | Text nodes, comment nodes, ordered links, choice text, and details editing |
| Implemented | Preview workflow | Transcript history, `Next`, `Back`, `Restart`, and `Jump To Active Node` |
| Implemented | Autosave | JSON snapshots stored under the consuming Unity project's `Library` folder |
| Implemented | Package sample | `Basic Adventure Sample` shows branching, conditions, and comment notes |
| Experimental | Graph surface | Built on `UnityEditor.Experimental.GraphView` |
| Out of scope | Production dialogue UI | No shipped in-game conversation presentation |
| Out of scope | Advanced expressions | Current conditions are intentionally lightweight |
| Out of scope | Undo/redo guarantees | Not documented as production-ready yet |
| Out of scope | Function/Scene/Debug execution | Palette entries are placeholders only |

## Runtime API at a glance

- `DialogueDatabaseAsset`: top-level `ScriptableObject` for serialized dialogue content
- `NpcEntry`: NPC container with a list of dialogues
- `DialogueEntry`: dialogue record with a start condition and graph payload
- `DialogueGraphData`: graph container for nodes and links
- `DialogueTextNodeData`: playable text node with start-node and choice-mode flags
- `CommentNodeData`: editor grouping and annotation node
- `NodeLinkData`: ordered outgoing edge with optional `ChoiceText`
- `ConditionData` and `ConditionType`: lightweight condition metadata
- `DialoguePlayer`: runtime traversal helper for starting, advancing, and choosing branches
- `DialogueChoice`: resolved choice option exposed by `DialoguePlayer`
- `IDialogueConditionEvaluator`: custom condition-evaluation extension point
- `IDialogueVariableStore`: variable lookup extension point
- `DictionaryDialogueVariableStore`: minimal built-in variable store

These APIs are suitable for the current MVP package workflow, but they should not yet be treated as a finalized long-term public schema.

## Sample flow

1. Import `Basic Adventure Sample` from Package Manager.
2. Run `Tools/New Dial/Create Basic Adventure Sample`.
3. Save the generated `DialogueDatabaseAsset` into the current Unity project.
4. Open it with `Window/New Dial/Dialogue Editor`.
5. Use `Preview` from the editor toolbar to inspect the branching flow.

## Notes

- Autosaves are written to the consuming Unity project's `Library/DialogueEditorAutosaves` folder.
- The editor currently prompts to save or discard unsaved changes before opening another dialogue database.
- Empty-graph messaging, nested comment-group movement, and clipboard-based group cutting are part of the current editor behavior.
- For a fuller implementation snapshot, see [`../docs/current-state.md`](../docs/current-state.md).
