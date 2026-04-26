# ТЗ для production dialogue system поверх `new_dial`

Используй этот документ как прямой рабочий промпт для Codex. Цель: спроектировать и реализовать production-ready диалоговую систему внутри Unity 6.x проекта, которая нативно работает с пакетом `com.danilkashulin.newdial.dialogue-editor` версии `0.1.0` как с внешней зависимостью. Система должна уметь либо создаваться с нуля поверх `new_dial`, либо адаптировать уже существующий runtime-стек проекта без смены authoring-формата.

## Контекст

Пакет `new_dial` уже предоставляет authoring и базовый runtime traversal, и именно этот код является источником истины для интеграции. Опираться нужно на следующие типы и их текущее поведение:

- `DialogueDatabaseAsset`: корневой `ScriptableObject`, хранит список `NpcEntry`.
- `NpcEntry`: хранит `Id`, `Name`, список `DialogueEntry`.
- `DialogueEntry`: хранит `Id`, `Name`, `StartCondition`, `Graph`.
- `DialogueGraphData`: хранит `Nodes` и `Links`.
- `DialogueTextNodeData`: основной playable node, содержит `BodyText`, опциональный `VoiceKey`, `IsStartNode`, `UseOutputsAsChoices`.
- `DialogueChoiceNodeData`: playable answer node, хранит `ChoiceText` для кнопки выбора, собственный `BodyText`, metadata реплики, локализацию и опциональное условие.
- `CommentNodeData`: editor-only сущность для заметок и группировки, не является runtime-репликой.
- `NodeLinkData`: связь между узлами, содержит `FromNodeId`, `ToNodeId`, `Order`; `ChoiceText` остается legacy fallback для старых direct-choice графов.
- `ConditionData`: легковесное описание условия с `Type`, `Key`, `Operator`, `Value`.
- `DialoguePlayer`: базовый traversal helper для `CanStart`, `Start`, `Next`, `Choose`.
- `IDialogueVariableStore`: контракт чтения значений переменных.
- `IDialogueConditionEvaluator`: контракт вычисления условий.

Текущий пакет не поставляет production in-game UI, долгоживущий session orchestration, интеграцию с save/load проекта, игровые side effects и полноценную runtime-архитектуру вокруг authored данных. Эти части и нужно реализовать в проекте-потребителе.

## Цель системы

Нужно реализовать production dialogue system, которая:

- напрямую читает authored данные `new_dial` без конвертации в отдельный внутренний authoring-формат;
- умеет находить доступные диалоги по NPC и conditions;
- запускает, ведет и завершает диалоговые сессии в runtime;
- показывает текущую реплику и список доступных выборов в in-game UI;
- публикует metadata текущей реплики для project-side озвучки через `VoiceKey`;
- работает через проектные переменные, условия, события и save/load;
- допускает подключение как новой UI, так и уже существующей UI/flow-инфраструктуры проекта;
- закладывает future-ready архитектуру под дальнейшее добавление `Function`, `Scene` и `Debug` узлов, но не реализует их исполнение в v1.

## Неподвижные ограничения

При реализации обязательно соблюдать следующие ограничения:

- Не менять сериализуемую схему пакета `new_dial`.
- Не менять editor-часть `new_dial`.
- Не дублировать graph-данные в отдельный runtime authoring-формат.
- Не вводить обязательный промежуточный экспорт authored данных в другую схему как условие работы системы.
- Не считать `CommentNodeData` игровым контентом.
- Не реализовывать в v1 исполнение `Function`, `Scene`, `Debug` узлов.
- Не ломать возможность использовать пакет как внешнюю UPM-зависимость.
- Не мутировать `DialogueDatabaseAsset` во время gameplay-сессии.

## Обязательная совместимость с текущим поведением пакета

Реализация обязана сохранить текущую semantics `new_dial`:

1. Доступность диалога определяется через `DialogueEntry.StartCondition` и `IDialogueConditionEvaluator`.
   - `DialogueRuntimeService` обязан проверять доступность до фактического старта сессии.
   - Нельзя полагаться только на `DialoguePlayer.Start()`, потому что текущая реализация `Start()` ищет стартовый узел, но сама не валидирует `StartCondition`.
