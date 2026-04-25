# New Dial Dialogue Editor

`New Dial Dialogue Editor` is a Unity UPM package for graph-based dialogue and cutscene authoring.

- Package name: `com.danilkashulin.newdial.dialogue-editor`
- Version: `0.1.0`
- Unity: `6000.0`

The package is intentionally scoped as an MVP. It already includes a reusable runtime dialogue data model, editor authoring tools, preview support, autosave, sample content, and EditMode tests, but it is not yet a full production narrative stack.

## What ships today

- dialogue database asset model built around NPCs, dialogues, graphs, nodes, and links
- graph editor for text, function, scene, debug, and comment nodes with ordered links, conditions, voice-key/localization metadata, choice-style branches, content-language switching, and an EN/RU editor language switcher
- explicit identifier editing for NPCs, dialogues, and nodes, including empty/duplicate warnings
- choice-flow diagnostics for choice nodes, broken targets, fallback labels, and ordering issues
- guided condition editing with generic variable checks, hints, and project-provided key suggestions
- per-dialogue speaker rosters with text-node speaker binding and preview speaker labels
- rich-text body authoring for text nodes with bold, italic, user-editable text colors, clear formatting, and formatted sanitized previews
- TSV/CSV localization import/export for Google Sheets dialogue rows using `Conversation/<conversationId>/Entry/<n>/Dialogue Text` keys, including selected or all-conversation batch import
- preview test variables with blocked-state explanations for conditions and broken flow
- Where Used blocks with internal references and a project-extensible external reference resolver
- native Unity undo/redo for graph-node operations and node-inspector edits
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

- Main entry point: `Tools/New Dial/Dialogue Editor`
- Alternate entry point: `Window/New Dial/Dialogue Editor`
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
| Implemented | Runtime dialogue database model | `DialogueDatabaseAsset`, NPC/dialogue records, per-dialogue speakers, graph nodes and links |
| Implemented | Traversal helper | `DialoguePlayer` supports start, linear advance, choice selection, executable node execution, and pending execution resume |
| Implemented | Conditions | Lightweight start/node gating through `ConditionData` and evaluator interfaces |
| Implemented | Graph authoring | Text nodes, speaker binding, rich-text body markup, comment nodes, ordered links, voice-key metadata, choice text, and details editing |
| Implemented | Executable nodes | Generic `Function`, `Scene`, and `Debug` nodes with primitive argument bags |
| Implemented | Execution extension points | Project-provided registries and executors drive concrete function and scene behavior |
| Implemented | Identifier management | NPC, dialogue, and node ids can be edited explicitly; node id changes update internal graph links |
| Implemented | Choice-flow diagnostics | Choice-mode nodes warn about missing outputs, invalid targets, fallback labels, order conflicts, and unreachable targets |
| Implemented | Guided conditions | Condition fields show generic variable-check operators, hints, and optional project key suggestions |
| Implemented | Preview test variables | Preview can simulate bool, number, and string values and show blocked-state explanations |
| Implemented | Where Used | Editor shows internal references and can display project-provided external references |
| Implemented | Undo/redo | Native Unity undo/redo for node creation, deletion, movement, resize, links, and node/link inspector edits |
| Implemented | Preview workflow | Transcript history, `Next`, `Back`, `Restart`, and `Jump To Active Node` |
| Implemented | Autosave | JSON snapshots stored under the consuming Unity project's `Library` folder |
| Implemented | Package sample | `Basic Adventure Sample` shows branching, conditions, and comment notes |
| Experimental | Graph surface | Built on `UnityEditor.Experimental.GraphView` |
| Out of scope | Production dialogue UI | No shipped in-game conversation presentation |
| Out of scope | Advanced expressions | Current conditions are intentionally lightweight |
| Out of scope | Project-tree undo/redo | NPC, Dialogue, and dialogue-settings edits outside node scope are not covered yet |
| Out of scope | Project-specific execution logic | No gameplay, battle, quest, reward, or Addressables adapters ship in the package |

## Runtime API at a glance

