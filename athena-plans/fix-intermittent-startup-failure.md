# Fix Intermittent Startup Failure

## Context

The app sometimes fails to start properly. Two failure modes have been observed:

1. **Silent hang / invisible window**: The process starts but the main window never becomes visible (stays at `Opacity = 0`).
2. **Crash on launch**: The app terminates unexpectedly during startup before any UI appears.

Both modes are intermittent and correlate with prior abnormal shutdowns (e.g., crash while saving settings, power loss, or force-kill).

## Root Cause

The startup sequence has three unguarded exception paths:

### 1. Corrupt `settings.json` crashes before error handling is active

`FolderSettingsService.LoadRecordSync()` and `LoadRecordAsync()` call `JsonSerializer.Deserialize` without any `try/catch`. If `settings.json` is truncated or malformed (e.g., from a crash during `SaveSync`), `JsonException` propagates up through:

```
FolderSettingsService.GetSettingsSync()
  → StartupStateService.Load()
    → App.InitializeDesktop() line 46
```

`InitializeDesktop` is `async void` and its `try/catch` only starts at line 88, so the exception is unobserved and crashes the application.

### 2. `MainWindow.Opened` handler can hang forever

In `MainWindow.axaml.cs` lines 126–151, the `Opened` event handler is an `async void` lambda. VM wiring code (lines 128–141) runs **before** the `try/finally`:

```csharp
Opened += async (_, _) =>
{
    if (DataContext is MainViewModel vm)
    {
        vm.PropertyChanged += OnViewModelPropertyChanged;
        // ... more setup ...
        SyncEditorText(vm.EditorBody);
        UpdateActiveEditorBindings();
    }

    try
    {
        await RestoreWindowLayoutAsync();
    }
    finally
    {
        _openedTaskSource.TrySetResult();   // only reached if code above doesn't throw
    }
};
```

If anything in the `if` block throws (e.g., `SyncEditorText` or `ApplyEditorDisplayMode`), `_openedTaskSource` is never signaled. `CompleteStartupInitializationAsync()` awaits `_openedTaskSource.Task` (line 230), so it blocks forever. The window remains at `Opacity = 0`.

### 3. `App.InitializeDesktop` has unguarded service construction

`InitializeDesktop` constructs services and loads startup state before the `try/catch`. Any exception from `FolderSettingsService`, `FontCatalogService`, `MainWindow` constructor, etc. is unhandled.

## Fix

### Step 1 – Harden `FolderSettingsService` against corrupt JSON

In `FolderSettingsService`, wrap both `LoadRecordSync()` and `LoadRecordAsync()` so that **any** read or deserialization failure returns a fresh `SettingsRecord()` (defaults) instead of throwing.

```csharp
private SettingsRecord LoadRecordSync()
{
    if (!File.Exists(_settingsFilePath))
    {
        return new SettingsRecord();
    }

    try
    {
        var json = File.ReadAllText(_settingsFilePath);
        var settings = JsonSerializer.Deserialize<SettingsRecord>(json, s_jsonOptions);
        return settings ?? new SettingsRecord();
    }
    catch
    {
        return new SettingsRecord();
    }
}
```

Apply the same pattern to `LoadRecordAsync()`.

**Why swallow all exceptions?** Settings are a convenience, not a requirement. A corrupt file should never prevent the app from launching. The user can reconfigure settings afterward.

### Step 2 – Restructure `MainWindow.Opened` to always complete the TCS

Move **all** handler code inside a `try/catch/finally` so `_openedTaskSource.TrySetResult()` is unconditional:

