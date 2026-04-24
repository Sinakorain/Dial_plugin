# Basic Adventure Sample

This sample adds a menu item that creates a playable `DialogueDatabaseAsset` for the package's current MVP workflow.

## What it creates

- 1 NPC: `Innkeeper`
- 2 dialogues: `Greeting` and `After Hours`
- branching choices driven by `ChoiceText` links
- a start node that uses outputs as explicit player choices
- a trust-gated branch using a simple numeric condition
- a dialogue-level start condition for an after-hours branch
- a comment node used as an in-editor design note

## Usage

1. Import the sample from Package Manager.
2. Run `Tools/New Dial/Create Basic Adventure Sample`.
3. Save the generated asset into the current Unity project.
4. Open the asset with `Tools/New Dial/Dialogue Editor`.
5. Use the editor's `Preview` button to inspect the branching flow.

## What it demonstrates

- basic package onboarding
- a branching dialogue authored with graph links
- simple node and dialogue conditions
- comment-node annotations in the editor
- the current preview workflow for authored dialogue graphs
