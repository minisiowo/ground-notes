using System.Text.Json;
using System.Text.Json.Serialization;
using GroundNotes.Models;

namespace GroundNotes.Services;

public sealed class FolderSettingsService : ISettingsService
{
    private static JsonSerializerOptions s_jsonOptions => JsonDefaults.ReadOptions;

    private readonly string _settingsFilePath;
    private readonly SemaphoreSlim _settingsLock = new(1, 1);

    public FolderSettingsService()
        : this(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "GroundNotes"))
    {
    }

    public FolderSettingsService(string settingsDirectory)
    {
        Directory.CreateDirectory(settingsDirectory);
        _settingsFilePath = Path.Combine(settingsDirectory, "settings.json");
    }

    public async Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
    {
        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var record = await LoadRecordAsync(cancellationToken).ConfigureAwait(false);
            return MapToAppSettings(record);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public AppSettings GetSettingsSync()
    {
        _settingsLock.Wait();
        try
        {
            return MapToAppSettings(LoadRecordSync());
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        var normalized = NormalizeSettings(settings);
        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await SaveAsync(MapToRecord(normalized), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public void SaveSettingsSync(AppSettings settings)
    {
        var normalized = NormalizeSettings(settings);
        _settingsLock.Wait();
        try
        {
            SaveSync(MapToRecord(normalized));
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public void UpdateSettingsSync(Func<AppSettings, AppSettings> update)
    {
        ArgumentNullException.ThrowIfNull(update);

        _settingsLock.Wait();
        try
        {
            var current = MapToAppSettings(LoadRecordSync(tolerateErrors: false));
            var updated = NormalizeSettings(update(current));
            SaveSync(MapToRecord(updated));
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(update);

        await _settingsLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var record = await LoadRecordAsync(cancellationToken, tolerateErrors: false).ConfigureAwait(false);
            var current = MapToAppSettings(record);
            var updated = NormalizeSettings(update(current));
            await SaveAsync(MapToRecord(updated), cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _settingsLock.Release();
        }
    }

    public async Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default)
    {
        var settings = await GetSettingsAsync(cancellationToken);
        return settings.AiSettings;
    }

    private static AppSettings NormalizeSettings(AppSettings settings)
    {
        return settings with
        {
            EditorIndentSize = EditorDisplaySettings.NormalizeIndentSize(settings.EditorIndentSize),
            EditorLineHeightFactor = EditorDisplaySettings.NormalizeLineHeightFactor(settings.EditorLineHeightFactor),
            AiSettings = AiSettings.Normalize(settings.AiSettings),
            KeyboardShortcuts = KeyboardShortcutSettings.Normalize(settings.KeyboardShortcuts),
            StandardNoteWindowLayout = NoteWindowLayout.Normalize(settings.StandardNoteWindowLayout),
            ZenNoteWindowLayout = NoteWindowLayout.Normalize(settings.ZenNoteWindowLayout)
        };
    }

    private static AppSettings MapToAppSettings(SettingsRecord record)
    {
        return NormalizeSettings(new AppSettings(
            record.NotesFolder,
            record.EditorFontSize,
            record.UiFontSize,
            record.EditorIndentSize,
            record.EditorLineHeightFactor,
            record.FontName,
            record.FontVariantName,
            record.SidebarFontName,
            record.SidebarFontVariantName,
            record.CodeFontName,
            record.CodeFontVariantName,
            record.ThemeName,
            record.ShowYamlFrontMatterInEditor ?? false,
            record.ShowScrollBars ?? true,
            record.WindowLayout is null
                ? null
                : new WindowLayout(
                    record.WindowLayout.Width,
                    record.WindowLayout.Height,
                    record.WindowLayout.X,
                    record.WindowLayout.Y,
                    record.WindowLayout.IsMaximized,
                    record.WindowLayout.SidebarWidth,
                    record.WindowLayout.SidebarCollapsed,
                    record.WindowLayout.SidebarCalendarExpanded,
                    record.WindowLayout.EditorCanvasWidth,
                    record.WindowLayout.PaneSplitWeights,
                    record.WindowLayout.MultiPaneSharedWidth,
                    record.WindowLayout.SidebarExpandedTagPaths),
            new AiSettings(
                record.OpenAiApiKey ?? string.Empty,
                record.OpenAiModel ?? string.Empty,
                record.AiEnabled ?? AiSettings.Default.IsEnabled,
                record.OpenAiProjectId ?? string.Empty,
                record.OpenAiOrganizationId ?? string.Empty,
                record.OpenAiReasoningEffort ?? string.Empty),
            MapKeyboardShortcutSettings(record.KeyboardShortcuts),
            record.SidebarFontSize,
            record.ShowSidebarListBackground ?? true,
            record.ShowSidebarListBorder ?? true,
            record.FileListFontName,
            record.FileListFontVariantName,
            record.FileListFontSize,
            record.UiFontName,
            record.UiFontVariantName,
            MapNoteWindowLayout(record.StandardNoteWindowLayout),
            MapNoteWindowLayout(record.ZenNoteWindowLayout)));
    }

    private static SettingsRecord MapToRecord(AppSettings settings)
    {
        return new SettingsRecord
        {
            NotesFolder = settings.NotesFolder,
            EditorFontSize = settings.EditorFontSize,
            UiFontSize = settings.UiFontSize,
            EditorIndentSize = settings.EditorIndentSize,
            EditorLineHeightFactor = settings.EditorLineHeightFactor,
            FontName = settings.FontName,
            FontVariantName = settings.FontVariantName,
            SidebarFontName = settings.SidebarFontName,
            SidebarFontVariantName = settings.SidebarFontVariantName,
            SidebarFontSize = settings.SidebarFontSize,
            ShowSidebarListBackground = settings.ShowSidebarListBackground,
            ShowSidebarListBorder = settings.ShowSidebarListBorder,
            FileListFontName = settings.FileListFontName,
            FileListFontVariantName = settings.FileListFontVariantName,
            FileListFontSize = settings.FileListFontSize,
            UiFontName = settings.UiFontName,
            UiFontVariantName = settings.UiFontVariantName,
            CodeFontName = settings.CodeFontName,
            CodeFontVariantName = settings.CodeFontVariantName,
            ThemeName = settings.ThemeName,
            ShowYamlFrontMatterInEditor = settings.ShowYamlFrontMatterInEditor,
            ShowScrollBars = settings.ShowScrollBars,
            WindowLayout = settings.WindowLayout is null
                ? null
                : new WindowLayoutRecord
                {
                    Width = settings.WindowLayout.Width,
                    Height = settings.WindowLayout.Height,
                    X = settings.WindowLayout.X,
                    Y = settings.WindowLayout.Y,
                    IsMaximized = settings.WindowLayout.IsMaximized,
                    SidebarWidth = settings.WindowLayout.SidebarWidth,
                    SidebarCollapsed = settings.WindowLayout.SidebarCollapsed,
                    SidebarCalendarExpanded = settings.WindowLayout.SidebarCalendarExpanded,
                    EditorCanvasWidth = settings.WindowLayout.EditorCanvasWidth,
                    PaneSplitWeights = settings.WindowLayout.PaneSplitWeights?.ToList(),
                    MultiPaneSharedWidth = settings.WindowLayout.MultiPaneSharedWidth,
                    SidebarExpandedTagPaths = settings.WindowLayout.SidebarExpandedTagPaths?.ToList()
                },
            OpenAiApiKey = settings.AiSettings.ApiKey,
            OpenAiModel = settings.AiSettings.DefaultModel,
            AiEnabled = settings.AiSettings.IsEnabled,
            OpenAiProjectId = settings.AiSettings.ProjectId,
            OpenAiOrganizationId = settings.AiSettings.OrganizationId,
            OpenAiReasoningEffort = settings.AiSettings.DefaultReasoningEffort,
            KeyboardShortcuts = MapKeyboardShortcutSettings(settings.KeyboardShortcuts),
            StandardNoteWindowLayout = MapNoteWindowLayout(settings.StandardNoteWindowLayout),
            ZenNoteWindowLayout = MapNoteWindowLayout(settings.ZenNoteWindowLayout)
        };
    }

    private static NoteWindowLayout? MapNoteWindowLayout(NoteWindowLayoutRecord? record)
    {
        return record is not { Width: { } width, Height: { } height }
            ? null
            : NoteWindowLayout.Normalize(new NoteWindowLayout(width, height));
    }

    private static NoteWindowLayoutRecord? MapNoteWindowLayout(NoteWindowLayout? layout)
    {
        var normalized = NoteWindowLayout.Normalize(layout);
        return normalized is null
            ? null
            : new NoteWindowLayoutRecord { Width = normalized.Width, Height = normalized.Height };
    }

    private static KeyboardShortcutSettings MapKeyboardShortcutSettings(KeyboardShortcutSettingsRecord? record)
    {
        if (record?.Bindings is null)
        {
            return KeyboardShortcutSettings.CreateDefault();
        }

        var storedModifier = record.ApplicationModifier ?? LegacyApplicationShortcutModifier.None;
        var storedModifierBinding = BuildModifierBinding(storedModifier);
        var convertedWithStoredModifier = ConvertBindings(record.Bindings, storedModifier);
        var isLegacy = record.ApplicationModifier.HasValue
                       || record.Bindings.Values.SelectMany(bindings => bindings).Any(binding => binding.Kind.HasValue);
        if (!isLegacy)
        {
            return KeyboardShortcutSettings.Normalize(new KeyboardShortcutSettings(convertedWithStoredModifier));
        }

        var usesCompleteLegacyDefaults = KeyboardShortcutCatalog.IsCompleteLegacyDefaultConfiguration(
            convertedWithStoredModifier,
            storedModifierBinding);
        var effectiveModifier = OperatingSystem.IsMacOS()
                                && usesCompleteLegacyDefaults
                                && storedModifier == LegacyApplicationShortcutModifier.Control
            ? LegacyApplicationShortcutModifier.Meta
            : storedModifier;
        var effectiveModifierBinding = BuildModifierBinding(effectiveModifier);
        var converted = ConvertBindings(record.Bindings, effectiveModifier);
        var migrated = new Dictionary<string, List<KeyboardShortcutBinding>>(StringComparer.Ordinal);
        foreach (var definition in KeyboardShortcutCatalog.Definitions)
        {
            if (converted.TryGetValue(definition.Id, out var configured))
            {
                migrated[definition.Id] = KeyboardShortcutCatalog
                    .NormalizeLegacyBindings(definition, configured, effectiveModifierBinding)
                    .ToList();
            }
            else if (definition.Id is not KeyboardShortcutActionIds.ToggleZenMode
                     and not KeyboardShortcutActionIds.NewNoteWindow)
            {
                migrated[definition.Id] = definition.DefaultBindings.ToList();
            }
        }

        return new KeyboardShortcutSettings(migrated);
    }

    private static KeyboardShortcutSettingsRecord MapKeyboardShortcutSettings(KeyboardShortcutSettings? settings)
    {
        var normalized = KeyboardShortcutSettings.Normalize(settings);
        return new KeyboardShortcutSettingsRecord
        {
            Bindings = normalized.Bindings.ToDictionary(
                pair => pair.Key,
                pair => pair.Value.Select(binding => new KeyboardShortcutBindingRecord
                {
                    Key = binding.Key,
                    Control = binding.Control,
                    Shift = binding.Shift,
                    Alt = binding.Alt,
                    Meta = binding.Meta
                }).ToList(),
                StringComparer.Ordinal)
        };
    }

    private static Dictionary<string, List<KeyboardShortcutBinding>> ConvertBindings(
        IReadOnlyDictionary<string, List<KeyboardShortcutBindingRecord>> bindings,
        LegacyApplicationShortcutModifier applicationModifier)
    {
        return bindings.ToDictionary(
            pair => pair.Key,
            pair => pair.Value.Select(binding => ConvertBinding(binding, applicationModifier)).ToList(),
            StringComparer.Ordinal);
    }

    private static KeyboardShortcutBinding ConvertBinding(
        KeyboardShortcutBindingRecord binding,
        LegacyApplicationShortcutModifier applicationModifier)
    {
        var key = binding.Key ?? string.Empty;
        if (binding.Kind == LegacyKeyboardShortcutBindingKind.Modifier)
        {
            return BuildModifierBinding(applicationModifier) with { Key = key };
        }

        return new KeyboardShortcutBinding(key, binding.Control, binding.Shift, binding.Alt, binding.Meta);
    }

    private static KeyboardShortcutBinding BuildModifierBinding(LegacyApplicationShortcutModifier modifier)
    {
        return modifier switch
        {
            LegacyApplicationShortcutModifier.Control => new KeyboardShortcutBinding(string.Empty, Control: true),
            LegacyApplicationShortcutModifier.Shift => new KeyboardShortcutBinding(string.Empty, Shift: true),
            LegacyApplicationShortcutModifier.Alt => new KeyboardShortcutBinding(string.Empty, Alt: true),
            LegacyApplicationShortcutModifier.Meta => new KeyboardShortcutBinding(string.Empty, Meta: true),
            _ => new KeyboardShortcutBinding(string.Empty)
        };
    }

    private async Task<SettingsRecord> LoadRecordAsync(
        CancellationToken cancellationToken,
        bool tolerateErrors = true)
    {
        if (!File.Exists(_settingsFilePath))
        {
            return new SettingsRecord();
        }

        try
        {
            await using var stream = File.OpenRead(_settingsFilePath);
            var settings = await JsonSerializer
                .DeserializeAsync<SettingsRecord>(stream, s_jsonOptions, cancellationToken)
                .ConfigureAwait(false);
            return settings ?? new SettingsRecord();
        }
        catch when (tolerateErrors)
        {
            return new SettingsRecord();
        }
    }

    private SettingsRecord LoadRecordSync(bool tolerateErrors = true)
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
        catch when (tolerateErrors)
        {
            return new SettingsRecord();
        }
    }

    private async Task SaveAsync(SettingsRecord record, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        var temporaryPath = CreateTemporarySettingsPath();
        try
        {
            await using (var stream = File.Create(temporaryPath))
            {
                await JsonSerializer
                    .SerializeAsync(stream, record, cancellationToken: cancellationToken)
                    .ConfigureAwait(false);
            }

            File.Move(temporaryPath, _settingsFilePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private void SaveSync(SettingsRecord record)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);
        var temporaryPath = CreateTemporarySettingsPath();
        try
        {
            var json = JsonSerializer.Serialize(record);
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, _settingsFilePath, overwrite: true);
        }
        finally
        {
            File.Delete(temporaryPath);
        }
    }

    private string CreateTemporarySettingsPath()
    {
        return $"{_settingsFilePath}.{Guid.NewGuid():N}.tmp";
    }

    private sealed class SettingsRecord
    {
        public string? NotesFolder { get; set; }
        public double? EditorFontSize { get; set; }
        public double? UiFontSize { get; set; }
        public int? EditorIndentSize { get; set; }
        public double? EditorLineHeightFactor { get; set; }
        public string? FontName { get; set; }
        public string? FontVariantName { get; set; }
        public string? SidebarFontName { get; set; }
        public string? SidebarFontVariantName { get; set; }
        public double? SidebarFontSize { get; set; }
        public bool? ShowSidebarListBackground { get; set; }
        public bool? ShowSidebarListBorder { get; set; }
        public string? FileListFontName { get; set; }
        public string? FileListFontVariantName { get; set; }
        public double? FileListFontSize { get; set; }
        public string? UiFontName { get; set; }
        public string? UiFontVariantName { get; set; }
        public string? CodeFontName { get; set; }
        public string? CodeFontVariantName { get; set; }
        public string? ThemeName { get; set; }
        public bool? ShowYamlFrontMatterInEditor { get; set; }
        public bool? ShowScrollBars { get; set; }
        public WindowLayoutRecord? WindowLayout { get; set; }
        public NoteWindowLayoutRecord? StandardNoteWindowLayout { get; set; }
        public NoteWindowLayoutRecord? ZenNoteWindowLayout { get; set; }
        public string? OpenAiApiKey { get; set; }
        public string? OpenAiModel { get; set; }
        public bool? AiEnabled { get; set; }
        public string? OpenAiProjectId { get; set; }
        public string? OpenAiOrganizationId { get; set; }
        public string? OpenAiReasoningEffort { get; set; }
        public KeyboardShortcutSettingsRecord? KeyboardShortcuts { get; set; }
    }

    private sealed class KeyboardShortcutSettingsRecord
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LegacyApplicationShortcutModifier? ApplicationModifier { get; set; }

        public Dictionary<string, List<KeyboardShortcutBindingRecord>>? Bindings { get; set; }
    }

    private sealed class KeyboardShortcutBindingRecord
    {
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public LegacyKeyboardShortcutBindingKind? Kind { get; set; }

        public string? Key { get; set; }
        public bool Control { get; set; }
        public bool Shift { get; set; }
        public bool Alt { get; set; }
        public bool Meta { get; set; }
    }

    private enum LegacyApplicationShortcutModifier
    {
        None,
        Control,
        Alt,
        Shift,
        Meta
    }

    private enum LegacyKeyboardShortcutBindingKind
    {
        Modifier,
        Direct
    }

    private sealed class NoteWindowLayoutRecord
    {
        public double? Width { get; set; }
        public double? Height { get; set; }
    }

    private sealed class WindowLayoutRecord
    {
        public double Width { get; set; }
        public double Height { get; set; }
        public double X { get; set; }
        public double Y { get; set; }
        public bool IsMaximized { get; set; }
        public double? SidebarWidth { get; set; }
        public bool? SidebarCollapsed { get; set; }
        public bool? SidebarCalendarExpanded { get; set; }
        public double? EditorCanvasWidth { get; set; }
        public List<double>? PaneSplitWeights { get; set; }
        public double? MultiPaneSharedWidth { get; set; }
        public List<string>? SidebarExpandedTagPaths { get; set; }
    }
}
