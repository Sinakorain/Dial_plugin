# Current State

This file describes the current working-tree behavior of `new_dial`, including changes that exist locally but may not be part of a tagged release yet.

## Snapshot

- Package: `com.danilkashulin.newdial.dialogue-editor`
- Display name: `New Dial Dialogue Editor`
- Version: `0.1.0`
- Unity target: `6000.0`
- License: proprietary; all rights reserved; see `new_dial/LICENSE.md`
- Main editor entry point: `Tools/New Dial/Dialogue Editor`
- Alternate editor entry point: `Window/New Dial/Dialogue Editor`
- Sample creation entry point: `Tools/New Dial/Create Basic Adventure Sample`
- Test assembly: `NewDial.DialogueEditor.Tests.Editor`
- Codex workflow guidance: `docs/codex-workflow.md`

## Implemented

### Data model

- `DialogueDatabaseAsset` is the top-level `ScriptableObject` and stores database-wide `DialogueVariableDefinition` entries plus a list of `NpcEntry`.
- `DialogueVariableDefinition` stores a variable `Key`, optional `DisplayName`, primitive `Type`, and typed default value.
- `NpcEntry` stores an `Id`, a display `Name`, and a list of `DialogueEntry`.
- `DialogueEntry` stores an `Id`, `Name`, per-dialogue `Speakers`, `StartCondition`, and a `DialogueGraphData`.
- `DialogueSpeakerEntry` stores a stable `Id` and display `Name` for a speaker available inside one dialogue.
- `DialogueGraphData` stores `Nodes` through `SerializeReference` plus ordered `Links`.
- `DialogueTextNodeData` is the main playable text node type. It carries backward-compatible `BodyText`, a `LocalizationKey`, per-language `LocalizedBodyText`, optional `SpeakerId` and `VoiceKey` metadata, `IsStartNode`, and `UseOutputsAsChoices`.
- `DialogueChoiceNodeData` is the playable answer-line node type. It stores `ChoiceText` for the player-facing button plus its own `BodyText`, localization, optional `SpeakerId`, and `VoiceKey`.
- `DialogueTextLocalizationUtility` resolves and updates localized text by language code with fallback to `BodyText` for both text and answer-line nodes.
- `DialogueRichTextUtility` defines the supported `BodyText` rich-text subset, strips supported tags for plain text, wraps selection ranges, validates strict custom color codes, sanitizes preview strings, and parses sanitized text into styled runs for editor rendering.
- `FunctionNodeData`, `SceneNodeData`, and `DebugNodeData` are executable runtime node types.
- `DialogueArgumentEntry` and `DialogueArgumentValue` store primitive executable parameters: string, int, float, and bool.
- `CommentNodeData` stores editor-only grouping information: `Area`, `Comment`, and `Tint`.
- `NodeLinkData` connects nodes with ordered outgoing edges; legacy `ChoiceText` remains readable for older direct-choice graphs.
- `ConditionData` plus `ConditionType` represent start conditions and node entry conditions.

### Runtime traversal

- `DialoguePlayer` supports `CanStart`, `Start`, `Next`, `Choose`, current line-node resolution, current speaker resolution, executable node execution, and pending execution resume.
- `DialogueChoice` exposes the selected `NodeLinkData`, optional answer node, target runtime node, target text-node convenience property, and resolved display text.
- `IDialogueFunctionExecutor` and `IDialogueSceneExecutor` execute project-provided function and scene behavior.
- `IDialogueExecutionRegistry` supplies optional function and scene descriptors for editor/runtime metadata.
- Executable nodes are entered immediately, do not display text or choices, and continue along the first valid outgoing link sorted by `Order` then stable link id.
- Missing executors and execution failures are handled diagnostically through `DialogueExecutionResult` and node failure policy.
- `IDialogueVariableStore` abstracts variable lookup.
- `IDialogueVariableState` extends variable lookup with typed mutable values.
- `DialogueVariableState` is the built-in session-state implementation initialized from database variable defaults.
- `DictionaryDialogueVariableStore` remains the built-in string-only compatibility implementation.
- `DialogueBuiltInFunctions.SetVariableFunctionId` (`newdial.variable.set`) is handled by `DialoguePlayer` before project executors and writes to the current mutable variable state.
- `IDialogueConditionEvaluator` abstracts condition evaluation.
- `DefaultDialogueConditionEvaluator` currently supports:
  - pass-through for `ConditionType.None`
  - universal `ConditionType.VariableCheck` lookups through typed `IDialogueVariableState` when available, with `IDialogueVariableStore` string fallback
  - numeric comparisons for `>`, `<`, `>=`, `<=`
  - string comparisons for `==`, `!=`, and `Contains`
  - a `Truthy` operator for simple boolean-like values
  - `ConditionType.Custom` currently evaluates to `false`

