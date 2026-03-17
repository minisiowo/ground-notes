using Avalonia.Controls;
using Avalonia.Input;
using QuickNotesTxt.Models;

namespace QuickNotesTxt.Views;

public partial class SettingsWindow : Window
{
    private IReadOnlyList<BundledFontFamilyOption> _fontFamilies = [];
    private bool _isInitializing;
    private bool _isUpdatingVariantSelection;
    private SettingsDialogModel? _lastPreviewModel;

    public Func<SettingsDialogModel, Task>? PreviewSettingsAsync { get; set; }

    public SettingsWindow()
    {
        InitializeComponent();
        Opened += (_, _) => ThemeComboBox.Focus();
    }

    public SettingsWindow(SettingsDialogModel model) : this()
    {
        _isInitializing = true;
        _fontFamilies = model.FontFamilies;

        ThemeComboBox.ItemsSource = model.ThemeNames;
        ThemeComboBox.SelectedItem = model.SelectedThemeName;

        SidebarFontFamilyComboBox.ItemsSource = _fontFamilies.Select(font => font.DisplayName).ToList();
        SidebarFontFamilyComboBox.SelectedItem = model.SelectedSidebarFontFamilyName;

        FontFamilyComboBox.ItemsSource = _fontFamilies.Select(font => font.DisplayName).ToList();
        FontFamilyComboBox.SelectedItem = model.SelectedFontFamilyName;

        CodeFontFamilyComboBox.ItemsSource = _fontFamilies.Select(font => font.DisplayName).ToList();
        CodeFontFamilyComboBox.SelectedItem = model.SelectedCodeFontFamilyName;

        EditorFontSizeComboBox.ItemsSource = Enumerable.Range(10, 15).Select(static size => size.ToString()).ToList();
        EditorFontSizeComboBox.SelectedItem = Math.Round(model.EditorFontSize).ToString("0");

        UiFontSizeComboBox.ItemsSource = Enumerable.Range(10, 11).Select(static size => size.ToString()).ToList();
        UiFontSizeComboBox.SelectedItem = Math.Round(model.UiFontSize).ToString("0");

        AiEnabledToggle.IsChecked = model.IsAiEnabled;
        ApiKeyTextBox.Text = model.ApiKey;
        DefaultModelTextBox.Text = model.DefaultModel;
        ProjectIdTextBox.Text = model.ProjectId;
        OrganizationIdTextBox.Text = model.OrganizationId;
        PromptsFolderTextBlock.Text = string.IsNullOrWhiteSpace(model.PromptsDirectory)
            ? "Choose a notes folder first."
            : model.PromptsDirectory;

        PopulateVariants(SidebarFontVariantComboBox, model.SelectedSidebarFontFamilyName, model.SelectedSidebarFontVariantName);
        PopulateVariants(FontVariantComboBox, model.SelectedFontFamilyName, model.SelectedFontVariantName);
        PopulateVariants(CodeFontVariantComboBox, model.SelectedCodeFontFamilyName, model.SelectedCodeFontVariantName);
        _isInitializing = false;
    }