2. Старт диалога ищется так же, как в `DialogueGraphUtility.FindStartNode`:
   - сначала `DialogueTextNodeData` с `IsStartNode == true`;
   - если такого нет, используется первый `DialogueTextNodeData` в графе.
3. Линейный переход идет по первому валидному `NodeLinkData` из исходящих links, отсортированных по `Order`, затем по `Id`.
4. Узел `DialogueTextNodeData` работает как узел выбора, если у него есть исходящие `Text -> Answer` links или legacy-флаг `UseOutputsAsChoices == true`:
   - автоматический `Next` не должен молча выбирать ветку вместо игрока, пока есть доступные choices;
   - новые choices строятся из валидных `DialogueChoiceNodeData`, прошедших проверку условий; после `Choose()` runtime входит в сам answer node;
   - `Next()` из answer node идет по его первому валидному исходящему runtime target, если он есть;
   - legacy direct-choice links без answer node остаются поддержанными для старых данных.
5. Текст выбора определяется так же, как в `DialogueChoice.Text`:
   - сначала `DialogueChoiceNodeData.ChoiceText`, если choice идет через answer node;
   - затем legacy `NodeLinkData.ChoiceText`, если он не пустой;
   - иначе `TargetNode.Title`;
   - если `Target` отсутствует, допустим runtime fallback уровня `"Choice"` или эквивалентное безопасное значение.
6. Условия узлов и диалогов обязаны уважать `IDialogueConditionEvaluator`.
7. Базовая condition semantics должна оставаться совместимой с текущим `DefaultDialogueConditionEvaluator`:
   - `ConditionType.None` пропускает узел или диалог без дополнительных проверок;
   - `ConditionType.Custom` по умолчанию считается неподдержанным и должен быть закрыт проектной реализацией;
   - `ConditionType.VariableCheck` использует `variableStore`, `Key`, `Operator` и `Value`;
   - отсутствие `variableStore`, пустой `Key` или отсутствие значения по ключу приводят к `false`;
   - строковые сравнения `==`, `!=`, `Contains` должны быть case-insensitive;
   - numeric comparisons `>`, `<`, `>=`, `<=` должны работать по числам.
8. `CommentNodeData` игнорируется runtime-системой во всех in-game сценариях.
9. Некорректные графы не должны вызывать hard crash; они должны безопасно завершаться и логироваться.

## Целевая архитектура v1

Построй систему как набор отдельных слоев с четкими границами ответственности.

### 1. Dialogue Catalog

Подсистема отвечает за чтение `DialogueDatabaseAsset` и поиск диалогов:

- поиск NPC по `NpcEntry.Id`;
- поиск диалогов по `DialogueEntry.Id`;
- перечисление всех диалогов NPC;
- фильтрация доступных диалогов по `StartCondition`;
- безопасная работа с отсутствующими NPC, диалогами, графами и стартовыми узлами;
- отсутствие мутаций source asset во время runtime.

### 2. Dialogue Runtime Orchestration

Подсистема управляет активной сессией:

- старт новой сессии;
- отказ в старте при невалидных входных данных или недоступном диалоге;
- выдача текущего состояния сессии;
- линейное продвижение `Next`;
- выбор варианта `Choose`;
- отмена/принудительное завершение;
- корректное завершение при достижении конца графа;
- генерация lifecycle events.

Traversal-логика должна быть совместима с `DialoguePlayer`. Допустимо:

- либо использовать `DialoguePlayer` напрямую как внутренний engine;
- либо реализовать обертку/сервис поверх него;
- либо повторить его semantics в более крупной runtime-модели, если это нужно для интеграции, но без изменения поведения.

### 3. Dialogue Session State

Нужен read-only снимок активной сессии уровня runtime, доступный UI и внешним системам. Состояние должно содержать минимум:

- `NpcId`;
- `DialogueId`;
- `CurrentNodeId`;
- `CurrentNodeTitle`;
- `CurrentText`;
- `CurrentVoiceKey`;
- список доступных choices в уже отсортированном виде;
- флаги `CanAdvance`, `CanChoose`, `IsEnded`;
- технический статус сессии для safe recovery и диагностики.

