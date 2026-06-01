using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace UnityQuickTests.Tests
{
    public sealed class QuickTestInputPollerTests
    {
        [UnityTest]
        public IEnumerator Update_RaisesUpdatedEvent()
        {
            int updateCount = 0;
            var pollerObject = new GameObject("QuickTestInputPollerTests");

            QuickTestInputPoller.Updated += OnUpdated;
            pollerObject.AddComponent<QuickTestInputPoller>();

            yield return null;

            QuickTestInputPoller.Updated -= OnUpdated;
            Object.DestroyImmediate(pollerObject);

            Assert.That(updateCount, Is.GreaterThan(0));

            void OnUpdated()
            {
                updateCount++;
            }
        }
    }
}
