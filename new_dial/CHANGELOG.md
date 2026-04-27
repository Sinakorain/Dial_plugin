# Changelog

## Unreleased

### Added

- Added generic `Function`, `Scene`, and `Debug` executable node data with primitive argument support.
- Added execution extension points for project-provided function/scene metadata and executors.
- Added runtime traversal support for immediate executable-node execution, pending resume, failure policy handling, and automatic next-link traversal.
- Added editor palette, graph rendering, inspectors, validation, and autosave support for executable nodes.
- Added an editor EN/RU language switcher with localized toolbar, palette, inspector, preview, diagnostics, prompts, and node summaries.
- Added optional `VoiceKey` metadata on text nodes for future project-side voiceover/audio lookup.
- Added per-dialogue speaker rosters, text-node speaker binding, current-speaker runtime resolution, and speaker labels in graph and preview UI.
- Added a text-node rich-text toolbar, user-editable text-color slots saved in `EditorPrefs`, sanitized graph/preview rendering, and runtime helpers for supported dialogue body markup.
- Added text-node localization keys, per-language body text storage, content-language switching, and TSV/CSV dialogue localization import/export tooling.
- Added batch TSV/CSV localization import for selected conversations or all conversations in a loaded table.
- Added saved, customizable palette shortcuts for creating graph nodes from the focused canvas.
- Added visible answer nodes plus an `Add Choice` text-node action that creates playable `Text question -> Answer line` branches.
- Added editable graph header titles for text, answer, function, scene, debug, and comment nodes.
- Added inline graph editing for text-node body and answer-node button/body text.
- Added database-wide typed dialogue variables, mutable runtime session state, automatic condition suggestions, and a built-in `newdial.variable.set` function for Function nodes.

### Changed

