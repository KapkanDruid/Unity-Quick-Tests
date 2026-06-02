---
title: Unity Quick Tests
system: Tools
tags: [editor, tests, tooling]
---

# Unity Quick Tests

Модуль добавляет быстрые editor-only вызовы методов через атрибуты. Он
поддерживает static methods, instance methods на уже существующих
`UnityEngine.Object` targets и plain C# instances. Для обычных C# service-классов
editor-only IL PostProcessor автоматически добавляет weak registration в
constructors поддерживаемых типов. Пакет не зависит от игрового кода и рассчитан
на подключение как отдельный Unity package.

## Установка

Добавьте Git dependency в `Packages/manifest.json` Unity-проекта:

```json
"com.urbandruids.unity-quick-tests": "https://github.com/KapkanDruid/UnityQuickTests.git#v0.1.0"
```

## Использование

```csharp
using UnityQuickTests;
using UnityEngine;

public static class EditorSmokeTests
{
    [QuickTestHotkey(KeyCode.LeftControl, KeyCode.T)]
    private static void RunByHotkey()
    {
        Debug.Log("Ctrl+T");
    }

    [QuickTestSchedule(60, QuickTestScheduleUnit.Frames, QuickTestRepeatMode.Once)]
    private static void RunAfterFrames()
    {
        Debug.Log("After 60 editor updates");
    }

    [QuickTestSchedule(2.5, QuickTestScheduleUnit.Seconds, QuickTestRepeatMode.Repeat)]
    private static void RunEverySeconds()
    {
        Debug.Log("Every 2.5 seconds");
    }
}

public sealed class MonoSmokeTest : MonoBehaviour
{
    [QuickTestHotkey(KeyCode.LeftControl, KeyCode.M)]
    private void RunOnLiveMonoBehaviour()
    {
        Debug.Log(name);
    }
}

public sealed class PlainServiceSmokeTest
{
    [QuickTestHotkey(KeyCode.LeftControl, KeyCode.P)]
    private void RunOnLiveService()
    {
        Debug.Log("Auto-registered plain C# service");
    }
}
```

Ограничения:

- методы должны быть `void` и без параметров;
- inherited attributed methods не пере-регистрируются на derived types;
- generic methods и generic target types не поддерживаются;
- `async void`, `Task`, `ValueTask` и `UniTask`-like return types не
  поддерживаются; используйте parameterless `void` wrapper;
- static methods вызываются напрямую;
- instance methods поддерживаются для живых `UnityEngine.Object` targets;
- `MonoBehaviour` targets ищутся среди loaded scene instances, включая inactive;
- `ScriptableObject` и `EditorWindow` targets ищутся только среди уже loaded
  objects, без автоматической загрузки assets через `AssetDatabase`;
- поддерживаемые plain C# instance targets автоматически регистрируются через
  editor-only IL PostProcessor при выполнении constructor;
- ручной `QuickTestInstanceRegistry.Register(this)` остаётся fallback для
  serializers, нестандартных factory paths и неподдерживаемых типов;
- registry хранит weak references, дедуплицирует повторную регистрацию и
  очищается вокруг Play Mode transitions;
- если найдено несколько matching instances, вызываются все;
- hotkey в edit mode поддерживает модификаторы `Control`, `Shift`, `Alt`, `Command` плюс одну основную клавишу;
- hotkey в Play Mode проверяется через скрытый editor-only `MonoBehaviour`, который работает в обычном player-loop `Update`;
- hotkey в edit mode пока срабатывает из Scene View, потому что редакторские события клавиатуры приходят через GUI event loop;
- schedule работает через `EditorApplication.update`, поэтому кадры означают editor update ticks;
- runtime attributes остаются в player как metadata, но runner, hidden poller,
  CodeGen и injected registration calls проверяются как editor-only.

Меню `Tools/Unity Quick Tests/List Registered Tests` выводит diagnostic report:
trigger, declaring type, target scope, support status и warnings по конфликтным
или одиночным hotkeys. Для стабильной работы в редакторе предпочтительны
modifier combinations: `Control`, `Shift`, `Alt` или `Command` плюс одна trigger
key.

Дальнейшие фичи и порядок разработки описаны в
[`Docs/ROADMAP.md`](Docs/ROADMAP.md). Архитектурные решения и риски собраны в
[`Docs/DESIGN.md`](Docs/DESIGN.md). Инструкция по автоматическим тестам находится
в [`Docs/TESTING.md`](Docs/TESTING.md).
