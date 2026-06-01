using NUnit.Framework;
using UnityEngine;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestHotkeyAttributeTests
    {
        [Test]
        public void Constructor_StoresKeys()
        {
            var attribute = new QuickTestHotkeyAttribute(KeyCode.LeftControl, KeyCode.T);

            Assert.That(attribute.Keys, Is.EqualTo(new[] { KeyCode.LeftControl, KeyCode.T }));
        }

        [Test]
        public void Constructor_TreatsNullKeysAsEmpty()
        {
            var attribute = new QuickTestHotkeyAttribute((KeyCode[])null);

            Assert.That(attribute.Keys, Is.Empty);
        }
    }
}
