# Current State

This file describes the current working-tree behavior of `new_dial`, including changes that exist locally but may not be part of a tagged release yet.

## Snapshot

- Package: `com.danilkashulin.newdial.dialogue-editor`
- Display name: `New Dial Dialogue Editor`
- Version: `0.1.0`
- Unity target: `6000.0`
- Main editor entry point: `Tools/New Dial/Dialogue Editor`
- Alternate editor entry point: `Window/New Dial/Dialogue Editor`
- Sample creation entry point: `Tools/New Dial/Create Basic Adventure Sample`
- Test assembly: `NewDial.DialogueEditor.Tests.Editor`

## Implemented

### Data model

- `DialogueDatabaseAsset` is the top-level `ScriptableObject` and stores a list of `NpcEntry`.
- `NpcEntry` stores an `Id`, a display `Name`, and a list of `DialogueEntry`.
- `DialogueEntry` stores an `Id`, `Name`, per-dialogue `Speakers`, `StartCondition`, and a `DialogueGraphData`.
- `DialogueSpeakerEntry` stores a stable `Id` and display `Name` for a speaker available inside one dialogue.
- `DialogueGraphData` stores `Nodes` through `SerializeReference` plus ordered `Links`.
- `DialogueTextNodeData` is the main playable text node type. It carries backward-compatible `BodyText`, a `LocalizationKey`, per-language `LocalizedBodyText`, optional `SpeakerId` and `VoiceKey` metadata, `IsStartNode`, and `UseOutputsAsChoices`.
- `DialogueTextLocalizationUtility` resolves and updates localized text by language code with fallback to `BodyText`.
- `DialogueRichTextUtility` defines the supported `BodyText` rich-text subset, strips supported tags for plain text, wraps selection ranges, validates strict custom color codes, sanitizes preview strings, and parses sanitized text into styled runs for editor rendering.
- `FunctionNodeData`, `SceneNodeData`, and `DebugNodeData` are executable runtime node types.
- `DialogueArgumentEntry` and `DialogueArgumentValue` store primitive executable parameters: string, int, float, and bool.
- `CommentNodeData` stores editor-only grouping information: `Area`, `Comment`, and `Tint`.
- `NodeLinkData` connects nodes with ordered outgoing edges and optional `ChoiceText`.
- `ConditionData` plus `ConditionType` represent start conditions and node entry conditions.

### Runtime traversal

- `DialoguePlayer` supports `CanStart`, `Start`, `Next`, `Choose`, current speaker resolution, executable node execution, and pending execution resume.
- `DialogueChoice` exposes the selected `NodeLinkData`, target text node, and resolved display text.
- `IDialogueFunctionExecutor` and `IDialogueSceneExecutor` execute project-provided function and scene behavior.
- `IDialogueExecutionRegistry` supplies optional function and scene descriptors for editor/runtime metadata.
- Executable nodes are entered immediately, do not display text or choices, and continue along the first valid outgoing link sorted by `Order` then stable link id.
- Missing executors and execution failures are handled diagnostically through `DialogueExecutionResult` and node failure policy.
- `IDialogueVariableStore` abstracts variable lookup.
- `DictionaryDialogueVariableStore` is the built-in in-memory implementation.
- `IDialogueConditionEvaluator` abstracts condition evaluation.
- `DefaultDialogueConditionEvaluator` currently supports:
  - pass-through for `ConditionType.None`
  - universal `ConditionType.VariableCheck` lookups through `IDialogueVariableStore`
  - numeric comparisons for `>`, `<`, `>=`, `<=`
  - string comparisons for `==`, `!=`, and `Contains`
  - a `Truthy` operator for simple boolean-like values
  - `ConditionType.Custom` currently evaluates to `false`

### Editor surfaces

