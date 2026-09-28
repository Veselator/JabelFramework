using System.Collections.Generic;
using Jabel.Buffs;
using Jabel.Core;
using Jabel.Editor;
using Jabel.Events;
using Jabel.Localization;
using Jabel.Numbers;
using Jabel.Scripting;
using Jabel.Scripting.Blocks;
using Jabel.Variables;
using UnityEditor;
using UnityEngine;

namespace OneKMonkeys.Editor
{
    /// <summary>
    /// Creates all data of the "1000 Monkeys" demo: economy, upgrades, monkey levels, code database
    /// and localization. Everything is plain Jabel data — open the assets to tweak balance.
    /// </summary>
    public static class DemoData
    {
        public const string Root = "Assets/Data/Demo";
        public const string CodeFolder = Root + "/Code";
        public const string DemoCsv = "Assets/Localization/Demo/OneKMonkeys.csv";

        // Shared formulas: the buff and the UI must agree on them.
        public const string MonkeyChars = "round(1.75 ^ (level - 1)) + monkeyBonus";
        public const string MonkeyPeriod = "max(2, round(20 * 0.92 ^ (level - 1) * monkeySpeed))";

        /// <summary>Derived value with the index of the office wallpaper (0 = default).</summary>
        public const string OfficeStyleKey = "officeStyle";

        /// <summary>Levels of the office makeover upgrade (one per wallpaper after the default grey one).</summary>
        public const int OfficeLevels = 3;

        /// <summary>
        /// Code files shipped with the demo, in writing order. Existing database entries (including files
        /// added by hand in the inspector) are always kept; missing ones from this list are appended.
        /// </summary>
        public static readonly (string file, string display)[] CodeFiles =
        {
            ("hello_world.c.txt", "hello_world.c"),
            ("banana_sort.py.txt", "banana_sort.py"),
            ("Typewriter.cs.txt", "Typewriter.cs"),
            ("office_manager.js.txt", "office_manager.js"),
            ("theorem.rs.txt", "theorem.rs"),
            ("war_and_peace.txt", "war_and_peace.txt"),
            ("banana_inventory.sql.txt", "banana_inventory.sql"),
            ("MonkeyScheduler.java.txt", "MonkeyScheduler.java"),
            ("keyboard_driver.c.txt", "keyboard_driver.c"),
            ("jungle_router.go.txt", "jungle_router.go"),
            ("TypingStats.kt.txt", "TypingStats.kt"),
            ("coffee_machine.lua.txt", "coffee_machine.lua"),
            ("PayrollService.ts.txt", "PayrollService.ts"),
            ("vine_graph.cpp.txt", "vine_graph.cpp"),
            ("MonkeyBehaviour.cs.txt", "MonkeyBehaviour.cs"),
            ("shakespeare_detector.py.txt", "shakespeare_detector.py"),
            ("banana_market.rb.txt", "banana_market.rb"),
            ("OfficeSlack.swift.txt", "OfficeSlack.swift"),
            ("deploy_bananas.sh.txt", "deploy_bananas.sh"),
            ("typing_pool.ex.txt", "typing_pool.ex"),
            ("Tree.hs.txt", "Tree.hs"),
            ("ticket_desk.php.txt", "ticket_desk.php"),
            ("canopy_sim.rs.txt", "canopy_sim.rs"),
            ("monkey_ai.gd.txt", "monkey_ai.gd"),
            ("ReviewBot.scala.txt", "ReviewBot.scala"),
            ("RenderMonkey.hlsl.txt", "RenderMonkey.hlsl"),
        };
        public sealed class Result
        {
            public ClickerConfig Config;
            public ActiveBuff Monkey;
            public MonkeyLevelTable Levels;
            public CodeDatabase Code;
            public JabelScript OnWritten;
        }

