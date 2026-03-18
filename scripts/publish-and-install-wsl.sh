#!/usr/bin/env bash

set -euo pipefail

runtime="${1:-win-x64}"
configuration="${2:-Release}"

script_dir="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
repo_root="$(cd -- "$script_dir/.." && pwd)"
project_path="$repo_root/src/QuickNotesTxt/QuickNotesTxt.csproj"
publish_dir="$repo_root/src/QuickNotesTxt/bin/$configuration/net10.0/$runtime/publish"
install_dir="/mnt/c/Apps/QuickNotes"
shortcut_script_path="$repo_root/scripts/create-start-menu-shortcut.ps1"
shortcut_script_windows_path="$(wslpath -w "$shortcut_script_path" | tr -d '\r')"

start_menu_windows_path="$(powershell.exe -NoProfile -Command '[Environment]::GetFolderPath("Programs")' | tr -d '\r')"
if [[ -z "$start_menu_windows_path" ]]; then
    echo "Could not determine the Windows Start Menu path." >&2
    exit 1
fi

shortcut_windows_path="${start_menu_windows_path}\\QuickNotesTxt.lnk"
exe_windows_path="C:\\Apps\\QuickNotes\\QuickNotesTxt.exe"
working_dir_windows_path="C:\\Apps\\QuickNotes"

echo "Cleaning previous build artifacts..."
rm -rf "$repo_root/src/QuickNotesTxt/bin" "$repo_root/src/QuickNotesTxt/obj"

echo "Publishing QuickNotesTxt for $runtime..."
dotnet publish "$project_path" -c "$configuration" -r "$runtime" --self-contained true

if [[ ! -d "$publish_dir" ]]; then
    echo "Publish output was not found: $publish_dir" >&2
    exit 1
fi

echo "Refreshing install directory: $install_dir"
mkdir -p "$install_dir"
shopt -s dotglob nullglob
rm -rf "$install_dir"/*
cp -R "$publish_dir"/* "$install_dir"/
shopt -u dotglob nullglob

if [[ ! -f "$install_dir/QuickNotesTxt.exe" ]]; then
    echo "Installed executable was not found: $install_dir/QuickNotesTxt.exe" >&2
    exit 1
fi

echo "Creating Start Menu shortcut: $shortcut_windows_path"
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$shortcut_script_windows_path" -ShortcutPath "$shortcut_windows_path" -TargetPath "$exe_windows_path" -WorkingDirectory "$working_dir_windows_path"

echo "Done. QuickNotesTxt was installed to $install_dir"
