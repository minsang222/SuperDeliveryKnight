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

        int followCameraCount = Object.FindObjectsByType<Transform>()
            .Count(item => item.name == "Player Follow Camera");

        Assert.That(followCameraCount, Is.EqualTo(1));
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator PlayerFallTrajectory_UsesConstantGravity()
    {
        MonoBehaviour player = CreateIsolatedPlayer();
        Rigidbody2D rigidbody = player.GetComponent<Rigidbody2D>();
        rigidbody.position = new Vector2(rigidbody.position.x, 100f);
        rigidbody.linearVelocity = Vector2.zero;

        yield return WaitForFixedFrames(5);
        float firstVelocity = rigidbody.linearVelocityY;
        yield return WaitForFixedFrames(5);
        float secondVelocity = rigidbody.linearVelocityY;
        yield return WaitForFixedFrames(5);
        float thirdVelocity = rigidbody.linearVelocityY;

        Assert.That(thirdVelocity - secondVelocity,
            Is.EqualTo(secondVelocity - firstVelocity).Within(0.05f),
            "F6 계산과 일치하도록 낙하 가속도는 시간에 따라 감쇠하면 안 됩니다.");
        Object.Destroy(player.gameObject);
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
            new[] { typeof(Transform), typeof(Transform) },
            null);

        Assert.That(canReachMethod, Is.Not.Null,
            "Player.CanReachChunk(Transform, Transform) 시그니처가 필요합니다.");

        MonoBehaviour latestBuilding = FindBehaviours("PlatBuilding")
            .OrderBy(building => GetAnchor(building, "EndPoint").position.x)
            .LastOrDefault();

        Assert.That(latestBuilding, Is.Not.Null, "검사할 첫 청크가 필요합니다.");

        Transform previousEnd = GetAnchor(latestBuilding, "EndPoint");
        int inspectedCount = 0;

        for (int frame = 0; frame < 120 && inspectedCount < 30; frame++)
        {
            player.transform.position = new Vector3(previousEnd.position.x + 30f, player.transform.position.y, 0f);
            yield return null;

            MonoBehaviour[] newBuildings = FindBehaviours("PlatBuilding")
                .Where(building => GetAnchor(building, "StartPoint").position.x > previousEnd.position.x + 0.01f)
                .OrderBy(building => GetAnchor(building, "StartPoint").position.x)
                .ToArray();

            foreach (MonoBehaviour building in newBuildings)
            {
                Transform start = GetAnchor(building, "StartPoint");
                Vector2 margin = (Vector2)canReachMethod.Invoke(player, new object[] { previousEnd, start });

                Assert.That(start.position.x, Is.GreaterThan(previousEnd.position.x),
                    "새 청크는 직전 청크보다 앞에 생성되어야 합니다.");
                Assert.That(margin.x, Is.GreaterThanOrEqualTo(-0.001f),
                    $"수평 도달 여유가 부족한 청크가 생성되었습니다: {margin}");
                Assert.That(margin.y, Is.GreaterThanOrEqualTo(-0.001f),
                    $"수직 도달 여유가 부족한 청크가 생성되었습니다: {margin}");

                previousEnd = GetAnchor(building, "EndPoint");
                inspectedCount++;
            }
        }

        Assert.That(inspectedCount, Is.GreaterThanOrEqualTo(30),
            "연속 생성 검증을 위해 청크 30개가 필요합니다.");
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator PlayerReachability_UsesConfiguredGravityAndJumpForce()
    {
        MonoBehaviour player = CreateIsolatedPlayer();
        Rigidbody2D rigidbody = player.GetComponent<Rigidbody2D>();
        rigidbody.gravityScale = 3f;
        GameObject startObject = new GameObject("Test Chunk End");
        GameObject endObject = new GameObject("Test Chunk Start");
        startObject.transform.position = Vector3.zero;
        endObject.transform.position = new Vector3(5f, 0f, 0f);
        MethodInfo canReach = player.GetType().GetMethod(
            "CanReachChunk",
            BindingFlags.Public | BindingFlags.Instance,
            null,
            new[] { typeof(Transform), typeof(Transform) },
            null);

        rigidbody.linearVelocityY = 0f;
        Vector2 marginAtRest = (Vector2)canReach.Invoke(
            player,
            new object[] { startObject.transform, endObject.transform });
        rigidbody.linearVelocityY = 100f;
        Vector2 marginWhileMoving = (Vector2)canReach.Invoke(
            player,
            new object[] { startObject.transform, endObject.transform });

        Assert.That(marginAtRest.x, Is.EqualTo(3.59f).Within(0.02f));
        Assert.That(marginAtRest.y, Is.EqualTo(1.32f).Within(0.02f));
        Assert.That(marginWhileMoving, Is.EqualTo(marginAtRest),
            "청크 도달 계산은 호출 순간의 수직속도에 따라 달라지면 안 됩니다.");

        Object.Destroy(player.gameObject);
        Object.Destroy(startObject);
        Object.Destroy(endObject);
        yield return null;
    }

    [UnityTest]
    public IEnumerator Attack_UsesSeparateAnimationDurationAndInputCooldown()
    {
        MonoBehaviour player = CreateIsolatedPlayer();
        GameObject slashObject = new GameObject("Test Slash");
        slashObject.transform.SetParent(player.transform);
        BoxCollider2D hitbox = slashObject.AddComponent<BoxCollider2D>();
        hitbox.isTrigger = true;
        hitbox.enabled = false;
        slashObject.SetActive(false);
        SetField(player, "slashHitbox", hitbox);
        SetField(player, "slashObject", slashObject);
        float attackDuration = GetField<float>(player, "attackDuration");
        float attackCooldown = GetField<float>(player, "attackCooldown");
        MethodInfo attack = GetMethod(player, "Attack");

        attack.Invoke(player, null);
        Assert.That(hitbox.enabled, Is.True, "입력 순간에는 공격 판정이 활성화되어야 합니다.");
        Assert.That(slashObject.activeSelf, Is.True, "입력 순간에는 공격 연출도 시작해야 합니다.");

        yield return new WaitForSeconds(Time.fixedDeltaTime * 2f);
        Assert.That(hitbox.enabled, Is.False, "공격 판정은 한 물리 판정 뒤 남아 있으면 안 됩니다.");
        Assert.That(slashObject.activeSelf, Is.True, "판정 종료 뒤에도 attackDuration 동안 연출은 유지해야 합니다.");

        yield return new WaitForSeconds(attackDuration);
        Assert.That(slashObject.activeSelf, Is.False);

        yield return new WaitForSeconds(Mathf.Max(0f, attackCooldown - attackDuration));
        attack.Invoke(player, null);
        Assert.That(slashObject.activeSelf, Is.True,
            "재공격 가능 시점은 attackDuration과 더한 값이 아니라 입력 시점부터 attackCooldown 뒤여야 합니다.");
        Object.Destroy(player.gameObject);
    }

    [UnityTest]
    public IEnumerator Respawn_EntersTimedStateWithoutStoppingGameTime()
    {
        MonoBehaviour player = CreateIsolatedPlayer();
        FieldInfo isRespawning = player.GetType().GetField("_isRespawning", BindingFlags.Instance | BindingFlags.NonPublic);
        FieldInfo respawnElapsed = player.GetType().GetField("_respawnElapsed", BindingFlags.Instance | BindingFlags.NonPublic);

        try
        {
            GetMethod(player, "Respawn").Invoke(player, null);

            Assert.That(isRespawning, Is.Not.Null, "리스폰 상태 필드 이름은 _isRespawning이어야 합니다.");
            Assert.That(Time.timeScale, Is.EqualTo(1f), "리스폰은 게임오버와 달리 게임 시간을 멈추면 안 됩니다.");
            Assert.That(respawnElapsed, Is.Not.Null);
            Assert.That((float)respawnElapsed.GetValue(player), Is.Zero, "Respawn 진입 시 경과 시간을 초기화해야 합니다.");
            Assert.That((bool)isRespawning.GetValue(player), Is.True);
        }
        finally
        {
            Time.timeScale = 1f;
            Object.Destroy(player.gameObject);
        }

        yield return null;
    }

    [UnityTest]
    public IEnumerator RespawnPoint_UsesTheNextChunkStart()
    {
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        MonoBehaviour player = FindBehaviour("Player");
        MonoBehaviour platformManager = FindBehaviour("PlatformManager");
        MonoBehaviour[] buildings = System.Array.Empty<MonoBehaviour>();

        for (int frame = 0; frame < 30 && buildings.Length < 2; frame++)
        {
            player.transform.position += Vector3.right * 30f;
            yield return null;
            buildings = FindBehaviours("PlatBuilding")
                .Where(building => building.transform.IsChildOf(platformManager.transform))
                .OrderBy(building => GetAnchor(building, "StartPoint").position.x)
                .ToArray();
        }

        Assert.That(buildings.Length, Is.GreaterThanOrEqualTo(2));

        Transform currentStart = GetAnchor(buildings[0], "StartPoint");
        Transform currentEnd = GetAnchor(buildings[0], "EndPoint");
        float playerX = (currentStart.position.x + currentEnd.position.x) * 0.5f;
        Transform expectedStart = buildings
            .Select(building => GetAnchor(building, "StartPoint"))
            .First(start => start.position.x > playerX);
        MethodInfo getRespawnPoint = platformManager.GetType().GetMethod("TryGetRespawnPoint");
        object[] arguments = { playerX, 5f, Vector3.zero };

        bool found = (bool)getRespawnPoint.Invoke(platformManager, arguments);
        Vector3 actual = (Vector3)arguments[2];

        Assert.That(found, Is.True);
        Assert.That(actual, Is.EqualTo(expectedStart.position + Vector3.up * 5f));
    }

    [UnityTest]
    public IEnumerator GeneratedObstacles_AreOwnedByTheirChunk()
    {
        Random.InitState(20260821);
        SceneManager.LoadScene("SampleScene");
        yield return null;
        yield return null;

        MonoBehaviour player = FindBehaviour("Player");
        MonoBehaviour platformManager = FindBehaviour("PlatformManager");
        int inspectedCount = 0;

        for (int frame = 0; frame < 30; frame++)
        {
            player.transform.position += Vector3.right * 30f;
            yield return null;

            MonoBehaviour[] obstacles = FindBehaviours("Obstacle")
                .Where(obstacle => obstacle.transform.IsChildOf(platformManager.transform))
                .ToArray();

            foreach (MonoBehaviour obstacle in obstacles)
            {
                bool belongsToChunk = obstacle.GetComponentsInParent<MonoBehaviour>(true)
                    .Any(component => component != null && component.GetType().Name == "PlatBuilding");

                Assert.That(belongsToChunk, Is.True,
                    $"{obstacle.name} 장애물은 소속 청크의 자식이어야 합니다.");
                inspectedCount++;
            }
        }

        Assert.That(inspectedCount, Is.GreaterThan(0), "소유권을 검사할 생성 장애물이 필요합니다.");
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
        return Object.FindObjectsByType<MonoBehaviour>()
            .Where(component => component != null && component.GetType().Name == typeName)
            .ToArray();
    }

    private static Transform GetAnchor(MonoBehaviour building, string propertyName)
    {
        PropertyInfo property = building.GetType().GetProperty(propertyName, BindingFlags.Public | BindingFlags.Instance);
        Assert.That(property, Is.Not.Null, $"{building.GetType().Name}.{propertyName} 프로퍼티가 필요합니다.");
        return (Transform)property.GetValue(building);
    }

    private static MethodInfo GetMethod(MonoBehaviour target, string methodName)
    {
        MethodInfo method = target.GetType().GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(method, Is.Not.Null, $"{target.GetType().Name}.{methodName} 메서드가 필요합니다.");
        return method;
    }

    private static T GetField<T>(MonoBehaviour target, string fieldName)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드가 필요합니다.");
        return (T)field.GetValue(target);
    }

    private static void SetField<T>(MonoBehaviour target, string fieldName, T value)
    {
        FieldInfo field = target.GetType().GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.That(field, Is.Not.Null, $"{target.GetType().Name}.{fieldName} 필드가 필요합니다.");
        field.SetValue(target, value);
    }

    private static MonoBehaviour CreateIsolatedPlayer()
    {
        foreach (MonoBehaviour platformManager in FindBehaviours("PlatformManager"))
        {
            platformManager.enabled = false;
        }

        GameObject playerObject = new GameObject("Test Player");
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<BoxCollider2D>();
        System.Type playerType = System.Type.GetType("Player, Assembly-CSharp");
        Assert.That(playerType, Is.Not.Null, "Assembly-CSharp에 Player 타입이 필요합니다.");
        return (MonoBehaviour)playerObject.AddComponent(playerType);
    }

    private static IEnumerator WaitForFixedFrames(int count)
    {
        for (int frame = 0; frame < count; frame++)
        {
            yield return new WaitForFixedUpdate();
        }
    }
}