        /// <summary>
        /// Creates the demo data. With <paramref name="overwrite"/> = false (update mode) existing assets are kept
        /// exactly as they are (balance tweaks survive); only missing assets are created and missing entries
        /// (variables, derived values, buffs, tables) are added to the config.
        /// </summary>
        public static Result Create(IDictionary<string, Sprite> sprites, bool overwrite = true)
        {
            var result = new Result();

            // Returns the asset; 'fresh' tells whether it must be (re)configured.
            T Asset<T>(string path, out bool fresh) where T : ScriptableObject
            {
                if (overwrite)
                {
                    fresh = true;
                    return JabelEditorUtility.CreateOrReplace<T>(path);
                }
                return JabelEditorUtility.LoadOrCreate<T>(path, out fresh);
            }
            Sprite Icon(string n) => sprites.TryGetValue(n, out var s) ? s : null;
            var dollar = Icon("OneKMonkeys_8");
            var arrow = Icon("OneKMonkeys_9");
            var monkeyIcon = Icon("OneKMonkeys_7");
            var deskIcon = Icon("OneKMonkeys_1");
            var screenIcon = Icon("OneKMonkeys_0");

            // ---------------------------------------------------------- localization
            var loc = JabelEditorUtility.LoadOrCreate<LocalizationDatabase>(Root + "/Localization.asset", out bool locFresh);
            var requiredTables = new List<TextAsset>
            {
                AssetDatabase.LoadAssetAtPath<TextAsset>(ClickerSceneBuilder.FrameworkCsv),
                AssetDatabase.LoadAssetAtPath<TextAsset>(DemoCsv)
            };
            if (overwrite || locFresh)
            {
                loc.EditorSetup(new List<LanguageInfo>
                    {
                        new LanguageInfo { code = "en", nativeName = "English", systemLanguages = new List<SystemLanguage> { SystemLanguage.English } },
                        new LanguageInfo { code = "ru", nativeName = "Русский", systemLanguages = new List<SystemLanguage> { SystemLanguage.Russian, SystemLanguage.Belarusian, SystemLanguage.Ukrainian } }
                    }, "en", requiredTables);
                // The game always starts in English; players switch in the settings menu (their choice is remembered).
                JabelEditorUtility.Set(loc, "detectSystemLanguage", false);
            }
            else
            {
                // Keep languages and extra tables added by hand; just make sure ours are there.
                var tables = new List<TextAsset>(loc.Tables);
                foreach (var table in requiredTables)
                    if (table != null && !tables.Contains(table)) tables.Add(table);
                loc.EditorSetup(new List<LanguageInfo>(loc.Languages), loc.DefaultLanguage, tables);
            }
            EditorUtility.SetDirty(loc);

            // ---------------------------------------------------------- code
            var code = JabelEditorUtility.LoadOrCreate<CodeDatabase>(Root + "/CodeDatabase.asset", out _);
            // Merge, never replace: entries added by hand (other files, custom names, order) are preserved.
            var files = new List<CodeDatabase.CodeFile>();
            foreach (var entry in code.Files)
                if (entry != null && entry.content != null) files.Add(entry);
            foreach (var (file, display) in CodeFiles)
            {
                var asset = AssetDatabase.LoadAssetAtPath<TextAsset>(CodeFolder + "/" + file);
                if (asset == null || files.Exists(f => f.content == asset)) continue;
                files.Add(new CodeDatabase.CodeFile { fileName = display, content = asset });
            }
            code.EditorSetup(files);
            EditorUtility.SetDirty(code);
            result.Code = code;

            // ---------------------------------------------------------- monkey levels
            result.Levels = CreateLevels(overwrite);

            // ---------------------------------------------------------- buffs
            string b = Root + "/Buffs/";
            var req = new System.Func<int, List<BuffRequirement>>(level =>
                new List<BuffRequirement> { new BuffRequirement { variable = "playerLevel", minimum = level } });

            PassiveBuff Passive(string id, string file, string key, Sprite icon, int order, double baseCost, double growth, int max, int level)
            {
                var buff = Asset<PassiveBuff>(b + file + ".asset", out bool fresh);
                if (!fresh) return buff;
                buff.EditorSetup(id, icon, $"up.{key}.name", $"up.{key}.desc", "upgrade", order,
                    new BuffCost { baseCost = baseCost, growth = growth }, max, BuffVisibility.Always, req(level), null);
                EditorUtility.SetDirty(buff);
                return buff;
            }

            var keyboard = Passive("mech_keyboard", "MechanicalKeyboard", "keyboard", arrow, 0, 5, 1.6, 15, 1);
            var stack = Passive("stack_overflow", "StackOverflow", "stack", dollar, 1, 25, 4, 8, 2);
            var bananas = Passive("bananas", "PremiumBananas", "bananas", monkeyIcon, 2, 40, 3.5, 6, 2);
            var bigScreen = Passive("big_screen", "BigScreen", "bigscreen", screenIcon, 4, 60, 1, 1, 3);
            var duck = Passive("rubber_duck", "RubberDuck", "duck", arrow, 5, 120, 5, 5, 3);
            var coffee = Passive("coffee", "DoubleEspresso", "coffee", arrow, 6, 150, 12, 5, 3);
            var review = Passive("code_review", "CodeReview", "review", dollar, 7, 200, 6, 5, 4);
            var autocomplete = Passive("autocomplete", "Autocomplete", "autocomplete", monkeyIcon, 8, 300, 2.2, 20, 5);
            var openSpace = Passive("open_space", "OpenSpace", "openspace", deskIcon, 9, 500, 4, 5, 6);
            var gitBlame = Passive("git_blame", "GitBlame", "gitblame", arrow, 10, 1000, 5, 5, 7);
            var mentor = Passive("mentor", "Mentorship", "mentor", monkeyIcon, 11, 2500, 5, 5, 8);
            var shakespeare = Passive("shakespeare", "ShakespeareMode", "shakespeare", dollar, 12, 50000, 25, 3, 10);

            // Office makeover: ONE upgrade with 3 levels (grey -> yellow -> aqua -> purple), each +25% money.
            // Its owned count is the wallpaper index (officeStyle). Level N needs player level 3N.
            var office = Asset<PassiveBuff>(b + "OfficeMakeover.asset", out bool officeFresh);
            if (officeFresh)
            {
            office.EditorSetup("office_style", Icon("OneKMonkeys_11"), "up.office.name", "up.office.desc", "upgrade", 13,
                new BuffCost { mode = CostMode.Formula, formula = new JabelFormula("n == 0 ? 150 : n == 1 ? 5000 : 250000") },
                OfficeLevels, BuffVisibility.Always, req(3), null,
                "playerLevel >= 3 * (count('office_style') + 1)", "up.office.req");
            JabelEditorUtility.Set(office, "conditionHintArgument", new JabelFormula("3 * (count('office_style') + 1)"));
            // The shop shows the wallpaper the next level will give (the last one once maxed out).
            var officeIcons = new SerializedObject(office);
            var iconList = officeIcons.FindProperty("iconsByCount");
            iconList.arraySize = OfficeLevels + 1;
            string[] preview = { "OneKMonkeys_11", "OneKMonkeys_12", "OneKMonkeys_13", "OneKMonkeys_13" };
            for (int i = 0; i < preview.Length; i++) iconList.GetArrayElementAtIndex(i).objectReferenceValue = Icon(preview[i]);
            officeIcons.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(office);
            }

            // Old per-wallpaper upgrades (previous versions of the demo) are removed.
            foreach (var old in new[] { "OfficeYellow", "OfficeCyan", "OfficePurple" })
                AssetDatabase.DeleteAsset(b + old + ".asset");

            // The typewriter is an ActiveBuff without instances: one timer, output scales with 'count'.
            var typewriter = Asset<ActiveBuff>(b + "Typewriter.asset", out bool typewriterFresh);
            if (typewriterFresh)
            {
            typewriter.EditorSetup("typewriter", deskIcon, "up.typewriter.name", "up.typewriter.desc", "upgrade", 3,
                new BuffCost { baseCost = 20, growth = 1.35 }, 25, BuffVisibility.Always, req(2), null);
            typewriter.EditorSetupActive("4",
                new JabelScript(new CallFunctionBlock("WriteCode", new FunctionArgument("amount", "count", scaleWithBatch: true))),
                false, 1, null, null, null, null);
            EditorUtility.SetDirty(typewriter);
            }

            // Monkeys: instanced — every monkey has its own level, timer, colour seed.
            var monkey = Asset<ActiveBuff>(b + "Monkey.asset", out bool monkeyFresh);
            if (monkeyFresh)
            {
            monkey.EditorSetup("monkey", monkeyIcon, "monkey.hire", "monkey.hire", "monkey", 100,
                new BuffCost { baseCost = 1, growth = 1.035, multiplier = new JabelFormula("monkeyDiscount") },
                1000, BuffVisibility.Hidden, null, null);
            monkey.EditorSetupActive(MonkeyPeriod,
                new JabelScript(new CallFunctionBlock("WriteCode", new FunctionArgument("amount", MonkeyChars, scaleWithBatch: true))),
                true, 20,
                new BuffCost { mode = CostMode.Formula, formula = new JabelFormula("3 * 2.6 ^ level * upgradeDiscount") },
                "playerLevel >= level + 1", "monkey.req.level", null);
            EditorUtility.SetDirty(monkey);
            }
            result.Monkey = monkey;

            // ---------------------------------------------------------- config
            var config = Asset<ClickerConfig>(Root + "/ClickerConfig.asset", out bool configFresh);
            var allPassive = new List<PassiveBuff>
            {
                keyboard, stack, bananas, bigScreen, duck, coffee, review, autocomplete, openSpace, gitBlame, mentor, shakespeare,
                office
            };
            var allActive = new List<ActiveBuff> { typewriter, monkey };
            if (!configFresh)
            {
                MergeConfig(config, loc, allPassive, allActive);
                result.Config = config;
                result.OnWritten = OnWrittenScript();
                AssetDatabase.SaveAssets();
                return result;
            }
            config.configId = "one_k_monkeys";
            config.saveSlot = config.configId;
            config.saveVersion = 1;
            config.tickDuration = 0.25f;
            config.localization = loc;
            config.variables = DefaultVariables();
            config.derivedValues = DefaultDerived();
            EditorUtility.SetDirty(config);
            ConfigureScripts(config);
            result.Config = config;
            result.OnWritten = OnWrittenScript();
            AssetDatabase.SaveAssets();
            return result;
        }

