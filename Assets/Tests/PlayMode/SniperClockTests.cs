using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class SniperClockTests
{
    [SetUp]
    public void SetUp()
    {
        DestroyTestComponents("Sniper");
        DestroyTestComponents("Clock");
    }

    [TearDown]
    public void TearDown()
    {
        DestroyTestComponents("Sniper");
        DestroyTestComponents("Clock");
    }

    [UnityTest]
    public IEnumerator Sniper_StartsWithoutClockOrPlatformManager()
    {
        GameObject sniperObject = new GameObject("Sniper");
        sniperObject.AddComponent(GameType("Sniper"));

        yield return null;

        UnityEngine.Object.Destroy(sniperObject);
        LogAssert.NoUnexpectedReceived();
    }

    [UnityTest]
    public IEnumerator Sniper_UnsubscribesFromClockWhenDestroyed()
    {
        MonoBehaviour clock = AddComponent("Clock");
        MonoBehaviour sniper = AddComponent("Sniper");
        yield return null;

        Assert.That(HeartbeatSubscribers(clock).Any(callback => ReferenceEquals(callback.Target, sniper)), Is.True);
        UnityEngine.Object.Destroy(sniper.gameObject);
        yield return null;

        Assert.That(HeartbeatSubscribers(clock).Any(callback => ReferenceEquals(callback.Target, sniper)), Is.False);
        UnityEngine.Object.Destroy(clock.gameObject);
    }

    [Test]
    public void ScheduledShot_CountsDownWithoutAnotherRandomRoll()
    {
        MonoBehaviour sniper = AddComponent("Sniper");
        SetField(sniper, "_nextShot", 4);
        SetField(sniper, "thresholdChance", 1d);
        SetField(sniper, "_myDefaultPositionRandomSeed", new System.Random(0));

        Heartbeat(sniper);

        Assert.That(GetField<int>(sniper, "_nextShot"), Is.EqualTo(3));
        UnityEngine.Object.DestroyImmediate(sniper.gameObject);
    }

    [Test]
    public void NormalWarning_EmitsOneAimSignal()
    {
        MonoBehaviour sniper = AddComponent("Sniper");
        SetField(sniper, "_nextShot", 3);
        int aims = 0;
        AddAimListener(sniper, (_, _) => aims++);

        Heartbeat(sniper);

        Assert.That(aims, Is.EqualTo(1));
        UnityEngine.Object.DestroyImmediate(sniper.gameObject);
    }

    [Test]
    public void DoubleWarning_EmitsItsSecondAimOnTheNextHeartbeat()
    {
        MonoBehaviour sniper = AddComponent("Sniper");
        SetField(sniper, "_nextShot", 3);
        SetField(sniper, "_isDouble", true);
        int aims = 0;
        AddAimListener(sniper, (_, _) => aims++);

        Heartbeat(sniper);
        Assert.That(aims, Is.EqualTo(1));
        Heartbeat(sniper);

        Assert.That(aims, Is.EqualTo(2));
        UnityEngine.Object.DestroyImmediate(sniper.gameObject);
    }

    [Test]
    public void WarningWithoutAudioReferences_IsSafeAndStillEmitsAim()
    {
        MonoBehaviour sniper = AddComponent("Sniper");
        SetField(sniper, "_nextShot", 3);
        int aims = 0;
        AddAimListener(sniper, (_, _) => aims++);

        Assert.DoesNotThrow(() => Heartbeat(sniper));
        Assert.That(aims, Is.EqualTo(1));
        UnityEngine.Object.DestroyImmediate(sniper.gameObject);
    }

    [Test]
    public void RedRayDuration_EndsHalfParryWindowBeforeShot()
    {
        float duration = (float)GameType("Sniper")
            .GetMethod("RedRayDuration", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[] { 0.8333f, 0.1f });

        Assert.That(duration, Is.EqualTo(0.7833f).Within(0.0001f));
    }

    [Test]
    public void RayColor_TweensEveryColorChannel()
    {
        Color color = (Color)GameType("Sniper")
            .GetMethod("RayColor", BindingFlags.Static | BindingFlags.NonPublic)
            .Invoke(null, new object[]
            {
                new Color(0.2f, 0.4f, 0.6f, 0.8f),
                new Color(1f, 0f, 0.2f, 0.4f),
                0.25f
            });

        Assert.That(color, Is.EqualTo(new Color(0.4f, 0.3f, 0.5f, 0.7f)));
    }

    private static MonoBehaviour AddComponent(string name)
    {
        return new GameObject(name).AddComponent(GameType(name)) as MonoBehaviour;
    }

    private static Type GameType(string name)
    {
        return Type.GetType($"{name}, Assembly-CSharp");
    }

    private static void DestroyTestComponents(string name)
    {
        foreach (Component component in UnityEngine.Object.FindObjectsByType(GameType(name)))
        {
            UnityEngine.Object.DestroyImmediate(component.gameObject);
        }
    }

    private static void AddAimListener(MonoBehaviour sniper, Action<float, float> listener)
    {
        sniper.GetType().GetEvent("HasAimed").AddEventHandler(sniper, listener);
    }

    private static void Heartbeat(MonoBehaviour sniper)
    {
        sniper.GetType().GetMethod("OnHeartbeat", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(sniper, new object[] { 0 });
    }

    private static Delegate[] HeartbeatSubscribers(MonoBehaviour clock)
    {
        Delegate heartbeat = (Delegate)clock.GetType().GetField("Heartbeat", BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(clock);
        return heartbeat == null ? Array.Empty<Delegate>() : heartbeat.GetInvocationList();
    }

    private static T GetField<T>(object target, string name)
    {
        return (T)target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .GetValue(target);
    }

    private static void SetField(object target, string name, object value)
    {
        target.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(target, value);
    }
}
