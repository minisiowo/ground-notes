using QuickNotesTxt.Models;
using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class SettingsWindowLayoutServiceTests
{
    [Fact]
    public void SaveWindowLayoutSync_UsesSynchronousSettingsPath()
    {
        var settingsService = new RecordingSettingsService();
        var service = new SettingsWindowLayoutService(settingsService);
        var layout = new WindowLayout(1200, 800, 10, 20, false, 300, false);

        service.SaveWindowLayoutSync(layout);

        Assert.True(settingsService.SaveSettingsSyncCalled);
        Assert.False(settingsService.SaveSettingsAsyncCalled);
        Assert.Equal(layout, settingsService.Settings.WindowLayout);
    }

    private sealed class RecordingSettingsService : ISettingsService
    {
        public AppSettings Settings { get; private set; } = new(
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            null,
            AiSettings.Default);

        public bool SaveSettingsSyncCalled { get; private set; }

        public bool SaveSettingsAsyncCalled { get; private set; }

        public AppSettings GetSettingsSync() => Settings;

        public Task<AppSettings> GetSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Settings);
        }

        public void SaveSettingsSync(AppSettings settings)
        {
            SaveSettingsSyncCalled = true;
            Settings = settings;
        }

        public Task SaveSettingsAsync(AppSettings settings, CancellationToken cancellationToken = default)
        {
            SaveSettingsAsyncCalled = true;
            Settings = settings;
            return Task.CompletedTask;
        }

        public Task UpdateSettingsAsync(Func<AppSettings, AppSettings> update, CancellationToken cancellationToken = default)
        {
            Settings = update(Settings);
            return Task.CompletedTask;
        }

        public Task<AiSettings> GetAiSettingsAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Settings.AiSettings);
        }

        public Task SetAiSettingsAsync(AiSettings settings, CancellationToken cancellationToken = default)
        {
            Settings = Settings with { AiSettings = settings };
            return Task.CompletedTask;
        }
    }
}
