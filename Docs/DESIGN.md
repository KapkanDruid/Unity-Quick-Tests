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
`UnityEngine.Object` targets и на plain C# instances. Обычные C# service-классы
регистрируются автоматически через editor-only IL PostProcessor, а ручной weak
registry остаётся fallback.

Play Mode hotkey уже проверен вручную. Рабочий путь использует скрытый
`QuickTestInputPoller : MonoBehaviour`, который компилируется только под
`UNITY_EDITOR` и получает настоящий player-loop `Update` в Play Mode. Polling
только через `EditorApplication.update` оказался ненадёжным для коротких
состояний клавиатуры.

Edit Mode hotkey остаётся ограниченным fallback через `SceneView.duringSceneGui`.
Поддержка plain C# instance methods реализована через weak registry и
constructor injection в `Unity.UrbanDruids.UnityQuickTests.CodeGen`.

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
  Codegen.Editor/  # asmdef name: Unity.UrbanDruids.UnityQuickTests.CodeGen
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

### 5. Plain C# instance methods используют weak instance registry

Проблема:

- reflection не умеет найти все живые экземпляры обычного C# класса;
- для класса вроде `ArtifactModel : IService` нельзя универсально получить instance без `ServiceLocator`, DI container или другого registry.

Решение:

- editor-only IL PostProcessor injects `QuickTestInstanceRegistry.Register(this)`
  в constructors поддерживаемых plain C# types;
- registry хранит only weak references и не удерживает service objects в памяти;
- `Unregister(object target)` доступен как explicit cleanup;
- ручной `QuickTestInstanceRegistry.Register(this)` остаётся fallback для
  serializers и нестандартных factory paths.

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

### 9. IL PostProcessor как узкий слой поверх registry

Решение:

- runner + ручной weak registry проверены до включения ILPP;
- ILPP реализован как `Unity.UrbanDruids.UnityQuickTests.CodeGen`;
- assembly name использует шаблон `Unity.*.CodeGen`, потому что Unity только
  такие assemblies отправляет в ILPP runner;
- `autoReferenced: false`, так как Unity запрещает auto-reference для CodeGen
  assemblies.

Причина:

- IL weaving сложнее отлаживать;
- target resolution и invocation behavior остаются в проверенной registry-модели;
- consumer assemblies не получают compile-time dependency на CodeGen assembly.

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
- возвращать diagnostics для unsupported types, но не менять assembly, если в ней
  нет supported instance quick-test methods.

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

### CodeGen packaging зависит от имени assembly

Проблема:

- Unity 6000.2 компилирует обычные Editor assemblies, но не добавляет их в ILPP
  runner;
- assemblies с именем `Unity.*.CodeGen` распознаются как CodeGen;
- CodeGen assemblies не могут иметь `autoReferenced: true`.

Решение:

- asmdef name: `Unity.UrbanDruids.UnityQuickTests.CodeGen`;
- namespace остаётся `UnityQuickTests.Codegen.Editor`;
- `autoReferenced: false`;
- tests reference CodeGen assembly explicitly; consumer assemblies do not.

### autoReferenced runtime может расширить область сканирования

Проблема:

- если runtime asmdef `autoReferenced: true`, больше assemblies могут получить reference автоматически;
- ILPP будет чаще заходить в assemblies, где quick-test атрибутов нет.

Текущее решение:

- runtime остаётся `autoReferenced: true` для простого подключения;
- `WillProcess` и Cecil-scan оставляют обработку дешёвой и узкой;
- перейти на explicit runtime references можно позже, если profiling покажет
  лишний объём обработки.

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
- hidden Play Mode poller держать под `#if UNITY_EDITOR`, даже если файл лежит в
  Runtime assembly;
- runtime атрибуты оставлять в player как безвредные metadata;
- `QuickTestInstanceRegistry.Register/Unregister` доступны runtime-коду как
  conditional no-op в player build и не зависят от `UnityEditor`;
- injected registration call должен быть editor-only;
- player build smoke проверяет отсутствие Editor/CodeGen/Cecil assemblies,
  `QuickTestInputPoller` type и registry registration call sites в managed
  player script assemblies.

## Что проверено прототипом перед ILPP

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

3. Plain C# service through weak registry and manual fallback.

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
- no hidden Play Mode poller type in player;
- no injected editor-only registry calls in player;
- no compile errors in non-editor build target.

## Current ILPP implementation

After registry prototype validation:

1. `Unity.UrbanDruids.UnityQuickTests.CodeGen` contains the editor-only
   `QuickTestILPostProcessor`.
2. `WillProcess` filters by assembly references to `UnityQuickTests.Runtime`,
   excludes package/Unity/System/vendor assemblies, and requires `UNITY_EDITOR`.
3. Candidate assemblies are loaded with Cecil only after fast filtering.
4. Types are patched only when they contain supported instance quick-test
   methods.
5. Unsupported types are skipped:
   - `UnityEngine.Object` subclasses;
   - value types;
   - abstract types;
   - static classes;
   - compiler-generated types;
   - generic definitions until explicitly supported.
6. Constructor injection adds `QuickTestInstanceRegistry.Register(this)` before
   `ret` in non-chaining instance constructors.
7. `this(...)` constructor chaining is handled by patching only the terminal
   constructor body.
8. Registered objects are still stored through weak references.
9. Diagnostics are emitted for skipped or failed cases.
10. A dedicated `QuickTestCodegen.Consumer` test assembly validates automatic
    registration, invocation and manual fallback deduplication.

## Current validated prototype

Unity object instance methods are supported through Unity lookup. Plain C#
instance methods are supported when their live objects are constructed through
supported constructors. Manual registration remains valid:

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
- plain C# targets are therefore supplied by the package weak registry, either
  through ILPP constructor injection or manual fallback.

Next correction:

- improve editor UX and diagnostics around conflicting hotkeys, registered test
  listings, target scope and missing-target warnings;
- keep static direct, Unity object lookup, registry routing, ILPP registration
  and manual fallback unchanged.

## Confidence assessment

The proposed direction is feasible, but not fully proven yet.

High confidence:

- static invocation is straightforward;
- Unity object lookup for instance methods is feasible;
- weak registry solves plain C# instance lookup without project-specific service dependencies;
- ILPP can be filtered by assembly references and attributes;
- Unity 6000.2 recognizes the `Unity.*.CodeGen` packaging pattern;
- player build output excludes editor runner, hidden poller, CodeGen/Cecil
  assemblies and registry registration call sites.

Medium confidence:

- constructor injection works for ordinary project service classes in the
  package smoke assembly;
- domain reload disabled behavior can be made clean with registry pruning and lifecycle clearing.

Needs prototype/testing:

- behavior with serializers and nonstandard construction paths;
- generic/inherited method policy;
- hotkey reliability across Unity editor focus contexts.
