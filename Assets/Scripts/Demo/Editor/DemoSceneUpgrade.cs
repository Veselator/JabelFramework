using System.Collections.Generic;
using System.Linq;
using Jabel.Audio;
using Jabel.Editor;
using Jabel.Events;
using Jabel.Localization;
using Jabel.UI;
using UnityEditor;
using UnityEditor.Events;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace OneKMonkeys.Editor
{
    /// <summary>
    /// In-place additions to an existing demo scene (the merger only adds missing root objects):
    /// the settings menu replaces the old language button, and sound cues are assigned to components.
    /// Only empty fields are filled and missing objects created, so manual edits are never overwritten.
    /// Runs after every build (fresh scenes already contain the settings menu and just get their sounds).
    /// </summary>
    public static class DemoSceneUpgrade
    {
        private const string HudName = "HUD";
        private const string TopBarName = "TopBar";

        public static SettingsMenuFactory.Sounds SettingsSounds(DemoAudio.Cues cues) => new SettingsMenuFactory.Sounds
        {
            Open = null,  // the gear button already clicks
            Close = cues.Panel,
            Language = cues.Language,
            Slider = cues.Slider
        };

        public static string Apply(DemoAudio.Cues cues, SettingsMenuFactory.Style style, BuffShopItemView shopItemPrefab)
        {
            var log = new List<string>();
            var scene = SceneManager.GetActiveScene();
            var roots = scene.GetRootGameObjects();

            var hud = roots.FirstOrDefault(r => r.name == HudName && r.GetComponent<Canvas>() != null);
            if (hud == null) log.Add("HUD not found (settings menu skipped)");
            else EnsureSettings(hud.transform, cues, style, log);

            int wired = WireSounds(roots, cues);
            if (wired > 0) log.Add($"{wired} sound slot(s) filled");
            if (WireShopItem(shopItemPrefab, cues)) log.Add("shop item prefab sounds set");

            if (log.Count > 0) UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(scene);
            return log.Count == 0 ? "no upgrades needed" : string.Join(", ", log);
        }

        // ------------------------------------------------------------ settings

        private static void EnsureSettings(Transform hud, DemoAudio.Cues cues, SettingsMenuFactory.Style style, List<string> log)
        {
            var popup = hud.GetComponentsInChildren<PopupPanel>(true).FirstOrDefault(p => p.name == SettingsMenuFactory.PopupName);
            if (popup == null)
            {
                popup = SettingsMenuFactory.CreatePopup(hud, style, SettingsSounds(cues));
                popup.transform.SetAsLastSibling();
                log.Add("settings popup added");
            }

            bool hasButton = hud.GetComponentsInChildren<Button>(true).Any(b => b.name == SettingsMenuFactory.ButtonName);
            if (hasButton) return;

            // Old HUDs had a language button in the top bar: turn it into the settings button,
            // keeping its place, size and color (the user may have adjusted them).
            var language = hud.GetComponentsInChildren<LanguageButton>(true).FirstOrDefault();
            if (language != null)
            {
                var button = language.GetComponent<Button>();
                Object.DestroyImmediate(language);
                var label = button.transform.Find("Label");
                if (label != null) Object.DestroyImmediate(label.gameObject);
                button.name = SettingsMenuFactory.ButtonName;
                SettingsMenuFactory.AddGearIcon(button);
                SettingsMenuFactory.Wire(button, popup, cues.Panel);
                log.Add("language button converted to settings button");
                return;
            }

            var bar = FindDeep(hud, TopBarName);
            var created = SettingsMenuFactory.CreateButton(bar != null ? bar : hud, popup, style, new Vector2(96, 90), cues.Panel);
            JabelUIFactory.Place((RectTransform)created.transform, new Vector2(1, 0.5f), new Vector2(1, 0.5f), new Vector2(-138, 0), new Vector2(96, 90));
            log.Add("settings button added");
        }

        // ------------------------------------------------------------ sounds

        private static int WireSounds(GameObject[] roots, DemoAudio.Cues cues)
        {
            int n = 0;
            foreach (var root in roots)
            {
                foreach (var audio in root.GetComponentsInChildren<JabelAudio>(true))
                {
                    n += Set(audio, "music", cues.Music);
                    n += Set(audio, "defaultClick", cues.UiClick);
                }
                foreach (var area in root.GetComponentsInChildren<ClickArea>(true))
                    n += Set(area, "clickSound", cues.Typing);
                foreach (var computer in root.GetComponentsInChildren<PlayerComputer>(true))
                    n += Set(computer, "openSound", cues.OpenComputer);
                foreach (var slot in root.GetComponentsInChildren<HireSlot>(true))
                {
                    n += Set(slot, "hireSound", cues.Hire);
                    n += Set(slot, "deniedSound", cues.Denied);
                }
                foreach (var row in root.GetComponentsInChildren<MonkeyRow>(true))
                {
                    n += Set(row, "upgradeSound", cues.MonkeyUpgrade);
                    n += Set(row, "deniedSound", cues.Denied);
                }
                foreach (var juice in root.GetComponentsInChildren<ButtonJuice>(true))
                    if (juice.name == "UpgradesButton" || juice.name == SettingsMenuFactory.ButtonName)
                        n += Set(juice, "clickSound", cues.Panel);
                foreach (var popup in root.GetComponentsInChildren<PopupPanel>(true))
                    if (popup.name == SettingsMenuFactory.PopupName)
                        n += Set(popup, "closeSound", cues.Panel);
                foreach (var selector in root.GetComponentsInChildren<LanguageSelector>(true))
                    n += Set(selector, "changeSound", cues.Language);
                foreach (var slider in root.GetComponentsInChildren<VolumeSlider>(true))
                    n += Set(slider, "tickSound", cues.Slider);
                foreach (var listener in root.GetComponentsInChildren<FunctionListener>(true))
                    n += AddLevelUpSound(listener, cues.LevelUp);
            }
            return n;
        }

        /// <summary>Shop items play buy / denied sounds themselves, so their generic button click is muted (first time only).</summary>
        private static bool WireShopItem(BuffShopItemView prefab, DemoAudio.Cues cues)
        {
            if (prefab == null) return false;
            string path = AssetDatabase.GetAssetPath(prefab);
            if (string.IsNullOrEmpty(path)) return false;

            var root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                var view = root.GetComponent<BuffShopItemView>();
                bool firstTime = Set(view, "buySound", cues.BuyUpgrade) > 0;
                bool changed = firstTime | Set(view, "failSound", cues.Denied) > 0;
                if (firstTime)
                    foreach (var juice in root.GetComponentsInChildren<ButtonJuice>(true))
                        JabelEditorUtility.Set(juice, "playClickSound", false);
                if (changed) PrefabUtility.SaveAsPrefabAsset(root, path);
                return changed;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>The "LevelUp" function (called from JabelScript) also plays the level-up cue, once.</summary>
        private static int AddLevelUpSound(FunctionListener listener, SoundCue cue)
        {
            if (cue == null) return 0;
            var so = new SerializedObject(listener);
            if (so.FindProperty("function").stringValue != "LevelUp") return 0;
            var calls = so.FindProperty("onCalled.m_PersistentCalls.m_Calls");
            for (int i = 0; i < calls.arraySize; i++)
                if (calls.GetArrayElementAtIndex(i).FindPropertyRelative("m_Target").objectReferenceValue == cue) return 0;

            var field = typeof(FunctionListener).GetField("onCalled", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            var unityEvent = (UnityEngine.Events.UnityEventBase)field.GetValue(listener);
            UnityEventTools.AddVoidPersistentListener(unityEvent, cue.Play);
            EditorUtility.SetDirty(listener);
            return 1;
        }

        private static int Set(Object target, string field, Object value) =>
            JabelAudioEditorUtility.SetIfEmpty(target, field, value) ? 1 : 0;

        private static Transform FindDeep(Transform parent, string name)
        {
            foreach (Transform child in parent)
            {
                if (child.name == name) return child;
                var found = FindDeep(child, name);
                if (found != null) return found;
            }
            return null;
        }
    }
}
