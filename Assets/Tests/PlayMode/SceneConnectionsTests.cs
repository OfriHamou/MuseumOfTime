using System.Collections;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Step 6.4: verifies the first half of S9's chain end to end - "Lens
/// (scene 1) required in scene 2". The second half, "Hourglass required in
/// scene 3", is FrozenCitySceneTests.Exit_* - both together are what make
/// the acquisition chain (Step 3.9) an actual playable link between scenes
/// rather than just a flag nobody reads.
/// </summary>
public sealed class SceneConnectionsTests
{
    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    [Test]
    public void Exit_StaysInMuseumNightWithoutTheTimeLens()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();
        Assert.IsNotNull(exit, "No SceneExitTrigger in MuseumNight.");

        GameManager.Instance.State.hasTimeLens = false;
        InvokeOnPlayerEntered(exit);

        Assert.AreEqual("MuseumNight", SceneManager.GetActiveScene().name);
        Assert.IsFalse(SceneExitTrigger.LastExitSucceeded);
    }

    [UnityTest]
    public IEnumerator Exit_LeavesForFrozenCityOnceTheTimeLensIsHeld()
    {
        var exit = Object.FindFirstObjectByType<SceneExitTrigger>();
        Assert.IsNotNull(exit, "No SceneExitTrigger in MuseumNight.");

        GameManager.Instance.State.hasTimeLens = true;
        InvokeOnPlayerEntered(exit);
        yield return null;
        yield return null;

        Assert.IsTrue(SceneExitTrigger.LastExitSucceeded);
        Assert.AreEqual(
            "FrozenCity",
            SceneManager.GetActiveScene().name,
            "Holding the Time Lens should let the player leave MuseumNight for FrozenCity.");
    }

    private static void InvokeOnPlayerEntered(SceneExitTrigger trigger)
    {
        MethodInfo method = typeof(SceneExitTrigger).GetMethod(
            "OnPlayerEntered", BindingFlags.Instance | BindingFlags.NonPublic);

        method.Invoke(trigger, new object[] { null });
    }
}
