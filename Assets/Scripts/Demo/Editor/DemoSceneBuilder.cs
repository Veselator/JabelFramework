using System.Collections.Generic;
using System.Linq;
using Jabel.Core;
using Jabel.Editor;
using Jabel.Events;
using Jabel.Localization;
using Jabel.UI;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEditor.U2D.Sprites;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace OneKMonkeys.Editor
{
    /// <summary>Builds the complete "1000 Monkeys" demo scene from the sprite sheet and Jabel data.</summary>
    public static class DemoSceneBuilder
    {
        public const string SpriteSheet = "Assets/Sprites/OneKMonkeys.png";
        public const string ScenePath = "Assets/Scenes/Demo/OneKMonkeys.unity";
        public const string PrefabFolder = "Assets/Prefabs/Demo";
        public const string MaterialFolder = "Assets/Data/Demo/Materials";
        public const string MonkeyShader = "OneKMonkeys/MonkeyEffects";
        public const string FontFolder = "Assets/Fonts";
        public const string FontAssetFolder = "Assets/Fonts/TMP";
        public const string RegularFontFile = FontFolder + "/ComicRelief-Regular.ttf";
        public const string BoldFontFile = FontFolder + "/ComicRelief-Bold.ttf";
        public const string CodeFontFile = FontFolder + "/PressStart2P-Regular.ttf";

        // Font roles: normal text, accents (money, level, titles, buttons), code on monitors.
        private static TMP_FontAsset _codeFont;

        /// <summary>9-sliced sprite from the sheet used for every button and panel.</summary>
        public const string PanelSprite = "OneKMonkeys_6";
        /// <summary>Office wallpapers in upgrade order: default, then the background upgrades.</summary>
        public static readonly string[] BackgroundSprites = { "OneKMonkeys_10", "OneKMonkeys_11", "OneKMonkeys_12", "OneKMonkeys_13" };

        // World layout (units).
        private const float OrthoSize = 5.07f;
        private const float RowY = -3.0f;
        private const float FirstMonkeyX = 4.4f;
        private const float Spacing = 4.2f;
        private const float DeskScale = 1.3f;

        // Sorting orders.
        private const int OrderBackground = -100, OrderMonkey = 5, OrderEyes = 6, OrderDesk = 10, OrderGlow = 11, OrderBadge = 12;
        private const int OrderWorldText = 50, OrderWorldParticles = 60;
        private const int OrderOverlayCanvas = 40, OrderOverlayParticles = 45, OrderClickerCanvas = 80, OrderClickerParticles = 90;
        private const int OrderHud = 100, OrderHudParticles = 150;

        // Opaque on purpose: in linear color space even a few percent of transparency shows bright content through.
        private static readonly Color PanelColor = new Color(0.1f, 0.13f, 0.18f, 1f);

        // Particle systems must not live under a canvas: the canvas scale would shrink them to nothing.
        private static Transform _particles;
        private static bool _fromScratch;

        /// <summary>
        /// Default: creates the scene if missing, otherwise UPDATES it — objects that already exist in the scene
        /// (with all manual edits), existing prefabs and existing data assets are kept; only what is missing is added.
        /// </summary>
        [MenuItem("Tools/1000 Monkeys/Build or Update Demo Scene", priority = 0)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build();
        }

        [MenuItem("Tools/1000 Monkeys/Rebuild Demo Scene From Scratch", priority = 1)]
        public static void RebuildFromMenu()
        {
            if (!EditorUtility.DisplayDialog("Rebuild from scratch",
                    "The demo scene, its prefabs and data assets will be regenerated.\nAll manual changes to them will be LOST.",
                    "Rebuild", "Cancel")) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(fromScratch: true);
        }

        public static void Build(bool fromScratch = false)
        {
            _fromScratch = fromScratch;
            JabelUIFactory.RegularFont = JabelFontUtility.GetOrCreate(RegularFontFile, FontAssetFolder);
            JabelUIFactory.BoldFont = JabelFontUtility.GetOrCreate(BoldFontFile, FontAssetFolder);
            _codeFont = JabelFontUtility.GetOrCreate(CodeFontFile, FontAssetFolder, 64, 6);
            try
            {
                BuildScene();
            }
            finally
            {
                // Fonts are a demo choice; other builders keep the framework defaults.
                JabelUIFactory.ResetFonts();
            }
        }

        private static void BuildScene()
        {
            if (Shader.Find(MonkeyShader) == null)
            {
                Debug.LogError("[1000 Monkeys] Monkey shader not found or failed to compile: " + MonkeyShader);
                return;
            }
            // Idle games must keep ticking when the window loses focus.
            PlayerSettings.runInBackground = true;
            PlayerSettings.productName = "1000 Monkeys";
            var sprites = PrepareSprites();
            var data = DemoData.Create(sprites, overwrite: _fromScratch);

            var monkeyMaterial = CreateMonkeyMaterial();
            var dot = JabelEditorUtility.SoftDotSprite();
            var dotMaterial = JabelEditorUtility.ParticleMaterial(MaterialFolder + "/SoftDot.mat", dot);
            var sheetMaterial = JabelEditorUtility.ParticleMaterial(MaterialFolder + "/SpriteSheetParticle.mat", sprites["OneKMonkeys_8"]);

            // Prefabs are regenerated only from scratch (or when missing): manual prefab edits survive updates.
            // (Unity-null aware: no '??' on UnityEngine.Object.)
            TMP_Text worldTextPrefab = ExistingPrefab<TMP_Text>(PrefabFolder + "/WorldFloatingText.prefab");
            if (worldTextPrefab == null) worldTextPrefab = CreateWorldTextPrefab(PrefabFolder + "/WorldFloatingText.prefab");
            MonkeyStation stationPrefab = ExistingPrefab<MonkeyStation>(PrefabFolder + "/MonkeyStation.prefab");
            if (stationPrefab == null) stationPrefab = CreateStationPrefab(sprites, dot);
            BuffShopItemView shopItem = ExistingPrefab<BuffShopItemView>(PrefabFolder + "/ShopItem.prefab");
            if (shopItem == null) shopItem = ClickerSceneBuilder.CreateShopItemPrefab(PrefabFolder + "/ShopItem.prefab", sprites[PanelSprite]);

            var report = JabelSceneMerger.BuildOrUpdate(ScenePath, () => Populate(sprites, data, monkeyMaterial, dot, dotMaterial,
                sheetMaterial, worldTextPrefab, stationPrefab, shopItem), _fromScratch);

            ClickerSceneBuilder.AddSceneToBuild(ScenePath);
            AssetDatabase.SaveAssets();
            Debug.Log($"[1000 Monkeys] Demo scene {ScenePath}: {report}");
        }

        private static T ExistingPrefab<T>(string path) where T : Component
        {
            if (_fromScratch) return null;
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            return prefab != null ? prefab.GetComponent<T>() : null;
        }

        /// <summary>Creates every generated root object in the active scene.</summary>
        private static void Populate(IDictionary<string, Sprite> sprites, DemoData.Result data, Material monkeyMaterial, Sprite dot,
            Material dotMaterial, Material sheetMaterial, TMP_Text worldTextPrefab, MonkeyStation stationPrefab, BuffShopItemView shopItem)
        {

            // ---------------------------------------------------------- camera & core
            var camGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener), typeof(Physics2DRaycaster));
            camGo.tag = "MainCamera";
            var camera = camGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = OrthoSize;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.45f, 0.45f, 0.45f);
            camGo.transform.position = new Vector3(5, 0, -10);
            var scroller = camGo.AddComponent<CameraScroller>();
            scroller.SetBounds(5, 5);

            BuildOfficeBackground(camera, sprites);

            JabelUIFactory.EnsureEventSystem();
            _particles = new GameObject("Particles").transform;

            var managerGo = new GameObject("ClickerManager");
            var manager = managerGo.AddComponent<ClickerManager>();
            manager.EditorSetup(data.Config);

            var writerGo = new GameObject("CodeWriter");
            var writer = writerGo.AddComponent<CodeWriter>();
            writer.EditorSetup(data.Code, data.OnWritten);

            // ---------------------------------------------------------- canvases
            var hud = JabelUIFactory.CreateCanvas("HUD", camera, OrderHud, 1f);
            var clickerCanvas = JabelUIFactory.CreateCanvas("ClickerScreenCanvas", camera, OrderClickerCanvas, 2f);
            var overlay = JabelUIFactory.CreateCanvas("WorldOverlay", camera, OrderOverlayCanvas, 3f);
            var overlayGroup = overlay.gameObject.AddComponent<CanvasGroup>();

            // ---------------------------------------------------------- navigator & clicker screen
            var navigatorGo = new GameObject("ScreenNavigator");
            var navigator = navigatorGo.AddComponent<ScreenNavigator>();
            var clickerScreen = BuildClickerScreen(clickerCanvas.transform, sprites, writer, navigator, dotMaterial, sheetMaterial, dot);
            navigator.EditorSetup(clickerScreen.group, clickerScreen.content, overlayGroup);

            // ---------------------------------------------------------- world
            var world = new GameObject("World").transform;
            var worldTexts = new GameObject("WorldFloatingTexts").AddComponent<FloatingTextSpawner>();
            worldTexts.transform.SetParent(world, false);
            worldTexts.EditorSetup(worldTextPrefab, worldTexts.transform, 1.4f, 0.35f);

            BuildPlayerDesk(world, sprites, dot, navigator);

            var levelUpParticles = JabelUIFactory.CreateBurstParticles(world, "MonkeyLevelUpParticles", dotMaterial,
                Gradient2(new Color(1f, 0.9f, 0.4f), new Color(1f, 0.6f, 0.9f)), 2, 6, 0.1f, 0.3f, 1.2f, 0.6f, OrderWorldParticles);

            var hireSlot = BuildHireSlot(world, sprites, data);

            // ---------------------------------------------------------- HUD
            var tooltip = BuildHud(hud.transform, camera, sprites, manager, data, shopItem, dotMaterial, dot);

            // Opening the clicker screen closes the upgrades panel and the monkey tooltip.
            UnityEventTools.AddPersistentListener(navigator.EditorOnClickerOpened, hud.GetComponentInChildren<SlidePanel>(true).Close);
            UnityEventTools.AddPersistentListener(navigator.EditorOnClickerOpened, tooltip.HideAll);

            var rowGo = new GameObject("MonkeyRow");
            rowGo.transform.SetParent(world, false);
            var row = rowGo.AddComponent<MonkeyRow>();
            row.EditorSetup(data.Monkey, stationPrefab, rowGo.transform, hireSlot, data.Levels, monkeyMaterial,
                sprites["OneKMonkeys_7"], worldTexts, levelUpParticles, tooltip, FirstMonkeyX, Spacing, RowY);

            // ---------------------------------------------------------- big screen (upgrade)
            BuildBigScreen(overlay.transform, sprites, writer, dotMaterial, sheetMaterial);
        }

        // ================================================================== sprites & materials

        /// <summary>
        /// Sprite import tweaks the effects need: full-rect meshes (outline/glow can draw outside the
        /// silhouette), a transparent margin around the monkey, and bilinear filtering for smooth scaling.
        /// </summary>
        public static Dictionary<string, Sprite> PrepareSprites()
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(SpriteSheet);
            bool dirty = false;

            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            if (settings.spriteMeshType != SpriteMeshType.FullRect)
            {
                settings.spriteMeshType = SpriteMeshType.FullRect;
                importer.SetTextureSettings(settings);
                dirty = true;
            }
            if (importer.filterMode != FilterMode.Bilinear)
            {
                importer.filterMode = FilterMode.Bilinear;
                dirty = true;
            }
            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                dirty = true;
            }

            var factory = new SpriteDataProviderFactories();
            factory.Init();
            var provider = factory.GetSpriteEditorDataProviderFromObject(importer);
            provider.InitSpriteEditorDataProvider();
            var rects = provider.GetSpriteRects();
            foreach (var rect in rects)
            {
                // Monkey body: add a 12px transparent margin (effects draw there). Neighbours are clear of it.
                if (rect.name == "OneKMonkeys_7" && Mathf.Approximately(rect.rect.width, 251))
                {
                    rect.rect = new Rect(rect.rect.x - 12, rect.rect.y - 12, rect.rect.width + 24, rect.rect.height + 24);
                    dirty = true;
                }
            }
            if (dirty)
            {
                provider.SetSpriteRects(rects);
                provider.Apply();
                importer.SaveAndReimport();
            }

            return AssetDatabase.LoadAllAssetsAtPath(SpriteSheet).OfType<Sprite>().ToDictionary(s => s.name, s => s);
        }

        /// <summary>
        /// Tiled wallpaper that scrolls with the office. Two layers cross-fade when a background upgrade is bought.
        /// </summary>
        private static void BuildOfficeBackground(Camera camera, IDictionary<string, Sprite> sprites)
        {
            var root = new GameObject("OfficeBackground");
            root.transform.position = new Vector3(0, 0, 10);
            SpriteRenderer Layer(string name, int order)
            {
                var sr = CreateSprite(name, root.transform, sprites[BackgroundSprites[0]], order);
                sr.drawMode = SpriteDrawMode.Tiled;
                sr.tileMode = SpriteTileMode.Continuous;
                return sr;
            }
            var back = Layer("Back", OrderBackground - 1);
            var front = Layer("Front", OrderBackground);
            var styles = new Sprite[BackgroundSprites.Length];
            for (int i = 0; i < styles.Length; i++) styles[i] = sprites[BackgroundSprites[i]];
            root.AddComponent<OfficeBackground>().EditorSetup(camera, styles, DemoData.OfficeStyleKey, front, back);
        }

        private static Material CreateMonkeyMaterial()
        {
            string path = MaterialFolder + "/MonkeyEffects.mat";
            var material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material != null) return material;
            JabelEditorUtility.EnsureFolder(MaterialFolder);
            material = new Material(Shader.Find(MonkeyShader)) { name = "MonkeyEffects" };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }

        private static Gradient Gradient2(Color a, Color b)
        {
            var g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(a, 0), new GradientColorKey(b, 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            return g;
        }

        private static SpriteRenderer CreateSprite(string name, Transform parent, Sprite sprite, int order)
        {
            var go = new GameObject(name, typeof(SpriteRenderer));
            go.transform.SetParent(parent, false);
            var sr = go.GetComponent<SpriteRenderer>();
            sr.sprite = sprite;
            sr.sortingOrder = order;
            return sr;
        }

        private static TextMeshPro CreateWorldText(string name, Transform parent, string text, float size, Color color, int order)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var tmp = go.AddComponent<TextMeshPro>();
            JabelUIFactory.ApplyFont(tmp, FontStyles.Bold);
            tmp.text = text;
            tmp.fontSize = size;
            tmp.color = color;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            JabelUIFactory.ApplyOutline(tmp);
            tmp.rectTransform.sizeDelta = new Vector2(6, 1.5f);
            tmp.sortingOrder = order;
            return tmp;
        }

        private static TMP_Text CreateWorldTextPrefab(string path)
        {
            var text = CreateWorldText("WorldFloatingText", null, "+1", 3.4f, Color.white, OrderWorldText);
            JabelEditorUtility.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(text.gameObject, path);
            Object.DestroyImmediate(text.gameObject);
            return prefab.GetComponent<TMP_Text>();
        }

        // ================================================================== monkey station

        private static MonkeyStation CreateStationPrefab(IDictionary<string, Sprite> sprites, Sprite dot)
        {
            var root = new GameObject("MonkeyStation");

            var desk = CreateSprite("Desk", root.transform, sprites["OneKMonkeys_1"], OrderDesk);
            desk.transform.localScale = Vector3.one * DeskScale;

            var glow = CreateSprite("ScreenGlow", desk.transform, dot, OrderGlow);
            glow.transform.localPosition = new Vector3(0.48f, 0.5f, 0);
            glow.transform.localScale = new Vector3(1.1f, 1.0f, 1);
            glow.color = new Color(0.4f, 1f, 0.5f, 0.12f);

            var pivot = new GameObject("MonkeyPivot").transform;
            pivot.SetParent(root.transform, false);
            pivot.localPosition = new Vector3(-0.42f, 0.02f, 0);

            var body = CreateSprite("Body", pivot, sprites["OneKMonkeys_7"], OrderMonkey);
            body.transform.localScale = Vector3.one * 0.85f;
            body.transform.localPosition = new Vector3(0, 0.9f, 0);

            var eyeRoot = new GameObject("Eyes").transform;
            eyeRoot.SetParent(body.transform, false);
            var left = CreateSprite("LeftEye", eyeRoot, sprites["OneKMonkeys_2"], OrderEyes);
            left.transform.localPosition = new Vector3(-0.37f, 0.2f, 0);
            left.transform.localScale = Vector3.one * 1.15f;
            var right = CreateSprite("RightEye", eyeRoot, sprites["OneKMonkeys_2"], OrderEyes);
            right.transform.localPosition = new Vector3(0.08f, 0.2f, 0);
            right.transform.localScale = Vector3.one * 1.15f;
            var eyes = body.gameObject.AddComponent<MonkeyEyes>();
            eyes.EditorSetup(left, right, new[] { sprites["OneKMonkeys_2"], sprites["OneKMonkeys_3"], sprites["OneKMonkeys_4"] }, sprites["OneKMonkeys_5"]);

            var badgeBg = CreateSprite("LevelBadge", root.transform, sprites["OneKMonkeys_6"], OrderBadge);
            badgeBg.transform.localPosition = new Vector3(-1.25f, -0.35f, 0);
            badgeBg.transform.localScale = Vector3.one * 0.38f;
            badgeBg.color = new Color(1f, 0.9f, 0.55f);
            var badge = CreateWorldText("LevelText", root.transform, "1", 4f, new Color(0.25f, 0.18f, 0.05f), OrderBadge + 1);
            badge.transform.localPosition = new Vector3(-1.25f, -0.33f, 0);
            badge.fontSharedMaterial = badge.font.material;

            var anchor = new GameObject("FloatingAnchor").transform;
            anchor.SetParent(root.transform, false);
            anchor.localPosition = new Vector3(-0.35f, 2.2f, 0);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0, 0.45f);
            collider.size = new Vector2(3.6f, 3.4f);

            var station = root.AddComponent<MonkeyStation>();
            station.EditorSetup(desk, pivot, body, eyes, badge, glow, anchor);

            JabelEditorUtility.EnsureFolder(PrefabFolder);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root, PrefabFolder + "/MonkeyStation.prefab");
            Object.DestroyImmediate(root);
            return prefab.GetComponent<MonkeyStation>();
        }

        private static void BuildPlayerDesk(Transform world, IDictionary<string, Sprite> sprites, Sprite dot, ScreenNavigator navigator)
        {
            var root = new GameObject("PlayerDesk");
            root.transform.SetParent(world, false);
            root.transform.localPosition = new Vector3(0, RowY, 0);

            var visual = new GameObject("Visual").transform;
            visual.SetParent(root.transform, false);
            var desk = CreateSprite("Desk", visual, sprites["OneKMonkeys_1"], OrderDesk);
            desk.transform.localScale = Vector3.one * DeskScale;
            var glow = CreateSprite("ScreenGlow", desk.transform, dot, OrderGlow);
            glow.transform.localPosition = new Vector3(0.48f, 0.5f, 0);
            glow.transform.localScale = new Vector3(1.1f, 1.0f, 1);
            glow.color = new Color(0.5f, 0.9f, 1f, 0.2f);

            var label = CreateWorldText("YouLabel", root.transform, "YOU", 6f, new Color(1f, 0.9f, 0.4f), OrderWorldText);
            label.transform.localPosition = new Vector3(0.62f, 2.0f, 0);
            label.gameObject.AddComponent<LocalizedText>().Text = "hud.you";

            var hintHolder = new GameObject("StartHint");
            hintHolder.transform.SetParent(root.transform, false);
            var hint = CreateWorldText("Hint", hintHolder.transform, "Click your computer", 3.2f, Color.white, OrderWorldText);
            hint.transform.localPosition = new Vector3(0.3f, 2.8f, 0);
            hint.rectTransform.sizeDelta = new Vector2(9, 1);
            hint.gameObject.AddComponent<LocalizedText>().Text = "hud.startHint";
            hintHolder.AddComponent<ConditionalActivator>().EditorSetup("totalChars < 15", hint.gameObject);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0, 0.2f);
            collider.size = new Vector2(3.6f, 2.4f);

            root.AddComponent<PlayerComputer>().EditorSetup(navigator, visual, glow, label.transform);
        }

        private static HireSlot BuildHireSlot(Transform world, IDictionary<string, Sprite> sprites, DemoData.Result data)
        {
            var root = new GameObject("HireSlot");
            root.transform.SetParent(world, false);
            root.transform.localPosition = new Vector3(FirstMonkeyX, RowY, 0);

            var ghostDesk = CreateSprite("GhostDesk", root.transform, sprites["OneKMonkeys_1"], OrderDesk);
            ghostDesk.transform.localScale = Vector3.one * DeskScale;
            ghostDesk.color = new Color(1, 1, 1, 0.28f);
            var ghostMonkey = CreateSprite("GhostMonkey", root.transform, sprites["OneKMonkeys_7"], OrderMonkey);
            ghostMonkey.transform.localPosition = new Vector3(-0.42f, 0.92f, 0);
            ghostMonkey.transform.localScale = Vector3.one * 0.85f;
            ghostMonkey.color = new Color(0, 0, 0, 0.18f);

            var visual = new GameObject("Button").transform;
            visual.SetParent(root.transform, false);
            visual.localPosition = new Vector3(0, 1.25f, 0);
            var button = CreateSprite("Background", visual, sprites["OneKMonkeys_6"], OrderWorldText - 2);
            button.drawMode = SpriteDrawMode.Sliced;
            button.size = new Vector2(2.9f, 1.35f);
            button.color = new Color(0.35f, 0.8f, 0.45f);
            var title = CreateWorldText("Title", visual, "Hire", 3.6f, new Color(0.1f, 0.15f, 0.1f), OrderWorldText - 1);
            title.transform.localPosition = new Vector3(0, 0.25f, 0);
            title.fontSharedMaterial = title.font.material;
            var price = CreateWorldText("Price", visual, "$1.00", 4.2f, Color.white, OrderWorldText - 1);
            price.transform.localPosition = new Vector3(0, -0.28f, 0);

            var collider = root.AddComponent<BoxCollider2D>();
            collider.offset = new Vector2(0, 0.6f);
            collider.size = new Vector2(3.4f, 3.2f);

            var slot = root.AddComponent<HireSlot>();
            slot.EditorSetup(data.Monkey, button, title, price, visual);
            return slot;
        }

        // ================================================================== clicker screen

        private struct ClickerScreenRefs
        {
            public CanvasGroup group;
            public RectTransform content;
        }

        private static ClickerScreenRefs BuildClickerScreen(Transform canvas, IDictionary<string, Sprite> sprites, CodeWriter writer,
            ScreenNavigator navigator, Material dotMaterial, Material sheetMaterial, Sprite dot)
        {
            var rounded = sprites[PanelSprite];
            var root = JabelUIFactory.CreateRect("ClickerScreen", canvas);
            JabelUIFactory.Stretch(root);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            var dim = JabelUIFactory.CreateImage(root, "Dim", null, new Color(0.03f, 0.05f, 0.04f, 0.8f));
            JabelUIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;

            var content = JabelUIFactory.CreateRect("Content", root);
            JabelUIFactory.Stretch(content);

            var monitor = JabelUIFactory.CreateImage(content, "Monitor", sprites["OneKMonkeys_0"], Color.white);
            JabelUIFactory.Place(monitor.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(0, -40), new Vector2(1320, 849));

            var screen = BuildCodeScreen(monitor.transform, writer, 19, dotMaterial, sheetMaterial, OrderClickerParticles, out var feedbackRoot);

            // Floating "+$" texts over the monitor.
            var textsRoot = JabelUIFactory.CreateRect("FloatingTexts", content);
            JabelUIFactory.Stretch(textsRoot);
            var textPrefab = JabelUIFactory.CreateText(textsRoot, "FloatingTextPrefab", "+$1", 46, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            textPrefab.rectTransform.sizeDelta = new Vector2(400, 70);
            JabelUIFactory.ApplyOutline(textPrefab);
            var spawner = textsRoot.gameObject.AddComponent<FloatingTextSpawner>();
            spawner.EditorSetup(textPrefab, textsRoot, 150, 50);
            var textFeedback = feedbackRoot.gameObject.AddComponent<ClickTextFeedback>();
            textFeedback.EditorSetup(spawner, "hud.plusMoney", new Color(1f, 0.92f, 0.45f));
            JabelEditorUtility.Set(textFeedback, "numberFormat", Jabel.Numbers.NumberFormat.Money);

            var hint = JabelUIFactory.CreateText(content, "Hint", "Click the monitor to write code!", 34, new Color(0.85f, 1f, 0.85f), TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(hint.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 28), new Vector2(1400, 50));
            hint.gameObject.AddComponent<LocalizedText>().Text = "hud.clickHint";

            var perClick = JabelUIFactory.CreateText(content, "PerClick", "+1 chars per click", 28, new Color(0.7f, 0.9f, 0.7f));
            JabelUIFactory.Place(perClick.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 78), new Vector2(1000, 40));
            perClick.gameObject.AddComponent<FormulaText>().EditorSetup("hud.perClick", "clickPower");

            var back = JabelUIFactory.CreateButton(root, "BackButton", "Back", rounded, new Color(0.3f, 0.35f, 0.45f), new Vector2(220, 76), 32);
            JabelUIFactory.Place((RectTransform)back.transform, new Vector2(0, 1), new Vector2(0, 1), new Vector2(30, -140), new Vector2(220, 76));
            back.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizedText>().Text = "hud.back";
            UnityEventTools.AddPersistentListener(back.onClick, navigator.CloseClicker);

            return new ClickerScreenRefs { group = group, content = content };
        }

        /// <summary>Masked code area inside a monitor image, with click handling and particles.</summary>
        private static CodeScreenView BuildCodeScreen(Transform monitor, CodeWriter writer, float fontSize, Material dotMaterial,
            Material sheetMaterial, int particleOrder, out RectTransform clickRoot)
        {
            // The green area of the monitor sprite (normalized, from bottom-left).
            var area = JabelUIFactory.CreateRect("ScreenArea", monitor);
            area.anchorMin = new Vector2(0.055f, 0.145f);
            area.anchorMax = new Vector2(0.905f, 0.955f);
            area.offsetMin = area.offsetMax = Vector2.zero;
            area.gameObject.AddComponent<RectMask2D>().padding = new Vector4(12, 8, 12, 8);
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = new Color(0, 0, 0, 0);
            hit.raycastTarget = true;

            var text = JabelUIFactory.CreateText(area, "Code", "", fontSize, new Color(0.78f, 1f, 0.8f), TextAlignmentOptions.TopLeft);
            text.rectTransform.anchorMin = new Vector2(0, 1);
            text.rectTransform.anchorMax = new Vector2(1, 1);
            text.rectTransform.pivot = new Vector2(0, 1);
            text.rectTransform.offsetMin = new Vector2(18, -200);
            text.rectTransform.offsetMax = new Vector2(-18, -18);
            text.textWrappingMode = TextWrappingModes.NoWrap;
            // Pixel font for code: monospaced and readable at small sizes.
            if (_codeFont != null) text.font = _codeFont;
            text.lineSpacing = 30;

            var view = area.gameObject.AddComponent<CodeScreenView>();
            view.EditorSetup(writer, text, area);

            // No punch: the code must stay still while typing (only new characters animate).
            JabelEditorUtility.Set(area.gameObject.AddComponent<ClickArea>(), "punch", 0f);
            var sparks = JabelUIFactory.CreateBurstParticles(_particles, "CodeSparks", dotMaterial,
                Gradient2(new Color(0.4f, 1f, 0.5f), new Color(0.8f, 1f, 0.9f)), 2.5f, 7, 0.12f, 0.3f, 0.9f, 1.5f, particleOrder);
            var dollars = JabelUIFactory.CreateBurstParticles(_particles, "DollarParticles", sheetMaterial,
                Gradient2(new Color(1f, 1f, 1f), new Color(0.9f, 1f, 0.8f)), 3, 7.5f, 0.4f, 0.7f, 1.2f, 2.2f, particleOrder + 1);
            var sheet = dollars.textureSheetAnimation;
            sheet.enabled = true;
            sheet.mode = ParticleSystemAnimationMode.Sprites;
            sheet.SetSprite(0, AssetDatabase.LoadAllAssetsAtPath(SpriteSheet).OfType<Sprite>().First(s => s.name == "OneKMonkeys_8"));

            var sparkFeedback = area.gameObject.AddComponent<ClickParticlesFeedback>();
            sparkFeedback.EditorSetup(sparks);
            var dollarFeedback = area.gameObject.AddComponent<ClickParticlesFeedback>();
            dollarFeedback.EditorSetup(dollars);
            JabelEditorUtility.Set(dollarFeedback, "minCount", 1);
            JabelEditorUtility.Set(dollarFeedback, "maxCount", 25);
            JabelEditorUtility.Set(dollarFeedback, "perMagnitude", 3f);

            clickRoot = area;
            return view;
        }

        private static void BuildBigScreen(Transform overlay, IDictionary<string, Sprite> sprites, CodeWriter writer,
            Material dotMaterial, Material sheetMaterial)
        {
            var holder = JabelUIFactory.CreateRect("BigScreenHolder", overlay);
            JabelUIFactory.Stretch(holder);

            var monitor = JabelUIFactory.CreateImage(holder, "BigScreen", sprites["OneKMonkeys_0"], Color.white);
            JabelUIFactory.Place(monitor.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -135), new Vector2(700, 450));

            BuildCodeScreen(monitor.transform, writer, 10, dotMaterial, sheetMaterial, OrderOverlayParticles, out var area);

            var texts = JabelUIFactory.CreateRect("FloatingTexts", monitor.transform);
            JabelUIFactory.Stretch(texts);
            var prefab = JabelUIFactory.CreateText(texts, "FloatingTextPrefab", "+$1", 30, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            prefab.rectTransform.sizeDelta = new Vector2(300, 50);
            JabelUIFactory.ApplyOutline(prefab);
            var spawner = texts.gameObject.AddComponent<FloatingTextSpawner>();
            spawner.EditorSetup(prefab, texts, 90, 30);
            var feedback = area.gameObject.AddComponent<ClickTextFeedback>();
            feedback.EditorSetup(spawner, "hud.plusMoney", new Color(1f, 0.92f, 0.45f));
            JabelEditorUtility.Set(feedback, "numberFormat", Jabel.Numbers.NumberFormat.Money);

            holder.gameObject.AddComponent<ConditionalActivator>().EditorSetup("count('big_screen') > 0", monitor.gameObject);
        }

        // ================================================================== HUD

        private static MonkeyTooltip BuildHud(Transform hud, Camera camera, IDictionary<string, Sprite> sprites, ClickerManager manager,
            DemoData.Result data, BuffShopItemView shopItem, Material dotMaterial, Sprite dot)
        {
            var rounded = sprites[PanelSprite];

            // ---- top bar
            var bar = JabelUIFactory.CreateImage(hud, "TopBar", rounded, new Color(0.06f, 0.08f, 0.11f, 1f), true);
            JabelUIFactory.Place(bar.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -8), new Vector2(1890, 112));
            bar.rectTransform.anchorMin = new Vector2(0, 1);
            bar.rectTransform.anchorMax = new Vector2(1, 1);
            bar.rectTransform.offsetMin = new Vector2(14, -120);
            bar.rectTransform.offsetMax = new Vector2(-14, -8);

            // Money (left edge)
            var moneyIcon = JabelUIFactory.CreateImage(bar.transform, "MoneyIcon", sprites["OneKMonkeys_8"], Color.white);
            moneyIcon.preserveAspect = true;
            JabelUIFactory.Place(moneyIcon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(20, 0), new Vector2(64, 90));
            var money = JabelUIFactory.CreateText(bar.transform, "MoneyText", "$0.00", 58, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            JabelUIFactory.Place(money.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(92, 0), new Vector2(500, 90));
            money.textWrappingMode = TextWrappingModes.NoWrap;
            money.gameObject.AddComponent<ValueText>().EditorSetup(ValueText.SourceType.Variable, "money", null, "hud.money");

            // Level + progress, centred in the gap between the money (ends ~592) and the monkey counter (~1200).
            const float progressWidth = 480;
            const float progressX = -52;
            var level = JabelUIFactory.CreateText(bar.transform, "LevelText", "Level 1", 38, new Color(1f, 0.88f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(level.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(progressX, 24), new Vector2(progressWidth, 48));
            level.gameObject.AddComponent<ValueText>().EditorSetup(ValueText.SourceType.Variable, "playerLevel", null, "hud.level");

            // Progress bar:
            //   Progress (green, full)            <- white label sits on it
            //   Remaining (dark, Filled from the right, fillAmount = 1 - progress, Mask)
            //     LinesTextGreen                  <- only visible on the dark, unfilled part
            // So the label is white over the green part and green over the empty part.
            var barGreen = new Color(0.35f, 0.85f, 0.45f);
            var progressBg = JabelUIFactory.CreateImage(bar.transform, "Progress", rounded, barGreen, true);
            JabelUIFactory.Place(progressBg.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(progressX, -24), new Vector2(progressWidth, 36));
            progressBg.pixelsPerUnitMultiplier = 2.5f;

            var whiteText = JabelUIFactory.CreateText(progressBg.transform, "LinesTextWhite", "Lines 0 / 20", 22, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Stretch(whiteText.rectTransform);
            whiteText.textWrappingMode = TextWrappingModes.NoWrap;
            whiteText.gameObject.AddComponent<FormulaText>().EditorSetup("hud.lines", "linesThisLevel", "linesForNextLevel");

            const float inL = 6, inR = 7, inT = 5, inB = 6; // inside the hand-drawn border
            var remaining = JabelUIFactory.CreateImage(progressBg.transform, "Remaining", JabelEditorUtility.WhiteSprite(), new Color(0.13f, 0.16f, 0.21f), false);
            JabelUIFactory.Stretch(remaining.rectTransform, inL, inR, inT, inB);
            remaining.type = Image.Type.Filled;
            remaining.fillMethod = Image.FillMethod.Horizontal;
            remaining.fillOrigin = (int)Image.OriginHorizontal.Right;
            remaining.fillAmount = 1;
            remaining.gameObject.AddComponent<Mask>().showMaskGraphic = true;

            var greenText = JabelUIFactory.CreateText(remaining.transform, "LinesTextGreen", "Lines 0 / 20", 22, barGreen, TextAlignmentOptions.Center, FontStyles.Bold);
            // Same rect as the white label, so both lines of text overlap exactly.
            JabelUIFactory.Stretch(greenText.rectTransform, -inL, -inR, -inT, -inB);
            greenText.textWrappingMode = TextWrappingModes.NoWrap;
            greenText.gameObject.AddComponent<FormulaText>().EditorSetup("hud.lines", "linesThisLevel", "linesForNextLevel");
            // Both labels must not punch independently or they would drift apart.
            JabelEditorUtility.Set(whiteText.GetComponent<FormulaText>(), "punch", 0f);
            JabelEditorUtility.Set(greenText.GetComponent<FormulaText>(), "punch", 0f);

            var flash = JabelUIFactory.CreateImage(progressBg.transform, "Flash", rounded, new Color(1, 1, 0.6f, 0), true);
            JabelUIFactory.Stretch(flash.rectTransform);
            flash.pixelsPerUnitMultiplier = 2.5f;
            progressBg.gameObject.AddComponent<ValueProgressBar>().EditorSetup(remaining, flash, "linesThisLevel", "linesForNextLevel", remaining: true);

            // Monkeys: sized so even "1000 / 1000" stays clear of the buttons on the right.
            var monkeyIcon = JabelUIFactory.CreateImage(bar.transform, "MonkeyIcon", sprites["OneKMonkeys_7"], new Color(0.8f, 0.6f, 0.4f));
            monkeyIcon.preserveAspect = true;
            JabelUIFactory.Place(monkeyIcon.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-596, 0), new Vector2(96, 70));
            AddIconEyes(monkeyIcon, sprites);
            var monkeys = JabelUIFactory.CreateText(bar.transform, "MonkeysText", "0 / 1000", 36, Color.white, TextAlignmentOptions.Left, FontStyles.Bold);
            JabelUIFactory.Place(monkeys.rectTransform, new Vector2(1, 0.5f), new Vector2(0, 0.5f), new Vector2(-586, 0), new Vector2(226, 60));
            monkeys.textWrappingMode = TextWrappingModes.NoWrap;
            monkeys.enableAutoSizing = true;
            monkeys.fontSizeMin = 24;
            monkeys.fontSizeMax = 36;
            var monkeysText = monkeys.gameObject.AddComponent<FormulaText>();
            monkeysText.EditorSetup("hud.monkeys", "count('monkey')", "1000");
            // Whole numbers with thousands separators ("1 000"), never abbreviated to "1K".
            JabelEditorUtility.Set(monkeysText, "numberFormat", new Jabel.Numbers.NumberFormat { notation = Jabel.Numbers.NumberNotation.Grouped });

            // Shop panel (slides from the right)
            var shopPanel = JabelUIFactory.CreateImage(hud, "UpgradesPanel", rounded, PanelColor, true);
            shopPanel.raycastTarget = true;
            JabelUIFactory.Place(shopPanel.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -134), new Vector2(620, 900));
            var slide = shopPanel.gameObject.AddComponent<SlidePanel>();
            JabelEditorUtility.Set(slide, "hiddenOffset", new Vector2(700, 0));
            var shopTitle = JabelUIFactory.CreateText(shopPanel.transform, "Title", "Upgrades", 42, new Color(1f, 0.88f, 0.45f), TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(shopTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -12), new Vector2(560, 56));
            shopTitle.gameObject.AddComponent<LocalizedText>().Text = "hud.upgrades";
            var list = JabelUIFactory.CreateVerticalScroll(shopPanel.transform, "List");
            JabelUIFactory.Stretch((RectTransform)list.parent.parent, 10, 10, 76, 10);
            list.gameObject.AddComponent<BuffShopView>().EditorSetup(shopItem, list, BuffShopView.KindFilter.All, null);

            // Upgrade / language / exit buttons
            var upgrade = JabelUIFactory.CreateButton(bar.transform, "UpgradesButton", null, rounded, new Color(0.2f, 0.55f, 0.75f), new Vector2(96, 90));
            JabelUIFactory.Place((RectTransform)upgrade.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-250, 0), new Vector2(96, 90));
            var arrowIcon = JabelUIFactory.CreateImage(upgrade.transform, "Icon", sprites["OneKMonkeys_9"], Color.white);
            arrowIcon.preserveAspect = true;
            JabelUIFactory.Stretch(arrowIcon.rectTransform, 14, 14, 8, 8);
            JabelEditorUtility.Set(upgrade.GetComponent<ButtonJuice>(), "pulseWhenAvailable", true);
            UnityEventTools.AddPersistentListener(upgrade.onClick, slide.Toggle);

            var language = JabelUIFactory.CreateButton(bar.transform, "LanguageButton", "EN", rounded, new Color(0.3f, 0.33f, 0.45f), new Vector2(96, 90), 32);
            JabelUIFactory.Place((RectTransform)language.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-138, 0), new Vector2(96, 90));
            JabelEditorUtility.Set(language.gameObject.AddComponent<LanguageButton>(), "label", language.GetComponentInChildren<TMP_Text>());

            var exit = JabelUIFactory.CreateButton(bar.transform, "ExitButton", "X", rounded, new Color(0.7f, 0.25f, 0.25f), new Vector2(96, 90), 44);
            JabelUIFactory.Place((RectTransform)exit.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-26, 0), new Vector2(96, 90));
            UnityEventTools.AddPersistentListener(exit.onClick, manager.Quit);

            // Totals (bottom-left)
            var totals = JabelUIFactory.CreateText(hud, "Totals", "", 24, new Color(1, 1, 1, 0.75f), TextAlignmentOptions.BottomLeft);
            JabelUIFactory.Place(totals.rectTransform, Vector2.zero, Vector2.zero, new Vector2(24, 18), new Vector2(900, 40));
            JabelUIFactory.ApplyOutline(totals);
            totals.gameObject.AddComponent<FormulaText>().EditorSetup("hud.totals", "totalChars", "totalLines");

            // Row scrollbar (bottom)
            var scrollbarBg = JabelUIFactory.CreateImage(hud, "RowScrollbar", rounded, new Color(0, 0, 0, 0.35f), true);
            scrollbarBg.raycastTarget = true;
            scrollbarBg.pixelsPerUnitMultiplier = 3f;
            JabelUIFactory.Place(scrollbarBg.rectTransform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 62), new Vector2(900, 22));
            var handleArea = JabelUIFactory.CreateRect("Sliding Area", scrollbarBg.transform);
            JabelUIFactory.Stretch(handleArea, 4, 4, 3, 3);
            var handle = JabelUIFactory.CreateImage(handleArea, "Handle", rounded, new Color(1f, 0.85f, 0.45f, 0.9f), true);
            handle.raycastTarget = true;
            handle.pixelsPerUnitMultiplier = 3f;
            JabelUIFactory.Stretch(handle.rectTransform);
            var scrollbar = scrollbarBg.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.LeftToRight;
            var scrollGroup = scrollbarBg.gameObject.AddComponent<CanvasGroup>();
            scrollGroup.alpha = 0;
            JabelEditorUtility.Set(scrollbarBg.gameObject.AddComponent<RowScrollbar>(), "group", scrollGroup);

            // Monkey tooltip: follows the hovered monkey, never blocks clicks.
            var tooltipHolder = JabelUIFactory.CreateRect("MonkeyTooltip", hud);
            JabelUIFactory.Stretch(tooltipHolder);
            var plate = JabelUIFactory.CreateImage(tooltipHolder, "Plate", rounded, PanelColor, true);
            JabelUIFactory.Place(plate.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0f), Vector2.zero, new Vector2(560, 300));
            var plateGroup = plate.gameObject.AddComponent<CanvasGroup>();
            var plateLayout = plate.gameObject.AddComponent<VerticalLayoutGroup>();
            plateLayout.padding = new RectOffset(26, 26, 18, 22);
            plateLayout.spacing = 6;
            plateLayout.childControlHeight = true;
            plateLayout.childControlWidth = true;
            plateLayout.childForceExpandHeight = false;
            plateLayout.childForceExpandWidth = true;
            plate.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            var tTitle = JabelUIFactory.CreateText(plate.transform, "Title", "Monkey #1", 32, new Color(1f, 0.88f, 0.45f), TextAlignmentOptions.TopLeft, FontStyles.Bold);
            var tBody = JabelUIFactory.CreateText(plate.transform, "Body", "Level 1 / 20", 24, new Color(0.9f, 0.95f, 1f), TextAlignmentOptions.TopLeft);
            var tooltip = tooltipHolder.gameObject.AddComponent<MonkeyTooltip>();
            tooltip.EditorSetup(plate.rectTransform, plateGroup, tTitle, tBody, data.Levels, DemoData.MonkeyChars);

            // Toasts, offline popup (framework widgets)
            ClickerSceneBuilder.CreateToasts(hud, rounded);
            ClickerSceneBuilder.CreateOfflinePopup(hud, rounded);

            // Level-up confetti, triggered by the "LevelUp" function called from JabelScript.
            var confetti = JabelUIFactory.CreateBurstParticles(camera.transform, "LevelUpConfetti", dotMaterial,
                Gradient2(new Color(1f, 0.85f, 0.3f), new Color(0.4f, 0.8f, 1f)), 3, 9, 0.08f, 0.2f, 1.6f, 1.2f, OrderHudParticles);
            confetti.transform.localPosition = new Vector3(0, 3.6f, 8);
            var shape = confetti.shape;
            shape.shapeType = ParticleSystemShapeType.Circle;
            shape.radius = 2f;
            var burst = confetti.gameObject.AddComponent<ParticleBurst>();
            burst.EditorSetup(confetti, 80);
            var listener = confetti.gameObject.AddComponent<FunctionListener>();
            JabelEditorUtility.Set(listener, "function", "LevelUp");
            UnityEventTools.AddPersistentListener(GetUnityEvent(listener), burst.Burst);
            EditorUtility.SetDirty(listener);

            return tooltip;
        }

        /// <summary>
        /// Puts blinking eyes on a UI image of the monkey body (the body sprite has none of its own).
        /// Positions match the world monkey: eye centres at (-37, 20) and (8, 20) sprite pixels from the centre.
        /// </summary>
        private static void AddIconEyes(Image icon, IDictionary<string, Sprite> sprites)
        {
            var body = icon.sprite;
            var size = icon.rectTransform.sizeDelta;
            // preserveAspect: the sprite is fitted inside the rect.
            float scale = Mathf.Min(size.x / body.rect.width, size.y / body.rect.height);
            const float eyeScale = 1.15f;

            var left = JabelUIFactory.CreateImage(icon.transform, "LeftEye", sprites["OneKMonkeys_2"], Color.white);
            var right = JabelUIFactory.CreateImage(icon.transform, "RightEye", sprites["OneKMonkeys_2"], Color.white);
            foreach (var (eye, x) in new[] { (left, -37f), (right, 8f) })
            {
                var eyeSize = new Vector2(eye.sprite.rect.width, eye.sprite.rect.height) * scale * eyeScale;
                JabelUIFactory.Place(eye.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(x, 20f) * scale, eyeSize);
            }

            var eyes = icon.gameObject.AddComponent<UIMonkeyEyes>();
            eyes.EditorSetup(left, right, new[] { sprites["OneKMonkeys_2"], sprites["OneKMonkeys_3"], sprites["OneKMonkeys_4"] },
                sprites["OneKMonkeys_5"], 2.5f * scale);
        }

        private static UnityEngine.Events.UnityEvent<float> GetUnityEvent(FunctionListener listener)
        {
            var field = typeof(FunctionListener).GetField("onCalled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            return (UnityEngine.Events.UnityEvent<float>)field.GetValue(listener);
        }
    }
}
