![](https://staticdelivery.nexusmods.com/mods/2298/images/headers/2163_1771707731.jpg)

- [Games](https://www.nexusmods.com/)
- [Kingdom Come: Deliverance](https://www.nexusmods.com/games/kingdomcomedeliverance)
- [Mods](https://www.nexusmods.com/games/kingdomcomedeliverance/mods)
- [Utilities](https://www.nexusmods.com/games/kingdomcomedeliverance/mods?categoryName=Utilities)
- KCDMerge


# KCDMerge

- Endorsements



[43](https://www.nexusmods.com/Core/Libs/Common/Widgets/ModEndorsersPopUp?gameId=2298&modId=2163 "See who endorsed this mod")

- Unique DLs



\-\-

- Total DLs



\-\-

- Total views



\-\-

- Version



1.4.1


- Download:


0 of 0

- [![](https://staticdelivery.nexusmods.com/mods/2298/images/thumbnails/2163/2163-1771707751-587143414.jpg)](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163#)

## File information

### Last updated

02 April 2026, 5:02PM

### Original upload

21 February 2026, 9:15PM

### Created by

ZaAl


### Uploaded by

[ZaAl](https://www.nexusmods.com/kingdomcomedeliverance/users/2288561)

### Virus scan

Safe to use


## Tags for this mod

[Patched Table Files (PTF)](https://www.nexusmods.com/games/kingdomcomedeliverance/mods/?tags_yes[]=4715&tag=Patched+Table+Files+%28PTF%29)

[Tag this mod](https://www.nexusmods.com/Core/Libs/Common/Widgets/ModTaggingPopUp?mod_id=2163&game_id=2298)

- [Description](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=description)
- [Files2](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=files)
- [Images1](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=images)
- [Videos0](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=videos)
- [Posts71](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=posts)
- [Bugs0](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=bugs)
- [Logs](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=logs)
- [Stats](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163?tab=stats)

Current section

- [**Viewing:**](https://www.nexusmods.com/kingdomcomedeliverance/mods/2163#)

## About this mod

With the normal mod load order system conflicting XML files and rows are overwritten, leaving only one change in effect.KCDMerge addresses this by ID matching row records and combining value changes into a single merged mod.It also handle loose files, .cfg additions, assets, mods with a wrong structure.

- Share


Requirements


### Off-site requirements

| Mod name | Notes |
| --- | --- |
| [.Net 8.0](https://dotnet.microsoft.com/en-us/download/dotnet/8.0) | Optional |

Permissions and credits


### Credits and distribution permission

- Other user's assetsAll the assets in this file belong to the author, or are from free-to-use modder's resources
- Upload permissionYou are not allowed to upload this file to other sites under any circumstances
- Modification permissionYou must get permission from me before you are allowed to modify my files to improve it
- Conversion permissionYou are not allowed to convert this file to work on other games under any circumstances
- Asset use permissionYou must get permission from me before you are allowed to use any of the assets in this file
- Asset use permission in mods/files that are being soldYou are not allowed to use assets from this file in any mods/files that are being sold, for money, on Steam Workshop or other platforms
- Asset use permission in mods/files that earn donation pointsYou are not allowed to earn Donation Points for your mods if they use my assets

### Author notes

This author has not provided any additional notes regarding file permissions


### File credits

This author has not credited anyone else in this file


### Donation Points system

Please [log in](https://users.nexusmods.com/auth/sign_in?redirect_url=https%3A%2F%2Fwww.nexusmods.com%2Fkingdomcomedeliverance%2Fmods%2F2163) to find out whether this mod is receiving Donation Points


Changelogs


- ### Version 1.3.1




  - Fixed .tbl files being treated as Assets.
  - Fixed .tbl files being treated as Assets.
  - Fixed .tbl files being treated as Assets.

Mods using this mod (1)


Loading...

Donations


### Straight donations accepted

- [Donate](https://www.nexusmods.com/Core/Libs/Common/Widgets/PayPalPopUp?user=2288561)

Collections containing this mod


**Downloads**

- **Self-contained** — Includes .NET runtime, just extract and run
- **Framework-dependent**— Requires .NET 8.0 runtime installed


**What's New in v1.4.1**

- **Localization Conflict Rules** — Mod-vs-mod decisions now saved and reused across all conflicting keys
- **Fixed Detached Elements** — Localization merges now correctly track replaced rows for multi-mod conflicts
- **Locked PAK Retry** — All PAK files (Data and Localization) now prompt for retry if locked by another process


**What's New in v1.4.0**

- **Stale Mod Detection** — Detects when a mod's XML predates the vanilla file and prompts for merge strategy
- **Staleness Overrides** — Save decisions to ModConflictRules.yaml to avoid repeated prompts
- **Configurable Staleness Checks** — Control detection behavior with StalenessCheckMode (Off, WarnOnly, Prompt)
- **Per-File Timestamps** — Improved accuracy by comparing ZIP entry dates instead of PAK file dates
- **Enhanced Config Handling** — config\_template.yaml reference file with all options; YAML comments now preserved on save


**What's New in v1.3.0**

- **PTF Output Mode** — New default mode generates Patched Table Files containing only modified rows
- **Architecture Refactoring** — Core logic extracted into dedicated services for improved reliability
- **Improved File Handling** — Mod-added XML files no longer generate spurious .tbl files
- **Log Noise Reduction** — Replaced verbose per-row logs with structured summary blocks
- **Robust Table Detection** — Enhanced PK schema caching and fixed index-out-of-range errors for \_key-suffixed columns


**What's New in v1.2.2**

- **PAK Size Limit Fixed** — Fixed Archive splitting by 2GB limit. CryEngine does not support Zip64.
- **Code Refactoring** — Improved maintainability with cleaner architecture
- **TempPath Consistency** — Temporary files use configurable TempPath (default Windows temp folder)
- **TempPath Cleanup** — Temporary files are cleaned up after merging


**What's New in v1.2.0**

- **Patch PAK Support** — Game files are now correctly extracted
- **Asset-XML Detection** — Non-.xml files containing XML (e.g., .adb animation databases) are automatically detected and merged
- **Pre-flight Checks** — All mod PAK files and output PAK validated for accessibility before processing
- **Zip64 Support** — Output PAK files can now exceed 2GB without crashes or corruption
- **Enhanced Error Messages** — Actionable guidance when PAK files are locked or paths are invalid
- **Log Retention Control** — New LogRetentionCount config to keep N most recent log files
- **User.cfg.log Cleanup** — CryEngine log files now cleaned up alongside KCDMerge logs
- **XPath Improvements** — Better handling of duplicate siblings and subtree replacements in non-table XML
- **Debug Index Files** — PAK index files written to temp folder for troubleshooting


**Installation**

- Download the appropriate ZIP file
- Extract to a folder
- Run KCDMerge.exe
- Resolve conflicts
- Study summary
- Open log file if in doubt
- **Expert:** Adjust config.yaml (Options are in Readme.md)


**A Brief Overview**

This tool should be very safe to use, it does not alter game files or other mods.

I have tested this with dozens of mods and am pretty confident I have covered the typical modding cases.

The tool can potentially also fix old or less correctly built ones.

More details are in the Readme.md file.

**Merge Pipeline**

1\. Index vanilla game PAK files (cached after first run)

2\. Scan all mods and track XML + asset files

3\. For each XML file modified by mods:

a. Load the vanilla baseline from game PAKs

b. Compute a delta (what each mod changed vs vanilla)

c. Apply deltas in load-order priority

d. Detect and resolve conflicts between mods

e. Write merged result

4\. Copy non-XML assets (winner-takes-all for conflicts)

5\. Pack everything into PAK files

6\. Output the merged mod ready for the game to load

**XML Merge Types**

KCDMerge automatically detects the XML structure and applies the right strategy:

- **Data Tables**


  - **Detection:** Rows with PK + value column
  - **Strategy:** Delta normalization, attribute overlay
  - **User Prompt?:** Only on conflict
- **Junction Tables**


  - **Detection:** Rows with PK columns only
  - **Strategy:** Set operations (add/remove rows)
  - **User Prompt?:** Per-mod decision
- **Hash Tables**


  - **Detection:** Tables with non-unique keys
  - **Strategy:** Row-set comparison by content hash
  - **User Prompt?:** Only on conflict
- **Non-Table XML**


  - **Detection:** Behavior trees, flowgraphs, etc.
  - **Strategy:** XPath-based recursive merge
  - **User Prompt?:** Only on conflict
- **Localization**


  - **Detection:** Files in Localization/ PAKs
  - **Strategy:** Additive row merge keyed by first Cell
  - **User Prompt?:** Only on conflict
- **PTF Patches**


  - **Detection:** filename\_\_modname.xml pattern
  - **Strategy:** Merged into base file by row key
  - **User Prompt?:** Only on conflict

**Localization**

Localization files ("text\_\_ModName.xml" inside "Localization/{Language}\_xml.pak") are handled specially:

- Each language is merged independently (English with English, German with German, etc.)
- Rows are keyed by the first Cell value (the translation key)
- All mods' translations are combined additively
- Conflicts (two mods translating the same key differently) trigger the conflict resolver
- Output is a merged PTF file: "text\_\_KCDMerge.xml" per language


**Assets**

Asset files can only be taken as complete files.

Conflicts have to be prioritized.

**(user).cfg files**

Any detected (loose) .cfg files are merged into user.cfg.

Conflicts are detected by variable name and values applied accordingly.

**Patch PAK Handling**

Mods that ship multiple PAK files (base + patch) are indexed per-mod before processing:

- Only the newest version of each file (by ZIP entry modification date) is used
- Patch PAKs correctly override base PAKs
- Debug index files written to temp folder for troubleshooting


**Delta Caching**

KCDMerge caches computed deltas (what each mod changed vs vanilla) to speed up subsequent runs.

The cache is automatically invalidated when a mod's PAK file changes.

**Troubleshooting**

If something goes wrong:

- Open the generated log file (KCDMerge-\[timestamp\].log) for detailed error messages
- Check the merge summary table for warnings or errors
- Review conflict decisions in ModConflictRules.yaml
- Verify all mod PAK files are accessible (game must be closed)


**Worst Case Recovery**

If the game fails to load or behaves incorrectly:

- Delete the "KCDMerge" mod folder
- Restore mod\_order.txt from mod\_order.txt.backup
- Restore user.cfg from user.cfg.backup
- Launch the game again