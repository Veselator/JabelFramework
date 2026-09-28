# Jabel Framework + «1000 Monkeys»

Unity 6 (URP 2D). The **Jabel** clicker framework and the **1000 Monkeys** demo game live in one project, but in separate assemblies:

| Folder | Assembly | Contents |
|---|---|---|
| `Assets/Scripts/Jabel/Runtime` | `Jabel.Runtime` | the framework (knows nothing about the demo) |
| `Assets/Scripts/Jabel/Editor` | `Jabel.Editor` | JabelScript block editor, scene generator, validator, debug tools |
| `Assets/Scripts/Demo/Runtime` | `OneKMonkeys.Runtime` | demo gameplay systems |
| `Assets/Scripts/Demo/Editor` | `OneKMonkeys.Editor` | demo scene and data generator |
| `Assets/Localization/Jabel`, `.../Demo` | — | CSV translation tables (`key,en,ru`) |
| `Assets/Shaders/Demo` | — | universal monkey shader |
| `Assets/Audio/Music`, `Assets/Audio/SFX` | — | music and click sounds (one folder of variations + a Sound Cue asset per kind of click) |
| `Assets/Data/Demo` | — | demo ScriptableObject data (generated) |

## Quick start

- **Tools → 1000 Monkeys → Build or Update Demo Scene** — builds `Assets/Scenes/Demo/OneKMonkeys.unity` (data, prefabs, scene). If the scene already exists, it is **updated, not recreated**: only missing objects, assets and entries are added. Your scene edits, tuned assets and your own code files are kept. Press Play.
- **Tools → 1000 Monkeys → Rebuild Demo Scene From Scratch** — full regeneration (asks for confirmation, overwrites generated data).
- **Tools → Jabel → Build Clicker Scene** — an empty clicker with a click button, a shop with test items, HUD, toasts and an offline popup.
- **Tools → Jabel → Validate Configs** — checks formulas, unknown names, duplicate ids and missing translations.
- **Tools → Jabel → Formula Reference** — function reference, formula sandbox and live values in Play Mode.
- **Tools → Jabel → Debug** — delete the save, simulate 1 h / 8 h of absence, +1M money, 1920×1080 screenshot.
- Framework unit tests: `Assets/Scripts/Jabel/Tests` (Window → General → Test Runner → EditMode).
- Already generated: `Assets/Scenes/Demo/OneKMonkeys.unity` (first in Build Settings) and `Assets/Scenes/Jabel/ClickerTemplate.unity`.

## Jabel architecture

```
ClickerManager (MB, composition root)
 ├─ EventBus          — typed events: TickEvent, ActiveBuffBoughtEvent, PassiveBuffBoughtEvent,
 │                      AnyBuffBoughtEvent, FunctionCalledEvent (OnFunction), VariableChangedEvent, ClickEvent,
 │                      BuffInstanceCreated/LevelUp, BuffUnlocked, OfflineProgress, Notification, GameStarted...
 ├─ VariableStore     — global variables (BigNumber) + derived values (cached formulas)
 ├─ BuffSystem        — ownership, prices, requirements, leveled instances, active buff ticks
 ├─ FunctionRegistry  — bridge "Call function block → C# handler / FunctionListener / bus"
 ├─ SaveSystem        — JSON saves, backup, migrations, ISaveParticipant for game systems
 └─ Loc               — localization (CSV, plurals, localized number suffixes)

JabelAudio (MB)       — music + pooled 2D sound effects, volumes from AudioVolumes (no AudioMixer: WebGL-safe)
```

- **ClickerConfig** (SO) — the whole game as data: tick length, variables, derived formulas, passive/active buffs, `OnStart`, `OnNewGame`, `OnClick`, `OnTick`, `OnReturn` scripts, save and offline settings.
- **BaseBuff** (SO) → **PassiveBuff** / **ActiveBuff**: icon (optionally one per purchased level), name, tag, description, price (`BuffCost`), limit, requirements, `OnBought`. ActiveBuff adds `period` (ticks, formula) and `OnTick`; optionally **instances**: every unit has its own level, timer, seed and variables (the monkeys).
- All UI components derive from `JabelBehaviour` and bind to the session after loading (`OnBind/OnUnbind`).

### Audio and settings

- **SoundCue** (SO) — one kind of sound with several variations: every play picks a random clip (never the same twice in a row) and a random pitch (default 0.8–1.2), with a minimum interval and a voice limit so fast clicking never turns into noise. The inspector has a preview button.
- **JabelAudio** — music with fade-in and a pool of 2D sources; `JabelAudio.Play(cue)` works from anywhere (a bare instance is created on demand). Every `Button` with `ButtonJuice` plays the default UI click unless it has its own `clickSound`; `ClickArea`, `BuffShopItemView` and `PopupPanel` have their own sound slots; the "Play sound" block accepts a cue.
- **AudioVolumes** — master / music / effects volumes (0..1, squared for a natural loudness curve), saved in PlayerPrefs.
- **Settings popup** (`SettingsMenuFactory`): `PopupPanel` + `LanguageSelector` (‹ language ›) + three `VolumeSlider`s, opened by a gear button.
- Import settings for the web are applied automatically to new clips: `Audio/SFX` — mono, decompressed on load; `Audio/Music` — streamed; WebGL uses AAC (**Tools → Jabel → Audio → Reapply Web Import Settings** re-applies them).

### JabelScript — block programming in the Inspector

