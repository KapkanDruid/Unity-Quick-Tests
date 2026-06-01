# Unity Quick Tests: решения, риски и план прототипа

Дата: 2026-05-30

## Состояние версии 0.1.0

Пакет вынесен в отдельный UPM repository и переименован в **Unity Quick Tests**.

Публичные идентификаторы:

```text
package id: com.urbandruids.unity-quick-tests
runtime assembly: UnityQuickTests.Runtime
editor assembly: UnityQuickTests.Editor
namespace: UnityQuickTests
```

В `0.1.0` поддерживаются `static void` методы без параметров. Текущая ветка
прототипа также поддерживает instance `void` методы без параметров на живых
`UnityEngine.Object` targets.

Play Mode hotkey уже проверен вручную. Рабочий путь использует скрытый editor-only
`QuickTestInputPoller : MonoBehaviour`, который получает настоящий player-loop
`Update`. Polling только через `EditorApplication.update` оказался ненадёжным для
коротких состояний клавиатуры.

Edit Mode hotkey остаётся ограниченным fallback через `SceneView.duringSceneGui`.
Поддержка plain C# instance methods через weak registry и IL PostProcessor
остаётся планом следующих итераций в порядке, описанном ниже.

## Цель

Сделать простой переиспользуемый Unity-модуль для быстрых тестовых вызовов методов через атрибуты:

- вызов по hotkey через `UnityEngine.KeyCode`;
- вызов через N editor/runtime кадров или секунд;
- режим `Once` или `Repeat`;
- модуль должен быть легко вынести в отдельный репозиторий/package.

## Принятые решения

### 1. Набор атрибутов вместо одного перегруженного атрибута

Решение:

- использовать отдельные атрибуты под разные типы триггеров;
- текущие базовые имена: `QuickTestHotkeyAttribute`, `QuickTestScheduleAttribute`;
- общие enum-ы: `QuickTestScheduleUnit`, `QuickTestRepeatMode`.

Причина:

- API читается по имени атрибута;
- меньше неоднозначных overload-ов;
- легче расширять новые режимы.

Пример целевого API:

```csharp
[QuickTestHotkey(KeyCode.LeftControl, KeyCode.T)]
private void RunByHotkey()
{
}

[QuickTestSchedule(60, QuickTestScheduleUnit.Frames, QuickTestRepeatMode.Once)]
private void RunAfterFrames()
{
}

[QuickTestSchedule(2.5, QuickTestScheduleUnit.Seconds, QuickTestRepeatMode.Repeat)]
private void RunEverySeconds()
{
}
```

### 2. Модуль должен быть изолирован

Решение:

- держать модуль отдельной папкой;
- разделить `Runtime` и `Editor`;
- дать каждому слою отдельный `.asmdef`.

Текущий путь в проекте:

```text
Assets/_Game/ExternalModules/UnityQuickTests/
  Runtime/
  Editor/
```

Потенциальный будущий package layout:

```text
UnityQuickTests/
  Runtime/
  Editor/
  Codegen.Editor/
  package.json
```

### 3. Static methods вызываются напрямую

Решение:

- если метод `static`, runner вызывает его обычным reflection-вызовом;
- для static методов не нужен target instance.

Пример:

```csharp
[QuickTestHotkey(KeyCode.W)]
private static void RunStaticTest()
{
}
```

### 4. Unity object instance methods ищутся через Unity pipeline

Решение:

- если метод instance и declaring type наследуется от `UnityEngine.Object`, искать все live instances через Unity API;
- вызвать метод на всех найденных экземплярах;
- не создавать объекты вручную;
- `MonoBehaviour` искать через scene object lookup, включая inactive objects;
- `ScriptableObject` и loaded `EditorWindow` искать через resources lookup;
- не загружать отсутствующие assets через `AssetDatabase` в default scope;
- `UnityEditor.Editor` targets пока пропускать до отдельной lifecycle-проверки.

Покрываемые типы:

- `MonoBehaviour`;
- `ScriptableObject`;
- другие `UnityEngine.Object` subclasses, если Unity может их найти.

### 5. Plain C# instance methods требуют instance registry

Проблема:

- reflection не умеет найти все живые экземпляры обычного C# класса;
- для класса вроде `ArtifactModel : IService` нельзя универсально получить instance без `ServiceLocator`, DI container или другого registry.

Решение на будущее:

- добавить editor-only weak instance registry;
- позже автоматически регистрировать plain C# instances через IL PostProcessor;
- до ILPP проверить эту модель вручную на прототипе.

### 6. Инструмент не должен создавать target objects

Решение:

- никогда не делать `new TargetType()` ради quick-test вызова;
- instance method вызывается только на уже существующем instance;
- если instance нет, метод не вызывается.

Причина:

- создание объекта вручную ломает реальный lifecycle;
- зависимости, сервисы, инициализация и runtime state будут невалидны.

