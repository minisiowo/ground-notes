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

        Assert.NotNull(snapshot.TerminalFontFamily);
        Assert.NotNull(snapshot.TerminalFontVariant);
        Assert.NotNull(snapshot.SidebarFontFamily);
        Assert.NotNull(snapshot.SidebarFontVariant);
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
