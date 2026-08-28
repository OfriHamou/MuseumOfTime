using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// Nothing in the game may render in Unity's magenta error shader.
///
/// This exists because two of the graded requirement props shipped bright pink
/// at the foot of the clock tower and nothing complained. The LOD prefabs
/// (T11) and fracture shards (T10) are built by AssetPrefabBuilder, which
/// loaded MuseumMarble.mat by path - but MuseumBuilder is what CREATES that
/// material, and it runs four steps later in the rebuild. On a clean rebuild
/// the load returned null, every tier was written with an empty material slot,
/// and Unity drew them all in the error shader.
///
/// A null material is not an error to Unity. It logs nothing, throws nothing,
/// and every other test passed. The only symptom is visual, which is exactly
/// the kind of defect a test suite is supposed to catch on the author's behalf
/// rather than the player's.
/// </summary>
public sealed class MaterialIntegrityTests
{
    private static readonly string[] Scenes =
    {
        "MainMenu", "MuseumNight", "FrozenCity", "ClockCore", "Victory",
    };

    private static string PathOf(Transform t)
    {
        string path = t.name;

        for (Transform p = t.parent; p != null; p = p.parent)
        {
            path = p.name + "/" + path;
        }

        return path;
    }

    [UnityTest]
    public IEnumerator NoRendererAnywhereDrawsInTheErrorShader()
    {
        var broken = new List<string>();

        foreach (string sceneName in Scenes)
        {
            SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
            yield return null;
            yield return null;

            Renderer[] renderers = Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            foreach (Renderer renderer in renderers)
            {
                // Particle trails and the like legitimately have none.
                if (renderer is LineRenderer || renderer is TrailRenderer)
                {
                    continue;
                }

                Material[] materials = renderer.sharedMaterials;

                for (int slot = 0; slot < materials.Length; slot++)
                {
                    Material material = materials[slot];

                    if (material == null)
                    {
                        broken.Add(sceneName + ": " + PathOf(renderer.transform) +
                                   " slot " + slot + " has NO material");
                        continue;
                    }

                    if (material.shader == null)
                    {
                        broken.Add(sceneName + ": " + PathOf(renderer.transform) +
                                   " slot " + slot + " material '" + material.name +
                                   "' has no shader");
                        continue;
                    }

                    if (material.shader.name == "Hidden/InternalErrorShader")
                    {
                        broken.Add(sceneName + ": " + PathOf(renderer.transform) +
                                   " slot " + slot + " is on the error shader");
                    }
                }
            }
        }

        Assert.IsEmpty(
            broken,
            "These renderers draw in the magenta error shader:\n  " +
            string.Join("\n  ", broken));
    }
}
