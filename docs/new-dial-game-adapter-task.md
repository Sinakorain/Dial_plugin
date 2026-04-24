# ТЗ Для Codex: Адаптер Игры Под Executable Nodes `new_dial`

## Summary

Нужно доработать игровой проект, не меняя пакет `new_dial`, чтобы редактор диалогов увидел проектные функции в `FunctionNode`, проектные сцены в `SceneNode`, а runtime смог выполнять эти ноды через игровые сервисы.

Пакет уже предоставляет универсальные API:

- `IDialogueExecutionRegistry`
- `IDialogueFunctionExecutor`
- `IDialogueSceneExecutor`
- `DialogueFunctionDescriptor`
- `DialogueSceneDescriptor`
- `DialogueParameterDescriptor`
- `FunctionNodeData`
- `SceneNodeData`
- `DialogueExecutionResult`

Вся игровая логика должна жить в проекте игры, например в `Assets/Scripts/Dialogue`.

## Implementation Changes

### Project-Side Registry

Добавить project-side registry:

- создать `HeartlineDialogueExecutionRegistry : IDialogueExecutionRegistry`
- зарегистрировать его при старте редактора/проекта через `DialogueExecutionRegistry.Register(...)`
- корректно вызывать `Unregister(...)` при domain reload / shutdown
- registry должен отдавать metadata только для редактора и валидации, не выполнять gameplay

### Function Descriptors

В `GetFunctions()` описать функции:

- `session.set_flag`
  - `key: String` required
- `session.clear_flag`
  - `key: String` required
- `session.set_int`
  - `key: String` required
  - `value: Int` required
- `session.add_int`
  - `key: String` required
  - `value: Int` required
- `scene.activate_binding`
  - `key: String` required
- `scene.deactivate_binding`
  - `key: String` required
- `scene.invoke_action`
  - `key: String` required
  - `actionId: String` required
- `battle.start`
  - `encounterId: String` required
  - `sceneKey: String` optional, default `Battle_Arena`
  - `returnSceneId: String` optional
  - `returnSpawnId: String` optional
  - `transitionId: String` optional
- `dialogue.end`
  - без обязательных аргументов

### Scene Descriptors

В `GetScenes()` описать игровые scene keys:

- минимум `Battle_Arena`
- добавить остальные сцены, которые должны быть доступны в выпадающем списке `SceneNode`
- если в проекте уже есть каталог сцен, использовать его как source of truth

### Runtime Executors

Добавить runtime executors:

- `HeartlineDialogueFunctionExecutor : IDialogueFunctionExecutor`
- `HeartlineDialogueSceneExecutor : IDialogueSceneExecutor`

Требования:

- исполнители должны получать зависимости через конструктор или DI/service locator проекта
- не добавлять Heartline-specific код в пакет `new_dial`
- не использовать reflection
- не добавлять custom object references в аргументы executable-ноды

### DialoguePlayer Integration

Подключить executors в месте создания `DialoguePlayer`.

Найти текущий `DialogueRuntimeService` или другой сервис запуска диалогов и создавать player так:

```csharp
new DialoguePlayer(
    conditionEvaluator,
    variableStore,
    heartlineFunctionExecutor,
    heartlineSceneExecutor);
```

Старые text-only диалоги должны продолжить работать без изменений.

## Function Behavior

### `session.set_flag`

- прочитать `key`
- записать flag в `DialogueSessionState`
- вернуть `DialogueExecutionResult.Success()`

### `session.clear_flag`

- прочитать `key`
- удалить/сбросить flag в `DialogueSessionState`
- вернуть `DialogueExecutionResult.Success()`

### `session.set_int`

- прочитать `key`
- прочитать `value`
- записать int в `DialogueSessionState`
- вернуть `DialogueExecutionResult.Success()`

### `session.add_int`

- прочитать `key`
- прочитать `value`
- прибавить `value` к текущему int в `DialogueSessionState`
- вернуть `DialogueExecutionResult.Success()`

### `scene.activate_binding`

- найти `DialogueSceneObjectBinding` по `key`
- активировать связанный объект
- если binding не найден, вернуть `DialogueExecutionResult.Failed(...)`

### `scene.deactivate_binding`

- найти `DialogueSceneObjectBinding` по `key`
- деактивировать связанный объект
- если binding не найден, вернуть `DialogueExecutionResult.Failed(...)`

### `scene.invoke_action`

- найти `DialogueSceneActionBinding` по `key`
- вызвать action по `actionId`
- если binding/action не найден, вернуть `DialogueExecutionResult.Failed(...)`

### `dialogue.end`

- завершить активный диалог чисто
- вернуть:

```csharp
return DialogueExecutionResult.EndDialogue();
```

### `battle.start`

