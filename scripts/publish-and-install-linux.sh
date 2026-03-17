#!/usr/bin/env bash

set -euo pipefail

# Default to linux-x64 and Release configuration
runtime="${1:-linux-x64}"
configuration="${2:-Release}"

# Paths
script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
project_path="$repo_root/src/QuickNotesTxt/QuickNotesTxt.csproj"
publish_dir="$repo_root/src/QuickNotesTxt/bin/$configuration/net10.0/$runtime/publish"
install_dir="$HOME/.local/opt/QuickNotesTxt"

echo "Building and publishing QuickNotesTxt for $runtime ($configuration)..."

# Ensure a fresh build by cleaning previous artifacts
dotnet clean "$repo_root/QuickNotesTxt.sln" -c "$configuration"
dotnet publish "$project_path" -c "$configuration" -r "$runtime" --self-contained true

if [[ ! -d "$publish_dir" ]]; then
    echo "Error: Publish output not found at $publish_dir" >&2
    exit 1
fi

echo "Preparing fresh install directory at $install_dir..."

# Create directory if it doesn't exist
mkdir -p "$install_dir"

# Completely clear the destination to avoid orphaned files (like old fonts)
# Use dotglob to ensure hidden files are also removed
shopt -s dotglob nullglob
rm -rf "$install_dir"/*
cp -R "$publish_dir"/* "$install_dir"/
shopt -u dotglob nullglob

# Verify the executable exists
if [[ ! -f "$install_dir/QuickNotesTxt" ]]; then
    echo "Error: Executable not found at $install_dir/QuickNotesTxt" >&2
    exit 1
fi

echo "Success! QuickNotesTxt has been installed to $install_dir"
echo "You can now launch it via your desktop shortcut or by running: $install_dir/QuickNotesTxt"
