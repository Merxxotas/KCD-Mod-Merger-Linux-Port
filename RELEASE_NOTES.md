# KCDMerge v1.4.2 — Release Notes

## Overview

**KCDMerge v1.4.2** is a critical bugfix release resolving game-breaking interaction failures, unresponsive keybindings, and settings menu freezes caused by dummy `.tbl` file generation in non-table CryEngine subsystems.

---

## 🚨 Critical Bug Fixes

### 1. CryEngine ActionMap & Input System Protection
* **Problem**: In previous versions, the merge pipeline generated zero-byte `.tbl` override files for any XML file processed during the merge. When mods modified engine configuration files such as `Libs/Config/defaultProfile.xml` or `Libs/Config/keybindSuperactions.xml`, KCDMerge generated zero-byte dummy `.tbl` files (e.g., `Libs/Config/defaultProfile.tbl`).
* **Game Impact**: CryEngine's ActionMap subsystem crashed or failed to initialize keybindings when encountering zero-byte `.tbl` files. In-game, this resulted in:
  * Interaction key (`[E]`) completely non-responsive for looting chests, talking to NPCs, and picking herbs.
  * Lockpicking minigame unable to rotate or engage.
  * Jump, dog commands, and secondary action bindings failing to trigger.
* **Resolution**: Strict path filtering has been implemented in `ModOutputBuilder.CreateTblFiles()`. Only XML files strictly located under `Libs/Tables/` are eligible for `.tbl` override creation. Non-table XML files are now safely bypassed.

### 2. Flash UI & Graphics Settings Freeze Prevention
* **Problem**: UI action maps and flowgraphs (e.g., `Libs/UI/UIActions/MM_GraphicsSettings.xml`) were previously also assigned empty `.tbl` overrides.
* **Game Impact**: The in-game graphics and pause menus could freeze, hang, or display raw `[object]` string placeholders when attempting to query Flash UI data.
* **Resolution**: UI flowgraphs are no longer tagged with dummy `.tbl` files, allowing CryEngine's Flash UI engine to load them directly and reliably.

---

## 🧪 Automated Testing & Quality Assurance

* **Added Unit Test Suite**: `TblOverrideFilterTests` (`tests/KCDMerge.Tests/TblOverrideFilterTests.cs`)
  * Tests positive identification of valid table files (e.g., `Libs/Tables/rpg/rpg_param.xml`, `Libs\Tables\item\item.xml`).
  * Tests rejection of engine configuration XMLs (`Libs/Config/defaultProfile.xml`, `Libs/Config/keybindSuperactions.xml`).
  * Tests rejection of Flash UI files (`Libs/UI/UIActions/MM_GraphicsSettings.xml`).
  * Tests rejection of animation metadata (`Animations/Mannequin/ADB/tags.xml`) and material flowgraphs (`Libs/MaterialEffects/Flowgraphs/fx.xml`).
  * Ensures normalized cross-platform path handling (Windows `\` vs Linux `/`).

---

## 🛠️ Upgrading & Usage

### If you experienced broken interactions or frozen UI:
1. Download `KCDMerge-v1.4.2-linux-x64.tar.gz` from this release.
2. Extract the archive:
   ```bash
   tar -xzf KCDMerge-v1.4.2-linux-x64.tar.gz
   cd KCDMerge-v1.4.2-linux-x64
   ```
3. Copy the new `KCDMerge` executable into your existing merge folder (or update your configuration).
4. Re-run `KCDMerge`. It will automatically rebuild your merged `.pak` files without generating corrupting `.tbl` files for your action maps or UI.
