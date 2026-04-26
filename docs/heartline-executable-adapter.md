# Heartline Executable Dialogue Adapter

This repository branch implements the reusable `new_dial` package side only. Heartline integration should live in the Heartline project branch under its own `Assets/Scripts/Dialogue` code.

## How to use this with Codex

Use this document as task context for a project-side Heartline integration, not as permission to edit `new_dial` package internals. A good Codex prompt should include:

- Goal: implement or update the Heartline executable adapter.
- Context: this document, `new_dial/README.md`, `docs/current-state.md`, and the Heartline runtime services that will own execution, audio, scene loading, and save state.
- Constraints: do not add Heartline gameplay, audio, Addressables, battle, deck, quest, reward, or save logic to `new_dial`.
- Done when: registry metadata appears in the editor, runtime executors handle the listed ids safely, voiceover lookup receives `VoiceKey`, and the manual validation list below passes or has an explicit note.

## Package Boundary

`new_dial` provides:

- `DialogueTextNodeData.VoiceKey` as optional text-line metadata for project-side voiceover lookup
- `FunctionNodeData`, `SceneNodeData`, and `DebugNodeData`
- primitive argument data: string, int, float, bool
- `IDialogueExecutionRegistry` metadata
- `IDialogueFunctionExecutor` and `IDialogueSceneExecutor`
- traversal semantics in `DialoguePlayer`

`new_dial` must not contain Heartline battle, deck, quest, reward, enemy, save, or scene-binding logic.

It also must not contain Heartline audio playback, `AudioClip` references, FMOD/Wwise event references, Addressables lookup, or voice asset catalogs.

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

## Heartline Voiceover Bridge

Heartline should treat `DialogueTextNodeData.VoiceKey` as a stable line id for voiceover lookup.

Expected behavior:

- when the active text node changes, read `VoiceKey`
- ignore empty `VoiceKey` without warning
- resolve non-empty `VoiceKey` through Heartline audio/localization data
- include current locale and speaker context in lookup
- play the resolved Unity Audio clip, FMOD event, or Wwise event through Heartline audio services
- log a diagnostic warning for a missing asset, but keep dialogue traversal running

Recommended lookup shape:

```text
VoiceKey + Locale + SpeakerId -> Voice Asset / Audio Event
```

`VoiceKey` should remain stable across locales. Text, subtitles, clips, and audio events may vary by locale; the key itself should not be translated.

## Manual Validation In Heartline

After integrating the adapter in the Heartline branch:

- run `Tools/Heartline/Validate Dialogue Assets`
- smoke test `Assets/Scenes/Playground.unity`
- verify a dialogue graph can trigger `battle.start`
- verify a text node with `VoiceKey` triggers the Heartline voiceover bridge
- verify an empty `VoiceKey` does not trigger audio lookup
- verify existing non-executable dialogues still work
- verify scene object/action bindings still resolve correctly
