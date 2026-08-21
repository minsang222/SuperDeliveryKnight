using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerParryTests
{
    [UnityTest]
    public IEnumerator Player_UnsubscribesFromSniperWhenDestroyed()
    {
        System.Type playerType = System.Type.GetType("Player, Assembly-CSharp");
        System.Type sniperType = System.Type.GetType("Sniper, Assembly-CSharp");
        Assert.That(playerType, Is.Not.Null);
        Assert.That(sniperType, Is.Not.Null);
        DestroyComponents(playerType);
        DestroyComponents(sniperType);

        MonoBehaviour sniper = new GameObject("Test Sniper").AddComponent(sniperType) as MonoBehaviour;
        GameObject playerObject = new GameObject("Test Player");
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<BoxCollider2D>();
        MonoBehaviour player = playerObject.AddComponent(playerType) as MonoBehaviour;
        yield return null;

        Assert.That(AimSubscribers(sniper).Any(callback => ReferenceEquals(callback.Target, player)), Is.True);
        Object.Destroy(playerObject);
        yield return null;

        Assert.That(AimSubscribers(sniper).Any(callback => ReferenceEquals(callback.Target, player)), Is.False);
        Object.Destroy(sniper.gameObject);
    }

    [UnityTest]
    public IEnumerator LaterAim_DoesNotCancelAnEarlierParryWindow()
    {
        GameObject playerObject = new GameObject("Test Player");
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<BoxCollider2D>();
        System.Type playerType = System.Type.GetType("Player, Assembly-CSharp");
        Assert.That(playerType, Is.Not.Null, "Assembly-CSharp에 Player 타입이 필요합니다.");
        MonoBehaviour player = playerObject.AddComponent(playerType) as MonoBehaviour;
        MethodInfo attack = playerType.GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo readyParry = playerType.GetMethod("ReadyParry", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo activeWindows = playerType.GetField("_activeParryWindows", BindingFlags.Instance | BindingFlags.NonPublic);

        try
        {
            Time.timeScale = 0f;
            readyParry.Invoke(player, new object[] { 0.08f, 0.04f });
            yield return new WaitForSecondsRealtime(0.02f);
            readyParry.Invoke(player, new object[] { 0.08f, 0.04f });
            yield return new WaitForSecondsRealtime(0.05f);

            Assert.That(((ICollection<int>)activeWindows.GetValue(player)).Count, Is.EqualTo(1),
                "뒤에 예약된 조준이 이미 열릴 첫 패링 창을 취소하면 안 됩니다.");
            attack.Invoke(player, null);

            Assert.That(((ICollection<int>)activeWindows.GetValue(player)).Count, Is.Zero,
                "공격은 열린 패링 창을 소비해야 합니다.");
        }
        finally
        {
            Time.timeScale = 1f;
            Object.Destroy(playerObject);
        }
    }

    [UnityTest]
    public IEnumerator Parry_OnlySucceedsDuringTheRealtimeWindow()
    {
        GameObject playerObject = new GameObject("Test Player");
        playerObject.AddComponent<Rigidbody2D>();
        playerObject.AddComponent<BoxCollider2D>();
        System.Type playerType = System.Type.GetType("Player, Assembly-CSharp");
        Assert.That(playerType, Is.Not.Null, "Assembly-CSharp에 Player 타입이 필요합니다.");
        MonoBehaviour player = playerObject.AddComponent(playerType) as MonoBehaviour;
        MethodInfo attack = playerType.GetMethod("Attack", BindingFlags.Instance | BindingFlags.NonPublic);
        MethodInfo readyParry = playerType.GetMethod("ReadyParry", BindingFlags.Instance | BindingFlags.Public);
        FieldInfo activeWindows = playerType.GetField("_activeParryWindows", BindingFlags.Instance | BindingFlags.NonPublic);

        try
        {
            Time.timeScale = 0f;
            readyParry.Invoke(player, new object[] { 0.08f, 0.04f });
            attack.Invoke(player, null);
            Assert.That(((ICollection<int>)activeWindows.GetValue(player)).Count, Is.Zero,
                "창이 열리기 전 공격은 패링 창을 소비하면 안 됩니다.");

            yield return new WaitForSecondsRealtime(0.07f);
            Assert.That(((ICollection<int>)activeWindows.GetValue(player)).Count, Is.EqualTo(1));
            attack.Invoke(player, null);
            Assert.That(((ICollection<int>)activeWindows.GetValue(player)).Count, Is.Zero,
                "실시간 패링 창 안의 공격은 창을 소비해야 합니다.");

            yield return new WaitForSecondsRealtime(0.06f);
            Assert.That(((ICollection<int>)activeWindows.GetValue(player)).Count, Is.Zero,
                "창이 닫힌 뒤에는 활성 패링 창이 남으면 안 됩니다.");
            LogAssert.NoUnexpectedReceived();
        }
        finally
        {
            Time.timeScale = 1f;
            Object.Destroy(playerObject);
        }
    }

    private static System.Delegate[] AimSubscribers(MonoBehaviour sniper)
    {
        System.Delegate aimed = (System.Delegate)sniper.GetType()
            .GetField("HasAimed", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(sniper);
        return aimed == null ? System.Array.Empty<System.Delegate>() : aimed.GetInvocationList();
    }

    private static void DestroyComponents(System.Type type)
    {
        foreach (Component component in Object.FindObjectsByType(type))
        {
            Object.DestroyImmediate(component.gameObject);
        }
    }
}
