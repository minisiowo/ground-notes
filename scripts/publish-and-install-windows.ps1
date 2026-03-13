param(
    [string]$Runtime = "win-x64",
    [string]$Configuration = "Release"
)

$ErrorActionPreference = "Stop"

$repoRoot = Split-Path -Parent $PSScriptRoot
$projectPath = Join-Path $repoRoot "src/QuickNotesTxt/QuickNotesTxt.csproj"
$publishDir = Join-Path $repoRoot "src/QuickNotesTxt/bin/$Configuration/net10.0/$Runtime/publish"
$installDir = "C:\Apps\QuickNotes"
$startMenuDir = Join-Path $env:APPDATA "Microsoft\Windows\Start Menu\Programs"
$shortcutPath = Join-Path $startMenuDir "QuickNotesTxt.lnk"
$exePath = Join-Path $installDir "QuickNotesTxt.exe"
$shortcutScriptPath = Join-Path $PSScriptRoot "create-start-menu-shortcut.ps1"

Write-Host "Publishing QuickNotesTxt for $Runtime..."
dotnet publish $projectPath -c $Configuration -r $Runtime --self-contained true

if (-not (Test-Path -LiteralPath $publishDir)) {
    throw "Publish output was not found: $publishDir"
}

Write-Host "Refreshing install directory: $installDir"
New-Item -ItemType Directory -Path $installDir -Force | Out-Null

$existingItems = Get-ChildItem -LiteralPath $installDir -Force
if ($existingItems.Count -gt 0) {
    Remove-Item -LiteralPath $existingItems.FullName -Recurse -Force
}

Copy-Item -Path (Join-Path $publishDir "*") -Destination $installDir -Recurse -Force

if (-not (Test-Path -LiteralPath $exePath)) {
    throw "Installed executable was not found: $exePath"
}

Write-Host "Creating Start Menu shortcut: $shortcutPath"
& $shortcutScriptPath -ShortcutPath $shortcutPath -TargetPath $exePath -WorkingDirectory $installDir

Write-Host "Done. QuickNotesTxt was installed to $installDir"