Состояние должно обновляться атомарно после каждого действия игрока или системного перехода.

### 4. Variable Store Bridge

Нужен адаптер проекта к `IDialogueVariableStore`:

- чтение переменных из фактического runtime-хранилища проекта;
- единая точка преобразования project data в строковые значения, совместимые с `new_dial`;
- поддержка локальных, глобальных, quest и trust-like значений без привязки к конкретной реализации проекта;
- отсутствие логики UI внутри variable layer.

### 5. Condition Evaluator Bridge

Нужна обязательная точка расширения вокруг `IDialogueConditionEvaluator`:

- поддержка базовой semantics пакета для `None`, сравнений строк, сравнений чисел, `Contains`, `Truthy`;
- проектное расширение для `ConditionType.Custom`;
- возможность подменить evaluator без переписывания runtime/UI;
- диагностирование неподдерживаемых condition cases.

Если в проекте уже есть собственная система условий, нужно сделать adapter, а не переписывать authoring-данные `new_dial`.

### 6. UI / Presenter Layer

UI должен быть отделен от traversal и condition logic. Нужен контракт вида `IDialogueView` и отдельный presenter/controller:

- view отвечает только за отображение;
- presenter получает `DialogueSessionState`, подписывается на lifecycle events и обновляет view;
- выборы отправляются обратно в runtime-service через явные команды;
- один и тот же runtime должен уметь работать и с новой UI, и с существующей legacy UI.

Для v1 UI должна уметь:

- показывать имя NPC или другой display context;
- показывать текущую реплику;
- показывать список выборов;
- поддерживать линейное продолжение, когда choices нет;
- обрабатывать конец диалога;
- безопасно переживать пропуски текста, пустые заголовки и пустые choices.

### 7. Voiceover Bridge

Озвучка должна подключаться как project-side bridge поверх runtime events и `DialogueTextNodeData.VoiceKey`.

Требования:

- `VoiceKey` является стабильным строковым id реплики, а не прямой ссылкой на `AudioClip`, FMOD event или локализованный текст.
- Пустой `VoiceKey` валиден и означает, что для реплики не назначена озвучка.
- При входе в новый `DialogueTextNodeData` runtime должен передавать `VoiceKey` в `DialogueSessionState` и событие показа узла.
- Audio layer должен слушать событие показа узла или изменение `DialogueSessionState`, резолвить `VoiceKey` с учетом текущего языка/локали и speaker context, затем проигрывать нужный проектный asset.
- Для Unity Audio допустим resolver вида `VoiceKey + Locale + SpeakerId -> AudioClip` и отдельный `AudioSource`/voice channel на персонажа.
- Для FMOD/Wwise допустим resolver вида `VoiceKey + Locale + SpeakerId -> event id/path`.
- Core dialogue runtime не должен зависеть от Unity `AudioSource`, `AudioClip`, FMOD, Wwise, Addressables или конкретного audio manager.
- При переходе на новую реплику audio layer сам решает, остановить ли предыдущую озвучку speaker-а, дать ей доиграть или сделать fade.
- Ошибка lookup-а по непустому `VoiceKey` должна диагностироваться, но не должна ломать traversal диалога.

### 8. Persistence Boundary

Нужна отдельная граница уровня `IDialoguePersistence`:

- сохранение активной runtime-сессии;
- восстановление активной runtime-сессии;
- безопасный отказ от восстановления, если asset/узел/диалог больше не существует;
- отсутствие владения долгоживущими world variables.

Важно: система сохранения диалоговой сессии не должна забирать на себя ownership глобального игрового состояния. Глобальные переменные, факты, квестовые статусы и trust-параметры остаются во владении основной save-системы проекта.

### 9. Dialogue Lifecycle Events

Нужно ввести явные runtime events:

- диалог доступен для старта;
- диалог стартовал;
- показан новый узел;
- показан новый узел с `VoiceKey`, если он задан;
- список choices обновился;
- сделан выбор;
- диалог завершен;
- диалог отменен;
- произошла runtime-ошибка или обнаружен невалидный граф.