        private static List<VariableDefinition> DefaultVariables() => new List<VariableDefinition>
        {
            new VariableDefinition { key = "money", displayName = "var.money", format = NumberFormat.Money, showInOfflineReport = true },
            new VariableDefinition { key = "playerLevel", displayName = "var.playerLevel", initialValue = 1, showInOfflineReport = true },
            new VariableDefinition { key = "linesThisLevel" },
            new VariableDefinition { key = "totalChars", displayName = "var.totalChars", persistence = VariablePersistence.Permanent, showInOfflineReport = true },
            new VariableDefinition { key = "totalLines", displayName = "var.totalLines", persistence = VariablePersistence.Permanent, showInOfflineReport = true }
        };

        private static List<DerivedValueDefinition> DefaultDerived() => new List<DerivedValueDefinition>
            {
                Derived("clickPower", "(1 + count('mech_keyboard')) * 2 ^ count('coffee')"),
                Derived("critChance", "0.05 * count('rubber_duck')"),
                Derived("officeComfort", "1 + 0.25 * count('office_style')"),
                Derived(OfficeStyleKey, "count('office_style')"),
                Derived("incomePerChar", "0.1 * 1.5 ^ count('stack_overflow') * 2 ^ count('shakespeare') * officeComfort"),
                Derived("levelReward", "10 * playerLevel * 2 ^ count('code_review') * 2 ^ count('shakespeare')"),
                Derived("linesForNextLevel", "floor(20 * 2.5 ^ (playerLevel - 1))"),
                Derived("monkeySpeed", "0.9 ^ count('bananas')"),
                Derived("monkeyBonus", "count('autocomplete')"),
                Derived("monkeyDiscount", "0.9 ^ count('open_space')"),
                Derived("upgradeDiscount", "0.85 ^ count('mentor')")
            };

