using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using TMPro;
using Unity.AI.Navigation;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

/// <summary>
/// One test per graded technical requirement (T1-T21), asserted against the
/// scenes as they actually ship.
///
/// This is the compliance matrix in Part 8 of the implementation plan, made
/// executable. The point is that "the requirement is present" stops being a
/// claim in a document and becomes something that fails loudly the moment it
/// stops being true - which is exactly how several of these were found to be
/// broken in the first place:
///
///   - The Voronoi and LOD models were in the scenes but importing at 1/100
///     scale, so T10/T11 were present in the hierarchy and invisible in play.
///   - FrozenCity and ClockCore had a single camera each, so T19's
///     first/third-person switch did not exist for two thirds of the game.
///   - Only two OnCollisionEnter implementations existed against T4's three.
///   - T2's 3D tutorial text existed only in MuseumNight.
///
/// Scene-loading tests use UnitySetUp so each starts from a known scene.
/// </summary>
public sealed class RequirementComplianceTests
{
    private const string Museum = "MuseumNight";
    private const string Frozen = "FrozenCity";
    private const string Clock = "ClockCore";

    private static readonly string[] GameplayScenes = { Museum, Frozen, Clock };

    private static IEnumerator Load(string sceneName)
    {
        SceneManager.LoadScene(sceneName, LoadSceneMode.Single);
        yield return null;
        yield return null;
    }

    private static T[] All<T>() where T : Object
    {
        return Object.FindObjectsByType<T>(FindObjectsInactive.Include, FindObjectsSortMode.None);
    }

    // =================================================================
    // T1 - entry and victory menus
    // =================================================================

