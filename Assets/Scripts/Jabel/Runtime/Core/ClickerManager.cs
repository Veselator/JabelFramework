using System;
using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Save;
using Jabel.Scripting;
using Jabel.Variables;
using UnityEngine;

namespace Jabel.Core
{
    /// <summary>
    /// Heart of a Jabel game: builds all services from a <see cref="ClickerConfig"/>, runs the tick
    /// loop, loads/saves, applies offline progress and exposes the runtime to scripts and UI.
    /// Put exactly one in a scene.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    [DisallowMultipleComponent]
    [AddComponentMenu("Jabel/Clicker Manager")]
    public class ClickerManager : MonoBehaviour, IJabelRuntime
    {
        public const string LocalValue = "value";
        public const string LocalCritical = "critical";

        [SerializeField] private ClickerConfig config;
        [Tooltip("Load the save on start. Disable to always start fresh (useful while balancing).")]
        [SerializeField] private bool loadSave = true;

        public static ClickerManager Instance { get; private set; }

        /// <summary>Clears static state between play sessions when domain reload is disabled.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics() => Instance = null;

        public ClickerConfig Config => config;
        public IEventBus Events { get; private set; }
        public VariableStore Variables { get; private set; }
        public BuffSystem Buffs { get; private set; }
        public FunctionRegistry Functions { get; private set; }
        public SaveSystem Save { get; private set; }

        public IJabelRuntime Runtime => this;

        public long CurrentTick { get; private set; }
        public float TickDuration => config != null ? config.tickDuration : 0.25f;
        public bool IsReady { get; private set; }
        public bool IsNewGame { get; private set; }
        public double TotalPlaySeconds { get; private set; }
        public int SessionCount { get; private set; }
        public DateTime FirstLaunchUtc { get; private set; }

        /// <summary>How long the player was away before this session (or the last resume).</summary>
        public double SecondsAway { get; private set; }

        private readonly List<Action> _readyCallbacks = new List<Action>();
        private double _tickAccumulator;
        private float _autosaveTimer;
        private DateTime? _pausedAtUtc;
        // Not serialized: after a script hot-reload in play mode the session simply stops instead of crashing.
        [NonSerialized] private bool _started;

        // ------------------------------------------------------------ lifecycle

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogError("[Jabel] Two ClickerManagers in the scene. Destroying the new one.");
                Destroy(gameObject);
                return;
            }
            if (config == null)
            {
                Debug.LogError("[Jabel] ClickerManager has no ClickerConfig assigned.", this);
                enabled = false;
                return;
            }

            Instance = this;
            if (config.localization != null) Loc.Initialize(config.localization);

            Events = new EventBus();
            Variables = new VariableStore(Events, this);
            foreach (var v in config.variables) Variables.Declare(v);
            foreach (var d in config.derivedValues) Variables.DeclareDerived(d);
            foreach (var t in config.tables) JabelTableRegistry.Register(t);

