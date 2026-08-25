# KCDMerge Linux Port - Implementation Plan

## Executive Summary

KCDMerge is a .NET 8.0 C# application distributed as Windows executables (.exe files). To create a Linux version, we need to leverage .NET's cross-platform capabilities and either:
1. Recompile from source for Linux (PREFERRED)
2. Decompile the Windows binaries and rebuild for Linux (if source unavailable)

**Target: Universal Linux binary** that works across all major distributions (Ubuntu, Fedora, Debian, Arch, openSUSE, etc.)

## Current Situation Analysis

### What We Have
- **Framework-dependent version**: 3.3 MB exe (requires .NET 8.0 runtime)
- **Self-contained version**: 66.9 MB exe (includes .NET 8.0 runtime)
- **Dependencies identified**:
  - .NET 8.0 (NETCoreApp v8.0)
  - Ionic.Zip.dll (DotNetZip library for ZIP/PAK handling)
  - Serilog.dll (logging framework)
  - SharpZipLib (compression library)
  - Microsoft.CSharp, Microsoft.VisualBasic (standard .NET libraries)

### Key Findings
- The application is **native .NET Core/8.0** - already cross-platform compatible at the framework level
- Main work: interacting with ZIP files (PAK format), XML processing, file I/O
- No Windows-specific GUI dependencies (console application)
- GitHub repository exists: `https://github.com/Za-Pa-Al/KCDMerge`

## Implementation Approaches (Ranked by Viability)

### Approach 1: Build from Source (STRONGLY RECOMMENDED) ⭐

**Prerequisites:**
- Install .NET 8.0 SDK on Linux
- Clone the GitHub repository
- Review build configuration

