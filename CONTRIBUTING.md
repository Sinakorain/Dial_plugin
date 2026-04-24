# Contributing

## Scope

This repository stores a Unity UPM package, not a standalone Unity project. Most contribution work happens inside `new_dial/`.

## Working with the package

1. Open a Unity 6.x project that will consume the package.
2. Add `new_dial/` to that project as a local package in the Package Manager, or work with it as an embedded package during development.
3. Make code and documentation changes in this repository, not inside generated Unity package cache folders.

## Key directories

- `new_dial/Runtime`: runtime data model and traversal code
- `new_dial/Editor`: editor windows, graph view, preview, autosave, styling
- `new_dial/Samples~`: importable sample content and sample docs
- `new_dial/Tests/Editor`: EditMode tests
- `docs/current-state.md`: implementation snapshot for the current working tree

## Validation workflow

### EditMode tests

Run the `NewDial.DialogueEditor.Tests.Editor` assembly in Unity Test Runner when Unity is available.

### Manual smoke checks

Use a Unity 6.x consumer project and verify these flows after meaningful editor or runtime changes:

- Create a new dialogue database from `Tools/New Dial/Dialogue Editor`
- Load an existing `DialogueDatabaseAsset` from the same window
- Switch to another database with unsaved changes and confirm the save prompt behavior
- Create text and comment nodes, then verify the graph empty state disappears and returns correctly
- Author branching links and confirm choice-mode nodes expose selectable branches in Preview
- Open Preview from the editor toolbar and verify `Next`, `Back`, `Restart`, and `Jump To Active Node`
- Import `Basic Adventure Sample` from Package Manager and run `Tools/New Dial/Create Basic Adventure Sample`

## Documentation expectations

Update documentation in the same change when you modify:

- public package capabilities or limitations
- menu paths or editor workflows
- package metadata in `new_dial/package.json`
- sample behavior or onboarding steps

In practice that usually means touching one or more of these files:

- `new_dial/README.md`
- `docs/current-state.md`
- `new_dial/Samples~/BasicAdventureSample/README.md`
- `new_dial/CHANGELOG.md`

Update the root `README.md` only when the repository entry point or document map changes.

## Changelog policy

Add user-facing changes to `new_dial/CHANGELOG.md` under `Unreleased`. Leave released sections unchanged except when preparing an actual release.

## Pull request checklist

- Change is scoped to the package and does not rely on undocumented local setup
- Relevant EditMode tests were run or their absence is called out
- Manual smoke checks were updated when editor workflows changed
- Docs and changelog match the implemented behavior