### 7. Несколько экземпляров вызываются все

Решение:

- если найдено несколько instances одного типа, метод вызывается на каждом;
- уникальность instance является ответственностью пользователя тулзы.

Причина:

- это простое, предсказуемое правило;
- модуль не должен угадывать, какой instance правильный.

### 8. Доступность runtime state является ответственностью пользователя

Решение:

- runner не пытается понять, можно ли сейчас вызывать метод;
- если пользователь нажал hotkey в неподходящий момент, это пользовательская ошибка тестового метода;
- missing target должен давать тихое отсутствие вызова или ограниченный warning, но не сложную state-machine логику.

### 9. IL PostProcessor только после проверки прототипа

Решение:

- сначала проверить runner + ручной weak registry;
- только потом добавлять IL injection как слой удобства.

Причина:

- IL weaving сложнее отлаживать;
- сначала нужно доказать, что target resolution и invocation behavior сами по себе подходят.

## Проблемы и потенциальные решения

### Reflection не видит live plain C# instances

Проблема:

- `System.Reflection` видит типы, методы и атрибуты;
- она не умеет перечислять объекты в heap.

Решение:

- `UnityEngine.Object` instances искать через Unity object lookup;
- plain C# instances регистрировать в weak registry.

### Registry может удерживать объекты в памяти

Проблема:

- если registry хранит strong references, объекты не будут собираться GC.

Решение:

- хранить `WeakReference`;
- периодически prune dead references;
- explicit unregister можно добавить, но не делать обязательным для корректности.

### Constructor injection не подходит для Unity objects

Проблема:

- `MonoBehaviour` и `ScriptableObject` constructors не являются нормальным Unity lifecycle hook;
- Unity может создавать такие объекты через native pipeline.

Решение:

- не инжектить регистрацию в constructors типов, наследующих `UnityEngine.Object`;
- эти типы искать через Unity object lookup.

### IL PostProcessor может обработать лишние assemblies

Проблема:

- ILPP получает много compiled assemblies;
- обрабатывать все нельзя: это медленно и рискованно.

Решение:

- фильтровать в `WillProcess(ICompiledAssembly compiledAssembly)`;
- обрабатывать только assemblies, которые ссылаются на `UnityQuickTests.Runtime`;
- исключать `UnityQuickTests.*`, Unity assemblies, vendor/package assemblies при необходимости;
- после загрузки через Cecil дополнительно проверять наличие quick-test атрибутов.

Пример правила:

```csharp
public override bool WillProcess(ICompiledAssembly compiledAssembly)
{
    if (compiledAssembly.Name.StartsWith("UnityQuickTests"))
        return false;

    if (compiledAssembly.Name.StartsWith("Unity."))
        return false;

    return compiledAssembly.References.Any(reference =>
        Path.GetFileNameWithoutExtension(reference) == "UnityQuickTests.Runtime");
}
```

### autoReferenced может расширить область сканирования

Проблема:

- если runtime asmdef `autoReferenced: true`, больше assemblies могут получить reference автоматически;
- ILPP будет чаще заходить в assemblies, где quick-test атрибутов нет.

Решения:

- оставить `autoReferenced: true` для удобства, но делать глубокую проверку атрибутов;
- или сделать `autoReferenced: false`, чтобы пользователь явно подключал `UnityQuickTests.Runtime` только в нужные asmdef.

Рекомендация для внешнего package:

- рассмотреть `autoReferenced: false`, если ILPP станет частью модуля.

### IL weaving может сломать сборку трудными ошибками

Проблема:

- неверная IL injection может привести к compile/import errors, которые сложно читать.

Решение:

- сначала ручной registry-прототип;
- ILPP делать максимально узким;
- патчить только reference types с instance quick-test методами;
- не патчить structs, abstract/static/compiler-generated types, Unity object subclasses;
- при ошибке возвращать diagnostics, а не молча портить assembly.

### Constructor injection может пропустить некоторые объекты

Проблема:

- serializers, native allocation или специальные factory paths могут создавать объекты не так, как ожидается;
- не все случаи гарантированно проходят через обычный `.ctor` в понятном виде.

Решение:

- сначала проверить на реальных service-классах проекта;
- если класс не регистрируется автоматически, оставить возможность ручной регистрации;
- явно документировать unsupported cases.

### Domain reload disabled может оставить static registry в старом состоянии

Проблема:

- при отключенном domain reload static collections могут жить дольше ожидаемого;
- stale weak references могут копиться.

Решение:

- чистить registry при enter/exit play mode;
- prune dead references при каждом lookup;
- отдельно протестировать workflow с disabled domain reload.

### Generic и inherited methods требуют правил

Проблема:

- generic methods нельзя просто вызвать без type arguments;
- inherited attributes и base-class methods могут дать неожиданные вызовы.

Решение:

