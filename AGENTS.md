# AGENTS.md

## Repository expectations

- This repository currently contains one Unity UPM package: `new_dial/`.
- Treat `new_dial/package.json`, the C# code under `new_dial/Runtime` and `new_dial/Editor`, and the tests under `new_dial/Tests/Editor` as the primary source of truth.
- Use `docs/current-state.md` as a fast implementation snapshot, not as a replacement for reading the code when behavior details matter.
- Use `docs/codex-workflow.md` for detailed Codex task, validation, and review guidance.
- Keep agent instructions short here. Put human-facing detail in `README.md`, `CONTRIBUTING.md`, and `docs/`.

## Layout

- `new_dial/Runtime`: public data model and runtime traversal contracts
- `new_dial/Editor`: Unity editor windows, graph tooling, preview UI, and autosave
- `new_dial/Samples~`: importable sample content
- `new_dial/Tests/Editor`: EditMode tests for runtime and editor behavior
- `docs/current-state.md`: current working-tree snapshot for docs
- `docs/codex-workflow.md`: reusable Codex workflow guidance

## Working rules

- Do not invent repository-wide build, lint, CI, or shell workflows. This repo does not define them yet.
- For non-trivial or ambiguous work, gather code context first and plan around goal, context, constraints, and done criteria before editing.
- Validate behavior changes through Unity 6.x and Unity Test Runner when that environment is available.
- When public behavior, menu paths, workflows, or package metadata change, update the related docs in the same change.
- Record user-facing package changes in `new_dial/CHANGELOG.md` under `Unreleased`.
- Keep documentation aligned with the current working tree when the task is to document current state, even if some behavior is not released yet.

## Done means

- Code, docs, and changelog agree with each other.
- Behavior changes have either EditMode coverage or an explicit manual verification note.
- No documentation claims rely on assumptions that are not visible in code, tests, or `package.json`.