        /// <summary>Update mode: adds what is missing, never changes or removes what the user has.</summary>
        private static void MergeConfig(ClickerConfig config, LocalizationDatabase loc, List<PassiveBuff> passive, List<ActiveBuff> active)
        {
            if (config.localization == null) config.localization = loc;
            // Deleted assets (e.g. buffs replaced in a newer demo version) leave empty slots behind.
            config.passiveBuffs.RemoveAll(buff => buff == null);
            config.activeBuffs.RemoveAll(buff => buff == null);
            foreach (var buff in passive)
                if (!config.passiveBuffs.Contains(buff)) config.passiveBuffs.Add(buff);
            foreach (var buff in active)
                if (!config.activeBuffs.Contains(buff)) config.activeBuffs.Add(buff);
            foreach (var variable in DefaultVariables())
                if (!config.variables.Exists(v => v.key == variable.key)) config.variables.Add(variable);
            foreach (var derived in DefaultDerived())
                if (!config.derivedValues.Exists(d => d.key == derived.key)) config.derivedValues.Add(derived);
            EditorUtility.SetDirty(config);
        }

        private static void ConfigureScripts(ClickerConfig config)
        {

            // Click: power (maybe critical x10) goes into the shared program; the income becomes 'value' for feedback.
            config.onClick = new JabelScript(
                new SetVariableBlock(VariableScope.Local, "power", "clickPower"),
                new ChanceBlock("critChance", new JabelScript(
                    new ModifyVariableBlock(VariableScope.Local, "power", ModifyOperation.Multiply, "10"),
                    new SetVariableBlock(VariableScope.Local, ClickerManager.LocalCritical, "1"),
                    new NotifyBlock("notify.critical", NotificationStyle.Achievement))),
                new CallFunctionBlock("WriteCode", new FunctionArgument("amount", "power")) { resultLocal = ClickerManager.LocalValue });
            config.onNewGame = new JabelScript(new NotifyBlock("notify.welcome", NotificationStyle.Info));
            config.onStart = new JabelScript();
            config.onTick = new JabelScript();
            config.onReturn = new JabelScript(new CommentBlock { text = "The offline popup reports the gains; add a welcome-back bonus here if you like." });
            config.offlineProgress = true;
            config.offlineEfficiency = new JabelFormula("0.5 + 0.1 * count('git_blame')");
            config.offlineMaxSeconds = new JabelFormula("(4 + count('git_blame')) * 3600");
            EditorUtility.SetDirty(config);
        }

