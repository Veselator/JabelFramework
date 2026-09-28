using System;
using System.IO;
using UnityEngine;

namespace Jabel.Save
{
    /// <summary>Where save text lives. Swap implementations for cloud saves, platforms or tests.</summary>
    public interface ISaveStorage
    {
        bool TryRead(string slot, out string data);
        void Write(string slot, string data);
        void Delete(string slot);
        bool Exists(string slot);
    }

    /// <summary>
    /// File storage with crash safety: writes to a temp file, keeps the previous save as .bak and
    /// falls back to it when the main file is unreadable (power loss mid-write is a real-world
    /// source of "my progress is gone" reviews).
    /// </summary>
    public sealed class FileSaveStorage : ISaveStorage
    {
        private readonly string _directory;

        public FileSaveStorage(string directory = null)
        {
            _directory = directory ?? Application.persistentDataPath;
        }

        private string PathFor(string slot) => Path.Combine(_directory, $"jabel_{slot}.json");

        public bool Exists(string slot) => File.Exists(PathFor(slot)) || File.Exists(PathFor(slot) + ".bak");

        public bool TryRead(string slot, out string data)
        {
            data = null;
            string path = PathFor(slot);
            if (TryReadFile(path, out data)) return true;
            if (TryReadFile(path + ".bak", out data))
            {
                Debug.LogWarning("[Jabel] Main save unreadable, restored from backup.");
                return true;
            }
            return false;
        }

        private static bool TryReadFile(string path, out string data)
        {
            data = null;
            try
            {
                if (!File.Exists(path)) return false;
                data = File.ReadAllText(path);
                // A truncated JSON file is as bad as a missing one.
                return !string.IsNullOrWhiteSpace(data) && data.TrimEnd().EndsWith("}");
            }
            catch (Exception ex)
            {
                Debug.LogWarning($"[Jabel] Could not read save '{path}': {ex.Message}");
                return false;
            }
        }

        public void Write(string slot, string data)
        {
            string path = PathFor(slot);
            string temp = path + ".tmp";
            try
            {
                Directory.CreateDirectory(_directory);
                File.WriteAllText(temp, data);
                if (File.Exists(path))
                {
                    string backup = path + ".bak";
                    if (File.Exists(backup)) File.Delete(backup);
                    File.Move(path, backup);
                }
                File.Move(temp, path);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Jabel] Save failed: {ex.Message}");
            }
        }

        public void Delete(string slot)
        {
            string path = PathFor(slot);
            foreach (var p in new[] { path, path + ".bak", path + ".tmp" })
                if (File.Exists(p)) File.Delete(p);
        }
    }

    /// <summary>PlayerPrefs storage for WebGL and platforms without a writable file system.</summary>
    public sealed class PlayerPrefsSaveStorage : ISaveStorage
    {
        private static string Key(string slot) => "jabel.save." + slot;

        public bool Exists(string slot) => PlayerPrefs.HasKey(Key(slot));

        public bool TryRead(string slot, out string data)
        {
            data = PlayerPrefs.GetString(Key(slot), null);
            return !string.IsNullOrEmpty(data);
        }

        public void Write(string slot, string data)
        {
            PlayerPrefs.SetString(Key(slot), data);
            PlayerPrefs.Save();
        }

        public void Delete(string slot) => PlayerPrefs.DeleteKey(Key(slot));
    }
}