```csharp
Opened += async (_, _) =>
{
    try
    {
        if (DataContext is MainViewModel vm)
        {
            vm.PropertyChanged += OnViewModelPropertyChanged;
            vm.FocusEditorRequested += OnFocusEditorRequested;
            vm.SecondaryPanes.CollectionChanged += OnSecondaryPanesCollectionChanged;
            foreach (var pane in vm.SecondaryPanes)
            {
                pane.PropertyChanged += OnSecondaryPaneViewModelPropertyChanged;
            }
            _editorHost.SetBaseDirectoryPath(vm.NotesFolder);
            ApplyEditorDisplayMode(vm.ShowYamlFrontMatterInEditor);
            SyncEditorText(vm.EditorBody);
            UpdateActiveEditorBindings();
        }

        await RestoreWindowLayoutAsync();
    }
    catch (Exception ex)
    {
        // Optional: log or surface the error via the VM if available
        if (DataContext is MainViewModel vm)
        {
            vm.StatusMessage = $"Layout restore failed: {ex.Message}";
        }
    }
    finally
    {
        _openedTaskSource.TrySetResult();
    }
};
```

This guarantees the window handshake never deadlocks, even if layout restoration or editor sync throws.

### Step 3 – Guard early startup in `App.InitializeDesktop`

Wrap `StartupStateService.Load()` and the initial appearance application in a `try/catch` so that a failure in the startup snapshot path falls back to safe defaults rather than crashing:

```csharp
private async void InitializeDesktop(IClassicDesktopStyleApplicationLifetime desktop)
{
    var settingsService = new FolderSettingsService();
    var fontCatalog = new FontCatalogService();
    var startupStateService = new StartupStateService(settingsService, fontCatalog);

    StartupStateSnapshot startup;
    try
    {
        startup = startupStateService.Load();
    }
    catch (Exception)
    {
        // Fallback: use blank settings and the default theme so the app can still open.
        var fonts = fontCatalog.LoadBundledFonts();
        var defaultFont = FontResolutionHelper.FindByKey(fonts, FontCatalogService.DefaultFontKey) ?? fonts[0];
        var defaultVariant = FontResolutionHelper.GetDefaultVariant(defaultFont);
        startup = new StartupStateSnapshot(
            new AppSettings(),
            null,
            AppTheme.Dark,
            fonts,
            defaultFont,
            defaultVariant,
            defaultFont,
            defaultVariant,
            defaultFont,
            defaultVariant,
            12);
    }

    ApplyStartupAppearance(startup);
    // ... rest of the method unchanged ...
}
```

This is a last-resort safety net; Step 1 makes it unlikely to trigger, but it protects against future service-level regressions.

## Files to Modify

| File | Change |
|------|--------|
| `src/GroundNotes/Services/FolderSettingsService.cs` | Wrap `LoadRecordSync()` and `LoadRecordAsync()` in `try/catch`, returning `new SettingsRecord()` on failure. |
| `src/GroundNotes/Views/MainWindow.axaml.cs` | Move all `Opened` handler code into a `try/catch/finally`; keep `_openedTaskSource.TrySetResult()` in the `finally`. |
| `src/GroundNotes/App.axaml.cs` | Add `try/catch` around `StartupStateService.Load()` with a fallback `StartupStateSnapshot`. |
| `tests/GroundNotes.Tests/FolderSettingsServiceTests.cs` | Add tests verifying corrupt/truncated JSON returns defaults instead of throwing. |

## Verification

1. **Build**
   ```bash
   dotnet build GroundNotes.sln
   ```

2. **Unit tests**
   ```bash
   dotnet test tests/GroundNotes.Tests/GroundNotes.Tests.csproj --no-build --filter "FullyQualifiedName~FolderSettingsServiceTests"
   dotnet test GroundNotes.sln --no-build
   ```

3. **Manual regression – corrupt settings**
   ```bash
   # Replace settings.json with garbage
   echo "not json {" > ~/.local/share/GroundNotes/settings.json
   dotnet run --project src/GroundNotes
   ```
   Expect: app starts with default settings, no crash, window becomes visible.

4. **Manual regression – missing settings folder**
   ```bash
   rm -rf ~/.local/share/GroundNotes
   dotnet run --project src/GroundNotes
   ```
   Expect: app starts normally with defaults.

5. **Full suite**
   ```bash
   dotnet test GroundNotes.sln --no-build
   ```
   All existing tests must still pass.
