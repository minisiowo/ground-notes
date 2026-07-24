using CommunityToolkit.Mvvm.ComponentModel;

using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

public sealed partial class KeyboardShortcutBindingViewModel : ViewModelBase
{
    private readonly KeyboardShortcutService _formatter = new();
    private bool _isCapturing;

    public KeyboardShortcutBindingViewModel(
        KeyboardShortcutBinding binding,
        IReadOnlyList<string> availableKeys)
    {
        AvailableKeys = availableKeys;
        _selectedKey = string.IsNullOrWhiteSpace(binding.Key)
            ? string.Empty
            : availableKeys.Contains(binding.Key, StringComparer.OrdinalIgnoreCase)
                ? availableKeys.First(key => string.Equals(key, binding.Key, StringComparison.OrdinalIgnoreCase))
                : binding.Key;
        _control = binding.Control;
        _shift = binding.Shift;
        _alt = binding.Alt;
        _meta = binding.Meta;
    }

    public event EventHandler? Changed;

    public event EventHandler? RemoveRequested;

    public IReadOnlyList<string> AvailableKeys { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    [NotifyPropertyChangedFor(nameof(IsEmpty))]
    private string _selectedKey;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private bool _control;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private bool _shift;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private bool _alt;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private bool _meta;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private bool _isRecording;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private bool _isApplied = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasValidationError))]
    private string _validationMessage = string.Empty;

    public bool IsEmpty => string.IsNullOrWhiteSpace(SelectedKey);

    public string Display => IsEmpty ? "Blank" : _formatter.Format(BuildBinding());

    public string CaptureButtonText => IsRecording ? "Press shortcut..." : Display;

    public string StatusText => IsApplied ? "Active" : "Not applied";

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public KeyboardShortcutBinding BuildBinding()
    {
        return new KeyboardShortcutBinding(SelectedKey, Control, Shift, Alt, Meta);
    }

    public void BeginRecording()
    {
        IsRecording = true;
    }

    public void CancelRecording()
    {
        IsRecording = false;
    }

    public void Capture(string key, bool control, bool shift, bool alt, bool meta)
    {
        _isCapturing = true;
        SelectedKey = key;
        Control = control;
        Shift = shift;
        Alt = alt;
        Meta = meta;
        _isCapturing = false;
        IsRecording = false;
        RaiseChanged();
    }

    public void Clear()
    {
        IsRecording = false;
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetValidation(string? message)
    {
        ValidationMessage = message ?? string.Empty;
        IsApplied = string.IsNullOrEmpty(ValidationMessage);
    }

    partial void OnSelectedKeyChanged(string value) => RaiseChanged();

    partial void OnControlChanged(bool value) => RaiseChanged();

    partial void OnShiftChanged(bool value) => RaiseChanged();

    partial void OnAltChanged(bool value) => RaiseChanged();

    partial void OnMetaChanged(bool value) => RaiseChanged();

    private void RaiseChanged()
    {
        if (_isCapturing)
        {
            return;
        }

        Changed?.Invoke(this, EventArgs.Empty);
    }
}
