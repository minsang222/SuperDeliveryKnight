using System;
using System.Linq;
using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

public class SceneContractTests
{
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";

    [Test]
    public void SampleScene_HasRequiredRunnerReferences()
    {
        SceneSetup[] previousSetup = EditorSceneManager.GetSceneManagerSetup();

        try
        {
            GameObject[] roots = EditorSceneManager.OpenScene(ScenePath).GetRootGameObjects();
            MonoBehaviour player = FindBehaviour(roots, "Player");
            MonoBehaviour platformManager = FindBehaviour(roots, "PlatformManager");
            MonoBehaviour followCamera = FindBehaviour(roots, "CinemachineCamera");

            Assert.That(player, Is.Not.Null, "SampleScene에 Player가 필요합니다.");
            Assert.That(platformManager, Is.Not.Null, "SampleScene에 PlatformManager가 필요합니다.");
            Assert.That(Camera.main, Is.Not.Null, "SampleScene에 MainCamera 태그 카메라가 필요합니다.");
            Assert.That(Camera.main.GetComponents<MonoBehaviour>().Any(IsType("CinemachineBrain")), Is.True);
            Assert.That(Camera.main.transform.parent, Is.Null, "Main Camera는 Player 이동을 중복 상속하면 안 됩니다.");
            Assert.That(followCamera, Is.Not.Null, "씬에 Player Follow Camera가 필요합니다.");
            Assert.That(GetProperty<Transform>(followCamera, "Follow"), Is.SameAs(player.transform));
            Assert.That(player.GetComponent<Rigidbody2D>(), Is.Not.Null);
            Assert.That(GetReference(player, "slashObject"), Is.Not.Null);
            Assert.That(GetReference(player, "slashHitbox"), Is.TypeOf<BoxCollider2D>());
            Assert.That(((Collider2D)GetReference(player, "slashHitbox")).isTrigger, Is.True);
            Assert.That(GetArray(platformManager, "buildings"), Is.Not.Empty.And.All.Not.Null);
            Assert.That(GetArray(platformManager, "obstacles"), Is.Not.Empty.And.All.Not.Null);
            Assert.That(GetReference(platformManager, "player"), Is.SameAs(player));
            Assert.That(GetReference(player, "platformManager"), Is.SameAs(platformManager));
            Assert.That(GetFloat(player, "respawnDropHeight"), Is.EqualTo(5f));
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

    [TestCase("Assets/Prefabs/Buildings/Building.prefab")]
    [TestCase("Assets/Prefabs/Buildings/Building 1.prefab")]
    public void BuildingPrefab_HasChunkAnchors(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        MonoBehaviour building = FindBehaviour(new[] { prefab }, "PlatBuilding");

        Assert.That(building, Is.Not.Null, $"{path}에 PlatBuilding이 필요합니다.");
        Assert.That(GetReference(building, "startPoint"), Is.Not.Null);
        Assert.That(GetReference(building, "endPoint"), Is.Not.Null);
        Assert.That(GetArray(building, "obstaclePoints"), Is.Not.Empty.And.All.Not.Null);
    }

    [TestCase("Assets/Prefabs/Obstacles/Obstacle_Box.prefab")]
    [TestCase("Assets/Prefabs/Obstacles/Obstacle_OutdoorUnit.prefab")]
    public void ObstaclePrefab_HasDestructionComponents(string path)
    {
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        Assert.That(FindBehaviour(new[] { prefab }, "Obstacle"), Is.Not.Null, $"{path}에 Obstacle이 필요합니다.");
        Assert.That(prefab.GetComponent<Collider2D>(), Is.Not.Null);
        Assert.That(prefab.GetComponent<Rigidbody2D>(), Is.Not.Null);
    }

    private static MonoBehaviour FindBehaviour(GameObject[] roots, string typeName)
    {
        return roots
            .Where(root => root != null)
            .SelectMany(root => root.GetComponentsInChildren<MonoBehaviour>(true))
            .FirstOrDefault(component => component != null && component.GetType().Name == typeName);
    }

    private static Func<MonoBehaviour, bool> IsType(string typeName)
    {
        return component => component != null && component.GetType().Name == typeName;
    }

    private static T GetProperty<T>(object target, string propertyName)
    {
        return (T)target.GetType().GetProperty(propertyName).GetValue(target);
    }

    private static UnityEngine.Object GetReference(MonoBehaviour target, string propertyName)
    {
        return GetProperty(target, propertyName).objectReferenceValue;
    }

    private static UnityEngine.Object[] GetArray(MonoBehaviour target, string propertyName)
    {
        SerializedProperty property = GetProperty(target, propertyName);
        return Enumerable.Range(0, property.arraySize)
            .Select(index => property.GetArrayElementAtIndex(index).objectReferenceValue)
            .ToArray();
    }

    private static float GetFloat(MonoBehaviour target, string propertyName)
    {
        return GetProperty(target, propertyName).floatValue;
    }

    private static SerializedProperty GetProperty(MonoBehaviour target, string propertyName)
    {
        SerializedProperty property = new SerializedObject(target).FindProperty(propertyName);
        Assert.That(property, Is.Not.Null, $"{target.GetType().Name}.{propertyName} 직렬화 필드가 필요합니다.");
        return property;
    }
}
