# Heartline Executable Dialogue Adapter

This repository branch implements the reusable `new_dial` package side only. Heartline integration should live in the Heartline project branch under its own `Assets/Scripts/Dialogue` code.

## Package Boundary

`new_dial` provides:

- `FunctionNodeData`, `SceneNodeData`, and `DebugNodeData`
- primitive argument data: string, int, float, bool
- `IDialogueExecutionRegistry` metadata
- `IDialogueFunctionExecutor` and `IDialogueSceneExecutor`
- traversal semantics in `DialoguePlayer`

`new_dial` must not contain Heartline battle, deck, quest, reward, enemy, save, or scene-binding logic.

## Heartline Registry

Heartline should register an `IDialogueExecutionRegistry` that describes these function ids:

- `session.set_flag`
- `session.clear_flag`
- `session.set_int`
- `session.add_int`
- `scene.activate_binding`
- `scene.deactivate_binding`
- `scene.invoke_action`
- `battle.start`
- `dialogue.end`

Descriptors should include display name, category, description, required parameters, defaults, wait support, and default failure policy.

## Heartline Executors

Heartline should provide:

- an `IDialogueFunctionExecutor` that routes function ids to project services
- an `IDialogueSceneExecutor` that resolves `SceneNodeData.SceneKey`, load mode, entry point, transition id, and parameters through Heartline scene loading

Expected function behavior:

- `session.set_flag`, `session.clear_flag`, `session.set_int`, and `session.add_int` mutate `DialogueSessionState`
- `scene.activate_binding` and `scene.deactivate_binding` resolve `DialogueSceneObjectBinding` by key
- `scene.invoke_action` resolves `DialogueSceneActionBinding` by key and action id
- `dialogue.end` ends the active dialogue cleanly
- `battle.start` writes a Heartline battle handoff payload, relies on `DeckRuntimeState` for deck state, closes dialogue as needed, and requests the battle scene through the Heartline scene executor

`battle.start` arguments:

- required: `encounterId`
- optional: `sceneKey` default `Battle_Arena`, `returnSceneId`, `returnSpawnId`, `transitionId`

Do not pass deck contents through dialogue node parameters.

## Manual Validation In Heartline

After integrating the adapter in the Heartline branch:

- run `Tools/Heartline/Validate Dialogue Assets`
- smoke test `Assets/Scenes/Playground.unity`
- verify a dialogue graph can trigger `battle.start`
- verify existing non-executable dialogues still work
- verify scene object/action bindings still resolve correctly
