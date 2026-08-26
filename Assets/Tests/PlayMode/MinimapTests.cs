using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Play-mode checks for Step 5.3: the minimap.
///
/// These cover what is structurally verifiable without eyes on the render
/// texture: the camera exists, is orthographic, renders the Minimap layer
/// ONLY (an allow-list is what keeps hidden Time Anchors off it - T21), is
/// excluded from the main gameplay camera, and follows the player. Whether
/// it actually *reads* correctly on screen is a visual check, documented as
/// such in Phase5_Unity_Walkthrough.md rather than faked here.
/// </summary>
public sealed class MinimapTests
{
    private GameObject player;

    [UnitySetUp]
    public IEnumerator LoadScene()
    {
        SceneManager.LoadScene("MuseumNight", LoadSceneMode.Single);
        yield return null;
        yield return null;

        player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' object in MuseumNight.");
    }

    [Test]
    public void MinimapCamera_IsOrthographicAndRendersOnlyTheMinimapLayer()
    {
        GameObject camGo = GameObject.Find("MinimapCamera");
        Assert.IsNotNull(camGo, "No MinimapCamera in the scene.");

        Camera cam = camGo.GetComponent<Camera>();
        Assert.IsNotNull(cam, "MinimapCamera has no Camera component.");
        Assert.IsTrue(cam.orthographic, "The minimap camera should be orthographic.");

        int minimapLayer = LayerMask.NameToLayer("Minimap");
        Assert.GreaterOrEqual(minimapLayer, 0, "No 'Minimap' layer defined in this project.");

        Assert.AreEqual(
            1 << minimapLayer,
            cam.cullingMask,
            "The minimap camera should render the Minimap layer only - " +
            "anything broader risks showing a hidden Time Anchor (T21).");

        Assert.IsNotNull(cam.targetTexture, "The minimap camera has no target RenderTexture.");
        Assert.AreNotEqual("MainCamera", camGo.tag, "The minimap camera must not compete for Camera.main.");
    }

    [Test]
    public void MinimapCamera_FollowsThePlayer()
    {
        GameObject camGo = GameObject.Find("MinimapCamera");
        Assert.IsNotNull(camGo, "No MinimapCamera in the scene.");

        var controller = camGo.GetComponent<MinimapController>();
        Assert.IsNotNull(controller, "MinimapCamera has no MinimapController.");

        var target = (Transform)typeof(MinimapController)
            .GetField("target", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
            .GetValue(controller);

        Assert.AreEqual(player.transform, target, "MinimapController is not following the Player.");
    }

    [UnityTest]
    public IEnumerator MinimapCamera_FollowsPositionAndRotatesToMatchHeading()
    {
        GameObject camGo = GameObject.Find("MinimapCamera");
        Assert.IsNotNull(camGo, "No MinimapCamera in the scene.");

        CharacterController cc = player.GetComponent<CharacterController>();
        cc.enabled = false;
        player.transform.position = new Vector3(4f, 1.08f, -3f);
        player.transform.rotation = Quaternion.Euler(0f, 123f, 0f);
        cc.enabled = true;

        yield return null;

        Vector3 camPos = camGo.transform.position;
        Assert.AreEqual(4f, camPos.x, 0.05f, "The minimap camera did not follow Noa's X position.");
        Assert.AreEqual(-3f, camPos.z, 0.05f, "The minimap camera did not follow Noa's Z position.");
        Assert.Greater(camPos.y, player.transform.position.y + 1f, "The minimap camera should stay well above Noa.");

        Assert.AreEqual(
            123f,
            camGo.transform.eulerAngles.y,
            1f,
            "The minimap did not rotate to match Noa's heading - T18 asks for " +
            "rotation specifically because it reads better for orientation.");
    }

    [Test]
    public void GameplayCamera_DoesNotRenderTheMinimapLayer()
    {
        GameObject mainCamera = GameObject.Find("MainCamera");
        Assert.IsNotNull(mainCamera, "No MainCamera in the scene.");

        Camera cam = mainCamera.GetComponent<Camera>();
        Assert.IsNotNull(cam, "MainCamera has no Camera component.");

        int minimapLayer = LayerMask.NameToLayer("Minimap");
        Assert.AreEqual(
            0,
            cam.cullingMask & (1 << minimapLayer),
            "The gameplay camera should not render the Minimap-layer marker.");
    }
}
