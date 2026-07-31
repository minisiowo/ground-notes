using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed class CustomSlashCommandListItemViewModel
{
    public CustomSlashCommandListItemViewModel(CustomSlashCommandDefinition definition)
    {
        Definition = definition;
        Name = definition.Name;
        Id = definition.Id;
        Description = definition.Description ?? string.Empty;
        Order = definition.Order;
        Aliases = definition.Aliases is { Count: > 0 } ? string.Join(", ", definition.Aliases) : string.Empty;
    }

    public CustomSlashCommandDefinition Definition { get; }
    public string Name { get; }
    public string Id { get; }
    public string Description { get; }
    public int Order { get; }
    public string Aliases { get; }
    public bool CanEdit => true;
    public bool CanDelete => true;
}