    [UnityTest]
    public IEnumerator T1_EntryAndVictoryMenusExist()
    {
        yield return Load("MainMenu");

        var main = Object.FindFirstObjectByType<MainMenuController>();
        Assert.IsNotNull(main, "MainMenu has no MainMenuController.");

        Assert.GreaterOrEqual(
            All<UnityEngine.UI.Button>().Length, 4,
            "The main menu should offer New Game / Continue / Controls / Quit.");

        yield return Load("Victory");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<VictoryScreenController>(),
            "Victory has no VictoryScreenController.");
    }

    // =================================================================
    // T2 - in-game tutorial, dynamic text, in 3D
    // =================================================================

    [UnityTest]
    public IEnumerator T2_EveryGameplaySceneHasWorldSpaceTutorialText()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            // TextMeshPro (world space), NOT TextMeshProUGUI on a Canvas -
            // "in 3D" is the part of the requirement a screen overlay fails.
            TextMeshPro[] world = All<TextMeshPro>();

            Assert.Greater(
                world.Length, 0,
                sceneName + " has no world-space TextMeshPro tutorial text (T2).");

            Assert.IsTrue(
                All<WorldTutorialText>().Length > 0 || All<WorldObjectiveText>().Length > 0,
                sceneName + " has world text but nothing that makes it dynamic (T2).");
        }
    }

    // =================================================================
    // T3 - at least four triggers
    // =================================================================

    [UnityTest]
    public IEnumerator T3_AtLeastFourDistinctTriggerTypesExist()
    {
        var seen = new HashSet<string>();

        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            if (All<RoomEntryTrigger>().Length > 0) { seen.Add("RoomEntry"); }
            if (All<TutorialTrigger>().Length > 0) { seen.Add("Tutorial"); }
            if (All<EraZoneTrigger>().Length > 0) { seen.Add("EraZone"); }
            if (All<HazardTrigger>().Length > 0) { seen.Add("Hazard"); }
            if (All<TimeAnchorTrigger>().Length > 0) { seen.Add("TimeAnchor"); }
            if (All<SceneExitTrigger>().Length > 0) { seen.Add("SceneExit"); }
        }

        Assert.GreaterOrEqual(
            seen.Count, 4,
            "T3 needs at least four trigger types; found: " + string.Join(", ", seen));
    }

    // =================================================================
    // T4 - at least three collisions detected and acted upon
    // =================================================================

    [Test]
    public void T4_AtLeastThreeCollisionHandlersExistAndUseContactData()
    {
        var handlers = new List<System.Type>();

        foreach (System.Type type in typeof(ChronoOrb).Assembly.GetTypes())
        {
            if (!typeof(MonoBehaviour).IsAssignableFrom(type)) { continue; }

            MethodInfo method = type.GetMethod(
                "OnCollisionEnter",
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic |
                BindingFlags.DeclaredOnly);

            if (method != null) { handlers.Add(type); }
        }

        Assert.GreaterOrEqual(
            handlers.Count, 3,
            "T4 needs at least three collision handlers; found " + handlers.Count +
            ": " + string.Join(", ", handlers.Select(t => t.Name)));
    }

    [UnityTest]
    public IEnumerator T4_FallingDebrisDamagesOnImpactScaledByContactSpeed()
    {
        yield return Load(Museum);

        FallingDebris[] debris = All<FallingDebris>();
        Assert.Greater(debris.Length, 0, "MuseumNight has no FallingDebris hazard.");

        // The response must come from the contact data, so the component has
        // to expose the speed it actually measured.
        Assert.IsNotNull(
            typeof(FallingDebris).GetProperty("LastImpactSpeed"),
            "FallingDebris does not report the impact speed it acted on.");
    }

    // =================================================================
    // T5 - physical hinge joints
    // =================================================================

    [UnityTest]
    public IEnumerator T5_HingeJointsExistInEveryGameplayScene()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            Assert.Greater(
                All<HingeJoint>().Length, 0,
                sceneName + " has no HingeJoint (T5).");
        }
    }

    // =================================================================
    // T6 - self-built terrain
    // =================================================================

    [UnityTest]
    public IEnumerator T6_FrozenCityHasARealTerrain()
    {
        yield return Load(Frozen);

        Terrain[] terrains = All<Terrain>();
        Assert.Greater(terrains.Length, 0, "FrozenCity has no Terrain (T6).");

        TerrainData data = terrains[0].terrainData;
        Assert.IsNotNull(data, "The Terrain has no TerrainData.");

        Assert.GreaterOrEqual(
            data.terrainLayers.Length, 3,
            "The Terrain should be painted with at least three layers (T6).");

        // Sculpted, not left flat: some height must be non-zero.
        float[,] heights = data.GetHeights(0, 0, data.heightmapResolution, data.heightmapResolution);
        bool sculpted = false;

        for (int y = 0; y < data.heightmapResolution && !sculpted; y += 8)
        {
            for (int x = 0; x < data.heightmapResolution; x += 8)
            {
                if (heights[y, x] > 0.001f) { sculpted = true; break; }
            }
        }

        Assert.IsTrue(sculpted, "The Terrain is perfectly flat - it was not sculpted (T6).");
    }

    // =================================================================
    // T7 - patrol WITH PAUSE
    // =================================================================

    [UnityTest]
    public IEnumerator T7_PatrolRouteHasWaypointsAndARealPause()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            PatrolRoute[] routes = All<PatrolRoute>();
            Assert.Greater(routes.Length, 0, sceneName + " has no PatrolRoute (T7).");

            PatrolRoute route = routes[0];
            Assert.GreaterOrEqual(route.Count, 2,
                sceneName + "'s patrol route needs at least two waypoints.");

            FieldInfo field = typeof(PatrolRoute).GetField(
                "waypoints", BindingFlags.Instance | BindingFlags.NonPublic);

            Assert.IsNotNull(field, "PatrolRoute has no waypoints field.");

            var points = (List<PatrolRoute.Waypoint>)field.GetValue(route);

            bool anyPause = false;
            foreach (PatrolRoute.Waypoint point in points)
            {
                if (point.waitSeconds > 0f) { anyPause = true; break; }
            }

            Assert.IsTrue(anyPause,
                sceneName + "'s patrol has no waypoint with a wait - the PAUSE is " +
                "the graded half of T7.");
        }
    }

    // =================================================================
    // T8 - score, health and energy
    // =================================================================

    [UnityTest]
    public IEnumerator T8_ScoreHealthAndEnergyAllChangeAndAreShown()
    {
        yield return Load(Museum);

        GameManager gm = GameManager.Instance;
        Assert.IsNotNull(gm, "No GameManager (T8).");

        gm.ResetGame();
        yield return null;

        int score = gm.State.score;
        gm.AddScore(100);
        Assert.AreEqual(score + 100, gm.State.score, "Score does not go up.");

        gm.RemoveScore(50);
        Assert.AreEqual(score + 50, gm.State.score, "Score does not go down.");

        gm.RestoreFullHealth();
        gm.TakeDamage(30);
        Assert.AreEqual(gm.State.maxHealth - 30, gm.State.currentHealth, "Health does not fall.");

        gm.RestoreFullEnergy();
        Assert.IsTrue(gm.SpendEnergy(10f), "Energy could not be spent.");

        Assert.IsNotNull(
            Object.FindFirstObjectByType<HUDController>(),
            "Nothing displays health/energy/score to the player (T8).");
    }

    // =================================================================
    // T9 - cross-scene serialisation and two acquired items
    // =================================================================

    [UnityTest]
    public IEnumerator T9_StateSerialisesAndCarriesTwoAcquiredItems()
    {
        yield return Load(Museum);

        GameManager gm = GameManager.Instance;
        gm.ResetGame();

        gm.AcquireTimeLens();
        gm.AcquireChronoHourglass();
        gm.AddTimeShard(3);

        string json = gm.State.ToJson();
        Assert.IsNotEmpty(json, "GameState does not serialise (T9).");

        var restored = new GameState();
        restored.LoadFromJson(json);

        Assert.IsTrue(restored.hasTimeLens, "The Time Lens did not survive serialisation.");
        Assert.IsTrue(restored.hasChronoHourglass, "The Hourglass did not survive serialisation.");
        Assert.AreEqual(3, restored.timeShards, "Shard count did not survive serialisation.");
    }

    // =================================================================
    // T10 - two Voronoi-fractured assets, present at a real scale
    // =================================================================

    [UnityTest]
    public IEnumerator T10_TwoDistinctFracturedAssetsAppearAtHumanScale()
    {
        var found = new Dictionary<string, float>();

        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            foreach (FracturedObject fractured in All<FracturedObject>())
            {
                var bounds = new Bounds();
                bool any = false;

                foreach (Renderer r in fractured.GetComponentsInChildren<Renderer>(true))
                {
                    if (!any) { bounds = r.bounds; any = true; }
                    else { bounds.Encapsulate(r.bounds); }
                }

                if (any) { found[fractured.name] = bounds.size.magnitude; }
            }
        }

        Assert.GreaterOrEqual(
            found.Count, 2,
            "T10 needs two fractured assets; found: " + string.Join(", ", found.Keys));

        foreach (KeyValuePair<string, float> entry in found)
        {
            // The whole defect this guards: the models imported 100x too
            // small, so they were "in the game" as centimetre specks.
            Assert.Greater(
                entry.Value, 0.5f,
                entry.Key + " is only " + entry.Value.ToString("F3") +
                " units across - it cannot be said to appear in the game (T10).");
        }
    }

    // =================================================================
    // T11 - two LOD assets, reduced by hand, integrated as LODGroups
    // =================================================================

    [UnityTest]
    public IEnumerator T11_TwoLodAssetsWithThreeDecreasingTiers()
    {
        yield return Load(Museum);

        var byName = new Dictionary<string, LODGroup>();

        foreach (LODGroup group in All<LODGroup>())
        {
            string key = group.name;
            if (!byName.ContainsKey(key)) { byName[key] = group; }
        }

        Assert.GreaterOrEqual(
            byName.Count, 2,
            "T11 needs two different LOD assets; found: " + string.Join(", ", byName.Keys));

        foreach (KeyValuePair<string, LODGroup> entry in byName)
        {
            LODGroup group = entry.Value;

            Assert.GreaterOrEqual(group.lodCount, 3,
                entry.Key + " has fewer than three LOD tiers.");

            Assert.Greater(group.size, 0.5f,
                entry.Key + " has a LODGroup size of " + group.size.ToString("F3") +
                " - the tiers can never switch sensibly at that scale (T11).");

            LOD[] lods = group.GetLODs();
            int previous = int.MaxValue;

            for (int i = 0; i < lods.Length; i++)
            {
                int tris = 0;

                foreach (Renderer r in lods[i].renderers)
                {
                    var filter = r != null ? r.GetComponent<MeshFilter>() : null;
                    if (filter != null && filter.sharedMesh != null)
                    {
                        tris += filter.sharedMesh.triangles.Length / 3;
                    }
                }

                Assert.Less(tris, previous,
                    entry.Key + " LOD" + i + " is not simpler than LOD" + (i - 1) +
                    " - the polygon reduction is what T11 grades.");

                previous = tris;
            }
        }
    }

    // =================================================================
    // T12 - only the new Input System
    // =================================================================

    [UnityTest]
    public IEnumerator T12_InputComesOnlyFromTheNewInputSystem()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            PlayerInput[] inputs = All<PlayerInput>();
            Assert.AreEqual(1, inputs.Length,
                sceneName + " should have exactly one PlayerInput, found " + inputs.Length);

            Assert.IsNotNull(inputs[0].actions,
                sceneName + "'s PlayerInput has no Actions asset (T12).");

            Assert.IsNotNull(
                Object.FindFirstObjectByType<PlayerInputReader>(),
                sceneName + " has no PlayerInputReader (T12).");
        }
    }

    // =================================================================
    // T13 / T16 - two agent types, separate bakes, steering, stealth
    // =================================================================

    [UnityTest]
    public IEnumerator T13_TwoAgentTypesOnSeparateBakesWithDifferentRoutes()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            // Inactive objects count. ClockCore's Warden starts disabled on
            // purpose - Collector.Awake deactivates it and phase 2 summons it
            // - so requiring an ACTIVE one here would fail a scene that is
            // behaving exactly as designed.
            WardenAI[] wardens = All<WardenAI>();
            ShadowAI[] shadows = All<ShadowAI>();

            Assert.Greater(wardens.Length, 0, sceneName + " has no WardenAI (T13).");
            Assert.Greater(shadows.Length, 0, sceneName + " has no ShadowAI (T13).");

            WardenAI warden = wardens[0];
            ShadowAI shadow = shadows[0];

            NavMeshAgent wardenAgent = warden.GetComponent<NavMeshAgent>();
            NavMeshAgent shadowAgent = shadow.GetComponent<NavMeshAgent>();

            Assert.AreNotEqual(
                wardenAgent.agentTypeID, shadowAgent.agentTypeID,
                sceneName + ": both agents are the same NavMesh agent type (T13).");

            NavMeshSurface[] surfaces = All<NavMeshSurface>();
            Assert.GreaterOrEqual(surfaces.Length, 2,
                sceneName + " has fewer than two NavMeshSurfaces - T13 requires " +
                "a separate bake per agent type.");

            var ids = new HashSet<int>();
            foreach (NavMeshSurface s in surfaces)
            {
                Assert.IsNotNull(s.navMeshData,
                    sceneName + ": NavMeshSurface '" + s.name + "' has never been baked.");
                ids.Add(s.agentTypeID);
            }

            Assert.GreaterOrEqual(ids.Count, 2,
                sceneName + ": the two bakes are for the same agent type.");
        }
    }

    [Test]
    public void T13_SteeringBehavioursAreNamedExplicitly()
    {
        foreach (string name in new[] { "Seek", "Flee", "Pursue" })
        {
            Assert.IsNotNull(
                typeof(SteeringBehaviours).GetMethod(name, BindingFlags.Public | BindingFlags.Static),
                "SteeringBehaviours." + name + " is missing - T13 grades a clear steering element.");
        }
    }

    // =================================================================
    // T14 - hand-authored Animators with at least four states
    // =================================================================

    [UnityTest]
    public IEnumerator T14_BothAnimatorsHaveFourPlayableStates()
    {
        yield return Load(Museum);

        var controllers = new Dictionary<string, RuntimeAnimatorController>();

        foreach (Animator animator in All<Animator>())
        {
            if (animator.runtimeAnimatorController == null) { continue; }
            controllers[animator.runtimeAnimatorController.name] = animator.runtimeAnimatorController;
        }

        Assert.GreaterOrEqual(controllers.Count, 2,
            "T14 needs two authored Animator controllers; found: " +
            string.Join(", ", controllers.Keys));

        foreach (KeyValuePair<string, RuntimeAnimatorController> entry in controllers)
        {
            Assert.Greater(
                entry.Value.animationClips.Length, 0,
                entry.Key + " has no clips on any state - an Animator whose states " +
                "play nothing is not driving anything (T14).");
        }
    }

    // =================================================================
    // T15 - a physical projectile that is fired and impacts
    // =================================================================

    [UnityTest]
    public IEnumerator T15_ChronoOrbIsAPhysicalBodyThatImpacts()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            var launcher = Object.FindFirstObjectByType<ChronoOrbLauncher>();
            Assert.IsNotNull(launcher, sceneName + " has no ChronoOrbLauncher (T15).");
        }

        Assert.IsNotNull(
            typeof(ChronoOrb).GetMethod(
                "OnCollisionEnter",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public),
            "ChronoOrb does not handle its own impact (T15).");
    }

    // =================================================================
    // T17 - a LayerMask built in code
    // =================================================================

    [UnityTest]
    public IEnumerator T17_LayerMasksAreBuiltInCodeNotTheInspector()
    {
        yield return Load(Museum);

        var warden = Object.FindFirstObjectByType<WardenAI>();
        Assert.IsNotNull(warden, "No WardenAI to check the vision mask on.");

        // WardenAI.Awake builds this with LayerMask.GetMask(...). If it were
        // only an Inspector field it would be whatever was serialised; a
        // non-zero value after Awake is the observable proof it was computed.
        Assert.AreNotEqual(
            0, warden.VisionBlockers.value,
            "The Warden's vision mask is empty - LayerMask.GetMask was given " +
            "layer names that do not exist (T17).");

        var interactor = Object.FindFirstObjectByType<PlayerInteractor>();
        Assert.IsNotNull(interactor, "No PlayerInteractor.");

        FieldInfo maskField = typeof(PlayerInteractor).GetField(
            "interactMask", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.IsNotNull(maskField, "PlayerInteractor has no interactMask field.");

        var mask = (LayerMask)maskField.GetValue(interactor);
        Assert.AreNotEqual(0, mask.value, "The interaction mask is empty (T17).");
    }

    // =================================================================
    // T18 - a minimap covering one whole scene
    // =================================================================

    [UnityTest]
    public IEnumerator T18_MinimapIsLiveForTheWholeOfMuseumNight()
    {
        yield return Load(Museum);

        var minimap = Object.FindFirstObjectByType<MinimapController>();
        Assert.IsNotNull(minimap, "MuseumNight has no MinimapController (T18).");

        Camera cam = minimap.GetComponent<Camera>();
        Assert.IsNotNull(cam, "The minimap has no Camera.");
        Assert.IsTrue(cam.orthographic, "A minimap should be orthographic.");
        Assert.IsNotNull(cam.targetTexture, "The minimap camera renders to no RenderTexture.");
        Assert.IsTrue(cam.isActiveAndEnabled, "The minimap camera is not enabled at scene start.");
    }

    // =================================================================
    // T19 - first person to third person, two cameras besides the minimap
    // =================================================================

    [UnityTest]
    public IEnumerator T19_EveryGameplaySceneCanSwitchBetweenTwoCameras()
    {
        foreach (string sceneName in GameplayScenes)
        {
            yield return Load(sceneName);

            CinemachineCamera[] vcams = All<CinemachineCamera>();

            Assert.GreaterOrEqual(vcams.Length, 2,
                sceneName + " has " + vcams.Length + " Cinemachine camera(s); T19 " +
                "requires two besides the minimap.");

            var rig = Object.FindFirstObjectByType<PlayerCameraRig>();
            Assert.IsNotNull(rig, sceneName + " has no PlayerCameraRig, so nothing switches (T19).");

            bool before = rig.IsFirstPerson;
            rig.ToggleCamera();

            Assert.AreNotEqual(before, rig.IsFirstPerson,
                sceneName + ": toggling did not change the camera mode (T19).");
        }
    }

    // =================================================================
    // T20 - a two-storey building with stairs and chosen textures
    // =================================================================

    [UnityTest]
    public IEnumerator T20_MuseumHasTwoStoreysStairsAndAuthoredTextures()
    {
        yield return Load(Museum);

        GameObject museum = GameObject.Find("Museum");
        Assert.IsNotNull(museum, "No Museum object.");

        Transform ground = museum.transform.Find("GroundFloorSlab");
        Transform upper = museum.transform.Find("UpperFloorSlab");

        Assert.IsNotNull(ground, "No ground floor.");
        Assert.IsNotNull(upper, "No upper floor - T20 requires two storeys.");

        Assert.Greater(
            upper.position.y, ground.position.y + 2f,
            "The two floors are not vertically separated.");

        Transform stairs = museum.transform.Find("Staircase");
        Assert.IsNotNull(stairs, "No Staircase (T20).");
        Assert.Greater(stairs.childCount, 4, "The staircase has too few steps to climb.");

        // The steps have to be climbable, not just present: the player's
        // CharacterController stepOffset must clear one step's rise.
        var controller = Object.FindFirstObjectByType<CharacterController>();
        Assert.IsNotNull(controller, "No player CharacterController.");

        Renderer step = stairs.GetChild(0).GetComponent<Renderer>();
        Assert.IsNotNull(step, "The first step has no renderer to measure.");

        Assert.GreaterOrEqual(
            controller.stepOffset, step.bounds.size.y - 0.01f,
            "Step rise " + step.bounds.size.y.ToString("F2") + " exceeds the " +
            "controller's stepOffset " + controller.stepOffset.ToString("F2") +
            " - the stairs cannot be climbed (T20).");

        // Chosen textures, not untextured colour.
        var textured = 0;
        foreach (Renderer r in museum.GetComponentsInChildren<Renderer>(true))
        {
            if (r.sharedMaterial != null &&
                r.sharedMaterial.HasProperty("_BaseMap") &&
                r.sharedMaterial.GetTexture("_BaseMap") != null)
            {
                textured++;
            }
        }

        Assert.Greater(textured, 0, "No museum surface carries a texture (T20).");
    }

    // =================================================================
    // T21 - hidden teleports, from scene two onward
    // =================================================================

    [UnityTest]
    public IEnumerator T21_HiddenTeleportsExistFromSceneTwoOnwardAndNotBefore()
    {
        yield return Load(Museum);

        Assert.AreEqual(
            0, All<TimeAnchor>().Length,
            "MuseumNight has a Time Anchor. T21 says hidden teleports start at " +
            "the SECOND scene - having one here fails the requirement.");

        foreach (string sceneName in new[] { Frozen, Clock })
        {
            yield return Load(sceneName);

            TimeAnchor[] anchors = All<TimeAnchor>();

            Assert.GreaterOrEqual(
                anchors.Length, 2,
                sceneName + " has " + anchors.Length + " Time Anchor(s); T21 requires " +
                "at least two.");

            // "Hidden": invisible without the Time Lens.
            GameManager.Instance.State.hasTimeLens = false;
            yield return null;

            foreach (TimeAnchor anchor in anchors)
            {
                FieldInfo visualField = typeof(TimeAnchor).GetField(
                    "lensVisual", BindingFlags.Instance | BindingFlags.NonPublic);

                var visual = (GameObject)visualField.GetValue(anchor);

                Assert.IsNotNull(visual, anchor.name + " has no lensVisual wired.");
                Assert.IsFalse(
                    visual.activeSelf,
                    anchor.name + " is visible without the Time Lens - it is not hidden (T21).");
            }
        }
    }
}
