using CommunityToolkit.Mvvm.ComponentModel;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class AiPromptEditorViewModel : ViewModelBase
{
    public AiPromptEditorViewModel(AiPromptDefinition? prompt, string defaultModel, string defaultReasoningEffort, bool duplicate)
    {
        DefaultModel = string.IsNullOrWhiteSpace(defaultModel) ? AiModelCatalog.DefaultChatModel : defaultModel;
        DefaultReasoningEffort = AiReasoningEffortCatalog.Normalize(defaultReasoningEffort);
        UseDefaultModelOption = $"Use default: {DefaultModel}";
        UseDefaultReasoningEffortOption = $"Use default: {DefaultReasoningEffort}";

        AvailableModelOptions = BuildModelOptions(prompt?.Model);
        AvailableReasoningEffortOptions = [UseDefaultReasoningEffortOption, .. AiReasoningEffortCatalog.ReasoningEfforts];

        if (prompt is null)
        {
            ReplaceSelection = true;
            SelectedModelOption = UseDefaultModelOption;
            SelectedReasoningEffortOption = UseDefaultReasoningEffortOption;
            Order = "100";
            return;
        }

        Id = duplicate ? BuildDuplicateId(prompt.Id) : prompt.Id;
        Name = duplicate ? $"{prompt.Name} Copy" : prompt.Name;
        Description = prompt.Description ?? string.Empty;
        PromptTemplate = prompt.PromptTemplate;
        ReplaceSelection = prompt.ReplaceSelection;
        Order = prompt.Order.ToString();
        Temperature = prompt.Temperature?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        MaxTokens = prompt.MaxTokens?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty;
        SelectedModelOption = string.IsNullOrWhiteSpace(prompt.Model) ? UseDefaultModelOption : prompt.Model;
        SelectedReasoningEffortOption = string.IsNullOrWhiteSpace(prompt.ReasoningEffort)
            ? UseDefaultReasoningEffortOption
            : AiReasoningEffortCatalog.Normalize(prompt.ReasoningEffort);
    }

    public string DefaultModel { get; }

    public string DefaultReasoningEffort { get; }

    public string UseDefaultModelOption { get; }

    public string UseDefaultReasoningEffortOption { get; }

    public IReadOnlyList<string> AvailableModelOptions { get; }

    public IReadOnlyList<string> AvailableReasoningEffortOptions { get; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _id = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _description = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _promptTemplate = string.Empty;

    [ObservableProperty]
    private string _selectedModelOption = string.Empty;

    [ObservableProperty]
    private string _selectedReasoningEffortOption = string.Empty;

    [ObservableProperty]
    private bool _replaceSelection = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _order = "100";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _temperature = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(CanSave))]
    [NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _maxTokens = string.Empty;

    public bool CanSave => Validate() is null;

    public string ValidationMessage => Validate() ?? string.Empty;

    public AiPromptDefinition BuildPrompt()
    {
        var validationMessage = Validate();
        if (validationMessage is not null)
        {
            throw new InvalidOperationException(validationMessage);
        }

        var model = string.Equals(SelectedModelOption, UseDefaultModelOption, StringComparison.Ordinal)
            ? null
            : SelectedModelOption.Trim();
        var reasoningEffort = string.Equals(SelectedReasoningEffortOption, UseDefaultReasoningEffortOption, StringComparison.Ordinal)
            ? null
            : AiReasoningEffortCatalog.Normalize(SelectedReasoningEffortOption);

        return new AiPromptDefinition(
            Id.Trim(),
            Name.Trim(),
            PromptTemplate.Trim(),
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            model,
            ReplaceSelection,
            int.Parse(Order.Trim(), System.Globalization.CultureInfo.InvariantCulture),
            false,
            TryParseDouble(Temperature, out var temperature) ? temperature : null,
            TryParseInt(MaxTokens, out var maxTokens) ? maxTokens : null,
            reasoningEffort);
    }

    partial void OnNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(Id))
        {
            Id = Slugify(value);
        }
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            return "Prompt name is required.";
        }

        if (string.IsNullOrWhiteSpace(Id))
        {
            return "Prompt ID is required.";
        }

        if (string.IsNullOrWhiteSpace(PromptTemplate))
        {
            return "Prompt template is required.";
        }

        if (!PromptTemplate.Contains("{selected}", StringComparison.Ordinal))
        {
            return "Prompt template must include {selected}.";
        }

        if (!TryParseInt(Order, out _))
        {
            return "Order must be a whole number.";
        }

        if (!string.IsNullOrWhiteSpace(Temperature)
            && (!TryParseDouble(Temperature, out var temperature)
                || !double.IsFinite(temperature)
                || temperature is < 0 or > 2))
        {
            return "Temperature must be a number from 0 to 2.";
        }

        if (!string.IsNullOrWhiteSpace(MaxTokens) && (!TryParseInt(MaxTokens, out var maxTokens) || maxTokens <= 0))
        {
            return "Max tokens must be a positive whole number.";
        }

        return null;
    }

    private IReadOnlyList<string> BuildModelOptions(string? promptModel)
    {
        var models = new List<string> { UseDefaultModelOption };
        models.AddRange(AiModelCatalog.ChatCompletionModels);

        if (!string.IsNullOrWhiteSpace(promptModel)
            && !models.Contains(promptModel, StringComparer.OrdinalIgnoreCase))
        {
            models.Add(promptModel.Trim());
        }

        return models;
    }

    private static bool TryParseInt(string value, out int result)
    {
        return int.TryParse(value.Trim(), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static bool TryParseDouble(string value, out double result)
    {
        return double.TryParse(value.Trim(), System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out result);
    }

    private static string BuildDuplicateId(string id)
    {
        return string.IsNullOrWhiteSpace(id) ? string.Empty : id.Trim() + "-copy";
    }

    private static string Slugify(string value)
    {
        var chars = value.Trim().ToLowerInvariant()
            .Select(static character => char.IsLetterOrDigit(character) ? character : '-')
            .ToArray();
        var slug = new string(chars).Trim('-');
        while (slug.Contains("--", StringComparison.Ordinal))
        {
            slug = slug.Replace("--", "-", StringComparison.Ordinal);
        }

        return slug;
    }
}
