using TMPro;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Rebuilds the gameplay HUD as a designed interface rather than raw
/// coloured rectangles.
///
///   Unity.exe -batchmode -quit -projectPath . ^
///             -executeMethod PremiumHudBuilder.BuildFromCommandLine
///
/// The previous HUD (HudBuilder) was functional but was four untextured
/// Image rects in the top-left corner with 22 px text overlapping itself.
/// This keeps every object NAME and every HUDController field the existing
/// tests bind to - HealthBar, EnergyBar, ShardCountText, EraText,
/// DetectionMeter, PauseMenuPanel - so the behaviour those tests cover is
/// unchanged, and only the presentation is replaced.
///
/// Run AFTER HudBuilder: this expects the pause menu and HUDController
/// wiring to already exist and re-skins what it finds.
/// </summary>
public static class PremiumHudBuilder
{
    private const string UiFolder = "Assets/UI/Generated";

    private static readonly string[] GameplayScenes =
    {
        "Assets/Scenes/MuseumNight.unity",
        "Assets/Scenes/FrozenCity.unity",
        "Assets/Scenes/ClockCore.unity",
    };

    // Palette - one place, so the HUD and the menus cannot drift apart.
    private static readonly Color Ink = new Color(0.92f, 0.95f, 1f, 1f);
    private static readonly Color InkDim = new Color(0.66f, 0.72f, 0.83f, 1f);
    private static readonly Color Health = new Color(0.93f, 0.36f, 0.38f, 1f);
    private static readonly Color Energy = new Color(0.38f, 0.73f, 1f, 1f);
    private static readonly Color Gold = new Color(0.86f, 0.73f, 0.42f, 1f);

    [MenuItem("Museum of Time/Build Premium HUD")]
    public static void BuildMenu() { Build(); }

    public static void BuildFromCommandLine() { Build(); }

    private static void Build()
    {
        ImportUiSprites();

        foreach (string scenePath in GameplayScenes)
        {
            if (AssetDatabase.LoadAssetAtPath<SceneAsset>(scenePath) == null) { continue; }

            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            BuildHudForOpenScene();

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);

            Debug.Log("HUD2 OK: " + scene.name);
        }

