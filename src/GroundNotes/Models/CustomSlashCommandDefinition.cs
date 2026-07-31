using System.Text.Json.Serialization;

namespace GroundNotes.Models;

public sealed record CustomSlashCommandDefinition(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("template")] string Template,
    [property: JsonPropertyName("description")] string? Description = null,
    [property: JsonPropertyName("order")] int Order = 0,
    [property: JsonPropertyName("aliases")] IReadOnlyList<string>? Aliases = null);
