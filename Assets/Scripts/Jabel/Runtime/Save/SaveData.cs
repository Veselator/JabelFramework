using System;
using System.Collections.Generic;

namespace Jabel.Save
{
    /// <summary>
    /// Root of a save file. Plain JsonUtility-friendly data (lists instead of dictionaries,
    /// numbers as invariant strings so BigNumbers never lose range).
    /// </summary>
    [Serializable]
    public class SaveData
    {
        public int formatVersion = SaveSystem.CurrentFormatVersion;
        /// <summary>Game-defined data version for migrations (ClickerConfig.saveVersion).</summary>
        public int gameVersion;
        public string configId;
        public long savedAtUtcTicks;
        public long firstLaunchUtcTicks;
        public double totalPlaySeconds;
        public long tick;
        public int sessionCount;
        public List<SavedValue> variables = new List<SavedValue>();
        public List<SavedBuff> buffs = new List<SavedBuff>();
        public List<SavedSection> sections = new List<SavedSection>();
    }

    [Serializable]
    public class SavedValue
    {
        public string key;
        public string value;

        public SavedValue() { }
        public SavedValue(string key, string value) { this.key = key; this.value = value; }
    }

    [Serializable]
    public class SavedBuff
    {
        public string id;
        public int count;
        public bool unlocked;
        public long tickCounter;
        public List<SavedInstance> instances = new List<SavedInstance>();
    }

    [Serializable]
    public class SavedInstance
    {
        public int level;
        public long tickCounter;
        public int seed;
        public long activations;
        public List<SavedValue> variables = new List<SavedValue>();
    }

    /// <summary>Opaque JSON owned by an <see cref="ISaveParticipant"/> (game-specific systems).</summary>
    [Serializable]
    public class SavedSection
    {
        public string key;
        public string json;
    }
}
