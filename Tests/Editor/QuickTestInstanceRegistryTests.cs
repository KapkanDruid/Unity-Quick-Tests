using System;
using System.Linq;
using System.Runtime.CompilerServices;
using NUnit.Framework;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestInstanceRegistryTests
    {
        [SetUp]
        public void SetUp()
        {
            QuickTestInstanceRegistry.Clear();
        }

        [TearDown]
        public void TearDown()
        {
            QuickTestInstanceRegistry.Clear();
        }

        [Test]
        public void Register_FindTargets_ReturnsRegisteredAssignableInstances()
        {
            var first = new RegistryTarget();
            var second = new DerivedRegistryTarget();
            var unrelated = new UnrelatedTarget();

            QuickTestInstanceRegistry.Register(first);
            QuickTestInstanceRegistry.Register(second);
            QuickTestInstanceRegistry.Register(unrelated);

            RegistryTarget[] targets = QuickTestInstanceRegistry
                .FindTargets(typeof(RegistryTarget))
                .Cast<RegistryTarget>()
                .ToArray();

            Assert.That(targets, Is.EquivalentTo(new RegistryTarget[] { first, second }));
        }

        [Test]
        public void Register_DeduplicatesSameTarget()
        {
            var target = new RegistryTarget();

            QuickTestInstanceRegistry.Register(target);
            QuickTestInstanceRegistry.Register(target);

            object[] targets = QuickTestInstanceRegistry.FindTargets(typeof(RegistryTarget)).ToArray();

            Assert.That(targets, Is.EqualTo(new object[] { target }));
        }

        [Test]
        public void Unregister_RemovesTarget()
        {
            var removed = new RegistryTarget();
            var remaining = new RegistryTarget();

            QuickTestInstanceRegistry.Register(removed);
            QuickTestInstanceRegistry.Register(remaining);

            QuickTestInstanceRegistry.Unregister(removed);

            object[] targets = QuickTestInstanceRegistry.FindTargets(typeof(RegistryTarget)).ToArray();

            Assert.That(targets, Is.EqualTo(new object[] { remaining }));
        }

        [Test]
        public void FindTargets_PrunesCollectedTargets()
        {
            WeakReference weakReference = RegisterTemporaryTarget();

            ForceGarbageCollection();

            Assert.That(weakReference.IsAlive, Is.False);
            Assert.That(QuickTestInstanceRegistry.FindTargets(typeof(RegistryTarget)), Is.Empty);
        }

        [Test]
        public void Clear_RemovesAllTargets()
        {
            QuickTestInstanceRegistry.Register(new RegistryTarget());
            QuickTestInstanceRegistry.Register(new RegistryTarget());

            QuickTestInstanceRegistry.Clear();

            Assert.That(QuickTestInstanceRegistry.FindTargets(typeof(RegistryTarget)), Is.Empty);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference RegisterTemporaryTarget()
        {
            var target = new RegistryTarget();
            var weakReference = new WeakReference(target);
            QuickTestInstanceRegistry.Register(target);
            target = null;
            return weakReference;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ForceGarbageCollection()
        {
            for (int i = 0; i < 8; i++)
            {
                GC.Collect(GC.MaxGeneration, GCCollectionMode.Forced, true);
                GC.WaitForPendingFinalizers();

                byte[][] pressure = new byte[16][];

                for (int j = 0; j < pressure.Length; j++)
                {
                    pressure[j] = new byte[1024 * 32];
                }
            }
        }

        private class RegistryTarget
        {
        }

        private sealed class DerivedRegistryTarget : RegistryTarget
        {
        }

        private sealed class UnrelatedTarget
        {
        }
    }
}
