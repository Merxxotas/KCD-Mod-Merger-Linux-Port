# KCDMerge

[![Build Status](https://github.com/Za-Pa-Al/KCDMerge/actions/workflows/build.yaml/badge.svg)](https://github.com/Za-Pa-Al/KCDMerge/actions/workflows/build.yaml)
[![Latest Release](https://img.shields.io/github/v/release/Za-Pa-Al/KCDMerge?label=release)](https://github.com/Za-Pa-Al/KCDMerge/releases)
[![Downloads](https://img.shields.io/github/downloads/Za-Pa-Al/KCDMerge/total?label=downloads)](https://github.com/Za-Pa-Al/KCDMerge/releases)
[![License](https://img.shields.io/badge/license-Custom%20License-red)](https://github.com/Za-Pa-Al/KCDMerge/blob/main/LICENSE)

**Automatic mod merger for Kingdom Come: Deliverance**

---

## What Does KCDMerge Do?

When you install multiple mods that change the same game files, only the last mod's version loads. All other mods' changes are lost. KCDMerge fixes this by intelligently combining changes from all your mods into a single merged mod.

**Without KCDMerge:**
- Install "Better Archery" + "Better Stamina" + "Unlimited Weight" — only one mod's `rpg_param.xml` loads

**With KCDMerge:**
- All three mods' changes to `rpg_param.xml` are merged together and work simultaneously

### How KCDMerge Works

```
📁 Mods Folder                                        🎮 Vanilla Game (baseline)
  ├── 🗂 Mod A  ─┐  Data/*.pak                         ├── Data/*.pak
  ├── 🗂 Mod B  ─┤  Localization/*.pak                 ├── Localization/*.pak
  ├── 🗂 Mod C  ─┘  *.cfg (loose)                      └── (used for diffing)
  └── 📄 mod_order.txt  (optional priority)
                              │
                              ▼  Scan .pak files, loose files & .cfg files
                              │
      ┌───────────────────┼──────────────────┬──────────────────┬──────────────────┐
      │                   │                  │                  │                  │
      ▼                   ▼                  ▼                  ▼                  ▼
┌────────────┐    ┌──────────────┐    ┌──────────────┐  ┌──────────────┐  ┌──────────────┐
│ 📄 XML     │    │ 📄 PTF XML   │    │ 🌐 Local.    │  │ 🖼 Assets     │  │ ⚙️ .cfg       │
│ Tables     │    │ (Patch Files)│    │ XML Tables   │  │ (non-XML)    │  │ Files        │
│            │    │              │    │              │  │              │  │              │
│ Full table │    │ Double under-│    │ From language│  │ Textures,    │  │ variable =   │
│ replaces   │    │ score in name│    │ paks, e.g.   │  │ LUA, sounds, │  │ value pairs  │
│ vanilla    │    │ e.g.         │    │ english_xml  │  │ meshes, etc. │  │              │
│            │    │ buff__mod    │    │              │  │              │  │ Merged into  │
│            │    │ Only patches │    │ Keyed by 1st │  │ Cannot be    │  │ game's       │
│            │    │ matching rows│    │ Cell value   │  │ merged       │  │ user.cfg     │
└─────┬──────┘    └──────┬───────┘    └──────┬───────┘  └──────┬───────┘  └──────┬───────┘
      │                  │                   │                 │                  │
      ▼                  ▼                   ▼                 ▼                  ▼
 Diff each mod      Removals are        Merge per         First mod         For each var:
 vs. vanilla:       ignored (partial    language:         extracts file     New? → add
 compute delta      file, not real      same key =        to staging       Same? → skip
      │             deletions)          compare cells          │           Different? ▼
      ▼                  │                   │                 │                  │
┌────────────────────┐   │              ⚠️ Same key,      ⚠️ Same file     ⚠️ Conflict:
│ Delta Merge:       │   │              different text?   from 2+ mods?    2+ mods set
│                    │   │               ┌───┴───┐         ┌───┴───┐      different value
│ Different rows     │   │              Yes     No        Yes     No           │
│  └► ✅ Auto-merge  │   │               │  (auto-add)     │  (just extract)  │
│                    │   │               │                 │              Check saved
│ Same row,          │   │        Ask User (once)    Check saved pref    CfgConflicts
│ different attrs    │   │        saved to           in ModConflict      in ModConflict
│  └► ✅ Auto-merge  │   │        ModConflictRules   Rules.yaml          Rules.yaml
│                    │   │        .yaml (XmlConflicts)     │                  │
│ Same row,          │   │               │           ┌─────┴─────┐     ┌─────┴─────┐
│ same attribute     │   │               │         Saved?     No saved Saved?    No saved
│  └► ⚠️ Conflict    │   │               │           │        rule?     │        rule?
│     Ask User once  │   │               │       Use saved       │  Use saved       │
│     saved to       │   │               │       preference  Ask User preference Ask User
│     ModConflict    │   │               │           │     AssetConflicts │     CfgConflicts
│     Rules.yaml     │   │               │           │          │        │          │
│     (XmlConflicts) │   │               │           └────┬─────┘        └────┬─────┘
│                    │   │               │                │                   │
│ ID-only / junction │   │               │                │                   │
│ table? ──► ⚠️      │   │               │                │                   │
│  └► IncludeAll     │   │               │                │                   │
│     AdditionsOnly  │   │               │                │                   │
│     Skip           │   │               │                │                   │
│     (saved to      │   │               │                │                   │
│     RowAddition    │   │               │                │                   │
│     Rules)         │   │               │                │                   │
└─────┬──────────────┘   │               │                │                   │
      └─────────────────┴───────────┬────┴────────────────┘                   │
                                    │                                         │
                                    ▼                                         ▼
       ┌─────────────────────────────────────────────────┐    ┌───────────────────────────┐
       │ 📦 Output: KCDMerge/                             │    │ ⚙️ Game Root:              │
       │ ├── 📄 mod.manifest                              │    │ └── user.cfg (merged)     │
       │ ├── 📁 Data/                                     │    │     backed up as:         │
       │ │   └── KCDMerge.pak                             │    │     user.cfg.(timestamp)  │
       │ │       ├── merged XML tables                    │    │     .backup               │
       │ │       ├── PTF patches applied on merged base   │    │     (keeps last 5)        │
       │ │       └── winning assets (textures, LUA, etc.) │    └───────────────────────────┘
       │ └── 📁 Localization/                             │
       │     ├── English_xml.pak  (merged per language)   │
       │     ├── German_xml.pak                           │
       │     └── ...                                      │
       └─────────────────────────────────────────────────┘
                                    │
                                    ▼
                 🎮 KCDMerge loads last → all mods work together

          All conflict decisions are saved and reused on future runs.
          Delete entries from ModConflictRules.yaml or config.yaml to re-decide.
```

---

## Getting Started

### Prerequisites

- Kingdom Come: Deliverance installed
- Mods organized in folders (standard mod structure)

No additional software required — KCDMerge is a standalone `.exe`.

### Setup

1. **Download** `KCDMerge.exe` and place it in any folder
2. **Run it once** — it will create a default `config.yaml` next to the executable
3. **Edit `config.yaml`** with your paths (see [Configuration](#configuration) below)
4. **Run again** — KCDMerge will scan, merge, and output a combined mod

### Mod Folder Structure

Your mods folder should look like this:

```
YourModsFolder/
├── BetterArchery/
│   └── Data/
│       └── Better_Archery.pak
├── BetterStamina/
│   ├── Data/
│   │   └── BetterStamina.pak
│   └── Localization/
│       └── English_xml.pak
├── Riposte/
│   ├── Data/
│   │   └── Riposte.pak
│   └── Localization/
│       ├── English_xml.pak
│       ├── German_xml.pak
│       └── Russian_xml.pak
├── mod_order.txt              (optional)
└── KCDMerge/                  (created by KCDMerge)
    ├── mod.manifest
    ├── Data/
    │   └── KCDMerge.pak
    └── Localization/
        ├── English_xml.pak
        ├── German_xml.pak
        └── ...
```

### Load Order

Create a `mod_order.txt` file in your mods folder to control which mod takes priority:

```
BetterArchery
BetterStamina
Riposte
```

- Mods listed first have **lower** priority (get overwritten by later mods on conflict)
- Mods not listed are added last in alphabetical order
- KCDMerge automatically adds itself as the final entry so it loads after all source mods

---

## Configuration

KCDMerge uses a `config.yaml` file located next to the executable. On first run, KCDMerge will:
1. Auto-detect common game installation paths
2. Prompt you to confirm or enter paths manually
3. Create the config file automatically

### config.yaml Reference

```yaml
# Path to your Kingdom Come: Deliverance installation
# Common locations:
#   - C:\Program Files (x86)\Steam\steamapps\common\KingdomComeDeliverance
#   - C:\Program Files\Steam\steamapps\common\KingdomComeDeliverance
#   - D:\SteamLibrary\steamapps\common\KingdomComeDeliverance
GamePath: ""

# Path to your Mods folder
# Usually: [GamePath]\Mods
# Example: C:\Program Files (x86)\Steam\steamapps\common\KingdomComeDeliverance\Mods
ModsPath: ""

# Name of the merged mod folder (created in ModsPath)
# Default: KCDMerge
OutputPath: "KCDMerge"

# Automatically open the log file after merge completes
# Default: false
OpenLogFileAfterMerge: false

# Number of log files to keep (0 = keep all)
# Default: 3
LogRetentionCount: 3

# Minimum log level written to the .log file
# Valid values: Verbose, Debug, Information, Warning, Error
# Console always shows Information and above regardless of this setting
# Default: Information
FileLogLevel: "Information"

# Temporary directory for staging files during merge
# Supports environment variables like %TEMP%
# Subfolders \Staging and \Staging_localization are created automatically
# Default: %TEMP%\KCDMerge
TempPath: "%TEMP%\\KCDMerge"

# Clean up temporary files after successful merge
# Default: true
TempCleanUp: true

# Output mode for merged table XML files
# PTF:  Patched Table Files — only changed rows, maximum compatibility with other mods
# Full: Full table replacement + .tbl — legacy behavior, replaces entire vanilla table
# In PTF mode, tables that cannot be expressed as PTF (deletions, hash tables, non-table XML)
# automatically fall back to Full for that specific file.
# Default: PTF
OutputMode: "PTF"

# Stale mod detection: action when a mod's XML file predates the vanilla file it modifies.
# "Off"      — no check, no warning
# "WarnOnly" — warn in report only, never prompt (default)
# "Prompt"   — prompt for a decision per stale file (UseWholeXml / MergeAnyway)
StalenessCheckMode: "WarnOnly"

# Mods that always trigger the staleness prompt regardless of StalenessCheckMode.
# Use the mod's display name or folder name (case-insensitive).
# Example: ["OldMod", "AnotherMod"]
StalenessCheckMods: []
```

### First Run Setup

If KCDMerge cannot find your game or mods folder automatically:

1. **GamePath not found:**
   ```
   Could not auto-detect GamePath.
   Enter GamePath: C:\Games\KingdomComeDeliverance
   ```

2. **ModsPath not found:**
   ```
   Could not auto-detect ModsPath.
   Enter ModsPath: C:\Users\YourName\Downloads\KingdomComeDeliverance\Mods
   ```

3. **Auto-detected but wrong:**
   ```
   Auto-detected GamePath: C:\Wrong\Path\To\Game
   Use this path? (y/n) [y]: n
   Enter GamePath: C:\Correct\Path\To\Game
   ```

The configuration is automatically saved to `config.yaml` after setup.

### Saved Decisions

KCDMerge remembers your conflict resolution choices so you don't have to answer the same questions every run.

**ModConflictRules.yaml** — Stores all mod-vs-mod conflict decisions in a separate file:

```yaml
XmlConflicts:
  "BetterArchery|BetterStamina": "BetterStamina"
  "ModernControls|Quicksave": "ModernControls"
  "30MinutesPotionsPTF|BCAICLITE1835Final": "30MinutesPotionsPTF"

AssetConflicts:
  "OpenSans|SmallerFont": "OpenSans"
  "mod_kcd_2.0|r457SortByTotalWeight": "mod_kcd_2.0"
```

- **XmlConflicts** — When two mods change the same XML data
- **AssetConflicts** — When two mods provide the same asset file (textures, sounds, etc.)
- **CfgConflicts** — When two mods set different values for the same `user.cfg` variable

The key is both mod names sorted alphabetically, separated by `|`. The value is the winning mod. For CfgConflicts, the variable name is prepended: `variable|ModA|ModB`.

```yaml
CfgConflicts:
  "g_skipIntro|FullIntroMod|SkipIntroMod": "SkipIntroMod"
```

**RowAdditionRules** — For junction tables (like `soul2perk.xml`), you decide per-mod how row changes are handled:

```yaml
RowAdditionRules:
  soul2perk:
    BetterPerks:
      Strategy: "IncludeAll"
    MakeThemReasonable:
      Strategy: "AdditionsOnly"
```

To **reset a decision**, simply delete the corresponding entry from `ModConflictRules.yaml` and run KCDMerge again.

---

## Conflict Management

KCDMerge handles three types of situations during merging:

### 1. Automatic Merging (No Prompt)

Most XML merges happen automatically. When mods change different rows or different attributes, KCDMerge combines them without asking:

```
[MERGE] rpg_param.xml: BetterArchery changed 3 attributes, BetterStamina changed 5 attributes
```

### 2. Mod-vs-Mod Conflicts

When two mods change the **same attribute** on the **same row**, KCDMerge asks you to pick a winner:

```
Mod Conflict Detected:
BetterArchery and BetterStamina modify the same data.
24 conflicting attribute(s)

Which mod should take priority?
> BetterArchery
  BetterStamina
```

Your choice applies to **all** conflicts between those two mods (across all files) and is saved for future runs.

### 3. Row Addition Decisions

For junction tables (tables where rows are just key pairs with no data values), KCDMerge asks how to handle each mod's changes:

```
ID-Only Table Change Detected: soul2perk.xml
Mod: BetterPerks

+ Adds 5 new row(s)
- Removes 2 row(s)

How should these changes be merged?
> Include additions AND removals (full mod changes)
  Include additions only (additive merge)
  Include removals only (subtractive merge)
  Skip this mod's changes (keep current state)
```

### 4. Asset Conflicts

For non-XML files (textures, sounds, etc.), only one version can exist. KCDMerge asks which mod's file to keep:

```
Conflict detected for textures/armor/helmet.dds!
Which mod should take priority?
> ModA
  ModB
```

### 5. Stale Mod Detection

A mod built against an older game version can silently corrupt a merge. When a mod's XML file predates the corresponding vanilla file (by ZIP entry timestamp), KCDMerge flags it as stale.

**Behavior is controlled by `StalenessCheckMode` in `config.yaml`:**

| Mode | Behavior |
|------|----------|
| `Off` | No check, no warning |
| `WarnOnly` | Log warning + add to merge report (default) |
| `Prompt` | Ask per stale file what to do |

**When `Prompt` mode is active (or the mod is in `StalenessCheckMods`):**

```
⚠ STALE MOD DETECTED: Libs/Tables/item/armor.xml
  Mod:     CoolArmor (file dated 2024-11-15)
  Vanilla: vanilla file dated 2026-01-12
  This mod's XML predates the game patch by 423 day(s).

  How should this file be handled?
  > Use whole XML (mod's file as-is, no merge)
    Merge anyway (delta merge, may produce incorrect results)
```

- **Use whole XML** — copies the mod's XML directly to the output, bypassing the delta merge. The file replaces vanilla entirely.
- **Merge anyway** — proceeds with the normal delta merge and adds a warning to the report.

Decisions are saved to `StalenessOverrides` in `ModConflictRules.yaml` and reused on future runs.

**Force prompt for specific mods** regardless of global mode:

```yaml
StalenessCheckMods:
  - "OldMod"
  - "AnotherMod"
```

### 6. user.cfg Merging

Some mods ship loose `.cfg` files with console variable settings (e.g., `g_skipIntro = 1`). KCDMerge automatically merges these into the game's `user.cfg`:

- **New variable** — added automatically
- **Same value** — skipped (no conflict)
- **Different value** — KCDMerge asks which mod's value to use

```
user.cfg variable conflict: g_skipIntro
  SkipIntroMod: g_skipIntro = 1
  FullIntroMod: g_skipIntro = 0

Which mod should take priority?
> SkipIntroMod
  FullIntroMod
```

Decisions are saved to `CfgConflicts` in `ModConflictRules.yaml`.

The existing `user.cfg` is backed up before modification as `user.cfg.(timestamp).backup` (last 5 backups are kept).

---

## How It Works

### Merge Pipeline

```
 1. Index vanilla game PAK files (cached after first run)
 2. Scan all mods and track XML + asset files
 3. For each XML file modified by mods:
    a. Load the vanilla baseline from game PAKs
    b. Compute a delta (what each mod changed vs vanilla)
    c. Apply deltas in load-order priority
    d. Detect and resolve conflicts between mods
    e. Write merged result
 4. Copy non-XML assets (winner-takes-all for conflicts)
 5. Pack everything into PAK files
 6. Output the merged mod ready for the game to load
```

### XML Merge Types

KCDMerge automatically detects the XML structure and applies the right strategy:

| Type | Detection | Strategy | User Prompt? |
|------|-----------|----------|--------------|
| **Data Tables** | Rows with PK + value columns | Delta normalization, attribute overlay | Only on conflict |
| **Junction Tables** | Rows with PK columns only | Set operations (add/remove rows) | Per-mod decision |
| **Hash Tables** | Tables with non-unique keys | Row-set comparison by content hash | Only on conflict |
| **Non-Table XML** | Behavior trees, flowgraphs, etc. | XPath-based recursive merge | Only on conflict |
| **Localization** | Files in `Localization/` PAKs | Additive row merge keyed by first Cell | Only on conflict |
| **PTF Patches** | `filename__modname.xml` pattern | Merged into base file by row key | Only on conflict |

### Localization

Localization files (`text__ModName.xml` inside `Localization/{Language}_xml.pak`) are handled specially:

- Each language is merged independently (English with English, German with German, etc.)
- Rows are keyed by the first `<Cell>` value (the translation key)
- All mods' translations are combined additively
- Conflicts (two mods translating the same key differently) trigger the conflict resolver
- Output is a merged PTF file: `text__KCDMerge.xml` per language

### Primary Key Detection

KCDMerge automatically identifies primary keys for XML tables to enable precise row-level merging:

**Detection Strategy:**
1. **Filename matching** — Columns ending in `_id`/`_key` whose name appears in the table filename (e.g., `perk_id` in `perk.xml`)
2. **Directory fallback** — If no match, tries `{parent_directory}_id` (e.g., `item_id` for tables in `Libs/Tables/item/`)
3. **Uniqueness validation** — Verifies detected PKs produce unique keys; falls back to hash-based comparison if not unique

**Examples:**
- `food.xml` in `item/` → uses `item_id` as PK (directory fallback)
- `armor.xml` in `item/` → uses `item_id` as PK (directory fallback)
- `perk.xml` → uses `perk_id` as PK (filename match)
- `quest_npc.xml` → `quest_id` detected but non-unique → falls back to hash comparison

**PK Schema Cache:**
- Derived PKs are cached in `.temp/pk_schema_cache.yaml` for performance
- Cache is automatically invalidated when KCDMerge version changes
- Ensures new PK detection logic takes effect after updates

### Delta Caching

KCDMerge caches computed deltas (what each mod changed vs vanilla) to speed up subsequent runs. The cache is automatically invalidated when a mod's PAK file changes.

### Non-XML Files With XML Content

Some game files use non-`.xml` extensions but contain XML data (e.g., `.adb` animation database files). KCDMerge detects these automatically by peeking at the first 64 bytes of each file:

- If the content starts with `<?xml` or `<` it is routed to the **XML merge pipeline** for proper delta merging
- If not, it is treated as a **binary asset** (winner-takes-all)
- Detection results are cached by extension in `.temp/asset_xml_cache.yaml` so only the first occurrence of each extension is inspected

### Positional Sibling Merge (Non-Table XML)

For non-table XML files (e.g., `.adb` animation databases), elements that appear as positional siblings — like multiple `<Fragment>` children under the same parent — are tracked by index. When a mod changes the **count** of such siblings, KCDMerge performs a **subtree replacement** at the parent level.

Critically, if a later mod carries the same sibling count as vanilla (i.e., it did not intentionally change that group), the subtree replacement is **skipped** so that earlier mods' deletions or additions are preserved. This ensures two mods touching different aspects of the same parent element both take effect.

### Patch PAK Handling

Mods that ship multiple PAK files (base + patch) are indexed per-mod before processing. Only the **newest version** of each file (by ZIP entry modification date) is used, with patch PAKs correctly overriding base PAKs.

---

## Logging

Each run creates a timestamped log file next to the executable:

```
KCDMerge - (2026.02.17_08.40.37).log
```

The log contains detailed information about every merge operation, conflict resolution, and any warnings or errors. Check the log if something doesn't look right in-game.

By default the **3 most recent** log files are kept. Configure this with `LogRetentionCount` in `config.yaml` (set to `0` to keep all logs).

### Log Level

Control how much detail is written to the `.log` file with `FileLogLevel` in `config.yaml`:

| Value | What gets logged |
|-------|-----------------|
| `Verbose` | Everything, including internal XPath indexing and per-node details |
| `Debug` | Detailed pipeline internals (delta types, cache hits, subtree decisions) |
| `Information` | **(default)** Normal operation — merge stats, conflicts, file writes |
| `Warning` | Only warnings and errors |
| `Error` | Only errors |

The **console** always shows `Information` and above regardless of this setting — the progress dots and summaries are unaffected.

### Useful Log Prefixes

| Prefix | Meaning |
|--------|---------|
| `[PIPELINE]` | Per-file merge start / end |
| `[MERGE]` | Attribute-level changes applied |
| `[SUBTREE]` | Positional sibling group handling |
| `[DELTA]` | Delta normalization for table files |
| `[CONFLICT-TRACKER]` | Row-level conflict detection |
| `[ASSET]` | Binary asset extraction / conflict |
| `[ASSET-XML]` | Non-.xml file routed to XML pipeline |
| `[LOAD]` | Vanilla baseline loaded from PAK |
| `[WRITE]` | Output written to staging |

### Debug Index Files

For debugging, KCDMerge writes index files to `.temp/pak_index/`:

- **`master.txt`** — All vanilla game files with their source PAKs and timestamps
- **`{ModName}.txt`** — Per-mod file index showing which PAK provides each file

---

## Troubleshooting

### First run takes a long time

Normal. KCDMerge needs to index all vanilla game PAK files (~30-60 seconds). This is cached for future runs.

### "Merge failed for [filename]"

Check the log file. Common causes:
- Corrupted or invalid XML in a mod's PAK
- Mod uses an unexpected XML structure

### "No mods found to merge"

- Verify `ModsPath` in `config.yaml` points to the correct folder
- If the path is wrong, delete `config.yaml` and run KCDMerge again to reconfigure
- Mods must follow the standard folder structure: `ModName/Data/ModName.pak`

### Wrong mod is winning conflicts

Delete the relevant entry from `ModConflictRules.yaml` and run again to re-choose.

### Resetting all decisions

Delete the `RowAdditionRules` and `ModConflictRules` sections from `ModConflictRules.yaml` (or delete the entire file to start fresh).

### PAK file is locked

KCDMerge checks if PAK files are accessible before processing. If you see a lock error:
- Close Kingdom Come: Deliverance and try again
- Close any mod manager tools that may have the PAK open

### Output PAK exceeds 2 GB

Supported — KCDMerge uses Zip64 streaming to write PAK files of any size.

### Non-`.xml` files not merging correctly

Delete `.temp/asset_xml_cache.yaml` and run again. The cache may have an incorrect entry for that file extension.

### Changes from one mod are overwriting another mod's changes in `.adb` / non-table XML files

This can happen when one mod carries the original (vanilla) subtree while another mod intentionally removes elements. KCDMerge detects this automatically: if a mod's positional sibling group matches vanilla exactly, it skips the subtree replacement and preserves the earlier mod's changes. If you still see incorrect results, check the `[SUBTREE]` lines in the log.

---

## Building from Source

```bash
dotnet publish KCDMerge.CLI -c Release -o publish
```

---

## License

Copyright (c) 2026. All rights reserved.

This software is provided for **personal, non-commercial use only**.

- You may use this tool to merge mods for your own personal gameplay.
- You may **not** sell, sublicense, or distribute this software for financial gain.
- You may **not** use this software or its output as part of any commercial product or service.
- Redistribution of modified versions requires explicit written permission from the author.

---

## Acknowledgments

- Kingdom Come: Deliverance by Warhorse Studios
- Built with [Spectre.Console](https://spectreconsole.net/) for CLI output
