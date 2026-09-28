using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using Jabel.Scripting.Blocks;
using Jabel.UI;
using Jabel.Variables;
using TMPro;
using UnityEditor;
using UnityEditor.Events;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

namespace Jabel.Editor
{
    /// <summary>
    /// "Build clicker scene": generates a playable clicker — config, test buffs, localization,
    /// click button with particles, shop, HUD, toasts and offline popup — as a starting point.
    /// </summary>
    public static class ClickerSceneBuilder
    {
        public const string FrameworkCsv = "Assets/Localization/Jabel/Jabel.csv";
        public const string TemplateCsv = "Assets/Localization/Jabel/ClickerTemplate.csv";

        [MenuItem("Tools/Jabel/Build Clicker Scene", priority = 0)]
        public static void BuildFromMenu()
        {
            string scenePath = EditorUtility.SaveFilePanelInProject("Build clicker scene", "MyClicker", "unity",
                "Choose where to create the clicker scene. Data assets go to a folder next to it.");
            if (string.IsNullOrEmpty(scenePath)) return;
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;
            Build(scenePath);
        }

        public static void Build(string scenePath)
        {
            string name = System.IO.Path.GetFileNameWithoutExtension(scenePath);
            JabelEditorUtility.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath));
            string dataFolder = JabelEditorUtility.EnsureFolder(System.IO.Path.GetDirectoryName(scenePath).Replace('\\', '/') + "/" + name + "_Data");

