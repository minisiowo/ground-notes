using Avalonia.Controls;
using Avalonia.Input;
using GroundNotes.Models;
using GroundNotes.ViewModels;

namespace GroundNotes.Views;

public partial class CustomSlashCommandEditorWindow : Window
{
    private readonly DialogWindowController _controller;
    public CustomSlashCommandDefinition? Command { get; private set; }
    public CustomSlashCommandEditorWindow() : this(new CustomSlashCommandEditorViewModel(null, false)) { }

    public CustomSlashCommandEditorWindow(CustomSlashCommandEditorViewModel model)
    {
        InitializeComponent(); DataContext = model;
        _controller = new DialogWindowController(this, () => Close(false), null);
        _controller.Attach();
    }
    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e) => _controller.OnTitleBarPointerPressed(e);
    private void OnCloseRequested(object? sender, EventArgs e) => _controller.OnCloseRequested();
    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e) => Close(false);
    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    { Command = ((CustomSlashCommandEditorViewModel)DataContext!).BuildCommand(); Close(true); }
    private void OnWindowKeyDown(object? sender, KeyEventArgs e) { if (_controller.HandleEscape(e)) e.Handled = true; }
}