        /// <summary>What happens when code is written (clicks, typewriter, monkeys, offline): pure JabelScript.</summary>
        private static JabelScript OnWrittenScript() =>
            new JabelScript(
                new ModifyVariableBlock(VariableScope.Global, "money", ModifyOperation.Add, "chars * incomePerChar") { scaleWithBatch = false },
                new ModifyVariableBlock(VariableScope.Global, "totalChars", ModifyOperation.Add, "chars") { scaleWithBatch = false },
                new ModifyVariableBlock(VariableScope.Global, "totalLines", ModifyOperation.Add, "lines") { scaleWithBatch = false },
                new ModifyVariableBlock(VariableScope.Global, "linesThisLevel", ModifyOperation.Add, "lines") { scaleWithBatch = false },
                new WhileBlock("linesThisLevel >= linesForNextLevel", new JabelScript(
                    new ModifyVariableBlock(VariableScope.Global, "linesThisLevel", ModifyOperation.Subtract, "linesForNextLevel") { scaleWithBatch = false },
                    new SetVariableBlock(VariableScope.Local, "reward", "levelReward"),
                    new ModifyVariableBlock(VariableScope.Global, "money", ModifyOperation.Add, "reward") { scaleWithBatch = false },
                    new ModifyVariableBlock(VariableScope.Global, "playerLevel", ModifyOperation.Add, "1") { scaleWithBatch = false },
                    new NotifyBlock("notify.levelup", NotificationStyle.Success, "playerLevel", "reward"),
                    new CallFunctionBlock("LevelUp", new FunctionArgument("level", "playerLevel")))));

        private static DerivedValueDefinition Derived(string key, string formula) =>
            new DerivedValueDefinition { key = key, formula = new JabelFormula(formula) };