**Steps:**
1. Install .NET 8.0 SDK for Linux (available via package managers or Microsoft's universal installer)
2. Clone repository: `git clone https://github.com/Za-Pa-Al/KCDMerge.git`
3. Examine project structure and dependencies
4. Build for Linux using: `dotnet publish -c Release -r linux-x64 --self-contained true`
5. Test with sample mod files

**Advantages:**
- Official build process
- Clean, maintainable code
- Self-contained build works on ANY Linux distribution
- Can contribute improvements back to project
- No legal/ethical concerns

**Challenges:**
- Requires access to GitHub repository
- May need to handle Linux-specific path issues (backslash vs forward slash)
- Config file may have Windows-style paths

**Estimated Effort:** 2-4 hours

---

### Approach 2: Decompile and Rebuild (FALLBACK)

**Prerequisites:**
- Install decompiler (ILSpy, dnSpy, or dotPeek)
- Install .NET 8.0 SDK on Linux

**Steps:**
1. Use ILSpy to decompile KCDMerge.exe to C# source
2. Extract embedded resources and dependencies
3. Recreate project structure (.csproj files)
4. Add NuGet package references (Ionic.Zip, Serilog, etc.)
5. Fix any decompilation artifacts
6. Build for Linux target

**Advantages:**
- Works if repository is inaccessible
- Full control over code modifications

**Challenges:**
- Decompiled code may have artifacts
- Need to reconstruct project dependencies
- Ethical/legal concerns (though for personal use should be acceptable)
- More time-consuming

**Estimated Effort:** 6-10 hours

---

### Approach 3: Wine Compatibility Layer (NOT RECOMMENDED)

**Why not recommended:**
- Overhead of running Windows .NET runtime through Wine
- Potential path translation issues
- Performance penalties
- Defeats purpose of native Linux solution

---

## Universal Linux Compatibility Strategy

### Self-Contained Build (RECOMMENDED for Universal Compatibility) ⭐

**Build command:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained true
```

**Why this works universally:**
- Bundles the entire .NET 8.0 runtime inside the binary
- No external dependencies required (except base system libraries)
- Works on: Ubuntu, Fedora, Debian, Arch, openSUSE, Manjaro, Pop!_OS, etc.
- User doesn't need to install .NET runtime
- Single executable that "just works"

**Size:** ~60-70 MB (includes full .NET runtime)

**System Requirements:**
- glibc-based Linux (covers 99% of distributions)
- x64 architecture
- Basic system libraries (libstdc++, libgcc - present on all distros)

---

### Alternative: Framework-Dependent Build

**Build command:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained false
```

**Pros:**
- Smaller binary (~3-5 MB)
- Shares .NET runtime with other apps

**Cons:**
- User must install .NET 8.0 runtime separately
- Different installation process per distro:
  - Ubuntu/Debian: `apt install dotnet-runtime-8.0`
  - Fedora: `dnf install dotnet-runtime-8.0`
  - Arch: `pacman -S dotnet-runtime-8.0`
  - openSUSE: `zypper install dotnet-runtime-8.0`
- NOT recommended for universal distribution

---

### Distribution Methods (Universal Linux)

#### Option 1: Self-Contained Binary (Simplest) ⭐
- Single executable file
- Distribute as `.tar.gz` or `.zip`
- Works immediately on any Linux distro
- No installation required

**Usage:**
```bash
tar -xzf KCDMerge-linux-x64.tar.gz
cd KCDMerge
./KCDMerge
```

#### Option 2: AppImage (Universal Package)
- Single-file portable application
- Works on all major Linux distributions
- No installation or root privileges required
- Double-click to run (GUI) or execute from terminal

**Build process:**
1. Create AppDir structure
2. Include self-contained binary
3. Package with appimagetool

**Benefits:**
- Sandboxed execution
- Desktop integration
- Update mechanisms available
- Widely accepted format

#### Option 3: Flatpak (Universal Package Manager)
- Works across all distributions via Flathub
- Sandboxed with permission system
- Automatic updates

**Cons:**
- More complex to set up initially
- May have file system access restrictions

#### Option 4: Multiple Distribution-Specific Packages (NOT RECOMMENDED for this project)
- Would require maintaining:
  - `.deb` (Debian/Ubuntu)
  - `.rpm` (Fedora/RHEL/openSUSE)
  - AUR package (Arch)
  - Snap package
- Too much maintenance overhead for single-person project

---

## Platform-Specific Considerations

### Path Handling
**Windows**: `C:\Users\...\Mods`  
**Linux**: `/home/user/.steam/steam/steamapps/common/KingdomComeDeliverance/Mods`

**Required Changes:**
- config.yaml needs to accept Linux paths
- Path separator handling (\ vs /)
- .NET's `Path.Combine()` should handle this automatically
- May need to update path detection/validation logic

### File System Case Sensitivity
- Windows: case-insensitive
- Linux: case-sensitive
- Ensure file lookups use proper casing or case-insensitive comparisons

### Default Paths for Linux
**Steam installation paths (all distros):**
```
~/.steam/steam/steamapps/common/KingdomComeDeliverance
~/.local/share/Steam/steamapps/common/KingdomComeDeliverance
```

**Proton/Wine prefixes:**
```
~/.steam/steam/steamapps/compatdata/379430/pfx/drive_c/
```

**Temp directory:**
- Windows: `%TEMP%` or `C:\Users\...\AppData\Local\Temp`
- Linux: `$TMPDIR` or `/tmp` (universal across all distros)

### Line Endings
- Windows: CRLF (`\r\n`)
- Linux: LF (`\n`)
- .NET should handle automatically, but verify config file parsing

---

## Detailed Implementation Plan

### Phase 1: Environment Setup (30 minutes)

**Install .NET 8.0 SDK (Universal Method):**
```bash
# Download and run Microsoft's universal installer script
wget https://dot.net/v1/dotnet-install.sh
chmod +x dotnet-install.sh
./dotnet-install.sh --channel 8.0
```

This works on ALL Linux distributions without package manager differences.

**Alternative - Distribution package managers:**
```bash
# Arch/Manjaro/CachyOS
sudo pacman -S dotnet-sdk-8.0

# Ubuntu/Debian
sudo apt install dotnet-sdk-8.0

# Fedora
sudo dnf install dotnet-sdk-8.0

# openSUSE
sudo zypper install dotnet-sdk-8.0
```

**Verify installation:**
```bash
dotnet --version  # Should show 8.0.x
```

### Phase 2: Source Acquisition (30 minutes - 2 hours)

**Option A - Clone from GitHub:**
```bash
git clone https://github.com/Za-Pa-Al/KCDMerge.git
cd KCDMerge
```

**Option B - Decompile (if needed):**
```bash
# Install ILSpy command line tool (works on all distros)
dotnet tool install -g ilspycmd

# Decompile
ilspycmd /tmp/kcdmerge-analysis/framework-dependent/KCDMerge.exe -o /tmp/kcdmerge-source
```

### Phase 3: Code Analysis and Linux Adaptation (2-4 hours)

1. **Review path handling code:**
   - Search for hardcoded Windows paths
   - Check path separator usage
   - Verify Path.Combine() usage

2. **Check file system operations:**
   - Case-sensitivity issues
   - File permissions handling
   - Temp directory usage

3. **Review external dependencies:**
   - Verify all NuGet packages are cross-platform
   - Check for Windows-specific P/Invoke calls
   - Ionic.Zip should be cross-platform compatible

4. **Configuration file handling:**
   - Update default paths for Linux
   - Add Linux game path detection
   - Handle ~/.steam/steam paths for Steam installations

### Phase 4: Build for Universal Linux (30 minutes)

**Self-contained build (RECOMMENDED):**
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishTrimmed=true
```

**Flags explained:**
- `-r linux-x64`: Target 64-bit Linux
- `--self-contained true`: Include .NET runtime (universal compatibility)
- `-p:PublishSingleFile=true`: Bundle everything into one executable
- `-p:PublishTrimmed=true`: Remove unused framework code (smaller size)

**Output:** Single executable file (~50-60 MB) that runs on any Linux distribution

**Optional - ReadyToRun optimization:**
```bash
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true -p:PublishReadyToRun=true
```
- Faster startup time
- Slightly larger binary

### Phase 5: Testing on Multiple Distributions (3-4 hours)

**Test matrix (ensure universal compatibility):**
- [ ] Ubuntu 22.04 / 24.04 (Debian-based)
- [ ] Fedora 39+ (RPM-based)
- [ ] Arch Linux / Manjaro / CachyOS (Rolling release)
- [ ] openSUSE Tumbleweed (RPM-based, rolling)
- [ ] Pop!_OS (Ubuntu derivative)
- [ ] Linux Mint (Ubuntu derivative)

**Test scenarios for each:**

1. **Basic functionality:**
   - Launch: `./KCDMerge`
   - Verify help/version output
   - Check config.yaml creation

2. **Path handling:**
   - Create test config with Linux paths
   - Test temp directory creation (`/tmp`)
   - Verify output directory creation

3. **Core functionality:**
   - Test with actual KCD mod files (PAK archives)
   - Verify XML parsing and merging
   - Check conflict resolution prompts
   - Validate output PAK file generation

4. **Edge cases:**
   - Long paths
   - Special characters in paths
   - Symlinks
   - Permission issues
   - Different terminal emulators

### Phase 6: Packaging for Distribution (2-3 hours)

#### Option A: Simple Archive (Easiest)
```bash
# Create release archive
tar -czf KCDMerge-v1.4.1-linux-x64.tar.gz KCDMerge config.yaml README.md LICENSE
```

**Distribution:**
- Upload to GitHub Releases
- Upload to Nexus Mods
- Direct download link

#### Option B: AppImage (Universal Package)
```bash
# 1. Create AppDir structure
mkdir -p KCDMerge.AppDir/usr/bin
cp KCDMerge KCDMerge.AppDir/usr/bin/

# 2. Create desktop file
cat > KCDMerge.AppDir/KCDMerge.desktop << EOF
[Desktop Entry]
Name=KCDMerge
Exec=KCDMerge
Icon=kcdmerge
Type=Application
Categories=Game;Utility;
EOF

# 3. Download appimagetool
wget https://github.com/AppImage/AppImageKit/releases/download/continuous/appimagetool-x86_64.AppImage
chmod +x appimagetool-x86_64.AppImage

# 4. Build AppImage
./appimagetool-x86_64.AppImage KCDMerge.AppDir
```

**Result:** `KCDMerge-x86_64.AppImage` - runs on all distributions

---

## Expected Modifications

### High Priority (Likely Needed)

1. **Path detection logic** - Linux Steam paths (universal):
   ```csharp
   // Add Linux path detection
   var linuxPaths = new[] {
       Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), 
                    ".steam/steam/steamapps/common/KingdomComeDeliverance"),
       Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                    ".local/share/Steam/steamapps/common/KingdomComeDeliverance")
   };
   ```

2. **Config defaults** - Update initial config.yaml template with Linux examples

3. **Temp directory** - Use cross-platform API:
   ```csharp
   // Replace %TEMP% detection with:
   Path.GetTempPath()  // Works on Windows, Linux, macOS
   ```

### Medium Priority (Possibly Needed)

1. **Case-sensitive file matching** - PAK file lookups
2. **Path separator normalization** - Use `Path.DirectorySeparatorChar`
3. **Console color support** - Verify Spectre.Console works on all Linux terminals

### Low Priority (Unlikely Needed)

1. Native library dependencies (Ionic.Zip is pure managed code)
2. Registry access (application doesn't appear to use it)
3. Windows-specific APIs (console app, no GUI)

---

## Risk Assessment

### Low Risk ✅
- .NET 8.0 is fully cross-platform
- Console application (no GUI dependencies)
- Standard file I/O operations
- ZIP/compression libraries are cross-platform
- Self-contained build ensures no distro-specific issues

### Medium Risk ⚠️
- Path handling differences may cause issues
- Case-sensitivity could affect file matching
- Game installation detection needs Linux implementation
- User prompts/console interaction may behave differently

### High Risk ❌
- Repository might be private/inaccessible (mitigated by decompilation option)
- Licensing concerns if modifying without permission (personal use should be acceptable)

---

## Success Criteria

### Minimum Viable Product (MVP)
- [ ] Application launches on multiple Linux distributions
- [ ] Accepts Linux-style paths in config.yaml
- [ ] Reads game PAK files correctly
- [ ] Performs basic XML merging
- [ ] Outputs merged PAK files
- [ ] Conflict prompts work in Linux terminals

### Full Feature Parity
- [ ] All Windows features work identically
- [ ] Auto-detection of Linux game paths
- [ ] Proper temp directory handling
- [ ] Log file generation
- [ ] Delta caching works
- [ ] All merge types function correctly
- [ ] User.cfg merging works

### Universal Compatibility
- [ ] Runs on Ubuntu/Debian-based distros
- [ ] Runs on Fedora/RHEL-based distros
- [ ] Runs on Arch-based distros
- [ ] Runs on openSUSE
- [ ] Single binary works across all tested distributions
- [ ] No distribution-specific dependencies

### Polish
- [ ] AppImage package (optional but recommended)
- [ ] Installation documentation
- [ ] Performance optimization
- [ ] Error handling for Linux-specific issues

---

## Alternative: Contact Developer

Before proceeding with decompilation, consider:
1. Check if developer has Linux build plans
2. Request Linux support on GitHub issues
3. Offer to help with Linux port
4. Check if source code is available under current license

GitHub: `https://github.com/Za-Pa-Al/KCDMerge`

