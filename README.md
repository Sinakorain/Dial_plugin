# Dial_plugin

`Dial_plugin` currently hosts one Unity UPM package: `new_dial`.

The package is an MVP dialogue and cutscene editor for Unity 6.x. It combines a reusable runtime dialogue data model with editor tooling for graph-based authoring, preview, autosave, sample content, and EditMode tests.

## Current status

- Active MVP package, not a finished production narrative stack
- Targets Unity `6000.0`
- Covers runtime data, traversal, graph editing, rich-text authoring/preview, autosave, package samples, and EditMode tests
- Still excludes production in-game UI, advanced condition expressions, undo/redo, and project-specific Function/Scene execution adapters

## Repository layout

- [`new_dial/`](new_dial/README.md): the package itself, including `Runtime`, `Editor`, `Samples~`, and `Tests`
- [`docs/current-state.md`](docs/current-state.md): implementation snapshot of the current working tree
- [`docs/codex-workflow.md`](docs/codex-workflow.md): reusable Codex task, validation, and review guidance for this repository
- [`CONTRIBUTING.md`](CONTRIBUTING.md): contributor workflow and validation expectations
- [`AGENTS.md`](AGENTS.md): short Codex-oriented repository instructions

## Where to start

- To use the package, begin with [`new_dial/README.md`](new_dial/README.md).
- To understand what is implemented right now, read [`docs/current-state.md`](docs/current-state.md).
- To contribute safely, read [`CONTRIBUTING.md`](CONTRIBUTING.md).
- To work with Codex, read [`AGENTS.md`](AGENTS.md) and [`docs/codex-workflow.md`](docs/codex-workflow.md).

## Package highlights

- `DialogueDatabaseAsset` stores NPCs, dialogues, per-dialogue speakers, graph nodes, links, and start conditions
- Editor workflow for creating or loading dialogue databases from `Tools/New Dial/Dialogue Editor`
- Graph authoring for header-editable text, answer, executable, and comment nodes, ordered links, and choice-based branches
- Preview flow with transcript history, restart, backtracking, and active-node jump
- Autosave snapshots stored under the consuming Unity project's `Library/DialogueEditorAutosaves`
- `Basic Adventure Sample` that demonstrates branching dialogue, comment notes, and simple conditions