### Editor surfaces

- `DialogueStartWindow` opens as a compact floating launcher from `Tools/New Dial/Dialogue Editor` and remains available from `Window/New Dial/Dialogue Editor`.
- The compact start launcher can create a new `DialogueDatabaseAsset`, load an existing asset, or reveal a collapsed advanced `Import / Export` section inside the same window; expanding the section grows the utility window downward from its current top-left position.
- `DialogueEditorWindow` is the main authoring surface. Its toolbar exposes `New`, `Load`, `Save`, `Preview`, `Localization`, `Delete`, `Dialogue Settings`, a per-user content-language switcher for authored text, and a separate `EN/RU` editor UI language switcher backed by `EditorPrefs`.
- The left dock includes a `Variables` section for creating database-wide variables, editing keys/display names/types/default values, and warning about empty or duplicate keys.
- The left dock uses collapsible `Project`, `Palette`, and `Variables` sections, with `Palette` shown before `Variables`.
- The palette supports `Text Node`, `Comment`, `Function`, `Scene`, and `Debug`; palette items show saved editor shortcuts, double-click starts shortcut rebinding with a compact `Press` prompt, and shortcuts create nodes only while the graph canvas is focused. Plain W/A/S/D are reserved for camera-style canvas movement, while modified shortcuts such as `Alt+W` remain bindable. Shortcut-created nodes use the same empty-project flow as palette placement by creating an NPC and dialogue when needed, appear near the cursor when it is over the canvas, otherwise near the current viewport center, and are clamped into the visible viewport.
- Shortcut-created nodes search nearby visible positions around the cursor or viewport center, so they avoid spawning directly on top of existing graph nodes when space is available.
- Dialogue settings expose a speaker roster editor. Text node inspectors can bind a line to a speaker from that dialogue.
- Text node inspectors prioritize speaker and body editing, align checkbox toggles with fixed-width wrapping labels, show the body text label above the full-width multiline editor, and keep `LocalizationKey` near the bottom as lower-priority localization metadata.
- The launcher's advanced import/export section handles Google Sheets `.tsv`/`.csv` exports for dialogue rows shaped like `Conversation/<conversationId>/Entry/<n>/Dialogue Text`, with selected or all-conversation batch import.
- Rich-text text-color slots are editor-only user preferences saved in `EditorPrefs`; `+` adds an empty slot with a swatch, exact `#RRGGBB` hex input, inline circular palette, brightness gradient bar, and `Apply` action for the current selection.
- `DialoguePreviewWindow` opens from the main editor toolbar for the currently selected dialogue and supports speaker labels, transcript history, advancing, choosing branches, going back, restarting, and jumping to the active node.
- Preview transcript choice entries render the selected choice as an outlined button-like chip followed by a colon and the target node text on the next line.
- The main editor, graph hints/summaries, preview window, start window, prompts, diagnostics, and inspector labels are localized for English and Russian. Authored dialogue content, serialized field names, ids, and public APIs remain unchanged.
- `DialogueEditorAutosaveStore` serializes snapshots as JSON into the consuming Unity project's `Library/DialogueEditorAutosaves` folder.
- Executable node inspectors support registry-backed metadata when available and free-form editing when no registry is registered.
- Executable validation reports empty function/scene ids, missing required arguments, type mismatches, invalid serialized argument values, and unknown registry references.

### Current editor UX behaviors