        AssetDatabase.SaveAssets();
        Debug.Log("=== PREMIUM HUD COMPLETE ===");
    }

    // ------------------------------------------------------------------
    // Sprite import
    // ------------------------------------------------------------------

    private static void ImportUiSprites()
    {
        // Panels are 9-sliced so one 96 px source stretches to any panel size
        // without the corner radius smearing.
        Slice("Panel_Glass.png", 28);
        Slice("Panel_Solid.png", 28);
        Slice("Panel_Slot.png", 18);
        Slice("Bar_Fill.png", 16);
        Slice("Bar_Track.png", 16);

        foreach (string n in new[]
        {
            "Icon_Health.png", "Icon_Energy.png", "Icon_Shard.png",
            "Icon_Lens.png", "Icon_Hourglass.png", "Icon_Detection.png",
            "Crosshair.png", "Vignette.png",
        })
        {
            Slice(n, 0);
        }
    }

    private static void Slice(string file, int border)
    {
        string path = UiFolder + "/" + file;
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { return; }

        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.alphaIsTransparency = true;
        importer.mipmapEnabled = false;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;

        if (border > 0)
        {
            importer.spriteBorder = new Vector4(border, border, border, border);
        }

        importer.SaveAndReimport();
    }

    private static Sprite Load(string file)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(UiFolder + "/" + file);
    }

    // ------------------------------------------------------------------
    // HUD assembly
    // ------------------------------------------------------------------

    private static void BuildHudForOpenScene()
    {
        GameObject canvasGo = GameObject.Find("HUDCanvas");
        if (canvasGo == null)
        {
            Debug.LogWarning("HUD2: no HUDCanvas in " +
                             SceneManager.GetActiveScene().name + " - skipped.");
            return;
        }

        // A HUD authored at 1920x1080 that scales with the screen. Without
        // this the whole interface is laid out in raw pixels and shrinks into
        // the corner on anything but the authoring resolution.
        var scaler = Ensure<CanvasScaler>(canvasGo);
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        var canvas = Ensure<Canvas>(canvasGo);
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = false;

        Transform root = canvasGo.transform;

        RemoveLegacyHud(canvasGo);

        Image healthFill = BuildStatusPanel(root, out Image energyFill,
                                            out TMP_Text shardText,
                                            out TMP_Text eraText);

        BuildItemSlots(root, out GameObject lensIcon, out GameObject hourglassIcon);
        BuildDetectionMeter(root, out GameObject detectionRoot, out Image detectionFill);
        BuildCrosshair(root);
        BuildPromptLine(root);
        BuildObjectiveBanner(root, out TMP_Text objectiveText, out TMP_Text objectiveHint);
        BuildControlsCard(root);
        BuildMessageFeed(root);
        BuildDeathOverlay(root);
        EnsureObjectiveTracker();
        StyleMinimapFrame(root);
        StylePauseMenu(root);

        // The numbers, built onto the rows the bars already made.
        Transform healthRow = root.Find("StatusPanel/HealthRow");
        Transform energyRow = root.Find("StatusPanel/EnergyRow");

        TMP_Text healthValue = healthRow != null
            ? BuildBarValue(healthRow.gameObject, "Health") : null;

        TMP_Text energyValue = energyRow != null
            ? BuildBarValue(energyRow.gameObject, "Energy") : null;

        WireHudController(healthFill, energyFill, healthValue, energyValue,
                          shardText, eraText,
                          lensIcon, hourglassIcon, detectionRoot, detectionFill,
                          objectiveText, objectiveHint);
    }

    /// <summary>Top-left: health and energy bars with icons, then shards and era.</summary>
    private static Image BuildStatusPanel(Transform root, out Image energyFill,
                                          out TMP_Text shardText, out TMP_Text eraText)
    {
        GameObject panel = Child(root.gameObject, "StatusPanel");
        RectTransform rt = Rect(panel);
        Anchor(rt, new Vector2(0f, 1f), new Vector2(36f, -36f), new Vector2(420f, 190f));

        Image bg = Ensure<Image>(panel);
        bg.sprite = Load("Panel_Glass.png");
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;

        Image healthFill = BuildBar(panel, "Health", "HealthBar", Load("Icon_Health.png"),
                                    Health, new Vector2(0f, -26f));
        energyFill = BuildBar(panel, "Energy", "EnergyBar", Load("Icon_Energy.png"),
                              Energy, new Vector2(0f, -80f));

        // Shard counter, bottom-left of the panel.
        GameObject shardRow = Child(panel, "ShardRow");
        RectTransform srt = Rect(shardRow);
        Anchor(srt, new Vector2(0f, 1f), new Vector2(22f, -132f), new Vector2(190f, 44f));

        GameObject shardIcon = Child(shardRow, "ShardIcon");
        RectTransform sirt = Rect(shardIcon);
        Anchor(sirt, new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(30f, 30f));
        sirt.anchorMin = new Vector2(0f, 0.5f);
        sirt.anchorMax = new Vector2(0f, 0.5f);
        sirt.pivot = new Vector2(0f, 0.5f);
        Image sIcon = Ensure<Image>(shardIcon);
        sIcon.sprite = Load("Icon_Shard.png");
        sIcon.color = Color.white;
        sIcon.raycastTarget = false;

        shardText = Text(shardRow, "ShardCountText", "0", 30f, Ink,
                         TextAlignmentOptions.MidlineLeft);
        RectTransform strt = Rect(shardText.gameObject);
        Anchor(strt, new Vector2(0f, 0.5f), new Vector2(40f, 0f), new Vector2(140f, 40f));
        strt.anchorMin = new Vector2(0f, 0.5f);
        strt.anchorMax = new Vector2(0f, 0.5f);
        strt.pivot = new Vector2(0f, 0.5f);
        shardText.fontStyle = FontStyles.Bold;

        // Era pill, bottom-right of the panel - the signature mechanic gets a
        // readable label of its own rather than sharing a line with the count.
        GameObject eraPill = Child(panel, "EraPill");
        RectTransform ert = Rect(eraPill);
        Anchor(ert, new Vector2(1f, 1f), new Vector2(-22f, -132f), new Vector2(150f, 44f));
        ert.anchorMin = new Vector2(1f, 1f);
        ert.anchorMax = new Vector2(1f, 1f);
        ert.pivot = new Vector2(1f, 0f);
        ert.anchoredPosition = new Vector2(-22f, -176f);

        Image pill = Ensure<Image>(eraPill);
        pill.sprite = Load("Panel_Slot.png");
        pill.type = Image.Type.Sliced;
        pill.color = new Color(1f, 1f, 1f, 0.9f);
        pill.raycastTarget = false;

        eraText = Text(eraPill, "EraText", "Present", 24f, Gold,
                       TextAlignmentOptions.Center);
        RectTransform txt = Rect(eraText.gameObject);
        Stretch(txt);
        eraText.fontStyle = FontStyles.Bold;
        eraText.characterSpacing = 6f;

        return healthFill;
    }

    /// <summary>
    /// One bar: icon, recessed track, gradient fill. "HealthBar"/"EnergyBar"
    /// stay the names of the FILL images, which is what HUDController drives
    /// and what the existing tests read fillAmount from.
    /// </summary>
    /// <summary>
    /// The number beside a bar. A bar at a fifth full is a sliver that reads
    /// as empty, and a player who believes they are at zero and sees nothing
    /// happen concludes the game is broken rather than that they are alive.
    /// </summary>
    private static TMP_Text BuildBarValue(GameObject row, string label)
    {
        TMP_Text value = Text(row, label + "Value", "", 19f, Ink,
                              TextAlignmentOptions.MidlineRight);

        RectTransform vrt = Rect(value.gameObject);
        vrt.anchorMin = new Vector2(1f, 0.5f);
        vrt.anchorMax = new Vector2(1f, 0.5f);
        vrt.pivot = new Vector2(1f, 0.5f);
        vrt.anchoredPosition = new Vector2(-6f, 0f);
        vrt.sizeDelta = new Vector2(120f, 26f);

        return value;
    }

    private static Image BuildBar(GameObject parent, string label, string fillName,
                                  Sprite icon, Color tint, Vector2 offset)
    {
        GameObject row = Child(parent, label + "Row");
        RectTransform rrt = Rect(row);
        Anchor(rrt, new Vector2(0f, 1f), new Vector2(22f, offset.y), new Vector2(376f, 40f));

        GameObject iconGo = Child(row, label + "Icon");
        RectTransform irt = Rect(iconGo);
        irt.anchorMin = new Vector2(0f, 0.5f);
        irt.anchorMax = new Vector2(0f, 0.5f);
        irt.pivot = new Vector2(0f, 0.5f);
        irt.anchoredPosition = new Vector2(0f, 0f);
        irt.sizeDelta = new Vector2(32f, 32f);

        Image ic = Ensure<Image>(iconGo);
        ic.sprite = icon;
        ic.color = Color.white;
        ic.raycastTarget = false;

        GameObject track = Child(row, label + "Track");
        RectTransform trt = Rect(track);
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(1f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.offsetMin = new Vector2(44f, -13f);
        trt.offsetMax = new Vector2(0f, 13f);

        Image trackImg = Ensure<Image>(track);
        trackImg.sprite = Load("Bar_Track.png");
        trackImg.type = Image.Type.Sliced;
        trackImg.color = Color.white;
        trackImg.raycastTarget = false;

        GameObject fill = Child(track, fillName);
        RectTransform frt = Rect(fill);
        frt.anchorMin = Vector2.zero;
        frt.anchorMax = Vector2.one;
        frt.offsetMin = new Vector2(3f, 3f);
        frt.offsetMax = new Vector2(-3f, -3f);

        Image fillImg = Ensure<Image>(fill);
        fillImg.sprite = Load("Bar_Fill.png");
        fillImg.type = Image.Type.Filled;
        fillImg.fillMethod = Image.FillMethod.Horizontal;
        fillImg.fillOrigin = (int)Image.OriginHorizontal.Left;
        fillImg.fillAmount = 1f;
        fillImg.color = tint;
        fillImg.raycastTarget = false;

        return fillImg;
    }

    /// <summary>Top-right: the two acquired items, dark until they are held.</summary>
    private static void BuildItemSlots(Transform root, out GameObject lensIcon,
                                       out GameObject hourglassIcon)
    {
        GameObject bar = Child(root.gameObject, "ItemBar");
        RectTransform brt = Rect(bar);
        brt.anchorMin = new Vector2(1f, 1f);
        brt.anchorMax = new Vector2(1f, 1f);
        brt.pivot = new Vector2(1f, 1f);
        brt.anchoredPosition = new Vector2(-36f, -36f);
        brt.sizeDelta = new Vector2(180f, 84f);

        lensIcon = BuildSlot(bar, "TimeLensIcon", Load("Icon_Lens.png"), new Vector2(-96f, 0f));
        hourglassIcon = BuildSlot(bar, "HourglassIcon", Load("Icon_Hourglass.png"), new Vector2(0f, 0f));
    }

    private static GameObject BuildSlot(GameObject parent, string name, Sprite icon, Vector2 pos)
    {
        // The SLOT is always visible so the player can see there is something
        // to find; the icon inside it is what appears on acquisition. The
        // object HUDController toggles keeps its original name.
        GameObject slot = Child(parent, name + "Slot");
        RectTransform srt = Rect(slot);
        srt.anchorMin = new Vector2(1f, 1f);
        srt.anchorMax = new Vector2(1f, 1f);
        srt.pivot = new Vector2(1f, 1f);
        srt.anchoredPosition = pos;
        srt.sizeDelta = new Vector2(84f, 84f);

        Image bg = Ensure<Image>(slot);
        bg.sprite = Load("Panel_Slot.png");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, 0.85f);
        bg.raycastTarget = false;

        GameObject iconGo = Child(slot, name);
        RectTransform irt = Rect(iconGo);
        Stretch(irt);
        irt.offsetMin = new Vector2(16f, 16f);
        irt.offsetMax = new Vector2(-16f, -16f);

        Image ic = Ensure<Image>(iconGo);
        ic.sprite = icon;
        ic.color = Color.white;
        ic.raycastTarget = false;

        iconGo.SetActive(false);
        return iconGo;
    }

    private static void BuildDetectionMeter(Transform root, out GameObject meterRoot,
                                            out Image fill)
    {
        meterRoot = Child(root.gameObject, "DetectionMeter");
        RectTransform mrt = Rect(meterRoot);
        mrt.anchorMin = new Vector2(0.5f, 1f);
        mrt.anchorMax = new Vector2(0.5f, 1f);
        mrt.pivot = new Vector2(0.5f, 1f);
        mrt.anchoredPosition = new Vector2(0f, -48f);
        mrt.sizeDelta = new Vector2(340f, 56f);

        Image bg = Ensure<Image>(meterRoot);
        bg.sprite = Load("Panel_Glass.png");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 0.85f, 0.85f, 0.95f);
        bg.raycastTarget = false;

        GameObject eye = Child(meterRoot, "DetectionIcon");
        RectTransform ert = Rect(eye);
        ert.anchorMin = new Vector2(0f, 0.5f);
        ert.anchorMax = new Vector2(0f, 0.5f);
        ert.pivot = new Vector2(0f, 0.5f);
        ert.anchoredPosition = new Vector2(16f, 0f);
        ert.sizeDelta = new Vector2(34f, 34f);
        Image eyeImg = Ensure<Image>(eye);
        eyeImg.sprite = Load("Icon_Detection.png");
        eyeImg.raycastTarget = false;

        GameObject track = Child(meterRoot, "DetectionTrack");
        RectTransform trt = Rect(track);
        trt.anchorMin = new Vector2(0f, 0.5f);
        trt.anchorMax = new Vector2(1f, 0.5f);
        trt.pivot = new Vector2(0f, 0.5f);
        trt.offsetMin = new Vector2(60f, -11f);
        trt.offsetMax = new Vector2(-16f, 11f);
        Image trackImg = Ensure<Image>(track);
        trackImg.sprite = Load("Bar_Track.png");
        trackImg.type = Image.Type.Sliced;
        trackImg.raycastTarget = false;

        GameObject fillGo = Child(track, "Fill");
        RectTransform frt = Rect(fillGo);
        Stretch(frt);
        frt.offsetMin = new Vector2(3f, 3f);
        frt.offsetMax = new Vector2(-3f, -3f);

        fill = Ensure<Image>(fillGo);
        fill.sprite = Load("Bar_Fill.png");
        fill.type = Image.Type.Filled;
        fill.fillMethod = Image.FillMethod.Horizontal;
        fill.fillAmount = 0f;
        fill.color = new Color(1f, 0.32f, 0.3f, 1f);
        fill.raycastTarget = false;

        meterRoot.SetActive(false);
    }

    private static void BuildCrosshair(Transform root)
    {
        GameObject cross = Child(root.gameObject, "Crosshair");
        RectTransform crt = Rect(cross);
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0.5f);
        crt.pivot = new Vector2(0.5f, 0.5f);
        crt.anchoredPosition = Vector2.zero;
        crt.sizeDelta = new Vector2(44f, 44f);

        Image img = Ensure<Image>(cross);
        img.sprite = Load("Crosshair.png");
        img.color = new Color(1f, 1f, 1f, 0.62f);
        img.raycastTarget = false;
    }

    /// <summary>
    /// The interaction prompt line, just under the crosshair. Driven at
    /// runtime by HUDController from PlayerInteractor.CurrentPrompt.
    /// </summary>
    private static void BuildPromptLine(Transform root)
    {
        GameObject holder = Child(root.gameObject, "InteractPrompt");
        RectTransform hrt = Rect(holder);
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.anchoredPosition = new Vector2(0f, -60f);
        hrt.sizeDelta = new Vector2(680f, 52f);

        Image bg = Ensure<Image>(holder);
        bg.sprite = Load("Panel_Slot.png");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, 0.8f);
        bg.raycastTarget = false;

        TMP_Text label = Text(holder, "InteractPromptText", "", 26f, Ink,
                              TextAlignmentOptions.Center);
        Stretch(Rect(label.gameObject));

        holder.SetActive(false);
    }

    /// <summary>
    /// Puts a frame behind the minimap so it stops reading as a black hole in
    /// the corner.
    ///
    /// The first version of this looked up the minimap RawImage and copied its
    /// anchors onto a "MinimapFrame" object. MinimapBuilder had ALREADY created
    /// a MinimapFrame as the RawImage's parent, so the lookup found that
    /// parent and copied its own child's stretch anchors (0,0 - 1,1) onto it.
    /// The frame became a full-screen, 84%-opaque dark panel covering the
    /// entire game view: the screen rendered black with a rounded border
    /// around the edge and only the HUD drawn on top.
    ///
    /// So: never touch the rect. Restyle the existing frame if there is one,
    /// and only create a sibling frame when there is not.
    /// </summary>
    /// <summary>
    /// The persistent "what am I doing" line, top-centre under the detection
    /// meter. The game previously answered that question only on world-space
    /// plaques the player had to stand next to.
    /// </summary>
    private static void BuildObjectiveBanner(Transform root, out TMP_Text objective,
                                             out TMP_Text hint)
    {
        GameObject banner = Child(root.gameObject, "ObjectiveBanner");
        RectTransform brt = Rect(banner);
        brt.anchorMin = brt.anchorMax = new Vector2(0.5f, 1f);
        brt.pivot = new Vector2(0.5f, 1f);
        brt.anchoredPosition = new Vector2(0f, -118f);
        brt.sizeDelta = new Vector2(920f, 86f);

        Image bg = Ensure<Image>(banner);
        bg.sprite = Load("Panel_Glass.png");
        bg.type = Image.Type.Sliced;
        bg.color = new Color(1f, 1f, 1f, 0.9f);
        bg.raycastTarget = false;

        objective = Text(banner, "ObjectiveText", "", 30f, Gold,
                         TextAlignmentOptions.Center);
        RectTransform ort = Rect(objective.gameObject);
        ort.anchorMin = new Vector2(0f, 1f);
        ort.anchorMax = new Vector2(1f, 1f);
        ort.pivot = new Vector2(0.5f, 1f);
        ort.offsetMin = new Vector2(16f, 0f);
        ort.offsetMax = new Vector2(-16f, 0f);
        ort.anchoredPosition = new Vector2(0f, -10f);
        ort.sizeDelta = new Vector2(ort.sizeDelta.x, 38f);
        objective.fontStyle = FontStyles.Bold;
        objective.characterSpacing = 4f;

        hint = Text(banner, "ObjectiveHintText", "", 21f, InkDim,
                    TextAlignmentOptions.Center);
        RectTransform hrt = Rect(hint.gameObject);
        hrt.anchorMin = new Vector2(0f, 1f);
        hrt.anchorMax = new Vector2(1f, 1f);
        hrt.pivot = new Vector2(0.5f, 1f);
        hrt.offsetMin = new Vector2(16f, 0f);
        hrt.offsetMax = new Vector2(-16f, 0f);
        hrt.anchoredPosition = new Vector2(0f, -48f);
        hrt.sizeDelta = new Vector2(hrt.sizeDelta.x, 30f);
        hint.textWrappingMode = TextWrappingModes.Normal;
    }

    /// <summary>
    /// A controls card at scene start. The bindings past WASD - Q/R for era
    /// travel, CTRL for slow time, C for the camera - are not guessable, and
    /// nothing showed them unless the player went looking in the pause menu.
    /// </summary>
    private static void BuildControlsCard(Transform root)
    {
        GameObject card = Child(root.gameObject, "ControlsCard");
        RectTransform crt = Rect(card);
        crt.anchorMin = crt.anchorMax = new Vector2(0.5f, 0f);
        crt.pivot = new Vector2(0.5f, 0f);
        crt.anchoredPosition = new Vector2(0f, 60f);
        crt.sizeDelta = new Vector2(940f, 128f);

        Image bg = Ensure<Image>(card);
        bg.sprite = Load("Panel_Glass.png");
        bg.type = Image.Type.Sliced;
        bg.color = Color.white;
        bg.raycastTarget = false;

        TMP_Text title = Text(card, "ControlsCardTitle", "CONTROLS", 20f, Gold,
                              TextAlignmentOptions.Center);
        RectTransform trt = Rect(title.gameObject);
        trt.anchorMin = new Vector2(0f, 1f);
        trt.anchorMax = new Vector2(1f, 1f);
        trt.pivot = new Vector2(0.5f, 1f);
        trt.offsetMin = new Vector2(12f, 0f);
        trt.offsetMax = new Vector2(-12f, 0f);
        trt.anchoredPosition = new Vector2(0f, -10f);
        trt.sizeDelta = new Vector2(trt.sizeDelta.x, 26f);
        title.characterSpacing = 8f;
        title.fontStyle = FontStyles.Bold;

        TMP_Text body = Text(card, "ControlsCardText",
            "WASD  move     MOUSE  look     SHIFT  run     SPACE  jump\n" +
            "E  interact     LEFT MOUSE  throw Chrono Orb     C  camera\n" +
            "Q / R  travel between eras     CTRL  slow time     ESC  pause",
            21f, Ink, TextAlignmentOptions.Center);

        RectTransform lrt = Rect(body.gameObject);
        lrt.anchorMin = new Vector2(0f, 0f);
        lrt.anchorMax = new Vector2(1f, 1f);
        lrt.offsetMin = new Vector2(12f, 10f);
        lrt.offsetMax = new Vector2(-12f, -40f);
        body.textWrappingMode = TextWrappingModes.Normal;
        body.lineSpacing = 14f;

        CanvasGroup group = Ensure<CanvasGroup>(card);
        group.alpha = 1f;
        group.blocksRaycasts = false;
        group.interactable = false;

        var hint = Ensure<ControlsHintCard>(card);
        var so = new SerializedObject(hint);

        SerializedProperty p = so.FindProperty("group");
        if (p != null) { p.objectReferenceValue = group; }

        // Set explicitly rather than relying on the field default: the default
        // is baked in when the component is first added, so changing it in
        // code later would not reach a component that already exists.
        SerializedProperty hold = so.FindProperty("holdSeconds");
        if (hold != null) { hold.floatValue = 15f; }

        SerializedProperty fade = so.FindProperty("fadeSeconds");
        if (fade != null) { fade.floatValue = 1.4f; }

        so.ApplyModifiedPropertiesWithoutUndo();

        card.SetActive(true);
    }

    /// <summary>
    /// Transient event messages, above the status panel on the left.
    ///
    /// Without this a Shadow could steal a Time Shard and dock 60 score in
    /// silence - the number in the corner just went down, which reads as a bug
    /// rather than as something that happened to you.
    /// </summary>
    /// <summary>
    /// The death screen.
    ///
    /// Built last in the HUD so it sits on top of everything, and as a full
    /// stretched panel so it genuinely covers the view - dying should not be
    /// something the player has to notice.
    /// </summary>
    private static void BuildDeathOverlay(Transform root)
    {
        GameObject overlay = Child(root.gameObject, "DeathOverlay");
        RectTransform ort = Rect(overlay);
        Stretch(ort);

        // Last sibling: on top of the bars, the objective and the minimap.
        overlay.transform.SetAsLastSibling();

        var group = Ensure<CanvasGroup>(overlay);
        group.alpha = 0f;
        group.interactable = false;
        group.blocksRaycasts = false;

        GameObject backdropGo = Child(overlay, "DeathBackdrop");
        Stretch(Rect(backdropGo));
        var backdrop = Ensure<Image>(backdropGo);
        backdrop.color = new Color(0.06f, 0.02f, 0.04f, 0.92f);
        backdrop.raycastTarget = false;

        TMP_Text headline = Text(overlay, "DeathHeadline", "YOU DIED", 92f,
                                 new Color(0.90f, 0.24f, 0.26f, 1f),
                                 TextAlignmentOptions.Center);

        RectTransform hrt = Rect(headline.gameObject);
        hrt.anchorMin = hrt.anchorMax = new Vector2(0.5f, 0.5f);
        hrt.pivot = new Vector2(0.5f, 0.5f);
        hrt.anchoredPosition = new Vector2(0f, 60f);
        hrt.sizeDelta = new Vector2(1000f, 130f);

        TMP_Text detail = Text(overlay, "DeathDetail", "", 28f, Ink,
                               TextAlignmentOptions.Center);

        RectTransform drt = Rect(detail.gameObject);
        drt.anchorMin = drt.anchorMax = new Vector2(0.5f, 0.5f);
        drt.pivot = new Vector2(0.5f, 0.5f);
        drt.anchoredPosition = new Vector2(0f, -60f);
        drt.sizeDelta = new Vector2(1000f, 140f);
        detail.textWrappingMode = TextWrappingModes.Normal;

        var component = Ensure<DeathOverlay>(overlay);
        var so = new SerializedObject(component);

        Assign(so, "group", group);
        Assign(so, "backdrop", backdrop);
        Assign(so, "headlineText", headline);
        Assign(so, "detailText", detail);

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void Assign(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; }
    }

    private static void BuildMessageFeed(Transform root)
    {
        GameObject feed = Child(root.gameObject, "MessageFeed");
        RectTransform frt = Rect(feed);
        frt.anchorMin = frt.anchorMax = new Vector2(0f, 1f);
        frt.pivot = new Vector2(0f, 1f);
        frt.anchoredPosition = new Vector2(36f, -240f);
        frt.sizeDelta = new Vector2(620f, 120f);

        TMP_Text label = Text(feed, "MessageFeedText", "", 23f, Ink,
                              TextAlignmentOptions.TopLeft);

        RectTransform lrt = Rect(label.gameObject);
        Stretch(lrt);
        label.textWrappingMode = TextWrappingModes.Normal;
        label.richText = true;
        label.lineSpacing = 10f;

        var component = Ensure<HudMessageFeed>(feed);
        var so = new SerializedObject(component);

        SerializedProperty p = so.FindProperty("label");
        if (p != null) { p.objectReferenceValue = label; }

        so.ApplyModifiedPropertiesWithoutUndo();
    }

    /// <summary>One tracker per gameplay scene, on its own object.</summary>
    private static void EnsureObjectiveTracker()
    {
        var existing = Object.FindFirstObjectByType<ObjectiveTracker>();
        if (existing != null) { return; }

        GameObject go = GameObject.Find("ObjectiveTracker");
        if (go == null) { go = new GameObject("ObjectiveTracker"); }

        if (go.GetComponent<ObjectiveTracker>() == null)
        {
            go.AddComponent<ObjectiveTracker>();
        }
    }

    private static void StyleMinimapFrame(Transform root)
    {
        RawImage raw = null;
        foreach (RawImage r in root.GetComponentsInChildren<RawImage>(true))
        {
            if (r.name.ToLowerInvariant().Contains("minimap")) { raw = r; break; }
        }

        if (raw == null) { return; }

        Transform parent = raw.transform.parent;
        bool parentIsFrame = parent != null &&
                             parent != root &&
                             parent.GetComponent<Image>() != null;

        if (parentIsFrame)
        {
            Image existing = parent.GetComponent<Image>();
            existing.sprite = Load("Panel_Glass.png");
            existing.type = Image.Type.Sliced;
            existing.color = Color.white;
            existing.raycastTarget = false;

            // Enforce the corner layout rather than trusting what is there.
            // An earlier version of this method stretched the frame to full
            // screen, and simply not repeating that mistake would leave the
            // damage in place in every already-saved scene. These are
            // MinimapBuilder's own values.
            var prt = (RectTransform)parent;
            prt.anchorMin = prt.anchorMax = new Vector2(1f, 0f);
            prt.pivot = new Vector2(1f, 0f);
            prt.anchoredPosition = new Vector2(-24f, 24f);
            prt.sizeDelta = new Vector2(190f, 190f);

            // And the display fills the frame with a small inset for the border.
            RectTransform drt = raw.rectTransform;
            drt.anchorMin = Vector2.zero;
            drt.anchorMax = Vector2.one;
            drt.offsetMin = new Vector2(7f, 7f);
            drt.offsetMax = new Vector2(-7f, -7f);

            EditorUtility.SetDirty(parent);
            return;
        }

        GameObject frame = Child(root.gameObject, "MinimapFrame");
        RectTransform frt = Rect(frame);
        RectTransform rrt = raw.rectTransform;

        frt.anchorMin = rrt.anchorMin;
        frt.anchorMax = rrt.anchorMax;
        frt.pivot = rrt.pivot;
        frt.anchoredPosition = rrt.anchoredPosition;
        frt.offsetMin = rrt.offsetMin - new Vector2(8f, 8f);
        frt.offsetMax = rrt.offsetMax + new Vector2(8f, 8f);

        Image img = Ensure<Image>(frame);
        img.sprite = Load("Panel_Glass.png");
        img.type = Image.Type.Sliced;
        img.color = Color.white;
        img.raycastTarget = false;

        frame.transform.SetSiblingIndex(raw.transform.GetSiblingIndex());
    }

    /// <summary>
    /// HudBuilder lays out a flat, untextured version of the same elements
    /// directly on the canvas root, and it runs before this does. Left in
    /// place, those become a second set of bars and labels drawn underneath
    /// the designed ones - visible in the corner as a stray red bar and an
    /// "0"/"Present" pair overlapping each other.
    ///
    /// Only direct children of the canvas root are removed, so the new
    /// hierarchy (which nests the same names inside StatusPanel and ItemBar)
    /// is untouched. This runs BEFORE the new HUD is built and rewired, so
    /// nothing that survives is left pointing at a destroyed object.
    /// </summary>
    private static void RemoveLegacyHud(GameObject canvasGo)
    {
        string[] legacy =
        {
            "HealthBar", "EnergyBar", "ShardCountText", "EraText",
            "TimeLensIcon", "HourglassIcon",
        };

        foreach (string name in legacy)
        {
            Transform t = canvasGo.transform.Find(name);
            if (t != null)
            {
                Object.DestroyImmediate(t.gameObject);
            }
        }

        // The old detection meter put its fill directly on the meter root.
        Transform meter = canvasGo.transform.Find("DetectionMeter");
        if (meter != null)
        {
            Transform strayFill = meter.Find("Fill");
            if (strayFill != null)
            {
                Object.DestroyImmediate(strayFill.gameObject);
            }
        }
    }

    private static void StylePauseMenu(Transform root)
    {
        foreach (string n in new[] { "PauseMenuPanel", "PauseControlsPanel" })
        {
            Transform t = FindDeep(root, n);
            if (t == null) { continue; }

            Image img = t.GetComponent<Image>();
            if (img == null) { continue; }

            img.sprite = Load("Panel_Solid.png");
            img.type = Image.Type.Sliced;
            img.color = Color.white;
        }
    }

    private static void WireHudController(Image healthFill, Image energyFill,
                                          TMP_Text healthValue, TMP_Text energyValue,
                                          TMP_Text shardText, TMP_Text eraText,
                                          GameObject lensIcon, GameObject hourglassIcon,
                                          GameObject detectionRoot, Image detectionFill,
                                          TMP_Text objectiveText, TMP_Text objectiveHint)
    {
        var hud = Object.FindFirstObjectByType<HUDController>();
        if (hud == null)
        {
            Debug.LogWarning("HUD2: no HUDController in " +
                             SceneManager.GetActiveScene().name + ".");
            return;
        }

        var so = new SerializedObject(hud);
        Set(so, "healthFill", healthFill);
        Set(so, "energyFill", energyFill);
        Set(so, "healthValueText", healthValue);
        Set(so, "energyValueText", energyValue);
        Set(so, "shardText", shardText);
        Set(so, "eraText", eraText);
        Set(so, "timeLensIcon", lensIcon);
        Set(so, "hourglassIcon", hourglassIcon);
        Set(so, "detectionMeterRoot", detectionRoot);
        Set(so, "detectionFill", detectionFill);
        Set(so, "objectiveText", objectiveText);
        Set(so, "objectiveHintText", objectiveHint);

        Transform prompt = FindDeep(hud.transform.root, "InteractPrompt");
        if (prompt == null)
        {
            GameObject canvasGo = GameObject.Find("HUDCanvas");
            if (canvasGo != null) { prompt = FindDeep(canvasGo.transform, "InteractPrompt"); }
        }

        if (prompt != null)
        {
            Set(so, "interactPromptRoot", prompt.gameObject);
            Set(so, "interactPromptText", prompt.GetComponentInChildren<TMP_Text>(true));
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(hud);
    }

    private static void Set(SerializedObject so, string field, Object value)
    {
        SerializedProperty p = so.FindProperty(field);
        if (p != null) { p.objectReferenceValue = value; }
    }

    // ------------------------------------------------------------------
    // Small helpers
    // ------------------------------------------------------------------

    private static GameObject Child(GameObject parent, string name)
    {
        Transform t = parent.transform.Find(name);
        if (t != null) { return t.gameObject; }

        var go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent.transform, false);
        return go;
    }

    private static Transform FindDeep(Transform root, string name)
    {
        if (root.name == name) { return root; }

        foreach (Transform child in root)
        {
            Transform found = FindDeep(child, name);
            if (found != null) { return found; }
        }

        return null;
    }

    private static T Ensure<T>(GameObject go) where T : Component
    {
        T c = go.GetComponent<T>();
        return c != null ? c : go.AddComponent<T>();
    }

    private static RectTransform Rect(GameObject go)
    {
        var rt = go.GetComponent<RectTransform>();
        if (rt == null)
        {
            GameObject tmp = go;
            rt = tmp.AddComponent<RectTransform>();
        }
        return rt;
    }

    private static void Anchor(RectTransform rt, Vector2 anchor, Vector2 pos, Vector2 size)
    {
        rt.anchorMin = rt.anchorMax = anchor;
        rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
    }

    private static void Stretch(RectTransform rt)
    {
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;
    }

    private static TMP_Text Text(GameObject parent, string name, string content,
                                 float size, Color color, TextAlignmentOptions align)
    {
        GameObject go = Child(parent, name);
        var tmp = Ensure<TextMeshProUGUI>(go);
        tmp.text = content;
        tmp.fontSize = size;
        tmp.color = color;
        tmp.alignment = align;
        tmp.raycastTarget = false;
        tmp.textWrappingMode = TextWrappingModes.NoWrap;
        tmp.overflowMode = TextOverflowModes.Overflow;
        return tmp;
    }
}