- `DialogueDatabaseAsset`: top-level `ScriptableObject` for serialized dialogue content
- `NpcEntry`: NPC container with a list of dialogues
- `DialogueEntry`: dialogue record with speakers, a start condition, and graph payload
- `DialogueSpeakerEntry`: per-dialogue speaker id and display name
- `DialogueGraphData`: graph container for nodes and links
- `DialogueTextNodeData`: playable text node with body text, optional speaker and voice-key metadata, start-node, and choice-mode flags
- `DialogueRichTextUtility`: supported rich-text sanitizer, plain-text stripper, and selection wrapper for dialogue body markup
- `FunctionNodeData`: generic project-function node with primitive arguments and failure policy
- `SceneNodeData`: generic scene request node with scene key, load mode, optional entry/transition ids, and parameters
- `DebugNodeData`: lightweight logging node for diagnostics
- `CommentNodeData`: editor grouping and annotation node
- `NodeLinkData`: ordered outgoing edge with optional `ChoiceText`
- `ConditionData` and `ConditionType`: lightweight `None`, `VariableCheck`, and `Custom` condition metadata
- `DialoguePlayer`: runtime traversal helper for starting, advancing, choosing branches, and resolving the current speaker
- `DialogueExecutionResult`: result contract for success, failure, pending, and end-dialogue execution outcomes
- `IDialogueExecutionRegistry`, `IDialogueFunctionExecutor`, and `IDialogueSceneExecutor`: extension points for project metadata and executable behavior
- `DialogueChoice`: resolved choice option exposed by `DialoguePlayer`
- `IDialogueConditionEvaluator`: custom condition-evaluation extension point
- `IDialogueVariableStore`: variable lookup extension point
- `DictionaryDialogueVariableStore`: minimal built-in variable store

These APIs are suitable for the current MVP package workflow, but they should not yet be treated as a finalized long-term public schema.

## Sample flow

1. Import `Basic Adventure Sample` from Package Manager.
2. Run `Tools/New Dial/Create Basic Adventure Sample`.
3. Save the generated `DialogueDatabaseAsset` into the current Unity project.
4. Open it with `Tools/New Dial/Dialogue Editor`.
5. Use `Preview` from the editor toolbar to inspect the branching flow.

## Notes

- Autosaves are written to the consuming Unity project's `Library/DialogueEditorAutosaves` folder.
- The editor currently prompts to save or discard unsaved changes before opening another dialogue database.
- `Cmd+Z` on macOS and `Ctrl+Z` on Windows restore node-scope graph and inspector changes through Unity's undo stack.
- Empty-graph messaging, nested comment-group movement, and clipboard-based group cutting are part of the current editor behavior.
- The editor language is a per-user preference saved in `EditorPrefs`; English is the default and Russian can be selected from the graph toolbar without reopening Unity.
- The content-language dropdown is separate from the editor UI language and controls which localized body text is displayed and edited in graph, inspector, and preview surfaces. New databases show only `ru`; extra language options appear after CSV/TSV import or other localized node data is present, including while the editor UI is set to Russian.
- `Localization` opens a TSV/CSV import/export window. Imported tables are grouped by `Conversation`; selected conversations or all conversations can be imported in one pass. Existing dialogues with matching `Dialogue.Id` values are updated, and missing conversations create new dialogues under the selected/current NPC.
- The first import into an empty dialogue creates a vertical top-to-bottom chain of text nodes; repeat imports match by `LocalizationKey` and update text data without rebuilding links, positions, executable nodes, speakers, conditions, or choice flags.
- WASD pans the focused graph canvas with smoothed screen-space movement so large graphs remain navigable without delayed-frame jumps.
- Text and executable nodes can be selected by clicking any non-button part of the node. Dragging from the lower half still starts link creation.
- Each dialogue has a speaker roster. Text nodes can bind to a speaker, and empty or missing speaker references fall back to the first speaker in that dialogue.
- Text node body text supports a small Unity/TMP-like rich-text subset: `<b>`, `<i>`, and `<color=#RRGGBB>`. Unsupported tags are preserved in authored data and shown as plain text in editor previews.
- Rich-text color slots are user editor preferences: `+` adds an empty slot, text colors use `#RRGGBB`, and each slot includes a swatch, exact hex input, inline circular palette, brightness gradient bar, and `Apply` action for the current selection.
- Text node `VoiceKey` values are stable string metadata for project-side voiceover or localization lookup; the package does not play audio clips, call FMOD events, or resolve voice assets itself.
- Text node `LocalizationKey` values are stable table row keys, while `BodyText` remains the Russian/default fallback for older content.
- NPC, dialogue, and node identifiers are editable in the editor. Node identifier regeneration updates internal graph links; NPC and dialogue id changes warn about possible external references, but external reference lookup is not implemented yet.
- Choice-mode node inspectors show authoring diagnostics for broken or unclear choice flows before entering play mode.
- Projects can register `IDialogueConditionMetadataProvider` implementations for condition key suggestions, `IDialogueExternalReferenceResolver` implementations for external Where Used results, and `IDialogueExecutionRegistry` implementations for executable function/scene metadata.
- Function and scene nodes intentionally use only primitive arguments. Project-specific payloads such as battles, deck state, rewards, and quest outcomes belong in project-side executors.
- The preview variable sandbox uses the built-in editor-side condition evaluator and is not intended to exactly reproduce project runtime logic.
- For a fuller implementation snapshot, see [`../docs/current-state.md`](../docs/current-state.md).
