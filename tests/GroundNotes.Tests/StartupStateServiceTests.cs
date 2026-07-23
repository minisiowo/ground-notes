using GroundNotes.Models;
using GroundNotes.Services;
using GroundNotes.Styles;
using GroundNotes.Tests.Helpers;
using Xunit;

namespace GroundNotes.Tests;

public sealed class StartupStateServiceTests : IDisposable
{
    private readonly TempDirectoryFixture _temp = new();

    [Fact]
    public void Load_ReturnsDefaultThemeWhenNoneConfigured()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.NotNull(snapshot.Theme);
        Assert.Equal(AppTheme.Dark.Name, snapshot.Theme.Name);
    }

    [Fact]
    public void Load_ResolvesBuiltInThemeByName()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        settingsService.SaveSettingsSync(new AppSettings(
            null, null, null, null, null,
            null, null, null, null, null, null,
            AppTheme.Light.Name, false, true, null, AiSettings.Default));
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.Equal(AppTheme.Light.Name, snapshot.Theme.Name);
    }

    [Fact]
    public void Load_ResolvesDefaultFontFamily()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.NotNull(snapshot.UiFontFamily);
        Assert.NotNull(snapshot.UiFontVariant);
        Assert.NotNull(snapshot.TerminalFontFamily);
        Assert.NotNull(snapshot.TerminalFontVariant);
        Assert.NotNull(snapshot.CodeFontFamily);
        Assert.NotNull(snapshot.CodeFontVariant);
    }

    [Fact]
    public void Load_ClampsUiFontSize()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        settingsService.SaveSettingsSync(new AppSettings(
            null, null, 999, null, null,
            null, null, null, null, null, null,
            null, false, true, null, AiSettings.Default));
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.True(snapshot.UiFontSize <= 20);
    }

    [Fact]
    public void Load_DefaultsFileListFontSize()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.Equal(11, snapshot.FileListFontSize);
    }

    [Fact]
    public void Load_UsesExistingSidebarSizeForFileListMigration()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        var settings = settingsService.GetSettingsSync() with { SidebarFontSize = 9 };
        settingsService.SaveSettingsSync(settings);
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.Equal(9, snapshot.FileListFontSize);
    }

    [Fact]
    public void Load_ClampsLegacySidebarSizeForFileListMigration()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        var settings = settingsService.GetSettingsSync() with { SidebarFontSize = 999 };
        settingsService.SaveSettingsSync(settings);
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.Equal(18, snapshot.FileListFontSize);
    }

    [Fact]
    public void Load_ReturnsSettingsObject()
    {
        var settingsService = new FolderSettingsService(_temp.Root);
        var fontCatalog = new FontCatalogService();
        var service = new StartupStateService(settingsService, fontCatalog);

        var snapshot = service.Load();

        Assert.NotNull(snapshot.Settings);
    }

    public void Dispose() => _temp.Dispose();
}
