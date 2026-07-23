using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;
using GroundNotes.Services;

namespace GroundNotes.ViewModels;

public sealed partial class KeyboardShortcutBindingViewModel : ViewModelBase
{
    private readonly KeyboardShortcutService _formatter = new();
    private ApplicationShortcutModifier _applicationModifier;
    private bool _isCapturing;

    public KeyboardShortcutBindingViewModel(
        KeyboardShortcutBinding binding,
        ApplicationShortcutModifier applicationModifier,
        IReadOnlyList<string> availableKeys)
    {
        _applicationModifier = applicationModifier;
        AvailableKeys = availableKeys;
        _kind = binding.Kind;
        _selectedKey = availableKeys.Contains(binding.Key, StringComparer.OrdinalIgnoreCase)
            ? availableKeys.First(key => string.Equals(key, binding.Key, StringComparison.OrdinalIgnoreCase))
            : availableKeys.FirstOrDefault() ?? "F8";
        _control = binding.Control;
        _shift = binding.Shift;
        _alt = binding.Alt;
        _meta = binding.Meta;
        UpdateFormatter();
    }

    public event EventHandler? Changed;

    public event EventHandler? RemoveRequested;

    public IReadOnlyList<string> AvailableKeys { get; }

    public string KindDisplay => Kind.ToString();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
    private KeyboardShortcutBindingKind _kind;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Display))]
    [NotifyPropertyChangedFor(nameof(CaptureButtonText))]
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

    public string Display => _formatter.Format(BuildBinding());

    public string CaptureButtonText => IsRecording ? "Press shortcut..." : Display;

    public string StatusText => IsApplied ? "Active" : "Not applied";

    public bool HasValidationError => !string.IsNullOrWhiteSpace(ValidationMessage);

    public KeyboardShortcutBinding BuildBinding()
    {
        return new KeyboardShortcutBinding(Kind, SelectedKey, Control, Shift, Alt, Meta);
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
        if (Kind == KeyboardShortcutBindingKind.Direct)
        {
            Control = control;
            Shift = shift;
            Alt = alt;
            Meta = meta;
        }
        else
        {
            Control = false;
            Shift = false;
            Alt = false;
            Meta = false;
        }
        _isCapturing = false;
        IsRecording = false;
        RaiseChanged();
    }

    [RelayCommand]
    private void Remove()
    {
        RemoveRequested?.Invoke(this, EventArgs.Empty);
    }

    public void SetValidation(string? message)
    {
        ValidationMessage = message ?? string.Empty;
        IsApplied = string.IsNullOrEmpty(ValidationMessage);
    }

    public void SetApplicationModifier(ApplicationShortcutModifier modifier)
    {
        if (_applicationModifier == modifier)
        {
            return;
        }

        _applicationModifier = modifier;
        UpdateFormatter();
        OnPropertyChanged(nameof(Display));
        OnPropertyChanged(nameof(CaptureButtonText));
    }

    partial void OnKindChanged(KeyboardShortcutBindingKind value)
    {
        OnPropertyChanged(nameof(KindDisplay));
        RaiseChanged();
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

        UpdateFormatter();
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void UpdateFormatter()
    {
        _formatter.ApplySettings(new KeyboardShortcutSettings(
            _applicationModifier,
            new Dictionary<string, List<KeyboardShortcutBinding>>(StringComparer.Ordinal)));
    }
}
