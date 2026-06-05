# Changelog

## Unreleased

## 1.1.1

- Added `Tools/Unity Quick Tests/Warning Settings` to toggle Unity Quick Tests warnings.
- Added guarded warnings for parameterized methods and null resolved targets.
- Added method signatures to the registered tests report.
- Filtered package test assemblies from runtime discovery so package fixtures do not appear in consumer registered-test lists.
- Fixed `QuickTestSchedule` startup timing so scheduled methods wait for the configured interval instead of firing immediately after reload.
- Deferred instance schedules until a live target exists; the interval starts when the target appears and missing-target warnings are suppressed while waiting.
- Updated installation examples for the renamed GitHub repository.

## 1.0.0

- Changed the package license to MIT.
- Added a Russian-language feature roadmap.
- Added a committed test host, batchmode runner script, and baseline automated tests.
- Added Russian-language testing documentation.
- Added Unity object instance quick-test invocation for live `MonoBehaviour`, `ScriptableObject`, and `EditorWindow` targets.
- Added weak registry support for plain C# instances.
- Added editor-only IL PostProcessor auto-registration for supported plain C# target constructors.
- Added player build safety smoke checks for Editor/CodeGen leakage, hidden poller exclusion, and injected registration call sites.
- Added editor diagnostics for registered tests, target scope/status, hotkey collisions, and missing targets.
- Defined API expansion boundaries for inherited, generic, async, parameterized, target-selection, and new-trigger scenarios.

## 0.1.0

- Added `QuickTestHotkeyAttribute` for static editor/play-mode quick test calls.
- Added `QuickTestScheduleAttribute` for editor update frame/seconds scheduling.
- Added editor discovery, schedule execution, Scene View event handling, and play-mode input polling.
