#!/usr/bin/env bash
set -euo pipefail

# Ensure dotnet in PATH if in ~/.dotnet
if ! command -v dotnet &>/dev/null; then
    if [ -d "$HOME/.dotnet" ]; then
        export DOTNET_ROOT="$HOME/.dotnet"
        export PATH="$HOME/.dotnet:$HOME/.dotnet/tools:$PATH"
    fi
fi

if ! command -v dotnet &>/dev/null; then
    echo "Error: .NET 8.0 SDK not found. Please install .NET 8.0 SDK." >&2
    exit 1
fi

VERSION="1.4.2"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "$SCRIPT_DIR"

echo "=== KCDMerge Linux Build & Package Tool v${VERSION} ==="
echo "Using dotnet: $(dotnet --version)"

echo "--> Running Tests..."
dotnet test --nologo -v q

echo "--> Publishing Self-Contained linux-x64 single file..."
rm -rf publish/linux-x64 dist
mkdir -p publish/linux-x64 dist

dotnet publish src/KCDMerge/KCDMerge.csproj \
    -c Release \
    -r linux-x64 \
    --self-contained true \
    -p:PublishSingleFile=true \
    -p:PublishReadyToRun=true \
    -o publish/linux-x64 \
    --nologo

chmod +x publish/linux-x64/KCDMerge

echo "--> Packaging Release Archive..."
RELEASE_DIR="dist/KCDMerge-v${VERSION}-linux-x64"
mkdir -p "$RELEASE_DIR"
cp publish/linux-x64/KCDMerge "$RELEASE_DIR/"
cp config.yaml "$RELEASE_DIR/"
cp config_template.yaml "$RELEASE_DIR/"
cp README.md "$RELEASE_DIR/"
cp LICENSE "$RELEASE_DIR/"
[ -f CHANGELOG.md ] && cp CHANGELOG.md "$RELEASE_DIR/"
[ -f RELEASE_NOTES.md ] && cp RELEASE_NOTES.md "$RELEASE_DIR/"

tar -czf "dist/KCDMerge-v${VERSION}-linux-x64.tar.gz" -C dist "KCDMerge-v${VERSION}-linux-x64"
rm -rf "$RELEASE_DIR"

echo "=== Build Complete ==="
echo "Archive created: dist/KCDMerge-v${VERSION}-linux-x64.tar.gz"
ls -lh dist/KCDMerge-v${VERSION}-linux-x64.tar.gz