События должны быть пригодны для подключения аудио, анимаций, камеры, input lock, quest updates и прочих игровых систем, но сами side effects не должны быть захардкожены в core traversal.

## Обязательные интерфейсы и контракты

Ниже зафиксированы минимальные интерфейсы, которые должна предоставить система. Имена можно адаптировать под стиль проекта, но responsibilities должны сохраниться.

### `DialogueRuntimeService`

Единая runtime-точка входа. Должна уметь:

- перечислять доступные диалоги для NPC;
- проверять, можно ли стартовать конкретный диалог;
- стартовать диалог по `NpcId` и `DialogueId` либо по прямой ссылке на `DialogueEntry`;
- выдавать текущее `DialogueSessionState`;
- выполнять `Next`;
- выполнять `Choose`;
- отменять текущую сессию;
- завершать текущую сессию;
- восстанавливать сессию из persistence layer;
- публиковать lifecycle events.

### `DialogueSessionState`

Read-only модель состояния runtime-сессии. Минимум:

- `NpcId`;
- `DialogueId`;
- `CurrentNodeId`;
- `CurrentNodeTitle`;
- `CurrentText`;
- `CurrentVoiceKey`;
- `Choices`;
- `CanAdvance`;
- `CanChoose`;
- `IsEnded`.

Допустимо добавить:

- display name NPC;
- speaker id / speaker display name;
- тип активного шага;
- технический error/reason code;
- internal sequence/version для UI sync.

### `IDialogueVariableStore` adapter

Обязательная интеграционная точка между `new_dial` и проектом. Реализация должна:

- читать фактические runtime values проекта;
- быть переиспользуемой между catalog filtering и активной сессией;
- не хранить UI-состояние;
- не менять authored данные.

### `IDialogueConditionEvaluator` adapter / wrapper

Обязательная точка расширения, особенно для `ConditionType.Custom`. Реализация должна:

- поддерживать semantics текущего пакета;
- расширяться проектными условиями;
- быть заменяемой без переписывания UI и runtime service.

### `IDialogueView`

Контракт слоя отображения. Минимум:

- показать текущее состояние диалога;
- показать choices;
- скрыть/закрыть диалоговое окно;
- сигнализировать пользовательские действия presenter-у.

### `IDialogueVoiceoverService`

Project-side сервис озвучки. Минимум:

- принимать событие входа в текстовую ноду или read-only `DialogueSessionState`;
- игнорировать пустой `CurrentVoiceKey`;
- резолвить непустой `CurrentVoiceKey` через проектный каталог озвучки;
- учитывать текущую локаль проекта;
- проигрывать найденный voice asset через проектный audio stack;
- логировать отсутствующий voice asset без остановки диалога.

### `IDialoguePersistence`

Граница сохранения активной сессии. Должна:

- сохранять state активной диалоговой сессии;
- восстанавливать state;
- очищать сохраненную сессию после завершения или отмены;
- не владеть глобальными world variables.

## Два режима реализации

Система должна быть спроектирована так, чтобы Codex мог выбрать один из двух путей в зависимости от конкретного проекта.

### Режим 1. Greenfield

Использовать этот путь, если в проекте нет зрелой диалоговой runtime-системы или ее проще заменить, чем адаптировать.

Требования:

- строить runtime напрямую поверх authored данных `new_dial`;
- использовать `DialoguePlayer` как основу semantics;
- выделить отдельные сервисы catalog, runtime, persistence, presenter;
- сделать UI поверх read-only `DialogueSessionState`;
- избегать сильной связности между traversal и view.

### Режим 2. Adaptation

Использовать этот путь, если в проекте уже есть UI, save-system, event bus или narrative framework, которые нельзя или невыгодно заменять.

Требования:

- сохранить существующие UI/save/event слои проекта;
- добавить adapter-слой между текущим runtime проекта и `DialogueEntry` / `DialogueGraphData`;
- адаптировать project variable store к `IDialogueVariableStore`;
- адаптировать project condition logic к `IDialogueConditionEvaluator`;
- интегрировать lifecycle active session без переавторинга диалоговых данных;
- не требовать ручной миграции authored графов в другой формат.