- прочитать `encounterId`
- прочитать optional `sceneKey`, default `Battle_Arena`
- прочитать optional `returnSceneId`, `returnSpawnId`, `transitionId`
- записать battle handoff payload в runtime handoff state игры
- не передавать deck через параметры ноды
- использовать существующий `DeckRuntimeState` как источник deck state
- закрыть dialogue UI, если это требуется текущей архитектурой
- запросить загрузку battle scene через `HeartlineDialogueSceneExecutor`
- вернуть `EndDialogue()` для обычного fire-and-close сценария
- вернуть `Pending()` только если runtime реально умеет потом вызвать `CompletePendingExecution(...)`

## Scene Executor Behavior

`HeartlineDialogueSceneExecutor.Execute(SceneNodeData node, DialogueExecutionContext context)` должен:

- валидировать `node.SceneKey`
- преобразовать `DialogueSceneLoadMode.Single/Additive` в игровой способ загрузки
- передать `EntryPointId`, `TransitionId` и primitive `Parameters` в игровой scene-loading слой
- при успешном запросе загрузки вернуть `DialogueExecutionResult.Success()`
- при ошибке вернуть `DialogueExecutionResult.Failed("понятное диагностическое сообщение")`

Если загрузка async и `node.WaitForCompletion == true`, executor должен:

- вернуть `DialogueExecutionResult.Pending()`
- обеспечить вызов после завершения загрузки:

```csharp
player.CompletePendingExecution(DialogueExecutionResult.Success());
```

## Argument Helpers

Добавить маленький helper для чтения аргументов:

- `GetString(FunctionNodeData node, string name, string defaultValue = "")`
- `GetInt(FunctionNodeData node, string name, int defaultValue = 0)`
- `GetBool(FunctionNodeData node, string name, bool defaultValue = false)`
- `GetFloat(FunctionNodeData node, string name, float defaultValue = 0f)`

Добавить аналогичные методы для `SceneNodeData.Parameters`.

Helper должен:

- искать аргумент по `Name`
- проверять `DialogueArgumentValue.Type`
- возвращать default для optional значений
- для required значений позволять executor вернуть `Failed(...)` с понятным текстом
- не бросать `NullReferenceException`

## Editor Workflow After Implementation

После интеграции:

- в `FunctionNode` должен появиться dropdown известных функций
- при выборе функции редактор должен показать её параметры, required/default/hint
- `SceneNode` должен показывать generic scene fields
- если registry отдаёт scenes, `SceneNode` должен показывать dropdown известных сцен
- для запуска боя дизайнер использует `FunctionNode` с `FunctionId = battle.start`
- отдельный battle-node создавать не нужно
- для одноразовых сценовых действий дизайнер использует:
  - `scene.activate_binding`
  - `scene.deactivate_binding`
  - `scene.invoke_action`
- для прямой загрузки сцены дизайнер использует `SceneNode`

## Test Plan

### Registry / Editor

- открыть Dialogue Graph
- создать `FunctionNode`
- убедиться, что доступны все function ids
- проверить, что required параметры показываются в inspector
- создать `SceneNode`
- убедиться, что известные сцены доступны в inspector

### Runtime Session

- создать граф `Text -> session.set_flag -> Text`
- пройти граф
- убедиться, что flag появился в `DialogueSessionState`
- проверить:
  - `session.clear_flag`
  - `session.set_int`
  - `session.add_int`

### Runtime Scene Bindings

- `scene.activate_binding` активирует правильный объект
- `scene.deactivate_binding` деактивирует правильный объект
- `scene.invoke_action` вызывает правильный action id
- неизвестный key/action даёт diagnostic failure, а не null reference

### Runtime Battle

- `battle.start` с одним `encounterId` грузит `Battle_Arena`
- handoff payload содержит encounter id
- handoff payload содержит return ids, если они заданы
- deck state берётся через `DeckRuntimeState`
- диалог закрывается корректно

### Compatibility

- старые text-only диалоги запускаются без regressions
- отсутствие registry не ломает runtime
- отсутствие registry только убирает dropdown metadata в редакторе
- ошибки executor не валят Unity null reference исключениями

## Assumptions

- Код адаптера пишется только в игровом проекте, не в `new_dial`.
- `FunctionId` и `SceneKey` являются стабильными строковыми контрактами.
- После использования в ассетах `FunctionId` и `SceneKey` нельзя переименовывать без миграции.
- Все сложные игровые payload остаются в игровых runtime-сервисах.
- Параметры executable-ноды используют только:
  - `String`
  - `Int`
  - `Float`
  - `Bool`
- `WaitForCompletion` включается только там, где проект действительно хранит ссылку на активный `DialoguePlayer` и может вызвать `CompletePendingExecution(...)`.
