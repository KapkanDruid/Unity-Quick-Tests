using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace UnityQuickTests.Editor
{
    internal static class QuickTestDiagnostics
    {
        private static readonly HashSet<KeyCode> CommonSceneViewShortcutKeys = new HashSet<KeyCode>
        {
            KeyCode.Q,
            KeyCode.W,
            KeyCode.E,
            KeyCode.R,
            KeyCode.T,
            KeyCode.Y,
            KeyCode.F
        };

        internal static IReadOnlyList<string> FindHotkeyWarnings(IEnumerable<QuickTestHotkeyBinding> hotkeyBindings)
        {
            QuickTestHotkeyBinding[] bindings = hotkeyBindings?.ToArray() ?? Array.Empty<QuickTestHotkeyBinding>();
            var warnings = new List<string>();

            warnings.AddRange(FindDuplicateHotkeyWarnings(bindings));
            warnings.AddRange(FindUnmodifiedHotkeyWarnings(bindings));

            return warnings;
        }

        internal static string BuildRegisteredTestsReport(
            IEnumerable<QuickTestHotkeyBinding> hotkeyBindings,
            IEnumerable<QuickTestScheduleBinding> scheduleBindings,
            IEnumerable<string> diagnosticWarnings)
        {
            QuickTestHotkeyBinding[] hotkeys = hotkeyBindings?.ToArray() ?? Array.Empty<QuickTestHotkeyBinding>();
            QuickTestScheduleBinding[] schedules = scheduleBindings?.ToArray() ?? Array.Empty<QuickTestScheduleBinding>();
            string[] warnings = diagnosticWarnings?.ToArray() ?? Array.Empty<string>();

            var builder = new StringBuilder();
            builder.AppendLine("[UnityQuickTests] Registered tests");
            builder.AppendLine($"Hotkeys: {hotkeys.Length}");
            builder.AppendLine($"Schedules: {schedules.Length}");
            builder.AppendLine("Edit Mode hotkeys: Scene View event loop only; use modifiers for reliable input.");

            AppendHotkeys(builder, hotkeys);
            AppendSchedules(builder, schedules);
            AppendWarnings(builder, warnings);

            return builder.ToString().TrimEnd();
        }

        private static IEnumerable<string> FindDuplicateHotkeyWarnings(QuickTestHotkeyBinding[] bindings)
        {
            return bindings
                .GroupBy(binding => binding.InputSignature)
                .Where(group => group.Count() > 1)
                .Select(group =>
                {
                    string methods = string.Join(", ", group
                        .Select(binding => binding.MethodName)
                        .OrderBy(name => name)
                    );

                    return $"[UnityQuickTests] Hotkey {group.First().Description} is registered for multiple tests: {methods}. All matching tests will run.";
                });
        }

        private static IEnumerable<string> FindUnmodifiedHotkeyWarnings(QuickTestHotkeyBinding[] bindings)
        {
            foreach (QuickTestHotkeyBinding binding in bindings.Where(binding => !binding.HasModifiers))
            {
                string recommendation = "Prefer Ctrl/Shift/Alt/Cmd plus one trigger key for stable editor input.";

                if (CommonSceneViewShortcutKeys.Contains(binding.TriggerKey))
                {
                    yield return
                        $"[UnityQuickTests] Hotkey {binding.Description} for {binding.MethodName} may conflict with Unity Scene View shortcuts. {recommendation}";
                    continue;
                }

                yield return
                    $"[UnityQuickTests] Hotkey {binding.Description} for {binding.MethodName} has no modifiers. {recommendation}";
            }
        }

        private static void AppendHotkeys(StringBuilder builder, QuickTestHotkeyBinding[] hotkeys)
        {
            builder.AppendLine("Hotkey bindings:");

            if (hotkeys.Length == 0)
            {
                builder.AppendLine("- none");
                return;
            }

            foreach (QuickTestHotkeyBinding binding in hotkeys.OrderBy(binding => binding.Description))
            {
                AppendBinding(
                    builder,
                    triggerKind: "Hotkey",
                    triggerDescription: binding.Description,
                    method: binding.Method
                );
            }
        }

        private static void AppendSchedules(StringBuilder builder, QuickTestScheduleBinding[] schedules)
        {
            builder.AppendLine("Schedule bindings:");

            if (schedules.Length == 0)
            {
                builder.AppendLine("- none");
                return;
            }

            foreach (QuickTestScheduleBinding binding in schedules.OrderBy(binding => binding.MethodName))
            {
                AppendBinding(
                    builder,
                    triggerKind: "Schedule",
                    triggerDescription: binding.Description,
                    method: binding.Method
                );
            }
        }

        private static void AppendBinding(
            StringBuilder builder,
            string triggerKind,
            string triggerDescription,
            QuickTestMethod method)
        {
            builder.AppendLine($"- {triggerKind}: {triggerDescription}");
            builder.AppendLine($"  method: {method.DisplayName}");
            builder.AppendLine($"  declaring type: {method.DeclaringTypeName}");
            builder.AppendLine($"  target scope: {method.TargetScopeDescription}");
            builder.AppendLine($"  status: {method.SupportStatusDescription}");
        }

        private static void AppendWarnings(StringBuilder builder, string[] warnings)
        {
            builder.AppendLine("Warnings:");

            if (warnings.Length == 0)
            {
                builder.AppendLine("- none");
                return;
            }

            foreach (string warning in warnings)
            {
                builder.AppendLine($"- {warning}");
            }
        }
    }
}
