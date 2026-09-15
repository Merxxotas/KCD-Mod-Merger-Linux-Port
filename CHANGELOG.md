# Changelog

All notable changes to **KCD-Mod-Merger-Linux-Port** will be documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.0.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

---

## [1.4.2] - 2026-09-15

### Fixed
- **CryEngine ActionMap & Keybinding Corruption**: Enforced strict path filtering in `ModOutputBuilder.CreateTblFiles()` to only generate zero-byte `.tbl` override files for XMLs within `Libs/Tables/`. Non-table XMLs (such as `Libs/Config/defaultProfile.xml` and `keybindSuperactions.xml`) are now excluded, resolving game-breaking issues where interaction (`[E]`), chest looting, lockpicking rotation, and jumping failed.
- **Flash UI & Settings Menu Freezes**: Prevented dummy `.tbl` creation for UI flowgraph files like `Libs/UI/UIActions/MM_GraphicsSettings.xml`, fixing UI freezes and `[object]` display glitches in the graphics and pause menus.

### Added
- **Unit Test Coverage**: Created `TblOverrideFilterTests` in `tests/KCDMerge.Tests/TblOverrideFilterTests.cs` to guarantee that table files (`Libs/Tables/`) are correctly differentiated from non-table engine configuration, UI, and animation XMLs across different path separator styles.
- **Subsystem Protection Documentation**: Documented CryEngine table override behavior and safety boundaries in `README.md`.

---

## [1.4.1] - 2026-08-25

### Added
- Native Linux x64 self-contained single-file binary build pipeline.
- CI/CD workflow with GitHub Actions for automated building, testing, and multi-distribution release packaging.
- Cross-platform path resolution supporting native Steam, Flatpak Steam, multi-library mounts, Heroic Games Launcher, Lutris, and Proton.
- Comprehensive `README.md` with full Linux setup, configuration, and troubleshooting documentation.
