using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace UnityQuickTests.Editor.Tests
{
    public sealed class QuickTestUnityObjectTargetTests
    {
        private readonly List<Object> _createdObjects = new List<Object>();

        [SetUp]
        public void SetUp()
        {
            QuickTestMonoBehaviourTarget.InvocationCount = 0;
            QuickTestScriptableObjectTarget.InvocationCount = 0;
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object createdObject in _createdObjects)
            {
                if (createdObject != null)
                {
                    Object.DestroyImmediate(createdObject);
                }
            }

            _createdObjects.Clear();
        }

        [Test]
        public void Resolver_ReturnsAllLiveMonoBehaviourInstances()
        {
            QuickTestMonoBehaviourTarget first = CreateMonoTarget("first", true);
            QuickTestMonoBehaviourTarget second = CreateMonoTarget("second", false);

            QuickTestMonoBehaviourTarget[] targets = UnityObjectQuickTestTargetResolver.Instance
                .FindTargets(typeof(QuickTestMonoBehaviourTarget))
                .Cast<QuickTestMonoBehaviourTarget>()
                .ToArray();

            Assert.That(targets, Does.Contain(first));
            Assert.That(targets, Does.Contain(second));
        }

        [Test]
        public void Resolver_ReturnsLoadedScriptableObjectInstances()
        {
            QuickTestScriptableObjectTarget target = CreateScriptableTarget();

            QuickTestScriptableObjectTarget[] targets = UnityObjectQuickTestTargetResolver.Instance
                .FindTargets(typeof(QuickTestScriptableObjectTarget))
                .Cast<QuickTestScriptableObjectTarget>()
                .ToArray();

            Assert.That(targets, Does.Contain(target));
        }

        [Test]
        public void Resolver_ReturnsLoadedEditorWindowInstances()
        {
            QuickTestEditorWindowTarget target = CreateEditorWindowTarget();

            QuickTestEditorWindowTarget[] targets = UnityObjectQuickTestTargetResolver.Instance
                .FindTargets(typeof(QuickTestEditorWindowTarget))
                .Cast<QuickTestEditorWindowTarget>()
                .ToArray();

            Assert.That(targets, Does.Contain(target));
        }

        [Test]
        public void Invoke_CallsInstanceMethodOnEachLiveMonoBehaviour()
        {
            CreateMonoTarget("first", true);
            CreateMonoTarget("second", true);

            CreateMethod(typeof(QuickTestMonoBehaviourTarget), "RunQuickTest").Invoke();

            Assert.That(QuickTestMonoBehaviourTarget.InvocationCount, Is.EqualTo(2));
        }

        [Test]
        public void Invoke_CallsInstanceMethodOnLoadedScriptableObject()
        {
            CreateScriptableTarget();

            CreateMethod(typeof(QuickTestScriptableObjectTarget), "RunQuickTest").Invoke();

            Assert.That(QuickTestScriptableObjectTarget.InvocationCount, Is.EqualTo(1));
        }

        private QuickTestMonoBehaviourTarget CreateMonoTarget(string name, bool isActive)
        {
            var gameObject = new GameObject(name);
            _createdObjects.Add(gameObject);
            gameObject.SetActive(isActive);
            return gameObject.AddComponent<QuickTestMonoBehaviourTarget>();
        }

        private QuickTestScriptableObjectTarget CreateScriptableTarget()
        {
            var target = ScriptableObject.CreateInstance<QuickTestScriptableObjectTarget>();
            _createdObjects.Add(target);
            return target;
        }

        private QuickTestEditorWindowTarget CreateEditorWindowTarget()
        {
            var target = ScriptableObject.CreateInstance<QuickTestEditorWindowTarget>();
            _createdObjects.Add(target);
            return target;
        }

        private static QuickTestMethod CreateMethod(Type targetType, string methodName)
        {
            MethodInfo method = targetType.GetMethod(
                methodName,
                BindingFlags.Instance | BindingFlags.NonPublic
            );

            return new QuickTestMethod(method);
        }
    }

    public sealed class QuickTestMonoBehaviourTarget : MonoBehaviour
    {
        public static int InvocationCount { get; set; }

        [QuickTestHotkey(KeyCode.T)]
        private void RunQuickTest()
        {
            InvocationCount++;
        }
    }

    public sealed class QuickTestScriptableObjectTarget : ScriptableObject
    {
        public static int InvocationCount { get; set; }

        [QuickTestHotkey(KeyCode.T)]
        private void RunQuickTest()
        {
            InvocationCount++;
        }
    }

    public sealed class QuickTestEditorWindowTarget : UnityEditor.EditorWindow
    {
        [QuickTestHotkey(KeyCode.T)]
        private void RunQuickTest()
        {
        }
    }
}
