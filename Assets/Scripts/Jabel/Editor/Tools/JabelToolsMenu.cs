using System;
using System.Linq;
using Jabel.Core;
using Jabel.Numbers;
using Jabel.Scripting;
using UnityEditor;
using UnityEngine;

namespace Jabel.Editor
{
    /// <summary>Debug helpers for clicker development: inspect live state, fake time away, wipe saves.</summary>
    public static class JabelToolsMenu
    {
        [MenuItem("Tools/Jabel/Debug/Delete Save (play mode restarts)", priority = 40)]
        public static void DeleteSave()
        {
            if (Application.isPlaying && ClickerManager.Instance != null)
            {
                ClickerManager.Instance.DeleteSaveAndRestart();
                return;
            }
            foreach (var guid in AssetDatabase.FindAssets("t:ClickerConfig"))
            {
                var config = AssetDatabase.LoadAssetAtPath<ClickerConfig>(AssetDatabase.GUIDToAssetPath(guid));
                new Jabel.Save.FileSaveStorage().Delete(config.saveSlot);
                new Jabel.Save.PlayerPrefsSaveStorage().Delete(config.saveSlot);
            }
            Debug.Log("[Jabel] Saves deleted.");
        }

        [MenuItem("Tools/Jabel/Debug/Simulate 1 hour away", priority = 41)]
        public static void SimulateHour() => SimulateAway(3600);

        [MenuItem("Tools/Jabel/Debug/Simulate 8 hours away", priority = 42)]
        public static void SimulateEightHours() => SimulateAway(8 * 3600);

        [MenuItem("Tools/Jabel/Debug/Simulate 1 hour away", true)]
        [MenuItem("Tools/Jabel/Debug/Simulate 8 hours away", true)]
        [MenuItem("Tools/Jabel/Debug/Add 1M money", true)]
        private static bool IsPlaying() => Application.isPlaying && ClickerManager.Instance != null;

        private static void SimulateAway(double seconds) => ClickerManager.Instance.ApplyOfflineProgress(seconds);

        [MenuItem("Tools/Jabel/Debug/Add 1M money", priority = 43)]
        public static void AddMoney() => ClickerManager.Instance.Variables.Add("money", 1_000_000);

        /// <summary>Renders the main camera (with its canvases) to a PNG — store screenshots at any resolution.</summary>
        [MenuItem("Tools/Jabel/Debug/Capture Screenshot (1920x1080)", priority = 60)]
        public static void CaptureFromMenu() => Debug.Log("[Jabel] Screenshot saved: " + Capture("Screenshots/shot_" + DateTime.Now.ToString("yyyyMMdd_HHmmss") + ".png", 1920, 1080));

        public static string Capture(string path, int width, int height)
        {
            var camera = Camera.main;
            if (camera == null) return null;
            var rt = new RenderTexture(width, height, 24);
            var previous = camera.targetTexture;
            camera.targetTexture = rt;
            var request = new UnityEngine.Rendering.RenderPipeline.StandardRequest { destination = rt };
            if (UnityEngine.Rendering.RenderPipeline.SupportsRenderRequest(camera, request))
                UnityEngine.Rendering.RenderPipeline.SubmitRenderRequest(camera, request);
            else camera.Render();
            camera.targetTexture = previous;

            RenderTexture.active = rt;
            var texture = new Texture2D(width, height, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, width, height), 0, 0);
            texture.Apply();
            RenderTexture.active = null;

            string full = System.IO.Path.GetFullPath(path);
            System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(full));
            System.IO.File.WriteAllBytes(full, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
            rt.Release();
            return full;
        }

        [MenuItem("Tools/Jabel/Formula Reference", priority = 21)]
        public static void OpenReference() => EditorWindow.GetWindow<FormulaReferenceWindow>("Jabel Formulas");
    }
}
