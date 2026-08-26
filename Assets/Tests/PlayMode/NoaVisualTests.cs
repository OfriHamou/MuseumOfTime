using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Verifies the Noa character integration is functionally correct in all
/// three gameplay scenes: the player has a visible skinned body, and the
/// Humanoid Avatar actually BINDS to the existing Animator (the one real
/// risk of keeping the Animator on the player root with the model as a
/// child - if binding failed, GetBoneTransform would return null and Noa
/// would be stuck in bind pose). No look/quality is asserted - that is
/// manual.
/// </summary>
public sealed class NoaVisualTests
{
    private static IEnumerator CheckScene(string scene)
    {
        SceneManager.LoadScene(scene, LoadSceneMode.Single);
        yield return null;
        yield return null;   // let the Animator initialise and bind

        GameObject player = GameObject.Find("Player");
        Assert.IsNotNull(player, "No 'Player' in " + scene + ".");

        var smrs = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        Assert.Greater(smrs.Length, 0, scene + ": the player has no visible skinned mesh (Noa model missing).");

        var animator = player.GetComponent<Animator>();
        Assert.IsNotNull(animator, scene + ": player has no Animator.");
        Assert.IsTrue(animator.isHuman, scene + ": player Animator is not Humanoid (no Avatar assigned?).");
        Assert.IsNotNull(
            animator.GetBoneTransform(HumanBodyBones.Hips),
            scene + ": Humanoid Avatar did not bind to the skeleton (Noa would be stuck in bind pose).");

        Assert.IsFalse(animator.applyRootMotion, scene + ": Apply Root Motion should stay off.");
        Assert.AreEqual("NoaController", animator.runtimeAnimatorController.name, scene + ": NoaController must be kept.");
    }

    [UnityTest]
    public IEnumerator Noa_IsVisibleAndBound_InMuseumNight()
    {
        yield return CheckScene("MuseumNight");
    }

    [UnityTest]
    public IEnumerator Noa_IsVisibleAndBound_InFrozenCity()
    {
        yield return CheckScene("FrozenCity");
    }

    [UnityTest]
    public IEnumerator Noa_IsVisibleAndBound_InClockCore()
    {
        yield return CheckScene("ClockCore");
    }
}
