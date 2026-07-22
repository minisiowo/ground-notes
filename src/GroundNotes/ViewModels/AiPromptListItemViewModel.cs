using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed class AiPromptListItemViewModel
{
    public AiPromptListItemViewModel(AiPromptDefinition definition, string defaultModel, string defaultReasoningEffort)
    {
        Definition = definition;
        Name = definition.Name;
        Id = definition.Id;
        Source = definition.IsBuiltIn ? "Built-in" : "Custom";
        Model = string.IsNullOrWhiteSpace(definition.Model)
            ? $"Default: {defaultModel}"
            : definition.Model;
        ReasoningEffort = string.IsNullOrWhiteSpace(definition.ReasoningEffort)
            ? $"Default: {defaultReasoningEffort}"
            : definition.ReasoningEffort;
    }

    public AiPromptDefinition Definition { get; }

    public string Id { get; }

    public string Name { get; }

    public string Model { get; }

    public string ReasoningEffort { get; }

    public string Source { get; }

    public bool IsBuiltIn => Definition.IsBuiltIn;

    public bool CanEdit => !IsBuiltIn;

    public bool CanDelete => !IsBuiltIn;
}
