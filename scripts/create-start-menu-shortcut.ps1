param(
    [Parameter(Mandatory = $true)]
    [string]$ShortcutPath,

    [Parameter(Mandatory = $true)]
    [string]$TargetPath,

    [Parameter(Mandatory = $true)]
    [string]$WorkingDirectory,

    [string]$Description = "QuickNotesTxt"
)

$ErrorActionPreference = "Stop"

$shortcutDirectory = Split-Path -Parent $ShortcutPath
if (-not [string]::IsNullOrWhiteSpace($shortcutDirectory)) {
    New-Item -ItemType Directory -Path $shortcutDirectory -Force | Out-Null
}

$shell = New-Object -ComObject WScript.Shell
$shortcut = $shell.CreateShortcut($ShortcutPath)
$shortcut.TargetPath = $TargetPath
$shortcut.WorkingDirectory = $WorkingDirectory
$shortcut.IconLocation = "$TargetPath,0"
$shortcut.Description = $Description
$shortcut.Save()