    private async void OnSidebarFontFamilySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedFamilyName = SidebarFontFamilyComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFamilyName))
        {
            return;
        }

        var selectedVariantName = SidebarFontVariantComboBox.SelectedItem as string;
        var variantChanged = PopulateVariants(SidebarFontVariantComboBox, selectedFamilyName, selectedVariantName);
        if (!variantChanged)
        {
            await RequestPreviewAsync();
        }
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
        {
            BeginMoveDrag(e);
        }
    }

    private async void OnFontFamilySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedFamilyName = FontFamilyComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFamilyName))
        {
            return;
        }

        var selectedVariantName = FontVariantComboBox.SelectedItem as string;
        var variantChanged = PopulateVariants(FontVariantComboBox, selectedFamilyName, selectedVariantName);
        if (!variantChanged)
        {
            await RequestPreviewAsync();
        }
    }

    private async void OnCodeFontFamilySelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        var selectedFamilyName = CodeFontFamilyComboBox.SelectedItem as string;
        if (string.IsNullOrWhiteSpace(selectedFamilyName))
        {
            return;
        }

        var selectedVariantName = CodeFontVariantComboBox.SelectedItem as string;
        var variantChanged = PopulateVariants(CodeFontVariantComboBox, selectedFamilyName, selectedVariantName);
        if (!variantChanged)
        {
            await RequestPreviewAsync();
        }
    }

    private async void OnAppearanceSelectionChanged(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        if (_isUpdatingVariantSelection)
        {
            return;
        }

        await RequestPreviewAsync();
    }

    private bool PopulateVariants(ComboBox variantComboBox, string familyName, string? selectedVariantName)
    {
        var family = _fontFamilies.FirstOrDefault(font => string.Equals(font.DisplayName, familyName, StringComparison.Ordinal));
        var variants = family?.StandardVariants.Select(variant => variant.DisplayName).ToList() ?? [];
        var nextSelection = variants.Contains(selectedVariantName, StringComparer.Ordinal)
            ? selectedVariantName
            : variants.FirstOrDefault();
        var selectionChanged = !string.Equals(variantComboBox.SelectedItem as string, nextSelection, StringComparison.Ordinal);

        _isUpdatingVariantSelection = true;
        try
        {
            variantComboBox.ItemsSource = variants;
            variantComboBox.SelectedItem = nextSelection;
        }
        finally
        {
            _isUpdatingVariantSelection = false;
        }

        return selectionChanged;
    }

    private void OnSaveClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(BuildCurrentModel());
    }

    private SettingsDialogModel BuildCurrentModel()
    {
        var themeName = ThemeComboBox.SelectedItem as string ?? string.Empty;
        var fontFamilyName = FontFamilyComboBox.SelectedItem as string ?? string.Empty;
        var fontVariantName = FontVariantComboBox.SelectedItem as string ?? string.Empty;

        return new SettingsDialogModel(
            ThemeComboBox.ItemsSource?.Cast<string>().ToList() ?? [],
            _fontFamilies,
            themeName,
            SidebarFontFamilyComboBox.SelectedItem as string ?? string.Empty,
            SidebarFontVariantComboBox.SelectedItem as string ?? string.Empty,
            fontFamilyName,
            fontVariantName,
            CodeFontFamilyComboBox.SelectedItem as string ?? string.Empty,
            CodeFontVariantComboBox.SelectedItem as string ?? string.Empty,
            ParseComboBoxDouble(EditorFontSizeComboBox, 12),
            ParseComboBoxDouble(UiFontSizeComboBox, 12),
            AiEnabledToggle.IsChecked ?? true,
            ApiKeyTextBox.Text?.Trim() ?? string.Empty,
            string.IsNullOrWhiteSpace(DefaultModelTextBox.Text) ? "gpt-4.1-mini" : DefaultModelTextBox.Text.Trim(),
            ProjectIdTextBox.Text?.Trim() ?? string.Empty,
            OrganizationIdTextBox.Text?.Trim() ?? string.Empty,
            PromptsFolderTextBlock.Text ?? string.Empty);
    }

    private async Task RequestPreviewAsync()
    {
        if (_isInitializing || PreviewSettingsAsync is null)
        {
            return;
        }

        var model = BuildCurrentModel();
        if (model == _lastPreviewModel)
        {
            return;
        }

        _lastPreviewModel = model;
        await PreviewSettingsAsync(model);
    }

    private static double ParseComboBoxDouble(ComboBox comboBox, double fallback)
    {
        return double.TryParse(comboBox.SelectedItem as string, out var value) ? value : fallback;
    }

    private void OnCancelClick(object? sender, Avalonia.Interactivity.RoutedEventArgs e)
    {
        Close(null);
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            e.Handled = true;
            OnSaveClick(sender, new Avalonia.Interactivity.RoutedEventArgs());
        }
        else if (e.Key == Key.Escape)
        {
            e.Handled = true;
            Close(null);
        }
    }
}
