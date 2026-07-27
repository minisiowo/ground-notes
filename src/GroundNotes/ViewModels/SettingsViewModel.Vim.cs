using CommunityToolkit.Mvvm.ComponentModel;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class SettingsViewModel
{
    private VimModeSettings _baseVimModeSettings = VimModeSettings.Default;

    [ObservableProperty]
    private bool _isVimModeEnabled;

    [ObservableProperty]
    private bool _useStandardVimCtrlBindings = true;

    [ObservableProperty]
    private int _vimKeySequenceTimeoutMilliseconds = VimModeSettings.DefaultKeySequenceTimeoutMilliseconds;

    [ObservableProperty]
    private bool _showVimStatus = true;

    private void InitializeVimModeSettings(VimModeSettings? settings)
    {
        _baseVimModeSettings = VimModeSettings.Normalize(settings);
        IsVimModeEnabled = _baseVimModeSettings.IsEnabled;
        UseStandardVimCtrlBindings = _baseVimModeSettings.UseStandardCtrlBindings;
        VimKeySequenceTimeoutMilliseconds = _baseVimModeSettings.KeySequenceTimeoutMilliseconds;
        ShowVimStatus = _baseVimModeSettings.ShowStatus;
    }

    private VimModeSettings BuildVimModeSettings()
    {
        return VimModeSettings.Normalize(_baseVimModeSettings with
        {
            IsEnabled = IsVimModeEnabled,
            UseStandardCtrlBindings = UseStandardVimCtrlBindings,
            KeySequenceTimeoutMilliseconds = VimKeySequenceTimeoutMilliseconds,
            ShowStatus = ShowVimStatus
        });
    }

    partial void OnIsVimModeEnabledChanged(bool value) => RaisePreviewRequested();

    partial void OnUseStandardVimCtrlBindingsChanged(bool value) => RaisePreviewRequested();

    partial void OnVimKeySequenceTimeoutMillisecondsChanged(int value) => RaisePreviewRequested();

    partial void OnShowVimStatusChanged(bool value) => RaisePreviewRequested();
}