            Functions = new FunctionRegistry(Events);
            Buffs = new BuffSystem(this, Events, config.AllBuffs);
            Save = new SaveSystem(CreateStorage(config.storage), config.saveSlot);

        }

        private void Start()
        {
            if (Instance != this) return;
            StartSession();
        }

        private void StartSession()
        {
            var now = DateTime.UtcNow;
            SaveData data = null;
            bool loaded = loadSave && Save.TryLoad(out data, config.saveVersion);

            if (loaded)
            {
                ApplySave(data);
                IsNewGame = false;
                SecondsAway = Math.Max(0, (now - new DateTime(Math.Max(0, data.savedAtUtcTicks), DateTimeKind.Utc)).TotalSeconds);
                // A clock set backwards must never produce negative progress; one set forward is capped by offlineMaxSeconds.
                if (data.savedAtUtcTicks > now.Ticks) SecondsAway = 0;
            }
            else
            {
                IsNewGame = true;
                FirstLaunchUtc = now;
                Save.ResetParticipants(false);
                SecondsAway = 0;
            }

            SessionCount++;
            Buffs.CheckUnlocks();

            if (IsNewGame) RunScript(config.onNewGame);
            RunScript(config.onStart);

            _started = true;
            IsReady = true;
            Events.Publish(new GameStartedEvent { IsNewGame = IsNewGame });

            foreach (var callback in _readyCallbacks)
            {
                try { callback(); }
                catch (Exception ex) { Debug.LogException(ex); }
            }
            _readyCallbacks.Clear();

            if (!IsNewGame && config.offlineProgress && SecondsAway > 1) ApplyOfflineProgress(SecondsAway);
        }

        /// <summary>Runs the callback once the session has started (immediately if it already has).</summary>
        public void WhenReady(Action callback)
        {
            if (callback == null) return;
            if (IsReady) callback();
            else _readyCallbacks.Add(callback);
        }

        private void Update()
        {
            if (!_started) return;

            float dt = Time.unscaledDeltaTime;
            TotalPlaySeconds += dt;

            _tickAccumulator += Math.Min(dt, 3600);
            long pending = (long)(_tickAccumulator / config.tickDuration);
            if (pending > 0)
            {
                _tickAccumulator -= pending * config.tickDuration;
                int individual = (int)Math.Min(pending, config.maxTicksPerFrame);
                for (int i = 0; i < individual; i++) Step(1, false);
                long rest = pending - individual;
                if (rest > 0) Step(rest, false);
            }

            if (config.autosaveInterval > 0)
            {
                _autosaveTimer += dt;
                if (_autosaveTimer >= config.autosaveInterval) SaveNow();
            }
        }

        /// <summary>One simulation step covering <paramref name="ticks"/> ticks.</summary>
        private void Step(long ticks, bool offline)
        {
            CurrentTick += ticks;
            Variables.ProcessCountdowns(ticks);

            if (!config.onTick.IsEmpty)
            {
                var ctx = JabelContext.Rent(this);
                ctx.Batch = ticks;
                ctx.IsOffline = offline;
                ctx.SetLocal("batch", ticks);
                try { config.onTick.Run(ctx); }
                finally { ctx.Release(); }
            }

            Buffs.Tick(ticks, offline);
            Events.Publish(new TickEvent { Tick = CurrentTick, Batch = ticks, IsOffline = offline });
        }

        private void OnApplicationPause(bool paused)
        {
            if (!_started) return;
            if (paused)
            {
                _pausedAtUtc = DateTime.UtcNow;
                SaveNow();
            }
            else if (_pausedAtUtc.HasValue)
            {
                // Mobile: returning from background is an offline session too.
                double away = (DateTime.UtcNow - _pausedAtUtc.Value).TotalSeconds;
                _pausedAtUtc = null;
                _tickAccumulator = 0;
                if (away > 2 && config.offlineProgress)
                {
                    SecondsAway = away;
                    ApplyOfflineProgress(away);
                }
            }
        }

        private void OnApplicationQuit()
        {
            if (_started) SaveNow();
        }

        private void OnDestroy()
        {
            if (Instance != this) return;
            Instance = null;
            Events?.Clear();
        }

        // ------------------------------------------------------------ offline

        /// <summary>Simulates <paramref name="secondsAway"/> of absence and publishes a report.</summary>
        public void ApplyOfflineProgress(double secondsAway)
        {
            double max = config.offlineMaxSeconds.EvaluateDouble(this, 8 * 3600);
            double efficiency = config.offlineEfficiency.EvaluateDouble(this, 1);
            double simulated = Math.Max(0, Math.Min(secondsAway, max) * efficiency);
            long ticks = (long)(simulated / config.tickDuration);

            var report = new OfflineReport();
            var before = new Dictionary<string, BigNumber>();
            foreach (var def in config.variables)
                if (def.showInOfflineReport) before[def.key] = Variables.Get(def.key);

            Variables.SuppressRateTracking = true;
            try
            {
                if (ticks > 0)
                {
                    int steps = (int)Math.Max(1, Math.Min(config.offlineSimulationSteps, ticks));
                    long perStep = ticks / steps;
                    long remainder = ticks - perStep * steps;
                    for (int i = 0; i < steps; i++) Step(perStep + (i == 0 ? remainder : 0), true);
                }

                var ctx = JabelContext.Rent(this);
                ctx.IsOffline = true;
                ctx.SetLocal("seconds", secondsAway);
                ctx.SetLocal("simulated", simulated);
                try { config.onReturn.Run(ctx); }
                finally { ctx.Release(); }
            }
            finally
            {
                Variables.SuppressRateTracking = false;
            }

            foreach (var def in config.variables)
            {
                if (!def.showInOfflineReport) continue;
                var after = Variables.Get(def.key);
                if (after == before[def.key]) continue;
                report.Entries.Add(new OfflineReport.Entry
                {
                    Key = def.key,
                    DisplayName = def.displayName.IsEmpty ? def.key : def.displayName.Resolve(),
                    Before = before[def.key],
                    After = after,
                    Format = def.format
                });
            }

            if (secondsAway >= config.offlineReportThreshold)
            {
                Events.Publish(new OfflineProgressEvent
                {
                    SecondsAway = secondsAway,
                    SecondsSimulated = simulated,
                    Ticks = ticks,
                    Report = report
                });
            }
        }

        // ------------------------------------------------------------ clicks

        public struct ClickResult
        {
            public BigNumber Value;
            public bool IsCritical;
        }

        /// <summary>
        /// Processes a click with the config's OnClick script (or an override).
        /// Returns the click value reported by the script through local 'value'.
        /// </summary>
        public ClickResult Click(Vector3 worldPosition, Vector2 screenPosition, UnityEngine.Object source = null,
            JabelScript overrideScript = null)
        {
            if (!_started) return default;
            var script = overrideScript != null && !overrideScript.IsEmpty ? overrideScript : config.onClick;

            var ctx = JabelContext.Rent(this);
            ctx.Source = source;
            ctx.Position = worldPosition;
            ctx.HasPosition = true;
            BigNumber value;
            bool critical;
            try
            {
                script.Run(ctx);
                value = ctx.GetLocal(LocalValue, BigNumber.One);
                critical = ctx.GetLocal(LocalCritical).ToBool();
            }
            finally
            {
                ctx.Release();
            }

            Events.Publish(new ClickEvent
            {
                WorldPosition = worldPosition,
                ScreenPosition = screenPosition,
                Value = value,
                IsCritical = critical,
                Source = source
            });
            return new ClickResult { Value = value, IsCritical = critical };
        }

        // ------------------------------------------------------------ scripts & values

        public void RunScript(JabelScript script)
        {
            if (script == null || script.IsEmpty) return;
            var ctx = JabelContext.Rent(this);
            try { script.Run(ctx); }
            finally { ctx.Release(); }
        }

        /// <summary>Global name resolution: variables, derived values, then built-ins.</summary>
        public bool TryResolve(string name, out BigNumber value)
        {
            if (Variables != null && Variables.TryGet(name, out value)) return true;
            switch (name)
            {
                case "tick": value = CurrentTick; return true;
                case "time": value = TotalPlaySeconds; return true;
                case "tickDuration": value = TickDuration; return true;
                case "awaySeconds": value = SecondsAway; return true;
                case "sessions": value = SessionCount; return true;
                case "batch": value = BigNumber.One; return true;
            }
            value = BigNumber.Zero;
            return false;
        }

        public void PlaySound(AudioClip clip, float volume)
        {
            Jabel.Audio.JabelAudio.PlayClip(clip, volume);
        }

        public void RequestSave() => SaveNow();

        // ------------------------------------------------------------ save

        private static ISaveStorage CreateStorage(SaveStorageType type)
        {
            switch (type)
            {
                case SaveStorageType.File: return new FileSaveStorage();
                case SaveStorageType.PlayerPrefs: return new PlayerPrefsSaveStorage();
                default:
                    return Application.platform == RuntimePlatform.WebGLPlayer
                        ? (ISaveStorage)new PlayerPrefsSaveStorage()
                        : new FileSaveStorage();
            }
        }

        public void SaveNow()
        {
            if (!_started) return;
            _autosaveTimer = 0;

            var data = new SaveData
            {
                gameVersion = config.saveVersion,
                configId = config.configId,
                firstLaunchUtcTicks = FirstLaunchUtc.Ticks,
                totalPlaySeconds = TotalPlaySeconds,
                tick = CurrentTick,
                sessionCount = SessionCount
            };
            foreach (var pair in Variables.GetPersistent())
                data.variables.Add(new SavedValue(pair.Key, pair.Value.ToInvariantString()));
            data.buffs = Buffs.Capture();
            Save.CaptureSections(data);
            Save.Write(data);

            Events.Publish(new GameSavedEvent());
        }

        private void ApplySave(SaveData data)
        {
            if (!string.IsNullOrEmpty(data.configId) && data.configId != config.configId)
                Debug.LogWarning($"[Jabel] Save belongs to '{data.configId}', config is '{config.configId}'. Loading anyway.");

            foreach (var v in data.variables)
            {
                // Session variables never reach the save; values of removed variables are kept harmlessly.
                if (BigNumber.TryParse(v.value, out var number)) Variables.Restore(v.key, number);
            }
            Buffs.Restore(data.buffs);
            Save.RestoreSections(data);

            CurrentTick = data.tick;
            TotalPlaySeconds = data.totalPlaySeconds;
            SessionCount = data.sessionCount;
            FirstLaunchUtc = data.firstLaunchUtcTicks > 0 ? new DateTime(data.firstLaunchUtcTicks, DateTimeKind.Utc) : DateTime.UtcNow;
        }

        /// <summary>Prestige: resets Run variables and resettable buffs.</summary>
        public void ResetRun()
        {
            Variables.ResetToInitial(false);
            Buffs.ResetRun();
            Save.ResetParticipants(true);
            Events.Publish(new RunResetEvent());
            SaveNow();
        }

        /// <summary>Wipes the save and reloads the scene (debug / "new game" button).</summary>
        public void DeleteSaveAndRestart()
        {
            _started = false;
            Save.Delete();
            var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
            UnityEngine.SceneManagement.SceneManager.LoadScene(scene.buildIndex >= 0 ? scene.buildIndex : 0);
        }

        /// <summary>Quits the application (saving first). Stops play mode in the editor.</summary>
        public void Quit()
        {
            SaveNow();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

#if UNITY_EDITOR
        public void EditorSetup(ClickerConfig clickerConfig) => config = clickerConfig;
#endif
    }
}