- Dialogue editor can now be opened from `Tools/New Dial/Dialogue Editor` while retaining the existing `Window/New Dial/Dialogue Editor` entry point.
- NPC, dialogue, and node identifiers can now be edited explicitly in the editor, with guarded generation actions and immediate empty/duplicate warnings.
- Node identifier changes now update internal graph links that reference the old node id.
- Choice-mode nodes now surface authoring diagnostics for missing answers, broken targets, fallback labels, legacy answer links, order conflicts, and unreachable choice targets.
- Choice text is now edited on answer nodes; answer nodes now show their own body text after `Choose()`, and legacy link-level `ChoiceText` remains readable for older direct-choice graphs.
- Text nodes now automatically expose outgoing `Answer` nodes as choices even when the legacy `UseOutputsAsChoices` flag is off.
- Single-clicking any node area now opens the details inspector, while inline fields remain editable directly on the graph.
- Condition editing now uses guided operator choices, generic variable-check hints, database variable suggestions, typed value controls for known variables, and optional project-provided key suggestions.
- Bool variable checks now normalize to explicit `== true`/`!= true` comparisons and tolerate existing empty bool comparison values as `true`.
- Dialogue start-gate conditions now use clearer editor wording, include a one-click clear action, and preview blocked-state reasons show current variable values when available.
- Removed game-specific condition types (`QuestState`, `TrustLevel`, `Fact`, and `GlobalVariableCheck`); conditions now use generic `VariableCheck` or project-defined `Custom`.
- Dialogue preview now seeds variables from database defaults, applies test-variable overrides, executes built-in variable sets, and explains blocked starts, unavailable choices, missing targets, branch ends, and fallback labels.
- Dialogue preview variable controls now list database variables automatically so their sandbox values can be toggled without manually adding matching test keys.
- Dialogue preview variable controls now reflect runtime variable changes after built-in Set Variable execution.
- Dialogue preview transcript choice entries now render the selected choice as an outlined chip before the target node text.
- Added Where Used sections with internal references and a project-extensible external reference resolver registry.
- Prompt to save or discard unsaved changes before opening another dialogue database in the editor.
- Graph empty-state visibility now updates when the first node is created and when the last node is removed.
- Graph canvas now uses a large, low-contrast grid slightly lighter than the canvas background.
- Graph zoom now uses a single content zoom manipulator so node positions and relative layout no longer drift while changing scale.
- Graph links are now slightly thicker, highlight subtly on hover, and can be removed with `Cmd`/`Ctrl` + left click on the link.
- Graph links now use restrained smoothed curves instead of large S-shaped waves.
- Graph link hover and click hit-testing now uses panel-space pointer coordinates so the interactive zone matches the visual link more closely.
- `Cmd`/`Ctrl` + `Delete`/`Backspace` on the graph canvas now uses the same comment-delete prompt as plain `Delete`/`Backspace`.
- Selected NPC and dialogue metadata in the project panel now uses compact inline rows instead of nested cards.
- Left dock sections now use collapsible Project, Palette, and Variables foldouts, with Palette shown before Variables.
- Left dock foldout headers now blend into the rounded panel surface without an inner square seam.
- Scene, function, start, and debug graph-node badges now use distinct semantic colors.
- Nested comment-group ownership and movement behavior now follow the most specific containing comment group.
- Dragging a comment group now keeps its drag-start membership instead of attaching newly overlapped nodes mid-drag.
- Comment inspectors now separate deleting only the comment node from deleting the whole comment group with contained nodes.
- Hotkey-delete for a single selected comment now removes empty comments immediately and prompts between comment-only and group deletion when the comment contains nodes.
- Cutting a selected root comment group removes the full nested hierarchy after copying it to the clipboard payload.
- Native Unity undo/redo now covers node-scope graph edits, comment resize, link edits, and node inspector changes on both macOS (`Cmd+Z`) and Windows (`Ctrl+Z`).
- Undo/redo now refreshes graph selection, preview sessions, and autosave dirty-state against the last saved database snapshot.
- Text, answer, executable, and comment nodes now edit their display title from the node header instead of a separate graph-body title field.
- Editable node header titles now use a compact field background with hover/focus feedback.
- Newly created graph nodes now get globally numbered default titles such as `Text Node_1` and `Answer_2`.
- Text and answer nodes now select from any non-field part of the node while preserving lower-half link dragging outside inline editors.
- Inline text and answer fields now soft-wrap visually, keep the initial node width, and grow downward without inserting automatic newline characters.
- Graph node meta hints are now shorter and clipped cleanly inside node cards.
- Scene node inspectors now write the first available Known Scene into an empty `SceneKey` instead of only showing it as the dropdown default.
- The editor left dock now keeps the NPC/dialogue project area at a fixed height and shows the full compact node palette without palette scrolling.
- Existing dialogues without speakers now receive a default speaker from their owning NPC when opened in the editor.
- Rich-text previews now use segmented UI Toolkit rendering for bold, italic, and text color instead of relying only on `Label.enableRichText`.
- Text-node body fields now visually wrap long raw lines in the inspector.
- Text-node inspectors now keep the localization key near the bottom so core authoring fields stay prominent.
- Text-node body fields now place the label above the multiline editor so the text box uses the full inspector width.
- Text-node inspector checkboxes now align by using fixed-width wrapping labels.
- Palette shortcuts now create nodes near the cursor when it is over the canvas or near the current viewport center otherwise, clamping them into view.
- Palette shortcut node creation now searches nearby visible positions so new nodes do not spawn directly on top of existing nodes.
- Palette shortcuts now create the first NPC and dialogue in an empty database before adding the requested node, matching palette drag/drop behavior.
- The start window is now a compact floating launcher and no longer mentions cutscenes.
- Localization import/export now lives in a collapsed advanced section of the unified dialogue launcher instead of a separate menu item.
- Fixed the start launcher opening path so it centers new windows after Unity creates them and keeps the current top-left position when the import/export section is toggled.
- The collapsed import/export entry is now visible in the launcher's initial compact size.
- The expanded import/export launcher size is now tighter and no longer leaves a large empty area below the advanced controls.
- Dialogue settings speaker rows now keep speaker name editing and removal controls on one line.
- Rich-text toolbar formatting now preserves selected text ranges when applying bold, italic, or text color.
- Rich-text color slots now keep hex editing visible and include an inline circular color palette with a brightness gradient bar.
- Rich-text color picker now uses a smoother color wheel edge and a brightness gradient bar.
- Removed rich-text highlight authoring and `<mark>` support; highlight tags now render as unsupported plain text.
- Localization imports update existing text-node and answer-node body data by `LocalizationKey` without rebuilding graph links, positions, executable nodes, conditions, speakers, or choice flags.
- Localization imports now update existing dialogues by matching `Dialogue.Id` to the imported conversation id, creating only missing dialogues.
- First-time localization imports now create a vertical top-to-bottom text-node chain instead of a horizontal row.
- WASD graph panning is restored for focused canvas navigation while remaining blocked during inline field editing.
- WASD graph panning now clears movement state on focus changes, inline fields, and modifier-key combinations, and preserves the current zoom while panning, including left and diagonal movement on zoomed-out graphs.
- WASD graph panning now consumes handled key events so Unity GraphView shortcuts such as `A` cannot trigger frame or zoom changes while navigating the canvas.
- WASD graph panning now also handles UI Toolkit character key events, preventing the built-in GraphView `A` frame-all shortcut from recentering the canvas.
- Handled graph canvas keyboard shortcuts now mark the underlying IMGUI event as used, avoiding Windows system beeps for shortcuts such as `Alt+1`.
- WASD graph panning now runs its editor tick only while movement input is active, clears stale movement on graph reload or panel detach, and avoids consuming unrelated key-up events when the canvas is not focused.
- Graph canvas keyboard and focus handling is now isolated in a dedicated input controller, and WASD now behaves as reserved camera-style viewport movement instead of being available as plain palette shortcuts.
- WASD graph panning no longer clears active movement merely because the pointer leaves the canvas.
- Inline Text and Answer node text boxes now apply soft wrapping directly to their UI Toolkit text input internals.
- Palette shortcut rebinding now shows the compact `Press` prompt so it fits inside the shortcut pill.
- Palette shortcut rebinding now cancels pending palette clicks so double-clicking a palette item does not also create that node.
- Content-language choices now refresh after localization import, including when the editor UI language is Russian.
- Where Used reference details are now collapsed by default in editor inspectors.
- Rich-text previews now preserve visible spaces at formatting boundaries.

