using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Views;

public partial class AiSettingsWindow : Window
{
    public AiSettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) => this.FindControl<TextBox>("ApiKeyTextBox")?.Focus();
    }

    public AiSettingsWindow(AiSettings settings, string promptsFolder) : this()
    {
        AiEnabledCheckBox.IsChecked = settings.IsEnabled;
        ApiKeyTextBox.Text = settings.ApiKey;
        DefaultModelTextBox.Text = settings.DefaultModel;
        ProjectIdTextBox.Text = settings.ProjectId;
        OrganizationIdTextBox.Text = settings.OrganizationId;
        PromptsFolderTextBlock.Text = string.IsNullOrWhiteSpace(promptsFolder)
            ? "Choose a notes folder first."
            : promptsFolder;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private void OnSaveClick(object? sender, RoutedEventArgs e)
    {
        var apiKey = ApiKeyTextBox.Text?.Trim() ?? string.Empty;
        var defaultModel = DefaultModelTextBox.Text?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(defaultModel))
        {
            defaultModel = AiSettings.Default.DefaultModel;
        }

        Close(new AiSettings(
            apiKey,
            defaultModel,
            AiEnabledCheckBox.IsChecked ?? true,
            ProjectIdTextBox.Text?.Trim() ?? string.Empty,
            OrganizationIdTextBox.Text?.Trim() ?? string.Empty));
    }

    private void OnCancelClick(object? sender, RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnSaveClick(sender, new RoutedEventArgs());
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }
}
