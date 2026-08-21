using System.Collections;
using System.Linq;
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
}