Если в проекте уже есть собственные dialogue widgets, presenters или controllers, их нужно переиспользовать через adapter, а не заменять автоматически.

## Детали реализации, которые обязательно учитывать

### Работа с графом

- Никогда не считать `CommentNodeData` игровым узлом.
- При traversal показывать `DialogueTextNodeData` и выбранный `DialogueChoiceNodeData` как playable line nodes; executable nodes остаются мгновенными runtime-переходами.
- Всегда фильтровать исходящие links по валидности target node.
- Всегда уважать сортировку по `Order`.
- Пустой или сломанный target должен приводить к безопасному пропуску ссылки или завершению сессии с диагностикой.
- В editor graph `Title` редактируется в header ноды для text, answer, executable и comment nodes; отдельное body-поле title не является основным UX.
- Inline text boxes в нодах должны переносить длинные строки визуально по ширине карточки, не вставляя автоматические `\n` в сохраненные данные.

### Работа с choices

- Choices должны пересобираться каждый раз при входе в text node с outgoing answer links или legacy choice-mode flag.
- В choices должны попадать только ветки с валидным answer node или legacy target и выполненными conditions на самой choice-ветке; conditions следующего target проверяются при `Next()` из answer node.
- UI не должна сама вычислять валидность choices; это ответственность runtime.
- Если text node в режиме выбора не имеет доступных валидных choices, система должна безопасно завершать диалог или переводить его в диагностируемое terminal state, но не зависать.

### Работа с текстом и display

- `BodyText` является главным источником текста реплики.
- `VoiceKey` является главным metadata-полем для поиска озвучки текущей реплики.
- `Title` не должен считаться полноценной репликой; это служебный display field, который можно показывать как secondary label или fallback.
- Пустой `BodyText` не должен ломать UI.
- Пустой `VoiceKey` не должен ломать UI или traversal и не должен считаться ошибкой.
- Пустой answer `ChoiceText` должен корректно падать в fallback через semantics `DialogueChoice`.
- Answer node после выбора показывает свой `BodyText`; дальнейшая ветка начинается только после `Next()` из этой answer-ноды.

### Работа с озвучкой

- Core runtime должен только переносить `VoiceKey` из активной text/answer line node в state/events.
- Audio playback должен жить в отдельном project-side сервисе, подключенном к lifecycle events.
- `VoiceKey` не переводится между локалями; переводятся текст, аудио asset/event и локализационные таблицы, которые на него ссылаются.
- Не использовать числовые ids для production voice keys, если проект не имеет отдельной стабильной таблицы миграций.
- Нельзя хранить прямые audio object references в `DialogueTextNodeData`.
- Отсутствующий asset для непустого `VoiceKey` должен давать warning/diagnostic и продолжать диалог.

### Работа с conditions

- Catalog availability и runtime node entry должны использовать согласованный evaluator.
- Нельзя допустить ситуацию, когда catalog считает диалог доступным, а runtime стартует его по другим правилам.
- Все project-specific condition extensions должны быть инкапсулированы внутри evaluator bridge.

### Работа с ошибками

- Не использовать hard exceptions как штатный control flow для кривых данных.
- Любой невалидный граф, отсутствующий node или невозможность восстановления сессии должны логироваться достаточно подробно для дебага.
- Ошибки должны возвращаться в виде безопасного runtime result/status, понятного UI и внешним системам.

## Нефункциональные требования

- Архитектура должна быть testable без реального UI.
- Traversal должен быть детерминированным.
- Зависимости между слоями должны позволять unit-тестировать runtime отдельно от Unity UI.
- Код должен быть готов к расширению под новые типы узлов без переписывания базового orchestration.
- Интеграция с `new_dial` должна быть достаточно тонкой, чтобы обновление пакета не требовало полной перестройки runtime, если его базовая semantics не изменилась.

## Критерии приемки

Считать реализацию завершенной только если выполнены все пункты ниже:

