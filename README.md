# Unity Quick Tests

Иногда нужно просто быстро дёрнуть метод: проверить расчёт, прогнать кусок
сервиса, пересобрать кеш, вызвать редакторскую утилиту или посмотреть, что
логика вообще работает. Но в Unity для этого часто приходится делать лишнюю
обвязку: временный `MonoBehaviour`, кнопку в инспекторе, пункт меню, отдельный
редакторский сервис, консольную команду или полноценный тест.

`Unity Quick Tests` убирает эту обвязку. Вы ставите атрибут на метод без
параметров, назначаете сочетание клавиш или расписание, а пакет сам найдёт
подходящие живые цели и вызовет метод в корректном жизненном цикле
Unity/редактора. Для обычных C#-классов регистрация сделана через слабые
ссылки, поэтому пакет не удерживает объекты в памяти и не создаёт лишних
проблем с очисткой.

## Установка

Вариант 1: в Unity откройте `Window/Package Manager`, нажмите `+`, выберите
`Add package from git URL...` и вставьте ссылку:

```text
https://github.com/KapkanDruid/Unity-Quick-Tests.git#v1.1.2
```

Вариант 2: добавьте Git dependency в `Packages/manifest.json` Unity-проекта:

```json
"com.urbandruids.unity-quick-tests": "https://github.com/KapkanDruid/Unity-Quick-Tests.git#v1.1.2"
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

## Что поддерживается

- `static` методы вызываются напрямую.
- Методы на `MonoBehaviour`, `ScriptableObject` и `EditorWindow` вызываются на
  уже загруженных экземплярах.
- Методы на обычных C#-классах, которые не наследуются от `UnityEngine.Object`,
  вызываются через регистрацию экземпляра.
- Вызов можно повесить на сочетание клавиш или на расписание в тиках обновления
  редактора / секундах.
- Для расписания доступны режимы `Once` и `Repeat`.

## Ограничения

- Метод, помеченный атрибутом пакета, должен быть `void` и без параметров.
- `async void`, `Task`, `ValueTask`, `UniTask` и методы с возвращаемым значением
  не поддерживаются. Для таких случаев сделайте обычную `void`-обёртку.
- Обобщённые методы и обобщённые типы не поддерживаются.
- Если метод с quick-test атрибутом объявлен в базовом классе, пакет не создаёт
  отдельные проверки для каждого наследника. Если нужен вызов именно на
  наследнике, добавьте в нём отдельный метод-обёртку.
- Пакет не создаёт объекты сам. Нестатические методы вызываются только на уже
  существующих экземплярах.
- Если ни одного подходящего экземпляра нет, метод не будет вызван.
- Если найдено несколько подходящих экземпляров, метод вызывается на каждом.
- `MonoBehaviour` ищется среди загруженных объектов сцены, включая неактивные.
- `ScriptableObject` и `EditorWindow` поддерживаются только если объект уже
  загружен. Пакет не сканирует проект через `AssetDatabase`.
- Обычные C#-классы, которые не наследуются от `UnityEngine.Object`,
  поддерживаются через регистрацию экземпляра. Обычно пакет добавляет её сам в
  конструктор через IL PostProcessor. Если объект создаётся нестандартным
  способом и не зарегистрировался автоматически, можно вызвать
  `QuickTestInstanceRegistry.Register(this)` вручную.
- Регистрация обычных C#-экземпляров хранит слабые ссылки (`WeakReference`),
  поэтому quick-test не удерживает эти объекты в памяти и не мешает сборщику
  мусора очищать их, когда они больше не нужны.
- Сочетания клавиш в Edit Mode работают через события `Scene View`, поэтому это
  не полноценная глобальная система горячих клавиш для всех окон редактора.
- В player build попадают только атрибуты как обычные метаданные. Код, который
  ищет и запускает quick-tests, работает только в редакторе.

## Диагностика

Меню `Tools/Unity Quick Tests/List Registered Tests` выводит список найденных
проверок: способ запуска, сигнатуру метода, тип, в котором метод объявлен,
область поиска цели, статус поддержки и предупреждения по конфликтующим
сочетаниям клавиш.

Предупреждения можно настроить через
`Tools/Unity Quick Tests/Warning Settings`.

## Тестирование

Инструкция по автоматическим проверкам находится в
[`Docs/TESTING.md`](Docs/TESTING.md).
