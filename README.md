# KCDMerge (Linux Port)

[![Build, Test & Release](https://github.com/Merxxotas/KCD-Mod-Merger-Linux-Port/actions/workflows/build.yml/badge.svg)](https://github.com/Merxxotas/KCD-Mod-Merger-Linux-Port/actions/workflows/build.yml)
[![Latest Release](https://img.shields.io/github/v/release/Merxxotas/KCD-Mod-Merger-Linux-Port?logo=github&label=release)](https://github.com/Merxxotas/KCD-Mod-Merger-Linux-Port/releases)

**Native cross-platform automatic mod merger for Kingdom Come: Deliverance on Linux**

---

## Overview

When you install multiple mods in Kingdom Come: Deliverance that modify the same game files, CryEngine's default mod loader only loads the last mod in the load order. All earlier mods' changes are completely overwritten and lost.

**KCDMerge solves this by intelligently merging XML tables, loose files, PTF patches, assets, and `.cfg` variables into a single combined mod (`KCDMerge`).**

- **Native Linux binary** — Compiled natively for 64-bit Linux (`x86_64`). No Wine or Proton required to run the merger.
- **Universal & Self-Contained** — Includes the .NET runtime bundled inside. Works on Arch, Ubuntu, Fedora, Debian, openSUSE, SteamOS / Steam Deck, and any modern Linux distribution out of the box with zero dependencies.
- **Smart Conflict Resolution** — Merges row-level attributes automatically, and remembers your choices in `ModConflictRules.yaml` when mod changes conflict.
- **Patched Table Files (PTF) & Full XML support** — Generates optimized PTF patches for maximum game compatibility.
- **Auto-Detection** — Automatically finds native Steam, Flatpak Steam, multi-library Steam mounts, Heroic, Lutris, and Proton installations.

---

## Quick Start (Running on Linux)

No .NET runtime or additional software is required to run the pre-built binary.

1. **Extract or place `KCDMerge`** into any folder (e.g. in your game folder, mods folder, or user tools).
2. **Make sure it has execute permissions:**
   ```bash
   chmod +x ./KCDMerge
   ```
3. **Run KCDMerge:**
   ```bash
   ./KCDMerge
   ```
4. On first run, KCDMerge will automatically detect your Kingdom Come: Deliverance installation and `Mods` directory, ask you to confirm or enter paths if needed, and save your settings to `config.yaml`.
5. Follow the interactive console prompts if there are any conflicting mods to resolve.

---

## Mod Folder Structure

Place your mods inside the game's `Mods` directory:

```
~/.steam/steam/steamapps/common/KingdomComeDeliverance/Mods/
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
├── mod_order.txt              (optional load order)
└── KCDMerge/                  (created automatically by KCDMerge)
    ├── mod.manifest
    ├── Data/
    │   └── KCDMerge.pak
    └── Localization/
        ├── English_xml.pak
        ├── German_xml.pak
        └── ...
```

### Load Order (`mod_order.txt`)

You can create an optional `mod_order.txt` file in your `Mods/` folder to control mod priority:

```
BetterArchery
BetterStamina
Riposte
```

- Mods listed higher have **lower** priority (overwritten by later mods if they change the same setting).
- Unlisted mods are loaded in alphabetical order.
- `KCDMerge` is automatically appended to the end of `mod_order.txt` so that merged files always take precedence.

---

## Configuration (`config.yaml`)

KCDMerge creates and reads `config.yaml` located next to the executable:

```yaml
# Path to your Kingdom Come: Deliverance installation
# Linux Steam default: ~/.steam/steam/steamapps/common/KingdomComeDeliverance
# Flatpak Steam: ~/.var/app/com.valvesoftware.Steam/.local/share/Steam/steamapps/common/KingdomComeDeliverance
GamePath: ""

# Path to your Mods directory (usually [GamePath]/Mods)
ModsPath: ""

# Name of the output merged mod folder (inside ModsPath)
OutputPath: "KCDMerge"

# Automatically open the log file after merge completes (uses xdg-open on Linux)
OpenLogFileAfterMerge: false

# Number of timestamped log files to retain (0 = keep all)
LogRetentionCount: 3

# Console & log detail level: Verbose, Debug, Information, Warning, Error
FileLogLevel: "Information"

# Temporary directory used for staging files during merge
# On Linux, %TEMP%\KCDMerge automatically resolves to /tmp/KCDMerge
TempPath: "%TEMP%\\KCDMerge"

# Clean up staging temp directory after successful merge
TempCleanUp: true

# Output mode: "PTF" (Patched Table Files, recommended) or "Full" (full table replacements)
OutputMode: "PTF"

# Stale mod detection mode: "WarnOnly", "Prompt", or "Off"
StalenessCheckMode: "WarnOnly"

# List of mods that should always prompt for staleness check decisions
StalenessCheckMods: []
```

> **Tip:** You can use `~` in `GamePath` or `ModsPath` (e.g. `~/.local/share/Steam/steamapps/common/KingdomComeDeliverance`). KCDMerge will automatically expand it to your home directory.

---

## Saved Conflict Decisions (`ModConflictRules.yaml`)

When two mods modify the exact same XML attribute or provide conflicting asset/cfg files, KCDMerge will prompt you once to choose which mod takes priority.

Your decisions are saved to `ModConflictRules.yaml` next to the executable:

- `XmlConflicts`: Mod priority when editing the same XML rows/attributes.
- `AssetConflicts`: Mod priority when providing conflicting textures, sounds, or meshes.
- `CfgConflicts`: Mod priority when setting different values for `user.cfg` variables.
- `RowAdditionRules`: Decision strategies for junction/ID-only tables (like `soul2perk.xml`).

To reset a decision, delete the rule from `ModConflictRules.yaml` or delete the file entirely.

---

## For Developers: Building & Testing

If you are modifying the source code or contributing to the Linux port:

### Prerequisites for Compiling
- **.NET 8.0 SDK** (Installed via Microsoft's `dotnet-install.sh` or distro package manager like `pacman -S dotnet-sdk-8.0` / `apt install dotnet-sdk-8.0`)

### Running Tests
```bash
dotnet test
```
*(If .NET SDK is installed in `~/.dotnet`, make sure `export PATH="$HOME/.dotnet:$PATH"` is set).*

### Building the Self-Contained Linux Binary & Release Package
Run the included build script:
```bash
./build.sh
```

This will:
1. Run all xUnit unit tests.
2. Compile and publish a self-contained single-file Linux executable to `publish/linux-x64/KCDMerge`.
3. Package a distributable `.tar.gz` archive to `dist/KCDMerge-v1.4.1-linux-x64.tar.gz`.

---

## License

This software is provided for **personal, non-commercial use only**. See [LICENSE](file:///home/merxx/Projects/kcd-mod-merger-linux-port/LICENSE) for full details.