- Opening another database while the current one has unsaved changes triggers a save/discard prompt before the switch completes.
- Native Unity undo/redo now covers node-scope graph operations: create/delete, drag, comment resize, links, cut/paste, and node/link inspector edits.
- Undo/redo refreshes graph selection, inspector state, preview sessions, and autosave dirty-state against the last saved database snapshot.
- The graph canvas shows a large, low-contrast grid slightly lighter than the canvas background.
- Graph zoom uses a single content zoom manipulator; changing scale preserves node canvas positions, comment areas, and relative graph layout while repainting link overlays.
- Graph links render slightly thicker for readability, highlight subtly on hover, and `Cmd`/`Ctrl` + left click on a link deletes that connection.
- `Delete`/`Backspace` and `Cmd`/`Ctrl` + `Delete`/`Backspace` use the same canvas delete flow; a single selected comment with contents prompts between comment-only and group deletion.
- The graph empty-state warning is visible for an empty graph, hides as soon as the first text or comment node is created, and returns when the last node is deleted.
- Selected NPC and dialogue metadata in the project panel uses compact inline ID and Where Used rows instead of nested cards.
- Dialogue settings speaker roster rows keep the speaker name field and remove action on one line.
- Text, answer, executable, and comment node titles are editable directly in the graph header. Text and answer body/button fields soft-wrap visually without inserting automatic newline characters. Single-clicking any node area opens details, inline fields remain directly editable, lower-half drags outside inline fields still begin link creation, and node width stays stable while long text grows downward.
- Newly created graph nodes use globally numbered default titles based on the current graph size, for example `Text Node_1`, `Function_2`, and `Answer_3`.
- Graph node meta hints use compact status text and clip inside the card instead of spilling past the node edge.
- NPC, dialogue, and node `Id` values are explicitly editable in the editor, with generate and safe-regenerate actions plus immediate empty/duplicate warnings.
- Changing a node `Id` updates internal graph links that referenced the old node `Id`; NPC and dialogue `Id` changes warn about possible external references but do not resolve them yet.
- Text node inspectors expose optional `VoiceKey` metadata for future project-side voiceover, audio, or localization lookup; the package does not resolve or play audio assets itself.
- Text node inspectors expose speaker selection. Empty or missing text-node speaker references fall back to the first speaker in the dialogue roster.
- The content-language toolbar dropdown changes which localized text graph nodes, inspectors, and preview surfaces display and edit; it is independent from the EN/RU editor UI language. A database starts with only `ru`; additional language codes appear after imported or authored localized node data exists, including when the editor UI is set to Russian.
- Localization table import groups rows by `Conversation`, can import checked conversations or all conversations in one pass, updates existing dialogues by matching `Dialogue.Id` to the conversation id, and creates missing dialogues under the selected/current NPC.
- Localization table import creates a vertical top-to-bottom chain of text nodes only when the target dialogue graph is empty. Repeat imports match text and answer-line nodes by `LocalizationKey` and update only `BodyText`/`LocalizedBodyText`; missing table rows are reported instead of being auto-created.
- Localization table export writes `Keys` plus only the language columns that exist in the selected dialogue data. Empty cells and `Loading...` are treated as missing translations on import.
- Text node `BodyText` supports `<b>`, `<i>`, and `<color=#RRGGBB>` markup. Unknown or malformed tags, including old `<mark>` highlight tags, remain in raw `BodyText` and render as plain text in editor previews.
- Inspector, graph, current-line preview, and transcript surfaces render supported rich text through a shared segmented UI Toolkit renderer for bold, italic, and text color.
- Text node inspectors include an `Answers` section. `Add Choice` creates a visible playable answer node and connects it as `Text question -> Answer line`; manually linked `Text -> Answer` outputs also appear as choices without requiring the legacy `UseOutputsAsChoices` flag.
- Text and answer nodes can edit their primary text fields directly on the graph, while all node titles are edited from their headers; single-clicking any node area opens the details inspector.
- Answer node inspectors edit `Button Text`, body text, speaker, voice key, localization key, conditions, outgoing next links, and deletion. The answer condition controls whether the choice appears.
- Text nodes with outgoing answers show editor diagnostics for missing answers, broken targets, empty/fallback answer labels, legacy direct-choice links, conflicting link order, negative link order, and unreachable choice targets.
- Condition editing uses generic `None`, `VariableCheck`, and `Custom` types; irrelevant fields are hidden, operators come from built-in metadata, database variables are suggested automatically, typed value controls appear for known variables, and projects can register extra key suggestions.
- Function node metadata includes a built-in `Variables: Set Variable` function (`newdial.variable.set`) with variable-key selection and typed value editing.
- The dialogue inspector labels the dialogue-level start condition as a start gate and provides a clear action so it is not confused with node or branch conditions.
- The preview window starts from database variable defaults, shows database variables as editable sandbox controls, applies bool/number/string test-variable overrides, reflects runtime variable changes after built-in Set Variable execution, and explains blocked dialogue starts with current variable values when available.
- Collapsible Where Used blocks show internal NPC/dialogue/node references and can include project-provided external references through an editor resolver registry.
- Comment groups can own text nodes, answer nodes, executable nodes, and nested comment groups.
- Comment inspectors expose separate delete actions for removing only the comment node or removing the whole comment group with its contained nodes.
- Hotkey-delete for a single selected comment removes empty comments immediately and prompts between comment-only and group deletion when the comment contains nodes.
- Nested ownership prefers the most specific containing comment group when several comment areas overlap.
- Moving a parent comment group moves directly contained nodes and nested comment groups captured when the drag started; nodes newly overlapped during the drag are not attached mid-drag.
- Cutting a selected root comment group removes the full nested hierarchy from the graph after copying it to the clipboard payload.
- Clipboard shortcuts for copy, cut, and paste are implemented in the graph view.
- WASD moves the focused graph canvas viewport like a camera without changing zoom, including left and diagonal movement while zoomed out; handled movement keys consume default GraphView shortcuts, typing in inline or other interactive graph fields does not move the canvas, mouse-leave does not cancel held movement, and movement state is cleared when focus/panel/graph context is lost.
- Graph links use restrained smoothed curves with shared render and hit-test geometry, avoiding large S-shaped waves while keeping the same top/bottom anchors.