---

## Next Steps (Post-Planning)

1. **Verify GitHub repository accessibility**
   - Check if repository is public
   - Review source code structure
   - Examine existing build configuration

2. **Install .NET 8.0 SDK**
   - Use Microsoft's universal installer script (works on all distros)

3. **Choose implementation approach**
   - Source build (if repo accessible)
   - Decompilation (if needed)

4. **Begin Phase 1: Environment Setup**

5. **Build self-contained Linux binary**

6. **Test on multiple distributions (if possible)**

7. **Package for distribution**

---

## Estimated Total Time

- **Best case** (source available, minimal changes): 5-8 hours
- **Expected case** (source available, some adaptations): 10-15 hours  
- **Worst case** (decompilation needed, significant fixes): 20-25 hours

---

## Tools and Resources Needed

### Software (Universal - Works on All Distros)
- .NET 8.0 SDK (via Microsoft installer or distro package)
- Git
- ILSpy (if decompiling) - `dotnet tool install -g ilspycmd`
- Text editor / IDE (VS Code, Rider, vim, nano)
- Steam (for KCD installation path testing)

### Testing Materials
- Kingdom Come: Deliverance (Linux/Proton version)
- Sample mod files (.pak format)
- Various test scenarios for merging

### Documentation
- .NET cross-platform guide
- Spectre.Console documentation
- CryEngine PAK format documentation (if needed)
- AppImage documentation (for packaging)

---

## Key Advantages of This Approach

1. **True Universal Compatibility**: Self-contained build works on ANY Linux distribution
2. **No User Dependencies**: User doesn't need to install .NET runtime
3. **Single Binary**: Easy to distribute and use
4. **Maintainable**: Based on official source or clean decompilation
5. **Future-Proof**: Can be updated as new versions release
