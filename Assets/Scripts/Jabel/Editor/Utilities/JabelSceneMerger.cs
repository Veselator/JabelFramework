using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Jabel.Editor
{
    /// <summary>
    /// Builds a generated scene without destroying manual work.
    /// The generator always runs into a temporary scene; if the target scene already exists,
    /// only root objects it does not contain yet are moved into it. Existing objects (and every
    /// edit made to them) are kept as they are. References from the newly added objects to generated
    /// objects that were discarded are redirected to their existing counterparts (matched by
    /// hierarchy path and component type), including UnityEvent targets.
    /// </summary>
    public static class JabelSceneMerger
    {
        public sealed class Report
        {
            public bool Created;
            public readonly List<string> Added = new List<string>();
            public readonly List<string> Kept = new List<string>();
            public int RemappedReferences;

            public override string ToString()
            {
                if (Created) return "scene created";
                return $"kept {Kept.Count} existing root object(s), added {Added.Count}" +
                       (Added.Count > 0 ? ": " + string.Join(", ", Added) : string.Empty) +
                       $"; {RemappedReferences} reference(s) linked to existing objects";
            }
        }

        /// <summary>
        /// Runs <paramref name="build"/> (which creates objects in the active scene) and saves the result to
        /// <paramref name="scenePath"/>, creating the scene or updating it additively.
        /// </summary>
        public static Report BuildOrUpdate(string scenePath, Action build, bool fromScratch = false)
        {
            var report = new Report();
            JabelEditorUtility.EnsureFolder(Path.GetDirectoryName(scenePath));

            if (fromScratch || !File.Exists(scenePath))
            {
                var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
                build();
                EditorSceneManager.SaveScene(scene, scenePath);
                report.Created = true;
                return report;
            }

            var target = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);
            var temp = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
            SceneManager.SetActiveScene(temp);
            try
            {
                build();
                Merge(temp, target, report);
            }
            finally
            {
                SceneManager.SetActiveScene(target);
                EditorSceneManager.CloseScene(temp, true);
            }

            EditorSceneManager.MarkSceneDirty(target);
            EditorSceneManager.SaveScene(target);
            return report;
        }

        private static void Merge(Scene source, Scene target, Report report)
        {
            var existing = new Dictionary<string, GameObject>();
            foreach (var root in target.GetRootGameObjects())
                if (!existing.ContainsKey(root.name)) existing.Add(root.name, root);

            // Generated object -> existing counterpart, for every discarded root.
            var map = new Dictionary<Object, Object>();
            var added = new List<GameObject>();
            foreach (var root in source.GetRootGameObjects())
            {
                if (existing.TryGetValue(root.name, out var counterpart))
                {
                    MapHierarchy(root, counterpart, map);
                    report.Kept.Add(root.name);
                }
                else added.Add(root);
            }

            foreach (var root in added)
            {
                SceneManager.MoveGameObjectToScene(root, target);
                report.Added.Add(root.name);
            }

            foreach (var root in added)
            foreach (var component in root.GetComponentsInChildren<Component>(true))
            {
                if (component == null) continue;
                report.RemappedReferences += Remap(component, map);
            }
        }

        /// <summary>Pairs objects by name path and components by type/index. Unmatched parts map to nothing.</summary>
        private static void MapHierarchy(GameObject generated, GameObject kept, Dictionary<Object, Object> map)
        {
            map[generated] = kept;

            var keptComponents = kept.GetComponents<Component>();
            var used = new HashSet<Component>();
            foreach (var component in generated.GetComponents<Component>())
            {
                if (component == null) continue;
                foreach (var candidate in keptComponents)
                {
                    if (candidate == null || used.Contains(candidate) || candidate.GetType() != component.GetType()) continue;
                    map[component] = candidate;
                    used.Add(candidate);
                    break;
                }
            }

            var usedChildren = new HashSet<Transform>();
            foreach (Transform child in generated.transform)
            {
                foreach (Transform candidate in kept.transform)
                {
                    if (usedChildren.Contains(candidate) || candidate.name != child.name) continue;
                    usedChildren.Add(candidate);
                    MapHierarchy(child.gameObject, candidate.gameObject, map);
                    break;
                }
            }
        }

        private static Scene? SceneOf(Object value)
        {
            switch (value)
            {
                case GameObject go: return go.scene;
                case Component c: return c.gameObject.scene;
                default: return null; // assets
            }
        }

        private static int Remap(Component component, Dictionary<Object, Object> map)
        {
            int changed = 0;
            var so = new SerializedObject(component);
            var it = so.GetIterator();
            while (it.Next(true))
            {
                if (it.propertyType != SerializedPropertyType.ObjectReference) continue;
                var value = it.objectReferenceValue;
                if (value == null) continue;
                if (map.TryGetValue(value, out var replacement))
                {
                    it.objectReferenceValue = replacement;
                    changed++;
                }
                else if (SceneOf(value) is Scene scene && scene.IsValid() && scene != component.gameObject.scene)
                {
                    // Points into the discarded generated copy and the user removed its counterpart: clear it.
                    it.objectReferenceValue = null;
                    changed++;
                }
            }
            if (changed > 0) so.ApplyModifiedPropertiesWithoutUndo();
            return changed;
        }
    }
}