- `DialogueStartWindow` opens from `Tools/New Dial/Dialogue Editor` and remains available from `Window/New Dial/Dialogue Editor`.
- The start window can create a new `DialogueDatabaseAsset` or load an existing asset inside the current Unity project.
- `DialogueEditorWindow` is the main authoring surface. Its toolbar exposes `New`, `Load`, `Save`, `Preview`, `Localization`, `Delete`, `Dialogue Settings`, a per-user content-language switcher for authored text, and a separate `EN/RU` editor UI language switcher backed by `EditorPrefs`.
- The palette supports `Text Node`, `Comment`, `Function`, `Scene`, and `Debug`.
- Dialogue settings expose a speaker roster editor. Text node inspectors can bind a line to a speaker from that dialogue.
- Text node inspectors prioritize speaker and body editing, with `LocalizationKey` kept near the bottom as lower-priority localization metadata.
- `DialogueLocalizationWindow` imports and exports Google Sheets `.tsv`/`.csv` exports for dialogue rows shaped like `Conversation/<conversationId>/Entry/<n>/Dialogue Text`, with selected or all-conversation batch import.
- Rich-text color and highlight lists are editor-only user preferences saved in `EditorPrefs`; `+` adds an empty hex slot, valid values collapse into color icons, one click selects a color, `Apply` formats the current selection, and double click reopens hex editing.
- `DialoguePreviewWindow` opens from the main editor toolbar for the currently selected dialogue and supports speaker labels, transcript history, advancing, choosing branches, going back, restarting, and jumping to the active node.
- The main editor, graph hints/summaries, preview window, start window, prompts, diagnostics, and inspector labels are localized for English and Russian. Authored dialogue content, serialized field names, ids, and public APIs remain unchanged.
- `DialogueEditorAutosaveStore` serializes snapshots as JSON into the consuming Unity project's `Library/DialogueEditorAutosaves` folder.
- Executable node inspectors support registry-backed metadata when available and free-form editing when no registry is registered.
- Executable validation reports empty function/scene ids, missing required arguments, type mismatches, invalid serialized argument values, and unknown registry references.

### Current editor UX behaviors

- Opening another database while the current one has unsaved changes triggers a save/discard prompt before the switch completes.
- Native Unity undo/redo now covers node-scope graph operations: create/delete, drag, comment resize, links, cut/paste, and node/link inspector edits.
- Undo/redo refreshes graph selection, inspector state, preview sessions, and autosave dirty-state against the last saved database snapshot.
- The graph canvas shows a large, low-contrast grid slightly lighter than the canvas background.
- The graph empty-state warning is visible for an empty graph, hides as soon as the first text or comment node is created, and returns when the last node is deleted.
- Selected NPC and dialogue metadata in the project panel uses compact inline ID and Where Used rows instead of nested cards.
- Dialogue settings speaker roster rows keep the speaker name field and remove action on one line.
- Text and executable nodes select from any non-button body area; lower-half drags still begin link creation and top-half targeting still accepts link drops.
- NPC, dialogue, and node `Id` values are explicitly editable in the editor, with generate and safe-regenerate actions plus immediate empty/duplicate warnings.
- Changing a node `Id` updates internal graph links that referenced the old node `Id`; NPC and dialogue `Id` changes warn about possible external references but do not resolve them yet.
- Text node inspectors expose optional `VoiceKey` metadata for future project-side voiceover, audio, or localization lookup; the package does not resolve or play audio assets itself.
- Text node inspectors expose speaker selection. Empty or missing text-node speaker references fall back to the first speaker in the dialogue roster.
- The content-language toolbar dropdown changes which localized text graph nodes, inspectors, and preview surfaces display and edit; it is independent from the EN/RU editor UI language. A database starts with only `ru`; additional language codes appear after imported or authored localized node data exists, including when the editor UI is set to Russian.
- Localization table import groups rows by `Conversation`, can import checked conversations or all conversations in one pass, updates existing dialogues by matching `Dialogue.Id` to the conversation id, and creates missing dialogues under the selected/current NPC.
- Localization table import creates a vertical top-to-bottom chain of text nodes only when the target dialogue graph is empty. Repeat imports match text nodes by `LocalizationKey` and update only `BodyText`/`LocalizedBodyText`; missing table rows are reported instead of being auto-created.
- Localization table export writes `Keys` plus only the language columns that exist in the selected dialogue data. Empty cells and `Loading...` are treated as missing translations on import.
- Text node `BodyText` supports `<b>`, `<i>`, `<color=#RRGGBB>`, and `<mark=#RRGGBBAA>` markup. Unknown or malformed tags remain in raw `BodyText` and render as plain text in editor previews.
- Inspector, graph, current-line preview, and transcript surfaces render supported rich text through a shared segmented UI Toolkit renderer for bold, italic, color, and highlight.
- Choice-mode text nodes show editor diagnostics for missing outgoing links, broken targets, empty/fallback choice labels, conflicting link order, negative link order, and unreachable choice targets.
- Condition editing uses generic `None`, `VariableCheck`, and `Custom` types; irrelevant fields are hidden, operators come from built-in metadata, hints explain expected values, and projects can register key suggestions.
- The preview window includes a bool/number/string test-variable sandbox and explains blocked dialogue starts, unavailable choices, missing targets, branch ends, and generic fallback labels.
- Collapsible Where Used blocks show internal NPC/dialogue/node references and can include project-provided external references through an editor resolver registry.
- Comment groups can own both text nodes and nested comment groups.
- Nested ownership prefers the most specific containing comment group when several comment areas overlap.
- Moving a parent comment group moves text nodes and nested comment groups that were directly contained when the drag started; nodes newly overlapped during the drag are not attached mid-drag.
- Cutting a selected root comment group removes the full nested hierarchy from the graph after copying it to the clipboard payload.
- Clipboard shortcuts for copy, cut, and paste are implemented in the graph view.
- WASD pans only while the graph canvas is focused, uses screen-space speed independent of zoom, keeps mouse-dragged selected nodes under the cursor with a drag-start baseline during pan, and clamps delayed editor ticks to avoid large jumps on big graphs.

