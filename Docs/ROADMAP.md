# Unity Quick Tests: план развития

Этот файл является живым планом следующих итераций пакета. Он фиксирует порядок
работ, критерии готовности и решения, которые пока намеренно отложены.

## Текущее состояние

Версия `0.1.0` опубликована и поддерживает:

- вызов `static void` методов без параметров через `QuickTestHotkeyAttribute`;
- вызов `static void` методов по расписанию через `QuickTestScheduleAttribute`;
- вызов instance `void` методов без параметров на live `UnityEngine.Object`
  targets;
- вызов instance `void` методов без параметров на plain C# targets через
  editor-only IL PostProcessor registration с ручным weak registry fallback;
- player build smoke подтверждает отсутствие editor-only assemblies, hidden
  poller type и registry registration call sites в managed player script
  assemblies;
- runtime quick-test attributes остаются в player как безвредные metadata;
- editor diagnostics report показывает trigger, declaring type, target scope,
  support status и warnings;
- API expansion policy закрепляет, что inherited, generic, async/Task/UniTask,
  parameterized methods, single-target selection и новые trigger attributes не
  входят в `0.1.x` без подтверждённого сценария;
- интервалы в editor update ticks и секундах;
- режимы `Once` и `Repeat`;
- Play Mode hotkey через скрытый editor-only `QuickTestInputPoller`;
- ограниченный Edit Mode fallback через `SceneView.duringSceneGui`.

Главная следующая цель: подготовить release hardening для следующей package
версии после стабилизации ядра.

## Принципы разработки

- Пакет не зависит от игровых `ServiceLocator`, DI container и других
  project-specific сервисов.
- Runtime-код не зависит от `UnityEditor`.
- Инструмент никогда не создаёт target object ради quick-test вызова.
- Если найдено несколько подходящих instances, метод вызывается на каждом.
- Missing target не должен создавать шумный лог на каждом editor update.
- IL PostProcessor остаётся узким editor-only слоем поверх проверенной
  registry-модели.
- Каждый этап сначала проверяется на маленьком smoke-сценарии, затем в игровом
  проекте.

## Этап 1. Закрыть базовую проверку версии 0.1.x

Цель: подтвердить уже существующее поведение и получить минимальный набор
регрессионных тестов до расширения discovery.

Задачи:

- [x] Проверить static hotkey в Play Mode.
- [x] Проверить static hotkey в Edit Mode через Scene View.
- [x] Проверить schedule по editor ticks в режиме `Once`.
- [x] Проверить schedule по секундам в режиме `Repeat`.
- [x] Проверить повторные входы и выходы из Play Mode.
- [x] Добавить Edit Mode tests для attribute validation и discovery static
  методов.
- [x] Добавить smoke-проверку, что Editor assembly не попадает в player build.

Критерий готовности:

- существующая версия имеет воспроизводимые smoke-тесты;
- перед расширением target resolution нет известных регрессий.

## Этап 2. Поддержать instance methods у Unity objects

Цель: разрешить quick-test методы на живых `MonoBehaviour`, `ScriptableObject` и
других поддерживаемых `UnityEngine.Object` instances.

Задачи:

- [x] Расширить discovery: находить instance `void` методы без параметров.
- [x] Разделить invocation routing:
  - static method вызывается напрямую;
  - `UnityEngine.Object` target ищется через Unity object lookup;
  - plain C# target направляется в ручной weak registry.
- [x] Вызывать метод на всех найденных live instances.
- [x] Не загружать автоматически отсутствующие assets в default scope.
- [x] Ограничить warning о missing target моментом фактического trigger.
- [x] Добавить smoke-тест для нескольких `MonoBehaviour` instances.
- [x] Добавить smoke-тест для loaded `ScriptableObject`.
- [x] Зафиксировать отдельные правила для editor-only targets:
  - loaded `EditorWindow` искать через Unity resources lookup;
  - lifecycle `UnityEditor.Editor` считать нестабильным и поддерживать только
    после отдельной проверки;
  - загрузку всех assets проекта делать только через explicit scope;
  - plain C# editor services направлять в registry.

Критерий готовности:

- instance quick-test работает на живых Unity objects;
- missing target не ломает runner и не создаёт повторяющийся лог;
- несколько instances получают по одному вызову.

## Этап 3. Добавить ручной weak registry для plain C# instances

Цель: доказать модель вызова методов на сервисах и моделях, которые не
наследуются от `UnityEngine.Object`.

Задачи:

- [x] Добавить editor-only behavior через `QuickTestInstanceRegistry`.
- [x] Хранить только `WeakReference`, без удержания объектов в памяти.
- [x] Добавить ручные `Register(object target)` и при необходимости
  `Unregister(object target)`.
- [x] Дедуплицировать повторную регистрацию одного target.
- [x] Делать prune мёртвых references при lookup.
- [x] Очищать lifecycle state при входе и выходе из Play Mode.
- [x] Проверить поведение с включённым и отключённым domain reload.
- [x] Добавить smoke-тесты для missing target, нескольких instances,
  duplicate registration и GC cleanup.

Критерий готовности:

- обычный C# service можно зарегистрировать вручную и вызвать по атрибуту;
- registry не создаёт memory leak;
- поведение стабильно между Play Mode sessions.

## Этап 4. Подготовить автоматическую регистрацию через IL PostProcessor

Цель: убрать ручной `Register(this)` для обычных service-классов после того, как
registry-модель доказана прототипом.

Задачи:

- [x] Добавить assembly `Unity.UrbanDruids.UnityQuickTests.CodeGen`.
- [x] Реализовать узкий `ILPostProcessor`.
- [x] В `WillProcess` быстро фильтровать assemblies по ссылке на
  `UnityQuickTests.Runtime`.
