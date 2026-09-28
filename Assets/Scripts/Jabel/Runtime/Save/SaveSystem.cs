using System;
using System.Collections.Generic;
using Jabel.Numbers;
using UnityEngine;

namespace Jabel.Save
{
    /// <summary>Game-specific system that stores its own state in the save file (e.g. the demo's code cursor).</summary>
    public interface ISaveParticipant
    {
        /// <summary>Unique section name in the save file.</summary>
        string SaveKey { get; }
        string CaptureState();
        void RestoreState(string json);
        /// <summary>Called for a new game or a run reset.</summary>
        void ResetState(bool isRunReset);
    }

    /// <summary>Upgrades old save data after the game's data layout changed.</summary>
    public interface ISaveMigration
    {
        /// <summary>Game version this migration upgrades from.</summary>
        int FromVersion { get; }
        void Migrate(SaveData data);
    }

    /// <summary>Changes of tracked variables while the player was away.</summary>
    public sealed class OfflineReport
    {
        public struct Entry
        {
            public string Key;
            public string DisplayName;
            public BigNumber Before;
            public BigNumber After;
            public NumberFormat Format;
            public BigNumber Delta => After - Before;
        }

        public readonly List<Entry> Entries = new List<Entry>();
    }

    /// <summary>
    /// Serializes the session to text and back. Knows nothing about ClickerManager: it receives
    /// capture/apply delegates, which keeps it testable and reusable.
    /// </summary>
    public sealed class SaveSystem
    {
        public const int CurrentFormatVersion = 1;

        private readonly ISaveStorage _storage;
        private readonly string _slot;
        private readonly List<ISaveParticipant> _participants = new List<ISaveParticipant>();
        private readonly List<ISaveMigration> _migrations = new List<ISaveMigration>();
        private readonly Dictionary<string, string> _pendingSections = new Dictionary<string, string>();

        public SaveSystem(ISaveStorage storage, string slot)
        {
            _storage = storage;
            _slot = string.IsNullOrEmpty(slot) ? "default" : slot;
        }

        public bool HasSave => _storage.Exists(_slot);

        public void AddMigration(ISaveMigration migration) => _migrations.Add(migration);

        /// <summary>
        /// Registers a participant. If a save was already loaded, its section is restored immediately,
        /// so registration order relative to loading does not matter.
        /// </summary>
        public void Register(ISaveParticipant participant)
        {
            if (participant == null || _participants.Contains(participant)) return;
            _participants.Add(participant);
            if (_pendingSections.TryGetValue(participant.SaveKey, out var json))
            {
                _pendingSections.Remove(participant.SaveKey);
                SafeRestore(participant, json);
            }
        }

        public void Unregister(ISaveParticipant participant) => _participants.Remove(participant);

        public IReadOnlyList<ISaveParticipant> Participants => _participants;

        public bool TryLoad(out SaveData data, int gameVersion)
        {
            data = null;
            if (!_storage.TryRead(_slot, out var text)) return false;
            try
            {
                data = JsonUtility.FromJson<SaveData>(text);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Jabel] Save is corrupted and was ignored: {ex.Message}");
                return false;
            }
            if (data == null) return false;

            // Apply migrations in order of version.
            _migrations.Sort((a, b) => a.FromVersion.CompareTo(b.FromVersion));
            foreach (var migration in _migrations)
            {
                if (data.gameVersion != migration.FromVersion) continue;
                migration.Migrate(data);
                data.gameVersion = Math.Max(data.gameVersion, migration.FromVersion + 1);
            }
            if (data.gameVersion < gameVersion) data.gameVersion = gameVersion;
            return true;
        }

        /// <summary>Restores participant sections; unknown sections wait for late registrations.</summary>
        public void RestoreSections(SaveData data)
        {
            _pendingSections.Clear();
            if (data?.sections == null) return;
            foreach (var section in data.sections)
            {
                var participant = _participants.Find(p => p.SaveKey == section.key);
                if (participant != null) SafeRestore(participant, section.json);
                else _pendingSections[section.key] = section.json;
            }
        }

        public void CaptureSections(SaveData data)
        {
            data.sections.Clear();
            foreach (var participant in _participants)
            {
                try
                {
                    data.sections.Add(new SavedSection { key = participant.SaveKey, json = participant.CaptureState() });
                }
                catch (Exception ex)
                {
                    Debug.LogException(ex);
                }
            }
            // Keep sections of participants that are not present in this scene.
            foreach (var pair in _pendingSections)
                data.sections.Add(new SavedSection { key = pair.Key, json = pair.Value });
        }

        public void ResetParticipants(bool isRunReset)
        {
            _pendingSections.Clear();
            foreach (var participant in _participants) participant.ResetState(isRunReset);
        }

        public void Write(SaveData data)
        {
            data.formatVersion = CurrentFormatVersion;
            data.savedAtUtcTicks = DateTime.UtcNow.Ticks;
            _storage.Write(_slot, JsonUtility.ToJson(data));
        }

        public void Delete()
        {
            _storage.Delete(_slot);
            _pendingSections.Clear();
        }

        private static void SafeRestore(ISaveParticipant participant, string json)
        {
            try
            {
                participant.RestoreState(json);
            }
            catch (Exception ex)
            {
                Debug.LogError($"[Jabel] Failed to restore save section '{participant.SaveKey}': {ex.Message}");
            }
        }
    }
}