### Sample content

`Basic Adventure Sample` creates one NPC named `Innkeeper` with two dialogues:

- `Greeting`: branching choice dialogue with room, rumor, and trust-gated branches
- `After Hours`: a dialogue gated by a start condition (`tavern_open == false`)

The sample also includes:

- an `Innkeeper` speaker roster entry used by text nodes through default speaker fallback
- link-level choice text
- a `UseOutputsAsChoices` start node
- a simple numeric variable condition
- a comment node used as an in-editor design note

## Runtime API at a glance

- `DialogueDatabaseAsset`: top-level serialized dialogue database asset intended for editor authoring and runtime consumption.
- `NpcEntry`: serializable NPC container that groups dialogues.
- `DialogueEntry`: serializable dialogue record with speakers, start condition, and graph payload.
- `DialogueSpeakerEntry`: serializable per-dialogue speaker record.
- `DialogueGraphData`: serializable graph container for node and link data.
- `DialogueTextNodeData`: playable dialogue node model with localization key/text data plus optional speaker and voice-key metadata.
- `DialogueLocalizedTextEntry` and `DialogueTextLocalizationUtility`: per-language text storage and fallback helpers.
- `DialogueRichTextUtility`: helper for supported dialogue body rich text.
- `FunctionNodeData`: executable project function node model.
- `SceneNodeData`: executable scene request node model.
- `DebugNodeData`: executable diagnostic log node model.
- `CommentNodeData`: editor grouping/annotation node, not a runtime dialogue line.
- `NodeLinkData`: ordered outgoing connection between nodes with optional choice text.
- `ConditionData` and `ConditionType`: lightweight condition metadata for start and node gating.
- `DialoguePlayer`: runtime traversal helper for moving through a dialogue graph and resolving the current speaker.
- `DialogueChoice`: resolved branch option returned by `DialoguePlayer`.
- `IDialogueConditionEvaluator`: extension point for custom condition evaluation.
- `IDialogueVariableStore`: extension point for variable lookup.
- `DictionaryDialogueVariableStore`: minimal built-in variable store implementation.
- `DialogueExecutionResult`: success/failure/pending/end-dialogue execution result.
- `IDialogueExecutionRegistry`: optional metadata source for function and scene descriptors.
- `IDialogueFunctionExecutor` and `IDialogueSceneExecutor`: project-side execution adapters.
- `IDialogueConditionMetadataProvider`: editor extension point for project-specific condition key suggestions.
- `IDialogueExternalReferenceResolver`: editor extension point for project-specific Where Used results.

These types are intended for the current MVP package workflow. They are not yet documented as a long-term stable external schema.

## Experimental

- The graph authoring surface is built on `UnityEditor.Experimental.GraphView`, which remains experimental in Unity 6.x.

## Out of scope for the current MVP

- production in-game dialogue UI
- advanced expression authoring
- undo/redo for NPC, Dialogue, and dialogue-settings edits outside node scope
- Project-specific gameplay execution logic inside the package
- Addressables-based scene loading
- Battle/deck/quest/reward schemas in package node data

## Tests available

EditMode coverage currently exists for:

- runtime traversal and choice handling
- executable node traversal, failure policy, pending resume, primitive argument cloning, validation, and missing executor fallback behavior
- graph utility deletion and autosave round-tripping
- preview session transcript, end-state, and backtracking behavior
- graph rendering, link order normalization, empty-state behavior, comment-group movement, nested ownership resolution, clipboard cut behavior, and keyboard-pan focus rules
- undo/redo for node creation, link edits, node movement, comment resize, selection restoration, and autosave dirty-state reset
- identifier validation and node `Id` rename link preservation
- choice-flow diagnostics for choice-mode nodes
- executable graph rendering and inspector behavior
- guided condition editor behavior, preview blocked-state explanations, and Where Used resolver results
- localization table parsing, batch import, repeated text-data updates, TSV export, content-language editing, and Russian UI language-list refresh after import
