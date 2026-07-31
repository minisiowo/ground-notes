using System.Globalization;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using GroundNotes.Models;

namespace GroundNotes.ViewModels;

public sealed partial class CustomSlashCommandEditorViewModel : ViewModelBase
{
    private readonly HashSet<string> _unavailableIds;

    public CustomSlashCommandEditorViewModel(
        CustomSlashCommandDefinition? command,
        bool duplicate,
        IEnumerable<string>? unavailableIds = null)
    {
        _unavailableIds = new HashSet<string>(unavailableIds ?? [], StringComparer.OrdinalIgnoreCase);
        if (command is not null && !duplicate)
        {
            _unavailableIds.RemoveWhere(token => string.Equals(token, command.Id, StringComparison.OrdinalIgnoreCase)
                || command.Aliases?.Contains(token, StringComparer.OrdinalIgnoreCase) == true);
        }
        if (command is null)
        {
            Order = "100";
            return;
        }

        Id = duplicate ? BuildDuplicateId(command.Id, _unavailableIds) : command.Id;
        Name = duplicate ? $"{command.Name} Copy" : command.Name;
        Description = command.Description ?? string.Empty;
        Template = command.Template;
        Aliases = duplicate ? string.Empty : string.Join(", ", command.Aliases ?? []);
        Order = command.Order.ToString(CultureInfo.InvariantCulture);
    }

    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave)), NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _id = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave)), NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _name = string.Empty;
    [ObservableProperty]
    private string _description = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave)), NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _template = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave)), NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _aliases = string.Empty;
    [ObservableProperty, NotifyPropertyChangedFor(nameof(CanSave)), NotifyPropertyChangedFor(nameof(ValidationMessage))]
    private string _order = "100";

    public bool CanSave => Validate() is null;
    public string ValidationMessage => Validate() ?? string.Empty;

    public CustomSlashCommandDefinition BuildCommand()
    {
        var error = Validate();
        if (error is not null) throw new InvalidOperationException(error);
        return new CustomSlashCommandDefinition(Id.Trim(), Name.Trim(), Template,
            string.IsNullOrWhiteSpace(Description) ? null : Description.Trim(),
            int.Parse(Order.Trim(), CultureInfo.InvariantCulture), ParseAliases());
    }

    partial void OnNameChanged(string value)
    {
        if (string.IsNullOrWhiteSpace(Id)) Id = Slugify(value);
    }

    private string? Validate()
    {
        if (string.IsNullOrWhiteSpace(Name)) return "Command name is required.";
        if (string.IsNullOrWhiteSpace(Id)) return "Command ID is required.";
        var normalizedId = Id.Trim();
        if (!normalizedId.Equals(Id, StringComparison.Ordinal) || normalizedId.Length == 0 || !IsValidId(normalizedId)) return "Command ID must contain only lowercase ASCII letters, digits, underscores, and hyphens.";
        if (_unavailableIds.Contains(Id)) return "Command ID is already in use.";
        if (string.IsNullOrWhiteSpace(Template)) return "Command template is required.";
        if (!int.TryParse(Order.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out _)) return "Order must be a whole number.";
        var aliases = ParseAliases();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { normalizedId };
        foreach (var alias in aliases)
        {
            if (!IsValidId(alias)) return $"Alias '{alias}' must contain only lowercase ASCII letters, digits, underscores, and hyphens.";
            if (!seen.Add(alias)) return $"Alias '{alias}' is already in use.";
            if (_unavailableIds.Contains(alias)) return $"Alias '{alias}' is already in use.";
        }
        return null;
    }

    private IReadOnlyList<string> ParseAliases() => Aliases
        .Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
        .ToList();

    private static bool IsValidId(string value)
    {
        if (value[0] is not (>= 'a' and <= 'z') and not (>= '0' and <= '9')) return false;
        return value.Skip(1).All(c => c is >= 'a' and <= 'z' or >= '0' and <= '9' or '_' or '-');
    }

    private static string Slugify(string value)
    {
        var builder = new StringBuilder();
        foreach (var c in value.Trim().ToLowerInvariant())
            if (c is >= 'a' and <= 'z' or >= '0' and <= '9') builder.Append(c);
            else if (builder.Length > 0 && builder[^1] != '-') builder.Append('-');
        return builder.ToString().Trim('-');
    }

    private static string BuildDuplicateId(string id, ISet<string> unavailable)
    {
        if (string.IsNullOrWhiteSpace(id)) return string.Empty;
        var baseId = id.Trim() + "-copy";
        var candidate = baseId;
        var suffix = 2;
        while (unavailable.Contains(candidate)) candidate = $"{baseId}-{suffix++}";
        return candidate;
    }
}