- [x] Исключить `UnityQuickTests.*`, `Unity.*`, `System.*` и неподходящие
  vendor assemblies.
- [x] После загрузки через Cecil проверять наличие instance quick-test
  атрибутов до изменения assembly.
- [x] Измерить область обработки assemblies и решить, оставлять ли
  `autoReferenced: true` ради простого подключения или перейти на
  `autoReferenced: false` с явными references в consumer asmdefs.
- [x] Инжектить editor-only регистрацию в constructors поддерживаемых типов.
- [x] Корректно обработать constructor chaining через `this(...)`.
- [x] Исключить structs, static classes, abstract types, compiler-generated
  types, generic definitions и `UnityEngine.Object` subclasses.
- [x] Возвращать diagnostics при пропуске или ошибке weaving.
- [x] Сохранить ручную регистрацию как fallback для serializers и нестандартных
  factory paths.

Критерий готовности:

- обычный поддерживаемый C# service регистрируется автоматически;
- duplicate registration не создаёт повторных вызовов;
- неподдерживаемые типы диагностируются без повреждения assembly.

## Этап 5. Проверить безопасность player build

Цель: гарантировать, что editor tooling не влияет на release builds игры.

Задачи:

- [x] Проверить отсутствие Editor assemblies в player.
- [x] Проверить отсутствие hidden input poller в player.
- [x] Проверить отсутствие editor-only injected registration calls в player.
- [x] Решить, оставлять ли runtime attributes как безвредные metadata или
  исключать их define-ами.
- [x] Добавить воспроизводимую build smoke-проверку.

Критерий готовности:

- package не создаёт compile errors и runtime overhead в player build.

## Этап 6. Улучшить editor UX и диагностику

Цель: сделать инструмент удобным при ежедневном использовании после проверки
основной модели.

Задачи:

- [x] Добавить предупреждения о конфликтных или неоднозначных hotkeys.
- [x] Рекомендовать modifier combinations для стабильной работы в редакторе.
- [x] Исследовать отдельный механизм глобальных editor hotkeys для окон вне
  Scene View.
- [x] Улучшить список зарегистрированных тестов: trigger, declaring type,
  target scope и статус поддержки.
- [x] Добавить ограниченные warnings для missing targets.
- [x] Решить, нужен ли explicit asset scope для загрузки ScriptableObject assets
  через `AssetDatabase`.

Критерий готовности:

- пользователь понимает, почему тест зарегистрирован, пропущен или не вызван;
- Edit Mode ограничения документированы и предсказуемы.

## Этап 7. Рассмотреть расширения API после стабилизации ядра

Эти возможности не входят в ближайший прототип. Их следует добавлять только при
подтверждённой потребности.

- [x] Определить правила inherited attributed methods.
- [x] Решить поддержку generic methods и generic types.
- [x] Решить поддержку `Task`, `UniTask` и обработки async exceptions.
- [x] Оценить необходимость параметров; базовый рекомендуемый путь остаётся
  wrapper method без параметров.
- [x] Решить, нужен ли выбор одного instance вместо вызова всех targets.
- [x] Рассмотреть новые отдельные trigger attributes, если появятся реальные
  сценарии.

Подтверждённое packaging-решение:

- CodeGen assembly должна называться по шаблону `Unity.*.CodeGen`, иначе Unity
  компилирует её как обычную assembly и не добавляет в ILPP runner;
- CodeGen assembly должна иметь `autoReferenced: false`; consumer assemblies
  получают регистрацию через ILPP, а тестовая assembly ссылается на неё явно.

Подтверждённое player-build решение:

- runtime атрибуты остаются в player как metadata, чтобы пользовательские
  assemblies могли компилироваться без define-ветвления;
- `QuickTestInstanceRegistry.Register/Unregister` остаются доступными runtime-коду
  как `Conditional("UNITY_EDITOR")` no-op для player source calls;
- `QuickTestInputPoller`, runner, CodeGen и injected registry call sites не должны
  попадать в player output.

Подтверждённое editor-UX решение:

- hotkey collisions и одиночные клавиши диагностируются при reload и в
  registration report;
- для стабильной работы в редакторе рекомендуются modifier combinations:
  `Control`, `Shift`, `Alt` или `Command` плюс одна trigger key;
- отдельного публичного global editor hotkey API для окон вне Scene View в
  Unity `6000.2.8f1` не найдено, поэтому Edit Mode остаётся Scene View fallback;
- `ScriptableObject` assets не загружаются через `AssetDatabase` автоматически:
  поддерживаются только already loaded objects, explicit asset scope остаётся
  отдельной будущей фичей.

Подтверждённое API expansion решение:

- inherited quick-test methods не пере-регистрируются на derived types:
  attributes имеют `Inherited = false`, discovery сканирует declared methods;
- generic methods и generic target types отклоняются;
- `async void`, `Task`, `ValueTask` и `UniTask`-like return types отклоняются,
  потому что exception handling требует отдельного async runner;
- параметры не поддерживаются; рекомендуемый путь остаётся wrapper method без
  параметров;
- target selection не добавляется: найденные live instances вызываются все;
- новых trigger attributes не добавляется без реального consumer-сценария.

## Ближайшая рекомендуемая итерация

Следующая итерация: release hardening. Нужно решить, нужна ли версия `0.1.1`,
обновить changelog/release notes, проверить package install through Git tag и
провести consumer smoke в игровом проекте.

## Правило обновления roadmap

После каждой завершённой итерации:

- отмечать выполненные пункты;
- добавлять подтверждённые ограничения и новые smoke-сценарии;
- переносить изменившиеся архитектурные решения в `Docs/DESIGN.md`;
- выпускать новую package-версию только после проверки установки через Git tag.