1. Диалоговая система читает authored данные `new_dial` напрямую из `DialogueDatabaseAsset`.
2. Доступность диалога корректно зависит от `StartCondition`.
3. Стартовый узел совпадает с правилами `new_dial`: `IsStartNode`, иначе первый `DialogueTextNodeData`.
4. Линейный `Next` выбирает первый валидный link по `Order`.
5. Choice-узел показывает только валидные ветки и в правильном порядке.
6. `CommentNodeData` никогда не попадает в in-game runtime UI и traversal.
7. Завершение графа корректно закрывает сессию и публикует событие окончания.
8. `DialogueSessionState` всегда отражает актуальное состояние runtime после пользовательского действия.
9. Save/load восстанавливает активную сессию без мутации `DialogueDatabaseAsset`.
10. Некорректные или неполные графы завершаются безопасно и диагностируются.
11. UI можно заменить или адаптировать без переписывания traversal core.
12. Adapter-сценарий подтверждает, что существующую диалоговую UI можно подключить без переавторинга authored данных.
13. При входе в текстовую ноду `CurrentVoiceKey` соответствует `DialogueTextNodeData.VoiceKey`.
14. Пустой `VoiceKey` не запускает озвучку и не считается ошибкой.
15. Непустой `VoiceKey` передается в project-side voiceover service без зависимости core runtime от конкретного audio stack.

## Test Plan

Минимальный набор тестов, который должен сопровождать реализацию:

- Проверка доступности диалога по `StartCondition`.
- Проверка выбора стартового узла.
- Проверка линейного перехода к первому валидному target по `Order`.
- Проверка auto-choice для `Text -> Answer` links без `UseOutputsAsChoices` и legacy choice-mode на `UseOutputsAsChoices`.
- Проверка fallback-логики текста choice.
- Проверка игнорирования `CommentNodeData`.
- Проверка завершения диалога без исходящих валидных links.
- Проверка передачи `VoiceKey` в `DialogueSessionState` при входе в текстовую ноду.
- Проверка, что пустой `VoiceKey` не вызывает audio lookup.
- Проверка, что непустой `VoiceKey` вызывает project-side voiceover resolver с текущей локалью.
- Проверка восстановления сохраненной сессии.
- Проверка безопасного поведения при отсутствии target node.
- Проверка adapter-сценария для existing UI layer.

Если в проекте принято разделение на unit и integration tests, то:

- traversal, catalog filtering, evaluator bridge и persistence logic должны покрываться unit-тестами;
- binding к конкретной UI и игровым системам может покрываться integration-тестами или manual verification note.

## Out of scope для v1

Следующие возможности не входят в текущую реализацию и не должны быть автоматически добавлены Codex в рамках v1:

- исполнение `Function` узлов;
- исполнение `Scene` узлов;
- исполнение `Debug` узлов;
- новый authoring editor взамен `new_dial`;
- изменение формата `DialogueDatabaseAsset`, `DialogueEntry`, `DialogueGraphData` или других данных пакета;
- собственный альтернативный graph format;
- полная narrative scripting language;
- сложный cinematic sequencer;
- полный локализационный pipeline за пределами передачи stable line/voice keys;
- production voice-over pipeline за пределами project-side lookup по `VoiceKey`;
- автоматическая миграция старых данных в новый формат без явного запроса;
- захват ownership над общей save-system проекта.

## Definition of done для Codex

Если ты, Codex, реализуешь систему по этому ТЗ, результат должен включать:

- runtime architecture, совместимую с `new_dial`;
- четко отделенные catalog/runtime/view/persistence границы;
- готовый путь либо для greenfield UI, либо для адаптации существующей UI;
- тесты или явную manual verification note для ключевого поведения;
- отсутствие изменений сериализуемой схемы и editor-части `new_dial`.

Если при реализации выяснится, что в проекте уже есть частично подходящие системы, приоритет такой:

1. Переиспользовать существующий слой проекта через adapter.
2. Добавить минимально необходимый bridging code.
3. Только при отсутствии разумной интеграции строить новый слой с нуля.
