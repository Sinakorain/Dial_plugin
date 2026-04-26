# Codex Workflow

This repository is maintained as a Unity UPM package, so Codex tasks work best when they are scoped around the package source, the Unity editor workflow, and the available EditMode tests.

This guide turns the repository rules into reusable task guidance for Codex. Keep `AGENTS.md` short; put operational detail here.

## Task prompt shape

For reliable Codex runs, describe each task with:

- Goal: the behavior, documentation, or package surface that should change.
- Context: relevant files, Unity menu paths, tests, errors, screenshots, or docs.
- Constraints: package boundaries, public API expectations, serialization compatibility, and out-of-scope work.
- Done when: the expected verification state, docs/changelog updates, and any manual notes.

Example:

```text
Goal: add editor diagnostics for invalid answer links.
Context: new_dial/Editor/DialogueChoiceFlowDiagnostics.cs and existing tests in new_dial/Tests/Editor/DialogueChoiceFlowDiagnosticsTests.cs.
Constraints: do not change the serialized graph schema; keep legacy direct-choice links readable.
Done when: EditMode tests cover the diagnostic and new_dial/CHANGELOG.md mentions the user-facing editor change.
```

## Repository context

- Source of truth: `new_dial/package.json`, `new_dial/Runtime`, `new_dial/Editor`, and `new_dial/Tests/Editor`.
- Fast snapshot: `docs/current-state.md`.
- User-facing package docs: `README.md`, `new_dial/README.md`, and sample READMEs.
- Integration task docs: `docs/dialogue-system-requirements.md`, `docs/new-dial-game-adapter-task.md`, and `docs/heartline-executable-adapter.md`.

Do not treat `docs/current-state.md` as a substitute for reading code when behavior details matter.

## Planning guidance

Ask Codex to plan first when the task is broad, ambiguous, touches serialization, changes editor workflows, or spans runtime and editor code. A good plan should identify:

- files likely to change;
- public behavior and compatibility risks;
- tests or manual Unity checks needed;
- docs and changelog files that must be updated.

Small documentation-only or test-only tasks can go straight to implementation.

## Validation guidance

This repository does not currently define a repository-wide shell build, lint, or CI workflow. Use Unity when behavior must be validated.

- Run `NewDial.DialogueEditor.Tests.Editor` in Unity Test Runner when Unity is available.
- For editor UX changes, use the manual smoke checks in `CONTRIBUTING.md`.
- If Unity is not available, make that explicit in the final note and describe which tests or smoke checks should be run later.
- Review the diff for stale docs, accidental metadata churn, and claims that are not visible in code, tests, or `package.json`.

## Documentation responsibilities

Update docs in the same change as public behavior changes:

- `new_dial/README.md` for package capabilities, installation, API notes, and workflows.
- `docs/current-state.md` for current working-tree behavior.
- `new_dial/Samples~/BasicAdventureSample/README.md` for sample behavior.
- `new_dial/CHANGELOG.md` under `Unreleased` for user-facing package changes.
- Root `README.md` and `CONTRIBUTING.md` when the repository entry point or contribution workflow changes.

Documentation-only changes should still be checked against code and package metadata.

## Codex configuration notes

Use personal Codex preferences in `~/.codex/config.toml` and repository guidance in `AGENTS.md` plus this document. Keep sandboxing and approvals conservative unless a trusted workflow needs more access.

Use MCP servers or skills only when they provide live or repeatable context that is not already in the repository. For this package, Unity access is the most valuable external context.

## Review checklist

Before accepting Codex output, check:

- The change is scoped to `new_dial` or clearly marked as project-side integration guidance.
- Serialized data compatibility is preserved unless the task explicitly asked for a schema migration.
- Runtime, editor, docs, and changelog tell the same story.
- Tests were updated for behavior changes, or a manual verification note explains the gap.
- No new build, lint, CI, or release workflow is documented unless it actually exists.
