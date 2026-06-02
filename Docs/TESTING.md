# Тестирование Unity Quick Tests

Этот документ описывает воспроизводимый тестовый контур пакета. Им пользуются
одинаково разработчик, агент и будущий CI.

## Быстрый запуск

Из корня репозитория выполните:

```powershell
.\scripts~\run-tests.ps1 -Mode All
```

Можно запускать слои отдельно:

```powershell
.\scripts~\run-tests.ps1 -Mode EditMode
.\scripts~\run-tests.ps1 -Mode PlayMode
.\scripts~\run-tests.ps1 -Mode PlayerBuild
```

Результаты сохраняются в локальной gitignored-папке `TestProject~/artifacts/`:

- `EditMode-results.xml` и `PlayMode-results.xml` содержат машинно-читаемый
  результат;
- `EditMode.log` и `PlayMode.log` содержат полный Unity log.
- `PlayerBuildSmoke.log` содержит лог player build smoke-проверки.

Скрипт возвращает ошибку, если Unity завершился с ненулевым кодом, не создал XML
или сообщил о проваленных тестах. Для `PlayerBuild` он также проверяет, что
сборка создана и в выходную папку не попали Editor/CodeGen/Cecil assemblies.
По умолчанию каждый слой ограничен пятью минутами. Лимит можно изменить
параметром `-TimeoutSeconds`.

## Тестовый host-проект

Минимальный Unity-проект находится в `TestProject~/`. Он зафиксирован в Git и
подключает текущий checkout пакета через локальную зависимость:

```json
"com.urbandruids.unity-quick-tests": "file:../.."
```

Это позволяет тестировать незакоммиченные изменения до публикации новой версии.
Версия Unity закреплена в `TestProject~/ProjectSettings/ProjectVersion.txt`.
Остальные файлы `ProjectSettings`, которые Unity создаёт при первом запуске,
локальны и исключены из Git.

Суффикс `~` выбран намеренно. Unity игнорирует такие папки при импорте UPM-пакета:
host-проект скачивается вместе с Git repository, но не импортируется в проект
пользователя и не требует `.meta`-файлов.

## Где находятся тесты

```text
Tests/
  CodegenConsumer/ отдельная assembly для ILPP auto-registration smoke
  Editor/    быстрые Edit Mode проверки внутренних компонентов
  Runtime/   Play Mode проверки поведения в player loop
```

`Tests/Editor` проверяет:

- валидацию schedule-атрибута;
- валидацию hotkey-атрибута;
- расписания `Once` и `Repeat`;
- срабатывание static hotkey через Scene View event;
- совпадение hotkey с editor-событием;
- rising edge hotkey через детерминированный fake input;
- сброс состояния hotkey между Play Mode sessions;
- reflection-вызов и логирование исключений;
- discovery поддерживаемых и неподдерживаемых static-методов;
- discovery Unity object и plain C# instance methods;
- explicit policy для inherited methods, generic methods/types, async methods и
  Task/UniTask-like return types;
- invocation routing на несколько `MonoBehaviour` instances;
- invocation routing на loaded `ScriptableObject`;
- invocation routing на зарегистрированные plain C# instances;
- ILPP constructor injection для plain C# service targets;
- отсутствие duplicate invocation при manual registry fallback после ILPP;
- `this(...)` constructor chaining без двойной регистрации;
- fast `WillProcess` filtering для CodeGen layer;
- отклонение player assemblies без `UNITY_EDITOR` define в `WillProcess`;
- weak registry: duplicate registration, unregister и GC cleanup;
- очистку registry при входе и выходе из Play Mode для сценариев без domain
  reload;
- lookup loaded `EditorWindow` через Unity resources;
- diagnostic warnings для duplicate/no-modifier hotkeys;
- registration report с trigger, declaring type, target scope и support status;
- explicit status, что `ScriptableObject` lookup не загружает assets через
  `AssetDatabase`;
- ограниченный warning при missing instance target.

`Tests/Runtime` проверяет, что `QuickTestInputPoller` действительно получает
`Update` в player loop и отправляет событие.

`PlayerBuild` smoke через `QuickTestPlayerBuildSmoke.Run` строит минимальный
StandaloneWindows64 player и проверяет:

- отсутствие `UnityQuickTests.Editor`, `Unity.UrbanDruids.UnityQuickTests.CodeGen`,
  `Mono.Cecil` и `Unity.CompilationPipeline.Common` assemblies в output;
- отсутствие `QuickTestInputPoller` type в managed player script assemblies;
- отсутствие call sites к `QuickTestInstanceRegistry.Register` в managed player
  script assemblies;
- наличие runtime quick-test attributes как безвредной player metadata.

## Почему hotkey тестируется через fake input

Физическое нажатие клавиши и фокус окна Unity нельзя стабильно воспроизводить в
headless-режиме. Поэтому чтение `Input.GetKey` отделено внутренним интерфейсом:

- production использует настоящий `UnityEngine.Input`;
- unit-тест управляет fake input и проверяет state machine без клавиатуры;
- реальная клавиатура остаётся короткой ручной smoke-проверкой перед релизом.

## Запуск из Unity Editor

Для интерактивной диагностики откройте `TestProject~` как обычный Unity-проект и
запустите тесты через `Window > General > Test Runner`.

Для проверки реального использования пакета дополнительно оставляется consumer
smoke-тест в игровом проекте. Consumer-проект не заменяет минимальный host:
у него есть собственные зависимости и состояние, которые не должны влиять на
регрессионные тесты пакета.

## Что пока остаётся ручной проверкой

- реальное нажатие hotkey в Play Mode;
- реальное нажатие hotkey в Scene View в Edit Mode.

## Если Play Mode был принудительно остановлен

Принудительная остановка Unity во время Play Mode transition может оставить
временную `InitTestScene` и неконсистентный host-cache. Если следующий запуск
зависает до выполнения теста:

1. Убедитесь, что batchmode-процесс `TestProject~` остановлен.
2. Удалите локальные `TestProject~/Library`, `TestProject~/Temp` и временные
   `TestProject~/Assets/InitTestScene*.unity*`.
3. Повторите `.\scripts~\run-tests.ps1 -Mode All`.

Эти файлы генерируются Unity и не входят в Git.