A script is a list of blocks (`[SerializeReference]`). In the Inspector they are colored collapsible blocks with ▲▼⧉✕ buttons and a "+ Add block" menu. A new block is a new class deriving from `JabelBlock` with a `[JabelBlock("Category/Name")]` attribute; the editor picks it up automatically.

| Category | Blocks |
|---|---|
| Variables | Set, Modify (+ − × ÷ ^ min max), Declare (local / global / instance) |
| Flow | If, Chance, Repeat, While, Break, Stop, Comment |
| Functions | Call function (named arguments, result into a local), Run script asset (subroutines) |
| Buffs | Grant, Buy, Unlock |
| Game | Reset run (prestige), Save |
| Feedback | Notify (localized toast), Play sound, Log (with `{formulas}`) |

Every numeric field is a **formula** (`JabelFormula`): `10 * 1.15 ^ count('monkey')`, `playerLevel >= 5 ? 2 : 1`, `min(a, b)`, `rate('money')`, `table('curve', level)`. A formula is compiled once and highlighted in red when it has an error.

### Common clicker problems the framework solves

| Problem | Jabel solution |
|---|---|
| double overflow (1e308) and precision | `BigNumber` (mantissa × 10^exp) for all economy values |
| Formatting "1.5M" / "1,5 млн" | `NumberFormatter`: short/scientific/engineering/grouped, suffixes from localization, aa/ab after decillions |
| Exponential prices, "buy x10 / max" | `BuffCost`: geometric progression with a closed-form sum and max-affordable |
| Saves break after rebalancing | derived values are recalculated from `count('id')` instead of being "applied" on purchase |
| Bonus order (+ and ×) | one readable derived formula per stat |
| Offline progress, clock tampering | UTC time, negative-time protection, cap and efficiency are formulas; batched simulation (`batch`), "While you were away" report |
| Returning from background on mobile | `OnApplicationPause` saves and counts the absence as offline time |
| FPS drops / background tab | fixed tick, per-frame tick cap, the remainder is collapsed into a batch |
| Thousands of generators | active buffs run in batches; the demo animates only visible stations |
| File corruption on shutdown | write to a temporary file + `.bak`, fall back on read |
| Temporary boosts "reset" after loading | `countdown` variables (decrease every tick, saved) |
| Progressive shop reveal | `variable ≥ value` requirements with an auto hint, `WhenUnlocked` visibility |
| Typos in data | validator, formula highlighting, translation preview in the Inspector |
| Prestige | `Reset run`: Run variables and buffs are reset, Permanent ones stay |
| Localizing "1 monkey / 2 monkeys / 5 monkeys" | plurals `key#one/#few/#many/#other` following CLDR rules |
| Static state leaking between play sessions (domain reload off) | listeners always unsubscribe; localization skips destroyed listeners and resets on entering Play Mode |

## 1000 Monkeys

- **Main screen**: your computer on the left (click it to open the clicker screen), the row of monkeys on the right, and a "Hire" slot with its price at the end. The camera scrolls by dragging, the mouse wheel, A/D and the scrollbar. The office background is tiled and scrolls with the camera. Top bar: money on the left, level + line progress in the middle, monkey count, upgrades, settings (language, master / music / effects volume), exit.
- **Clicker screen**: a monitor with code; clicking types characters. New characters slide up into place; free characters (spaces, punctuation, line breaks — but never brackets `(){}[]`) cost nothing and appear together with the character typed before them, syntax is highlighted, code scrolls under a mask; particles and `$` fly from the click point proportionally to income. Opening the clicker screen closes the upgrades panel.
- **Economy** (all in `Assets/Data/Demo/ClickerConfig.asset`): $0.1 per character, $10 × level per level-up, lines per level: 20, 50, 125… (×2.5). A basic monkey types 1 character every 20 ticks (tick = 0.25 s). The hire price grows geometrically.
- **Monkeys**: 20 levels, each with its own title and `OneKMonkeys/MonkeyEffects` shader effect (outline, glow, shine, pulse, jelly, hologram, rainbow, sparkles, gold, glitch, chromatic, electricity, fire, ghost, galaxy, dissolve, rainbow outline, halo…), a random color within the level's range, living eyes (blinking, gaze), swaying, breathing, "typing", floating income text. Hovering a monkey shows a tooltip plate with its stats and upgrade price; clicking upgrades it. If the upgrade is not possible (level too low or not enough money), the monkey and desk flash red, scale up and ease back over 0.7 s.
- **Upgrades**: mechanical keyboard, Stack Overflow tab, premium bananas, typewriter (active buff), **big screen** (a wall monitor attached to the camera — you can click it from the main screen), rubber duck (crits), double espresso, code review, autocomplete, open space, git blame (offline), mentoring, Shakespeare mode, **office makeover** (one multi-level upgrade that changes the background from basic grey to the final purple). Many unlock at a certain level.
- **Sound**: background music plus a separate set of click variations for each action — typing (keyboard), UI buttons (mouse), panels (light switch), hiring (power strip), monkey upgrade (ratchet), buying an upgrade (pen), denied (plastic case), language (controller), volume slider, opening the computer (arcade button), plus a level-up jingle (same random pitch). Cues live in `Assets/Audio/SFX/<Kind>.asset`; add clips to a kind's folder and to its cue to get more variety.
- The code database (`Assets/Data/Demo/Code`) holds the source files the monkeys type. The builder merges new files into it and never overwrites files you added.
- All the "code writing logic" (money, lines, level-ups, notifications) is the JabelScript `onWritten` on the **CodeWriter** object; C# only advances the cursor through the code database.
