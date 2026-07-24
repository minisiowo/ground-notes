using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class KeyboardShortcutActionViewModel : ViewModelBase
{
    public KeyboardShortcutActionViewModel(
        KeyboardShortcutDefinition definition,
        IEnumerable<KeyboardShortcutBinding> bindings,
        IReadOnlyList<string> availableKeys)
    {
        Definition = definition;
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
        return Bindings
            .Where(binding => !binding.IsEmpty)
            .Select(binding => binding.BuildBinding())
            .ToList();
    }

    public KeyboardShortcutBindingViewModel GetOrCreateEmptyShortcut()
    {
        if (Bindings.Count > 0)
        {
            return Bindings[0];
        }

        var viewModel = CreateBindingViewModel(new KeyboardShortcutBinding(string.Empty));
        Bindings.Add(viewModel);
        OnPropertyChanged(nameof(HasBindings));
        return viewModel;
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



    private KeyboardShortcutBindingViewModel CreateBindingViewModel(KeyboardShortcutBinding binding)
    {
        var viewModel = new KeyboardShortcutBindingViewModel(binding, AvailableKeys);
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
