using System;
using System.Reflection;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

public class TraversalContractTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [TestCase(0f, 1f)]
    [TestCase(0.2f, 1f)]
    [TestCase(0.4f, 0.5f)]
    [TestCase(0.6f, 0f)]
    [TestCase(1f, 0f)]
    public void FallGravityScale_FadesBetweenT1AndT2(float fallElapsed, float expected)
    {
        Type trajectoryType = FindRequiredType("JumpTrajectory");
        MethodInfo method = FindRequiredMethod(
            trajectoryType,
            "GetFallGravityScale",
            BindingFlags.Public | BindingFlags.Static,
            typeof(float),
            typeof(float),
            typeof(float));

        float actual = (float)method.Invoke(null, new object[] { fallElapsed, 0.2f, 0.6f });

        Assert.That(actual, Is.EqualTo(expected).Within(0.0001f));
    }

    [Test]
    public void PlayerReachability_AcceptsConservativeGapAndRejectsImpossibleOffsets()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            GameObject[] roots = EditorSceneManager.OpenScene(ScenePath).GetRootGameObjects();
            MonoBehaviour player = FindBehaviour(roots, "Player");
            MethodInfo method = FindRequiredMethod(
                player.GetType(),
                "CanReachChunk",
                BindingFlags.Public | BindingFlags.Instance,
                typeof(Vector2));

            Assert.That(InvokeReachability(method, player, new Vector2(1f, 0f)), Is.True,
                "가까운 평지 청크는 기본 속도에서 도달 가능해야 합니다.");
            Assert.That(InvokeReachability(method, player, new Vector2(1f, 100f)), Is.False,
                "점프 최대 높이보다 높은 청크는 거부해야 합니다.");
            Assert.That(InvokeReachability(method, player, new Vector2(1000f, 0f)), Is.False,
                "착지 시간을 넘기는 수평 간격은 거부해야 합니다.");
        }
        finally
        {
            if (previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }

    [Test]
    public void SampleScene_HasValidTraversalTuningAndGeneratorReference()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            GameObject[] roots = EditorSceneManager.OpenScene(ScenePath).GetRootGameObjects();
            MonoBehaviour player = FindBehaviour(roots, "Player");
            MonoBehaviour platformManager = FindBehaviour(roots, "PlatformManager");
            UnityEditor.SerializedObject serializedPlayer = new UnityEditor.SerializedObject(player);
            UnityEditor.SerializedProperty fadeStart = serializedPlayer.FindProperty("_fallGravityFadeStartTime");
            UnityEditor.SerializedProperty fadeEnd = serializedPlayer.FindProperty("_fallGravityFadeEndTime");
            UnityEditor.SerializedProperty safetyMargin = serializedPlayer.FindProperty("_jumpReachSafetyMargin");

            Assert.That(fadeStart, Is.Not.Null, "Player에 TUNE-P2 직렬화 필드가 필요합니다.");
            Assert.That(fadeEnd, Is.Not.Null, "Player에 TUNE-P3 직렬화 필드가 필요합니다.");
            Assert.That(safetyMargin, Is.Not.Null, "Player에 점프 도달 안전 마진이 필요합니다.");
            Assert.That(fadeStart.floatValue, Is.LessThan(fadeEnd.floatValue), "TUNE-P2는 TUNE-P3보다 작아야 합니다.");
            Assert.That(safetyMargin.floatValue, Is.GreaterThan(0f), "점프 도달 안전 마진은 양수여야 합니다.");

            UnityEditor.SerializedProperty playerReference =
                new UnityEditor.SerializedObject(platformManager).FindProperty("_player");
            Assert.That(playerReference, Is.Not.Null, "PlatformManager에 _player 직렬화 참조가 필요합니다.");
            Assert.That(playerReference.objectReferenceValue, Is.SameAs(player));
        }
        finally
        {
            if (previousSetup.Length > 0)
            {
                EditorSceneManager.RestoreSceneManagerSetup(previousSetup);
            }
            else
            {
                EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            }
        }
    }

    private static bool InvokeReachability(MethodInfo method, MonoBehaviour player, Vector2 offset)
    {
        return (bool)method.Invoke(player, new object[] { offset });
    }

    private static Type FindRequiredType(string typeName)
    {
        Type type = Type.GetType($"{typeName}, Assembly-CSharp");
        Assert.That(type, Is.Not.Null, $"Assembly-CSharp에 {typeName} 타입이 필요합니다.");
        return type;
    }

    private static MethodInfo FindRequiredMethod(
        Type type,
        string methodName,
        BindingFlags bindingFlags,
        params Type[] parameterTypes)
    {
        MethodInfo method = type.GetMethod(methodName, bindingFlags, null, parameterTypes, null);
        Assert.That(method, Is.Not.Null, $"{type.Name}.{methodName} 시그니처가 필요합니다.");
        return method;
    }

    private static MonoBehaviour FindBehaviour(GameObject[] roots, string typeName)
    {
        foreach (GameObject root in roots)
        {
            MonoBehaviour[] components = root.GetComponentsInChildren<MonoBehaviour>(true);

            foreach (MonoBehaviour component in components)
            {
                if (component != null && component.GetType().Name == typeName)
                {
                    return component;
                }
            }
        }

        Assert.Fail($"씬에 {typeName} 컴포넌트가 필요합니다.");
        return null;
    }
}
