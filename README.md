---
title: Unity Quick Tests
system: Tools
tags: [editor, tests, tooling]
---

# Unity Quick Tests

Модуль добавляет быстрые editor-only вызовы статических методов через атрибуты.
Папка `UnityQuickTests` не зависит от игрового кода и рассчитана на перенос в отдельный Unity package.

## Установка

Добавьте Git dependency в `Packages/manifest.json` Unity-проекта:

```json
"com.gggameworks.unity-quick-tests": "https://github.com/KapkanDruid/UnityQuickTests.git#v0.1.0"
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
```

Ограничения:

- методы должны быть `static`, `void` и без параметров;
- hotkey в edit mode поддерживает модификаторы `Control`, `Shift`, `Alt`, `Command` плюс одну основную клавишу;
- hotkey в Play Mode проверяется через скрытый editor-only `MonoBehaviour`, который работает в обычном player-loop `Update`;
- hotkey в edit mode пока срабатывает из Scene View, потому что редакторские события клавиатуры приходят через GUI event loop;
- schedule работает через `EditorApplication.update`, поэтому кадры означают editor update ticks.

Дальнейшие фичи и порядок разработки описаны в
[`Docs/ROADMAP.md`](Docs/ROADMAP.md). Архитектурные решения и риски собраны в
[`Docs/DESIGN.md`](Docs/DESIGN.md).
