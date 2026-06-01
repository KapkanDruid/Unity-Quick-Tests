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
```

Результаты сохраняются в локальной gitignored-папке `TestProject~/artifacts/`:

- `EditMode-results.xml` и `PlayMode-results.xml` содержат машинно-читаемый
  результат;
- `EditMode.log` и `PlayMode.log` содержат полный Unity log.

Скрипт возвращает ошибку, если Unity завершился с ненулевым кодом, не создал XML
или сообщил о проваленных тестах. По умолчанию каждый слой ограничен пятью
минутами. Лимит можно изменить параметром `-TimeoutSeconds`.

## Тестовый host-проект

Минимальный Unity-проект находится в `TestProject~/`. Он зафиксирован в Git и
подключает текущий checkout пакета через локальную зависимость:

```json
"com.gggameworks.unity-quick-tests": "file:../.."
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
  Editor/    быстрые Edit Mode проверки внутренних компонентов
  Runtime/   Play Mode проверки поведения в player loop
```

`Tests/Editor` проверяет:

- валидацию schedule-атрибута;
- расписания `Once` и `Repeat`;
- совпадение hotkey с editor-событием;
- rising edge hotkey через детерминированный fake input;
- reflection-вызов и логирование исключений;
- discovery поддерживаемых и неподдерживаемых static-методов.

`Tests/Runtime` проверяет, что `QuickTestInputPoller` действительно получает
`Update` в player loop и отправляет событие.

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
- реальное нажатие hotkey в Scene View в Edit Mode;
- отсутствие editor-only кода в player build.

Player-build smoke следует автоматизировать отдельным шагом перед расширением
instance methods.

## Если Play Mode был принудительно остановлен

Принудительная остановка Unity во время Play Mode transition может оставить
временную `InitTestScene` и неконсистентный host-cache. Если следующий запуск
зависает до выполнения теста:

1. Убедитесь, что batchmode-процесс `TestProject~` остановлен.
2. Удалите локальные `TestProject~/Library`, `TestProject~/Temp` и временные
   `TestProject~/Assets/InitTestScene*.unity*`.
3. Повторите `.\scripts~\run-tests.ps1 -Mode All`.

Эти файлы генерируются Unity и не входят в Git.
