# Basic Adventure Sample

This sample adds a menu item that creates a playable `DialogueDatabaseAsset` for the package's current MVP workflow.

Copyright (c) 2026 Danil Kashulin. All rights reserved. This sample is part of the proprietary `New Dial Dialogue Editor` package and is covered by the package license in `new_dial/LICENSE.md`.

## What it creates

- 1 NPC: `Innkeeper`
- 2 dialogues: `Greeting` and `After Hours`
- an `Innkeeper` speaker entry on each dialogue
- branching choices driven by visible answer nodes with header-editable titles plus inline button text and reply text
- a start node that routes explicit player choices into playable answer lines
- a trust-gated branch using a simple numeric condition
- a dialogue-level start condition for an after-hours branch
- a comment node used as an in-editor design note

## Usage

1. Import the sample from Package Manager.
2. Run `Tools/New Dial/Create Basic Adventure Sample`.
3. Save the generated asset into the current Unity project.
4. Open the asset with `Tools/New Dial/Dialogue Editor`.
5. Use the editor's `Preview` button to inspect the branching flow.

## Codex notes

When changing this sample, keep the generated content aligned with package behavior documented in `new_dial/README.md` and `docs/current-state.md`. User-facing sample changes should be recorded in `new_dial/CHANGELOG.md` under `Unreleased`.

## What it demonstrates

- basic package onboarding
- a branching dialogue authored as text question -> answer line
- text lines using dialogue speaker fallback
- simple node and dialogue conditions
- comment-node annotations in the editor
- the current preview workflow for authored dialogue graphs
