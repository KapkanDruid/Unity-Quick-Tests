---
title: Unity Quick Tests
system: Tools
tags: [editor, tests, tooling]
---

# Unity Quick Tests

Модуль добавляет быстрые editor-only вызовы методов через атрибуты. Он
поддерживает static methods и instance methods на уже существующих
`UnityEngine.Object` targets. Пакет не зависит от игрового кода и рассчитан на
подключение как отдельный Unity package.

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
```

Ограничения:

- методы должны быть `void` и без параметров;
- static methods вызываются напрямую;
- instance methods поддерживаются для живых `UnityEngine.Object` targets;
- `MonoBehaviour` targets ищутся среди loaded scene instances, включая inactive;
- `ScriptableObject` и `EditorWindow` targets ищутся только среди уже loaded
  objects, без автоматической загрузки assets через `AssetDatabase`;
- plain C# instance methods пока пропускаются с warning до появления weak
  registry;
- hotkey в edit mode поддерживает модификаторы `Control`, `Shift`, `Alt`, `Command` плюс одну основную клавишу;
- hotkey в Play Mode проверяется через скрытый editor-only `MonoBehaviour`, который работает в обычном player-loop `Update`;
- hotkey в edit mode пока срабатывает из Scene View, потому что редакторские события клавиатуры приходят через GUI event loop;
- schedule работает через `EditorApplication.update`, поэтому кадры означают editor update ticks.

Дальнейшие фичи и порядок разработки описаны в
[`Docs/ROADMAP.md`](Docs/ROADMAP.md). Архитектурные решения и риски собраны в
[`Docs/DESIGN.md`](Docs/DESIGN.md). Инструкция по автоматическим тестам находится
в [`Docs/TESTING.md`](Docs/TESTING.md).