        private static MonkeyLevelTable CreateLevels(bool overwrite)
        {
            var table = JabelEditorUtility.LoadOrCreate<MonkeyLevelTable>(Root + "/MonkeyLevels.asset", out bool created);
            if (!overwrite && !created) return table;

            // Each level: its own effect, colours drift from natural fur to vivid and mythical.
            var defs = new (MonkeyFx fx, Color a, Color b, Color glow, float energy)[]
            {
                (MonkeyFx.None, C(0.55f, 0.38f, 0.25f), C(0.72f, 0.55f, 0.36f), C(1, 1, 1), 0.8f),
                (MonkeyFx.Outline, C(0.62f, 0.45f, 0.28f), C(0.85f, 0.65f, 0.4f), C(1, 1, 1), 0.9f),
                (MonkeyFx.Glow, C(0.9f, 0.7f, 0.45f), C(1f, 0.85f, 0.6f), C(1f, 0.9f, 0.5f), 1f),
                (MonkeyFx.Shine, C(0.95f, 0.55f, 0.35f), C(1f, 0.7f, 0.5f), C(1, 1, 1), 1f),
                (MonkeyFx.Pulse, C(0.4f, 0.75f, 0.45f), C(0.55f, 0.95f, 0.6f), C(0.5f, 1f, 0.6f), 1.05f),
                (MonkeyFx.Wobble, C(0.45f, 0.6f, 0.95f), C(0.6f, 0.75f, 1f), C(0.5f, 0.7f, 1f), 1.1f),
                (MonkeyFx.Hologram, C(0.3f, 0.9f, 0.7f), C(0.45f, 1f, 0.85f), C(0.3f, 1f, 0.7f), 1.1f),
                (MonkeyFx.Rainbow, C(1f, 0.6f, 0.8f), C(0.8f, 0.6f, 1f), C(1f, 0.6f, 1f), 1.15f),
                (MonkeyFx.Sparkles, C(0.25f, 0.25f, 0.35f), C(0.4f, 0.4f, 0.55f), C(0.8f, 0.8f, 1f), 1.2f),
                (MonkeyFx.Gold, C(1f, 0.85f, 0.4f), C(1f, 0.95f, 0.6f), C(1f, 0.85f, 0.3f), 1.2f),
                (MonkeyFx.Glitch, C(0.9f, 0.3f, 0.6f), C(0.4f, 0.9f, 1f), C(1f, 0.3f, 0.8f), 1.25f),
                (MonkeyFx.Chromatic, C(0.85f, 0.85f, 0.9f), C(1f, 1f, 1f), C(0.6f, 0.9f, 1f), 1.25f),
                (MonkeyFx.Electric, C(0.3f, 0.45f, 0.9f), C(0.5f, 0.65f, 1f), C(0.4f, 0.8f, 1f), 1.3f),
                (MonkeyFx.Fire, C(0.9f, 0.35f, 0.15f), C(1f, 0.55f, 0.2f), C(1f, 0.5f, 0.1f), 1.35f),
                (MonkeyFx.Ghost, C(0.75f, 0.85f, 1f), C(0.9f, 0.95f, 1f), C(0.7f, 0.85f, 1f), 1.35f),
                (MonkeyFx.Galaxy, C(0.5f, 0.3f, 0.8f), C(0.7f, 0.45f, 1f), C(0.8f, 0.5f, 1f), 1.4f),
                (MonkeyFx.Dissolve, C(0.2f, 0.8f, 0.9f), C(0.4f, 1f, 1f), C(0.3f, 1f, 1f), 1.4f),
                (MonkeyFx.RainbowOutline | MonkeyFx.Shine, C(0.95f, 0.95f, 0.95f), C(1f, 1f, 1f), C(1, 1, 1), 1.45f),
                (MonkeyFx.Halo | MonkeyFx.InnerGlow, C(1f, 0.95f, 0.8f), C(1f, 1f, 0.9f), C(1f, 0.9f, 0.5f), 1.5f),
                (MonkeyFx.Gold | MonkeyFx.Halo | MonkeyFx.Glow | MonkeyFx.Sparkles, C(1f, 0.9f, 0.5f), C(1f, 1f, 0.7f), C(1f, 0.85f, 0.35f), 1.6f)
            };

            var levels = new List<MonkeyLevelTable.Level>();
            for (int i = 0; i < defs.Length; i++)
            {
                levels.Add(new MonkeyLevelTable.Level
                {
                    title = new LocalizedString($"monkey.level.{i + 1}"),
                    effects = defs[i].fx,
                    colorA = defs[i].a,
                    colorB = defs[i].b,
                    glowColor = defs[i].glow,
                    energy = defs[i].energy
                });
            }
            table.EditorSetup(levels);
            EditorUtility.SetDirty(table);
            return table;
        }

        private static Color C(float r, float g, float b) => new Color(r, g, b, 1);
    }
}
