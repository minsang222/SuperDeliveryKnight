using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class SceneSmokeTests
{
    [UnityTest]
    public IEnumerator SampleScene_StartsWithoutDuplicatingTheFollowCamera()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        int followCameraCount = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
            .Count(item => item.name == "Player Follow Camera");

        Assert.That(followCameraCount, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator PlayerFallVelocity_StopsAcceleratingAfterT2()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;

        MonoBehaviour player = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .First(component => component != null && component.GetType().Name == "Player");
        Rigidbody2D rigidbody = player.GetComponent<Rigidbody2D>();
        FieldInfo fadeEndField = player.GetType().GetField(
            "_fallGravityFadeEndTime",
            BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.That(fadeEndField, Is.Not.Null,
            "Player에 TUNE-P3용 _fallGravityFadeEndTime 직렬화 필드가 필요합니다.");

        float fadeEndTime = (float)fadeEndField.GetValue(player);
        rigidbody.position = new Vector2(rigidbody.position.x, 100f);
        rigidbody.linearVelocity = Vector2.zero;

        yield return new WaitForSeconds(fadeEndTime + Time.fixedDeltaTime * 3f);
        float terminalVelocity = rigidbody.linearVelocityY;
        yield return new WaitForSeconds(0.2f);

        Assert.That(rigidbody.linearVelocityY, Is.EqualTo(terminalVelocity).Within(0.05f),
            "T2 이후에는 낙하 속도가 더 빨라지면 안 됩니다.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator GeneratedChunks_AreReachableAtBaseSpeed()
    {
        Random.InitState(20260821);
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        MonoBehaviour player = FindBehaviour("Player");
        MethodInfo canReachMethod = player.GetType().GetMethod(
            "CanReachChunk",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(Vector2) },
            null);

        Assert.That(canReachMethod, Is.Not.Null, "Player.CanReachChunk(Vector2) 시그니처가 필요합니다.");

        MonoBehaviour latestBuilding = FindBehaviours("PlatBuilding")
            .OrderBy(building => GetAnchor(building, "EndPoint").position.x)
            .LastOrDefault();

        Assert.That(latestBuilding, Is.Not.Null, "검사할 첫 청크가 필요합니다.");

        Vector2 previousEnd = GetAnchor(latestBuilding, "EndPoint").position;
        int inspectedCount = 0;

        for (int frame = 0; frame < 120 && inspectedCount < 30; frame++)
        {
            player.transform.position = new Vector3(previousEnd.x + 30f, player.transform.position.y, 0f);
            yield return null;

            MonoBehaviour[] newBuildings = FindBehaviours("PlatBuilding")
                .Where(building => GetAnchor(building, "StartPoint").position.x > previousEnd.x + 0.01f)
                .OrderBy(building => GetAnchor(building, "StartPoint").position.x)
                .ToArray();

            foreach (MonoBehaviour building in newBuildings)
            {
                Vector2 start = GetAnchor(building, "StartPoint").position;
                Vector2 offset = start - previousEnd;
                bool canReach = (bool)canReachMethod.Invoke(player, new object[] { offset });

                Assert.That(canReach, Is.True,
                    $"도달 불가능한 청크 오프셋이 생성되었습니다: {offset}");

                previousEnd = GetAnchor(building, "EndPoint").position;
                inspectedCount++;
            }
        }

        Assert.That(inspectedCount, Is.GreaterThanOrEqualTo(30),
            "연속 생성 검증을 위해 청크 30개가 필요합니다.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator GeneratedObstacles_AreOwnedByTheirChunk()
    {
        Random.InitState(20260821);
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        MonoBehaviour player = FindBehaviour("Player");

        for (int frame = 0; frame < 30; frame++)
        {
            player.transform.position += Vector3.right * 30f;
            yield return null;
        }

        MonoBehaviour[] obstacles = FindBehaviours("Obstacle");
        Assert.That(obstacles, Is.Not.Empty, "소유권을 검사할 생성 장애물이 필요합니다.");

        foreach (MonoBehaviour obstacle in obstacles)
        {
            bool belongsToChunk = obstacle.GetComponentsInParent<MonoBehaviour>(true)
                .Any(component => component != null && component.GetType().Name == "PlatBuilding");

            Assert.That(belongsToChunk, Is.True,
                $"{obstacle.name} 장애물은 소속 청크의 자식이어야 합니다.");
        }

        LogAssert.NoUnexpectedReceived();
    }

    private static MonoBehaviour FindBehaviour(string typeName)
    {
        MonoBehaviour behaviour = FindBehaviours(typeName).FirstOrDefault();
        Assert.That(behaviour, Is.Not.Null, $"씬에 {typeName} 컴포넌트가 필요합니다.");
        return behaviour;
    }

    private static MonoBehaviour[] FindBehaviours(string typeName)
    {
        return Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None)
            .Where(component => component != null && component.GetType().Name == typeName)
            .ToArray();
    }

    private static Transform GetAnchor(MonoBehaviour building, string propertyName)
    {
        PropertyInfo property = building.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"{building.GetType().Name}.{propertyName} 프로퍼티가 필요합니다.");
        return (Transform)property.GetValue(building);
    }
}