- на первом этапе не поддерживать generic methods;
- явно решить, наследуются ли quick-test методы;
- лучше начать с `Inherited = false` и scan declared methods.

### Async methods и return values

Проблема:

- `async void`, `Task`, `UniTask` и возвращаемые значения требуют отдельной обработки ошибок;
- fire-and-forget может скрыть exceptions.

Решение:

- первый этап: только `void` methods без параметров;
- позже отдельно решить поддержку `Task`/`UniTask`, если это реально нужно.

### Parameterized methods

Проблема:

- атрибут не знает, какие аргументы передавать.

Решение:

- первый этап: только methods без параметров;
- если нужны параметры, пользователь делает wrapper method без параметров.

### Hotkey conflicts with Unity shortcuts

Проблема:

- `KeyCode.W` и похожие клавиши могут конфликтовать с Unity Scene View shortcuts.

Решение:

- технически разрешить одиночные клавиши;
- рекомендовать modifier combos для стабильности;
- позже можно добавить conflict warnings, но не усложнять первый вариант.

### Editor-only leakage into player builds

Проблема:

- quick-test runner, registry или injected calls не должны попасть в player build.

Решение:

- runner и ILPP держать в Editor assemblies;
- runtime атрибуты могут остаться в player как безвредные metadata, либо выноситься define-ами позже;
- injected registration call должен быть editor-only.

## Что нужно проверить прототипом перед ILPP

1. Static method discovery and invocation.

```csharp
[QuickTestHotkey(KeyCode.F8)]
private static void StaticSmokeTest()
{
}
```

2. `MonoBehaviour` instance method invocation on all loaded scene instances.

```csharp
public sealed class QuickTestMonoSmoke : MonoBehaviour
{
    [QuickTestHotkey(KeyCode.F9)]
    private void InstanceSmokeTest()
    {
    }
}
```

3. Plain C# service through manual weak registry.

```csharp
public sealed class PlainService
{
    public PlainService()
    {
        QuickTestInstanceRegistry.Register(this);
    }

    [QuickTestHotkey(KeyCode.F10)]
    private void InstanceSmokeTest()
    {
    }
}
```

4. Missing instance behavior.

Expected:

- no invocation;
- no repeated noisy logs every editor update;
- optional warning only when trigger is pressed.

5. Multiple instance behavior.

Expected:

- method invoked once per found instance.

6. WeakReference cleanup.

Expected:

- destroyed/GC-collected objects disappear after prune;
- repeated triggers do not accumulate stale targets.

7. Play mode lifecycle.

Expected:

- registry does not keep invalid state between play mode sessions;
- behavior is acceptable with domain reload on and off.

8. Player build safety.

Expected:

- no editor runner assembly in player;
- no injected editor-only registry calls in player;
- no compile errors in non-editor build target.

## Future ILPP plan

After prototype validation:

1. Add `UnityQuickTests.Codegen.Editor` assembly.
2. Implement IL PostProcessor.
3. In `WillProcess`, filter by assembly references to `UnityQuickTests.Runtime`.
4. Load candidate assembly with Cecil only after fast filtering.
5. Find types containing instance quick-test methods.
6. Skip unsupported types:
   - `UnityEngine.Object` subclasses;
   - value types;
   - abstract types;
   - static classes;
   - compiler-generated types;
   - generic definitions until explicitly supported.
7. Inject editor-only registration into all instance constructors.
8. Store registered objects through weak references.
9. Add diagnostics for skipped or failed cases.
10. Verify with small sample assemblies before applying to project gameplay assemblies.

## Current known mismatch in existing prototype

Unity object instance methods are supported. Plain C# instance methods are still
not registered until the manual weak registry prototype exists. This method is
therefore still skipped:

```csharp
public class ArtifactModel
{
    [QuickTestHotkey(KeyCode.W)]
    public void AddArtifactTest()
    {
    }
}
```

Reason:

- reflection cannot enumerate live plain C# heap instances;
- the package must not depend on project-specific service locators;
- plain C# targets require the weak registry from the next phase.

Required next correction:

- add manual weak registry for plain C# prototype;
- keep static direct and Unity object lookup routing unchanged;
- later add ILPP automatic registration only after the registry model is proven.

## Confidence assessment

The proposed direction is feasible, but not fully proven yet.

High confidence:

- static invocation is straightforward;
- Unity object lookup for instance methods is feasible;
- weak registry solves plain C# instance lookup without project-specific service dependencies;
- ILPP can be filtered by assembly references and attributes.

Medium confidence:

- constructor injection will work for ordinary project service classes;
- domain reload disabled behavior can be made clean with registry pruning and lifecycle clearing.

Needs prototype/testing:

- exact Unity 6000.2 IL PostProcessor packaging details;
- behavior with serializers and nonstandard construction paths;
- player-build exclusion of injected calls;
- generic/inherited method policy;
- hotkey reliability across Unity editor focus contexts.