### Tests

- Expanded `DialogueGraphViewTests` to cover empty-state visibility, nested comment ownership, direct parent resolution, nested group cutting, and WASD focus recovery behavior.
- Added undo/redo coverage for node creation, link changes, node movement, comment-group movement, comment resize, and grouped cut behavior.
- Added `DialogueEditorWindowTests` for selection restoration, inspector refresh, and dirty/autosave reset after undo.
- Added text-node voice-key clone and inspector editing coverage.
- Added choice-flow diagnostic coverage for choice nodes, fallback labels, broken targets, order conflicts, and inspector warnings.
- Added coverage for guided condition fields, preview test-variable gating, blocked-state reasons, and Where Used external resolver results.
- Added coverage for editor language switching, localized node summaries, and full-node runtime selection.
- Added coverage for speaker cloning, runtime speaker fallback, speaker inspector editing, roster removal, and autosave restore.
- Added coverage for rich-text wrapping, sanitizing, stripping, parsing, user color-list persistence, inline color picking, strict color validation, inspector preview refresh, toolbar selection formatting, and graph preview rendering.
- Added coverage for localization table parsing, first-import linear node creation/layout, repeat-import data-only updates, TSV export, and content-language editing.
- Added coverage for batch localization import and Russian UI content-language refresh after import.
- Added coverage for vertical localization import layout and WASD graph panning.
- Added coverage for WASD key-up focus handling, interactive graph fields, and graph reload movement-state reset.
- Added coverage for camera-direction WASD movement, character-based `A` input, Alt-digit event consumption, mouse-leave handling, canvas copy/paste shortcuts, and reserved plain-WASD palette bindings.

### Docs

- Added proprietary license files and package metadata notices that reserve all rights while the repository is publicly visible for review.
- Added `docs/codex-workflow.md` with reusable Codex task, planning, validation, documentation, and review guidance for the package repository.
- Updated repository, package, sample, contribution, and integration-task docs to reference the Codex workflow and make task constraints/done criteria explicit.
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
