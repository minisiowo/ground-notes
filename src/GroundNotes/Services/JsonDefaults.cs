using System.Text.Json;

namespace GroundNotes.Services;

public static class JsonDefaults
{
    public static JsonSerializerOptions ReadOptions { get; } = new()
    {
        PropertyNameCaseInsensitive = true
    };
}