            // Idle games must keep ticking when the window loses focus.
            PlayerSettings.runInBackground = true;
            var config = CreateData(dataFolder, name);
            var itemPrefab = CreateShopItemPrefab(dataFolder + "/ShopItem.prefab");

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            // Camera
            var cameraGo = new GameObject("Main Camera", typeof(Camera), typeof(AudioListener));
            cameraGo.tag = "MainCamera";
            var camera = cameraGo.GetComponent<Camera>();
            camera.orthographic = true;
            camera.orthographicSize = 5;
            camera.clearFlags = CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.1f, 0.12f, 0.18f);
            cameraGo.transform.position = new Vector3(0, 0, -10);

            JabelUIFactory.EnsureEventSystem();

            // Manager
            var managerGo = new GameObject("ClickerManager");
            var manager = managerGo.AddComponent<ClickerManager>();
            manager.EditorSetup(config);

            BuildUI(camera, manager, itemPrefab, dataFolder);

            EditorSceneManager.SaveScene(scene, scenePath);
            AddSceneToBuild(scenePath);
            AssetDatabase.SaveAssets();
            Selection.activeObject = config;
            Debug.Log($"[Jabel] Clicker scene created at {scenePath}. Edit the game in {AssetDatabase.GetAssetPath(config)}.");
        }

        // ------------------------------------------------------------ data

        private static ClickerConfig CreateData(string folder, string gameName)
        {
            var loc = JabelEditorUtility.LoadOrCreate<LocalizationDatabase>(folder + "/Localization.asset", out _);
            loc.EditorSetup(new List<LanguageInfo>
                {
                    new LanguageInfo { code = "en", nativeName = "English", systemLanguages = new List<SystemLanguage> { SystemLanguage.English } },
                    new LanguageInfo { code = "ru", nativeName = "Русский", systemLanguages = new List<SystemLanguage> { SystemLanguage.Russian, SystemLanguage.Belarusian, SystemLanguage.Ukrainian } }
                }, "en",
                new List<TextAsset> { AssetDatabase.LoadAssetAtPath<TextAsset>(FrameworkCsv), AssetDatabase.LoadAssetAtPath<TextAsset>(TemplateCsv) });
            EditorUtility.SetDirty(loc);

            // Buffs
            var mouse = JabelEditorUtility.CreateOrReplace<PassiveBuff>(folder + "/Buffs/BetterMouse.asset");
            mouse.EditorSetup("better_mouse", null, "tpl.buff.mouse.name", "tpl.buff.mouse.desc", "click", 0,
                new BuffCost { baseCost = 10, growth = 1.5 }, 25, BuffVisibility.Always, null, null);

            var golden = JabelEditorUtility.CreateOrReplace<PassiveBuff>(folder + "/Buffs/GoldenMouse.asset");
            golden.EditorSetup("golden_mouse", null, "tpl.buff.golden.name", "tpl.buff.golden.desc", "click", 1,
                new BuffCost { baseCost = 250, growth = 8 }, 5, BuffVisibility.Always,
                new List<BuffRequirement> { new BuffRequirement { variable = "clicks", minimum = 50 } }, null);

            var lucky = JabelEditorUtility.CreateOrReplace<PassiveBuff>(folder + "/Buffs/LuckyFinger.asset");
            lucky.EditorSetup("lucky_finger", null, "tpl.buff.lucky.name", "tpl.buff.lucky.desc", "click", 2,
                new BuffCost { baseCost = 100, growth = 3 }, 5, BuffVisibility.WhenUnlocked,
                new List<BuffRequirement> { new BuffRequirement { variable = "clicks", minimum = 25 } }, null);

            var auto = JabelEditorUtility.CreateOrReplace<ActiveBuff>(folder + "/Buffs/AutoClicker.asset");
            auto.EditorSetup("auto_clicker", null, "tpl.buff.auto.name", "tpl.buff.auto.desc", "auto", 10,
                new BuffCost { baseCost = 15, growth = 1.15 }, 0, BuffVisibility.Always, null, null);
            // Non-instanced: one timer for all units, the script multiplies by 'count'.
            auto.EditorSetupActive("4", new JabelScript(
                new ModifyVariableBlock(VariableScope.Global, "money", ModifyOperation.Add, "count * clickPower")), false, 1, null, null, null, null);

            var factory = JabelEditorUtility.CreateOrReplace<ActiveBuff>(folder + "/Buffs/Factory.asset");
            factory.EditorSetup("factory", null, "tpl.buff.factory.name", "tpl.buff.factory.desc", "auto", 11,
                new BuffCost { baseCost = 200, growth = 1.2 }, 0, BuffVisibility.WhenUnlocked,
                new List<BuffRequirement> { new BuffRequirement { variable = "money", minimum = 100 } }, null);
            // Instanced: every factory has its own timer and level.
            factory.EditorSetupActive("20", new JabelScript(
                    new ModifyVariableBlock(VariableScope.Global, "money", ModifyOperation.Add, "25 * level")), true, 5,
                new BuffCost { mode = CostMode.Formula, formula = new JabelFormula("100 * 3 ^ level") }, null, null, null);

            foreach (var b in new BaseBuff[] { mouse, golden, lucky, auto, factory }) EditorUtility.SetDirty(b);

            // Config
            var config = JabelEditorUtility.CreateOrReplace<ClickerConfig>(folder + "/ClickerConfig.asset");
            config.configId = gameName.ToLowerInvariant();
            // One save file per game, so several Jabel games in one project never share progress.
            config.saveSlot = config.configId;
            config.localization = loc;
            config.variables = new List<VariableDefinition>
            {
                new VariableDefinition { key = "money", displayName = "tpl.money.name", format = NumberFormat.Money, showInOfflineReport = true },
                new VariableDefinition { key = "clicks", displayName = "tpl.clicks.name", persistence = VariablePersistence.Permanent }
            };
            config.derivedValues = new List<DerivedValueDefinition>
            {
                new DerivedValueDefinition { key = "clickPower", formula = new JabelFormula("(1 + count('better_mouse')) * 2 ^ count('golden_mouse')") },
                new DerivedValueDefinition { key = "critChance", formula = new JabelFormula("0.05 * count('lucky_finger')") }
            };
            config.passiveBuffs = new List<PassiveBuff> { mouse, golden, lucky };
            config.activeBuffs = new List<ActiveBuff> { auto, factory };

            // OnClick: value = clickPower; maybe critical x5; money += value; clicks += 1.
            config.onClick = new JabelScript(
                new SetVariableBlock(VariableScope.Local, ClickerManager.LocalValue, "clickPower"),
                new ChanceBlock("critChance", new JabelScript(
                    new ModifyVariableBlock(VariableScope.Local, ClickerManager.LocalValue, ModifyOperation.Multiply, "5"),
                    new SetVariableBlock(VariableScope.Local, ClickerManager.LocalCritical, "1"))),
                new ModifyVariableBlock(VariableScope.Global, "money", ModifyOperation.Add, "value"),
                new ModifyVariableBlock(VariableScope.Global, "clicks", ModifyOperation.Add, "1"));
            config.onNewGame = new JabelScript(new CommentBlock { text = "Starting resources or tutorial flags go here." });
            config.onStart = new JabelScript();
            config.onTick = new JabelScript();
            config.onReturn = new JabelScript();
            EditorUtility.SetDirty(config);
            return config;
        }

        // ------------------------------------------------------------ prefabs

        private static readonly Color Panel = new Color(0.16f, 0.19f, 0.26f, 1f);
        private static readonly Color Accent = new Color(0.27f, 0.56f, 0.98f);

        /// <param name="panelSprite">9-sliced background; null = generated rounded rectangle.</param>
        public static BuffShopItemView CreateShopItemPrefab(string path, Sprite panelSprite = null)
        {
            var rounded = panelSprite != null ? panelSprite : JabelEditorUtility.RoundedRectSprite();
            var root = JabelUIFactory.CreateImage(null, "ShopItem", rounded, new Color(0.22f, 0.26f, 0.35f), true);
            root.raycastTarget = true;
            root.rectTransform.sizeDelta = new Vector2(560, 150);
            JabelUIFactory.Layout(root, 150);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root;
            var juice = root.gameObject.AddComponent<ButtonJuice>();
            JabelEditorUtility.Set(juice, "tintTarget", root);
            JabelEditorUtility.Set(juice, "hoverScale", 1.03f);
            var group = root.gameObject.AddComponent<CanvasGroup>();

            // Layout (from the top): title 10-50, description 50-100, lock hint 112-140; price bottom-right, count top-right.
            var icon = JabelUIFactory.CreateImage(root.transform, "Icon", null, Color.white);
            JabelUIFactory.Place(icon.rectTransform, new Vector2(0, 0.5f), new Vector2(0, 0.5f), new Vector2(14, 0), new Vector2(84, 84));

            var title = JabelUIFactory.CreateText(root.transform, "Title", "Item", 28, Color.white, TextAlignmentOptions.TopLeft, FontStyles.Bold);
            TopBand(title.rectTransform, 110, 110, 10, 40);
            title.textWrappingMode = TextWrappingModes.NoWrap;
            title.overflowMode = TextOverflowModes.Ellipsis;
            title.enableAutoSizing = true;
            title.fontSizeMin = 20;
            title.fontSizeMax = 28;

            var desc = JabelUIFactory.CreateText(root.transform, "Description", "Description", 20, new Color(0.8f, 0.85f, 0.95f), TextAlignmentOptions.TopLeft);
            TopBand(desc.rectTransform, 110, 16, 52, 48);
            desc.overflowMode = TextOverflowModes.Ellipsis;

            var count = JabelUIFactory.CreateText(root.transform, "Count", "0", 24, new Color(1f, 0.85f, 0.4f), TextAlignmentOptions.TopRight, FontStyles.Bold);
            JabelUIFactory.Place(count.rectTransform, new Vector2(1, 1), new Vector2(1, 1), new Vector2(-14, -12), new Vector2(100, 34));
            var price = JabelUIFactory.CreateText(root.transform, "Price", "$10", 28, new Color(0.55f, 1f, 0.55f), TextAlignmentOptions.BottomRight, FontStyles.Bold);
            JabelUIFactory.Place(price.rectTransform, new Vector2(1, 0), new Vector2(1, 0), new Vector2(-14, 8), new Vector2(220, 40));
            var lockText = JabelUIFactory.CreateText(root.transform, "Lock", "Requires", 19, new Color(1f, 0.7f, 0.4f), TextAlignmentOptions.BottomLeft, FontStyles.Italic);
            JabelUIFactory.Place(lockText.rectTransform, new Vector2(0, 0), new Vector2(0, 0), new Vector2(110, 8), new Vector2(300, 28));
            lockText.textWrappingMode = TextWrappingModes.NoWrap;

            var view = root.gameObject.AddComponent<BuffShopItemView>();
            JabelEditorUtility.Set(view, "icon", icon);
            JabelEditorUtility.Set(view, "title", title);
            JabelEditorUtility.Set(view, "description", desc);
            JabelEditorUtility.Set(view, "countText", count);
            JabelEditorUtility.Set(view, "priceText", price);
            JabelEditorUtility.Set(view, "lockText", lockText);
            JabelEditorUtility.Set(view, "button", button);
            JabelEditorUtility.Set(view, "juice", juice);
            JabelEditorUtility.Set(view, "group", group);

            JabelEditorUtility.EnsureFolder(System.IO.Path.GetDirectoryName(path));
            var prefab = PrefabUtility.SaveAsPrefabAsset(root.gameObject, path);
            Object.DestroyImmediate(root.gameObject);
            return prefab.GetComponent<BuffShopItemView>();
        }

        // ------------------------------------------------------------ scene UI

        private static void BuildUI(Camera camera, ClickerManager manager, BuffShopItemView itemPrefab, string dataFolder)
        {
            var rounded = JabelEditorUtility.RoundedRectSprite();
            var canvas = JabelUIFactory.CreateCanvas("Canvas", camera, 10);
            var root = canvas.transform;

            // Money + income
            var money = JabelUIFactory.CreateText(root, "MoneyText", "$0", 84, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(money.rectTransform, new Vector2(0.35f, 1), new Vector2(0.5f, 1), new Vector2(0, -40), new Vector2(900, 110));
            money.gameObject.AddComponent<ValueText>().EditorSetup(ValueText.SourceType.Variable, "money", null, "tpl.money");

            var income = JabelUIFactory.CreateText(root, "IncomeText", "$0 per second", 34, new Color(0.7f, 0.85f, 1f));
            JabelUIFactory.Place(income.rectTransform, new Vector2(0.35f, 1), new Vector2(0.5f, 1), new Vector2(0, -150), new Vector2(900, 50));
            var incomeValue = income.gameObject.AddComponent<ValueText>();
            incomeValue.EditorSetup(ValueText.SourceType.Formula, null, "rate('money')", "tpl.income");
            JabelEditorUtility.Set(incomeValue, "flashOnChange", false);

            // Click button
            var clickButton = JabelUIFactory.CreateButton(root, "ClickButton", null, rounded, Accent, new Vector2(420, 420), juice: true);
            JabelUIFactory.Place((RectTransform)clickButton.transform, new Vector2(0.35f, 0.45f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(420, 420));
            var clickLabel = JabelUIFactory.CreateText(clickButton.transform, "Label", "CLICK!", 72, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Stretch(clickLabel.rectTransform);
            clickLabel.gameObject.AddComponent<LocalizedText>().Text = "tpl.click";
            clickButton.gameObject.AddComponent<ClickArea>();

            var particleMaterial = JabelEditorUtility.ParticleMaterial(dataFolder + "/ClickParticles.mat", JabelEditorUtility.SoftDotSprite());
            var gradient = new Gradient();
            gradient.SetKeys(new[] { new GradientColorKey(new Color(1f, 0.85f, 0.3f), 0), new GradientColorKey(new Color(0.4f, 0.8f, 1f), 1) },
                new[] { new GradientAlphaKey(1, 0), new GradientAlphaKey(1, 1) });
            // Particles live outside the canvas (its scale would shrink them).
            var particles = JabelUIFactory.CreateBurstParticles(null, "ClickParticles", particleMaterial, gradient, 2, 7, 0.08f, 0.22f, 0.9f, 1.2f, 50);
            clickButton.gameObject.AddComponent<ClickParticlesFeedback>().EditorSetup(particles);

            var floatingRoot = JabelUIFactory.CreateRect("FloatingTexts", root);
            JabelUIFactory.Stretch(floatingRoot);
            var floatingPrefab = JabelUIFactory.CreateText(floatingRoot, "FloatingTextPrefab", "+1", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            floatingPrefab.rectTransform.sizeDelta = new Vector2(300, 60);
            JabelUIFactory.ApplyOutline(floatingPrefab);
            var spawner = floatingRoot.gameObject.AddComponent<FloatingTextSpawner>();
            spawner.EditorSetup(floatingPrefab, floatingRoot, 140, 40);
            clickButton.gameObject.AddComponent<ClickTextFeedback>().EditorSetup(spawner, "tpl.money", Color.white);

            // Shop
            var shopPanel = JabelUIFactory.CreateImage(root, "ShopPanel", rounded, Panel, true);
            JabelUIFactory.Place(shopPanel.rectTransform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-30, -20), new Vector2(620, 900));
            var shopTitle = JabelUIFactory.CreateText(shopPanel.transform, "Title", "Shop", 44, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(shopTitle.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -10), new Vector2(560, 60));
            shopTitle.gameObject.AddComponent<LocalizedText>().Text = "tpl.shop";
            var list = JabelUIFactory.CreateVerticalScroll(shopPanel.transform, "List");
            JabelUIFactory.Stretch((RectTransform)list.parent.parent, 10, 10, 80, 10);
            var shop = list.gameObject.AddComponent<BuffShopView>();
            shop.EditorSetup(itemPrefab, list, BuffShopView.KindFilter.All, null);

            // Bottom-left buttons
            var save = JabelUIFactory.CreateButton(root, "SaveButton", "Save", rounded, new Color(0.25f, 0.6f, 0.35f), new Vector2(220, 70));
            JabelUIFactory.Place((RectTransform)save.transform, Vector2.zero, Vector2.zero, new Vector2(30, 30), new Vector2(220, 70));
            save.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizedText>().Text = "tpl.save";
            UnityEventTools.AddPersistentListener(save.onClick, manager.SaveNow);

            var reset = JabelUIFactory.CreateButton(root, "NewGameButton", "New game", rounded, new Color(0.65f, 0.25f, 0.25f), new Vector2(220, 70));
            JabelUIFactory.Place((RectTransform)reset.transform, Vector2.zero, Vector2.zero, new Vector2(270, 30), new Vector2(220, 70));
            reset.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizedText>().Text = "tpl.reset";
            UnityEventTools.AddPersistentListener(reset.onClick, manager.DeleteSaveAndRestart);

            var language = JabelUIFactory.CreateButton(root, "LanguageButton", "EN", rounded, new Color(0.3f, 0.35f, 0.5f), new Vector2(110, 70));
            JabelUIFactory.Place((RectTransform)language.transform, Vector2.zero, Vector2.zero, new Vector2(510, 30), new Vector2(110, 70));
            var langButton = language.gameObject.AddComponent<LanguageButton>();
            JabelEditorUtility.Set(langButton, "label", language.GetComponentInChildren<TMP_Text>());

            CreateToasts(root, rounded);
            CreateOfflinePopup(root, rounded);
        }

        /// <summary>Stretches horizontally with margins and sits in a band measured from the top edge.</summary>
        private static void TopBand(RectTransform rect, float left, float right, float top, float height)
        {
            rect.anchorMin = new Vector2(0, 1);
            rect.anchorMax = new Vector2(1, 1);
            rect.pivot = new Vector2(0.5f, 1);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        public static NotificationToastView CreateToasts(Transform root, Sprite rounded)
        {
            var toastRoot = JabelUIFactory.CreateRect("Toasts", root);
            JabelUIFactory.Place(toastRoot, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -130), new Vector2(700, 400));
            var toast = JabelUIFactory.CreateImage(toastRoot, "ToastPrefab", rounded, new Color(0.15f, 0.2f, 0.28f, 0.95f), true);
            JabelUIFactory.Place(toast.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), Vector2.zero, new Vector2(680, 80));
            var text = JabelUIFactory.CreateText(toast.transform, "Text", "Notification", 32, Color.white, TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Stretch(text.rectTransform, 20, 20, 6, 6);
            var view = toastRoot.gameObject.AddComponent<NotificationToastView>();
            JabelEditorUtility.Set(view, "toastPrefab", toast.rectTransform);
            JabelEditorUtility.Set(view, "container", toastRoot);
            return view;
        }

        public static OfflineProgressPopup CreateOfflinePopup(Transform root, Sprite rounded)
        {
            var holder = JabelUIFactory.CreateRect("OfflinePopup", root);
            JabelUIFactory.Stretch(holder);
            var dim = JabelUIFactory.CreateImage(holder, "Backdrop", null, new Color(0, 0, 0, 0.55f));
            JabelUIFactory.Stretch(dim.rectTransform);
            dim.raycastTarget = true;
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            var panel = JabelUIFactory.CreateImage(holder, "Panel", rounded, new Color(0.12f, 0.15f, 0.22f, 1f), true);
            panel.raycastTarget = true;
            JabelUIFactory.Place(panel.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(800, 520));
            var title = JabelUIFactory.CreateText(panel.transform, "Title", "While you were away", 44, new Color(1f, 0.9f, 0.5f), TextAlignmentOptions.Center, FontStyles.Bold);
            JabelUIFactory.Place(title.rectTransform, new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -24), new Vector2(760, 70));
            var body = JabelUIFactory.CreateText(panel.transform, "Body", "+$100", 38, Color.white, TextAlignmentOptions.Center);
            JabelUIFactory.Stretch(body.rectTransform, 30, 30, 110, 130);
            var close = JabelUIFactory.CreateButton(panel.transform, "CollectButton", "Collect", rounded, new Color(0.25f, 0.65f, 0.35f), new Vector2(300, 80), 34);
            JabelUIFactory.Place((RectTransform)close.transform, new Vector2(0.5f, 0), new Vector2(0.5f, 0), new Vector2(0, 30), new Vector2(300, 80));
            close.GetComponentInChildren<TMP_Text>().gameObject.AddComponent<LocalizedText>().Text = "jabel.offline.collect";

            var popup = holder.gameObject.AddComponent<OfflineProgressPopup>();
            JabelEditorUtility.Set(popup, "panel", panel.rectTransform);
            JabelEditorUtility.Set(popup, "title", title);
            JabelEditorUtility.Set(popup, "body", body);
            JabelEditorUtility.Set(popup, "closeButton", close);
            JabelEditorUtility.Set(popup, "backdrop", dimGroup);
            return popup;
        }

        public static void AddSceneToBuild(string scenePath)
        {
            var scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            scenes.RemoveAll(s => s.path == scenePath);
            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