### Sample content

`Basic Adventure Sample` creates one NPC named `Innkeeper` with two dialogues:

- `Greeting`: branching choice dialogue with room, rumor, and trust-gated branches
- `After Hours`: a dialogue gated by a start condition (`tavern_open == false`)

The sample also includes:

- an `Innkeeper` speaker roster entry used by text nodes through default speaker fallback
- visible answer nodes with button text and playable reply text
- text-to-answer links that route through visible answer nodes
- a simple numeric variable condition
- a comment node used as an in-editor design note

## Runtime API at a glance

- `DialogueDatabaseAsset`: top-level serialized dialogue database asset intended for editor authoring and runtime consumption.
- `NpcEntry`: serializable NPC container that groups dialogues.
- `DialogueEntry`: serializable dialogue record with speakers, start condition, and graph payload.
- `DialogueVariableDefinition`: serializable database-wide variable definition with typed default value.
- `DialogueSpeakerEntry`: serializable per-dialogue speaker record.
- `DialogueGraphData`: serializable graph container for node and link data.
- `DialogueTextNodeData`: playable dialogue node model with localization key/text data plus optional speaker and voice-key metadata.
- `DialogueChoiceNodeData`: playable answer-line model with editable button text, body text, localization, optional speaker/voice metadata, and optional condition.
- `DialogueLocalizedTextEntry` and `DialogueTextLocalizationUtility`: per-language text storage and fallback helpers.
- `DialogueRichTextUtility`: helper for supported dialogue body rich text.
- `FunctionNodeData`: executable project function node model.
- `SceneNodeData`: executable scene request node model.
- `DebugNodeData`: executable diagnostic log node model.
- `CommentNodeData`: editor grouping/annotation node, not a runtime dialogue line.
- `NodeLinkData`: ordered outgoing connection between nodes; legacy direct-choice text remains supported for older data.
- `ConditionData` and `ConditionType`: lightweight condition metadata for start and node gating.
- `DialoguePlayer`: runtime traversal helper for moving through a dialogue graph and resolving the current speaker.
- `DialogueChoice`: resolved branch option returned by `DialoguePlayer`.
- `IDialogueConditionEvaluator`: extension point for custom condition evaluation.
- `IDialogueVariableStore`: extension point for variable lookup.
- `IDialogueVariableState`: extension point for typed mutable variable state.
- `DialogueVariableState`: built-in mutable session copy initialized from database variable defaults.
- `DictionaryDialogueVariableStore`: minimal string-only variable store implementation.
- `DialogueBuiltInFunctions`: built-in executable function ids, including `newdial.variable.set`.
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
- choice-flow diagnostics for text nodes with answers and legacy choice-mode nodes
- executable graph rendering and inspector behavior
- guided condition editor behavior, database variable suggestions, preview blocked-state explanations, preview/session variable mutation, and Where Used resolver results
- localization table parsing, batch import, repeated text-data updates, TSV export, content-language editing, and Russian UI language-list refresh after import

## Documentation workflow

- `AGENTS.md` intentionally stays short and points agents at the package source of truth plus `docs/codex-workflow.md`.
- `docs/codex-workflow.md` captures reusable Codex prompt shape, planning, validation, documentation, configuration, and review guidance.
- Documentation updates should stay grounded in code, tests, `new_dial/package.json`, or explicit integration task documents.
- The repository still does not define a shell build, lint, CI, or release automation workflow.
