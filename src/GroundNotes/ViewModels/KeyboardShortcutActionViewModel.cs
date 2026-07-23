using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class KeyboardShortcutActionViewModel : ViewModelBase
{
    private ApplicationShortcutModifier _applicationModifier;

    public KeyboardShortcutActionViewModel(
        KeyboardShortcutDefinition definition,
        IEnumerable<KeyboardShortcutBinding> bindings,
        ApplicationShortcutModifier applicationModifier,
        IReadOnlyList<string> availableKeys)
    {
        Definition = definition;
        _applicationModifier = applicationModifier;
        AvailableKeys = availableKeys;
        Bindings = new ObservableCollection<KeyboardShortcutBindingViewModel>(
            bindings.Select(CreateBindingViewModel));
    }

    public event EventHandler? Changed;

    public KeyboardShortcutDefinition Definition { get; }

    public string Id => Definition.Id;

    public string Name => Definition.Name;

    public string Category => Definition.Category;

    public KeyboardShortcutScope Scope => Definition.Scope;

    public IReadOnlyList<string> AvailableKeys { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasBindings))]
    private ObservableCollection<KeyboardShortcutBindingViewModel> _bindings;

    public bool HasBindings => Bindings.Count > 0;

    public IReadOnlyList<KeyboardShortcutBinding> BuildBindings()
    {
        return Bindings.Select(binding => binding.BuildBinding()).ToList();
    }

    public void SetApplicationModifier(ApplicationShortcutModifier modifier)
    {
        _applicationModifier = modifier;
        foreach (var binding in Bindings)
        {
            binding.SetApplicationModifier(modifier);
        }
    }

    [RelayCommand]
    private void AddModifierShortcut()
    {
        AddBinding(new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Modifier, "F8"));
    }

    [RelayCommand]
    private void AddDirectShortcut()
    {
        AddBinding(new KeyboardShortcutBinding(KeyboardShortcutBindingKind.Direct, "F8"));
    }

    [RelayCommand]
    private void RemoveShortcut(KeyboardShortcutBindingViewModel? binding)
    {
        if (binding is null || !Bindings.Remove(binding))
        {
            return;
        }

        binding.Changed -= OnBindingChanged;
        binding.RemoveRequested -= OnBindingRemoveRequested;
        OnPropertyChanged(nameof(HasBindings));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    [RelayCommand]
    private void ResetShortcuts()
    {
        foreach (var binding in Bindings)
        {
            binding.Changed -= OnBindingChanged;
            binding.RemoveRequested -= OnBindingRemoveRequested;
        }

        Bindings = new ObservableCollection<KeyboardShortcutBindingViewModel>(
            Definition.DefaultBindings.Select(CreateBindingViewModel));
        Changed?.Invoke(this, EventArgs.Empty);
    }

    private void AddBinding(KeyboardShortcutBinding binding)
    {
        var viewModel = CreateBindingViewModel(binding);
        Bindings.Add(viewModel);
        OnPropertyChanged(nameof(HasBindings));
        Changed?.Invoke(viewModel, EventArgs.Empty);
    }

    private KeyboardShortcutBindingViewModel CreateBindingViewModel(KeyboardShortcutBinding binding)
    {
        var viewModel = new KeyboardShortcutBindingViewModel(binding, _applicationModifier, AvailableKeys);
        viewModel.Changed += OnBindingChanged;
        viewModel.RemoveRequested += OnBindingRemoveRequested;
        return viewModel;
    }

    private void OnBindingChanged(object? sender, EventArgs e)
    {
        Changed?.Invoke(sender ?? this, EventArgs.Empty);
    }

    private void OnBindingRemoveRequested(object? sender, EventArgs e)
    {
        RemoveShortcut(sender as KeyboardShortcutBindingViewModel);
    }
}
