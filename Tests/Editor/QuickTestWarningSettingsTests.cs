using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestWarningSettingsTests
    {
        [TearDown]
        public void TearDown()
        {
            QuickTestWarningSettings.WarningsEnabled = true;
        }

        [Test]
        public void LogWarning_WhenDisabled_DoesNotWriteWarning()
        {
            QuickTestWarningSettings.WarningsEnabled = false;

            QuickTestWarningSettings.LogWarning("[UnityQuickTests] suppressed warning");

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void LogWarning_WhenEnabled_WritesWarning()
        {
            QuickTestWarningSettings.WarningsEnabled = true;

            LogAssert.Expect(LogType.Warning, "[UnityQuickTests] visible warning");

            QuickTestWarningSettings.LogWarning("[UnityQuickTests] visible warning");
        }
    }
}
