using System.Text.Json;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class AiPromptCatalogServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _builtInDir;
    private readonly string _notesDir;
    private readonly AiPromptCatalogService _service;

    public AiPromptCatalogServiceTests()
    {
        _builtInDir = Path.Combine(_tempRoot, "Assets", "AiPrompts");
        _notesDir = Path.Combine(_tempRoot, "notes");
        _service = new AiPromptCatalogService(_builtInDir);
    }

    [Fact]
    public async Task LoadPromptsAsync_ReturnsBuiltInPromptsWhenNotesFolderIsMissing()
    {
        WritePrompt(_builtInDir, "translate.json", "translate", "Translate With AI", 100);

        var result = await _service.LoadPromptsAsync(null);

        var prompt = Assert.Single(result.Prompts);
        Assert.True(prompt.IsBuiltIn);
        Assert.Equal("translate", prompt.Id);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task LoadPromptsAsync_LoadsCustomPromptsFromNotesFolder()
    {
        WritePrompt(_service.GetNotesFolderPromptsDirectory(_notesDir), "rewrite.json", "rewrite", "Rewrite", 200);

        var result = await _service.LoadPromptsAsync(_notesDir);

        var prompt = Assert.Single(result.Prompts);
        Assert.False(prompt.IsBuiltIn);
        Assert.Equal("rewrite", prompt.Id);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task LoadPromptsAsync_CustomPromptOverridesBuiltInById()
    {
        WritePrompt(_builtInDir, "translate.json", "translate", "Translate With AI", 100);
        WritePrompt(_service.GetNotesFolderPromptsDirectory(_notesDir), "translate.json", "translate", "My Translate", 5, model: "gpt-5.4");

        var result = await _service.LoadPromptsAsync(_notesDir);

        var prompt = Assert.Single(result.Prompts);
        Assert.Equal("My Translate", prompt.Name);
        Assert.False(prompt.IsBuiltIn);
        Assert.Equal("gpt-5.4", prompt.Model);
    }

    [Fact]
    public async Task LoadPromptsAsync_ReturnsWarningsForMalformedAndInvalidPrompts()
    {
        Directory.CreateDirectory(_builtInDir);
        await File.WriteAllTextAsync(Path.Combine(_builtInDir, "bad.json"), "{ no");
        await File.WriteAllTextAsync(
            Path.Combine(_builtInDir, "missing-template.json"),
            """
            {
              "id": "missing-template",
              "name": "Missing Template"
            }
            """);

        var result = await _service.LoadPromptsAsync(null);

        Assert.Empty(result.Prompts);
        Assert.Equal(2, result.Warnings.Count);
        Assert.Contains(result.Warnings, warning => warning.Contains("bad.json", StringComparison.Ordinal) && warning.Contains("malformed JSON", StringComparison.Ordinal));
        Assert.Contains(result.Warnings, warning => warning.Contains("missing-template.json", StringComparison.Ordinal) && warning.Contains("missing required fields", StringComparison.Ordinal));
    }

    [Fact]
    public async Task LoadPromptsAsync_DeserializesAdvancedParameters()
    {
        WritePrompt(_builtInDir, "advanced.json", "advanced", "Advanced", 1, model: "o1", temperature: 1.0, maxTokens: 500, reasoningEffort: "medium");

        var result = await _service.LoadPromptsAsync(null);

        var prompt = Assert.Single(result.Prompts);
        Assert.Equal(1.0, prompt.Temperature);
        Assert.Equal(500, prompt.MaxTokens);
        Assert.Equal("medium", prompt.ReasoningEffort);
    }

    [Fact]
    public async Task LoadPromptsAsync_DeserializesAdvancedParametersFromSnakeCase()
    {
        Directory.CreateDirectory(_builtInDir);
        var payload = """
        {
            "id": "snake",
            "name": "Snake",
            "promptTemplate": "test",
            "max_tokens": 123,
            "reasoning_effort": "low"
        }
        """;
        await File.WriteAllTextAsync(Path.Combine(_builtInDir, "snake.json"), payload);

        var result = await _service.LoadPromptsAsync(null);

        var prompt = Assert.Single(result.Prompts);
        Assert.Equal(123, prompt.MaxTokens);
        Assert.Equal("low", prompt.ReasoningEffort);
    }

    [Fact]
    public async Task LoadPromptsAsync_CachesBuiltInPromptsAcrossCalls()
    {
        WritePrompt(_builtInDir, "translate.json", "translate", "Translate With AI", 100);

        var first = await _service.LoadPromptsAsync(null);
        Assert.Equal("Translate With AI", Assert.Single(first.Prompts).Name);

        WritePrompt(_builtInDir, "translate.json", "translate", "Changed Title", 100);

        var second = await _service.LoadPromptsAsync(null);
        Assert.Equal("Translate With AI", Assert.Single(second.Prompts).Name);
    }

    private static void WritePrompt(string directory, string fileName, string id, string name, int order, string? model = null, double? temperature = null, int? maxTokens = null, string? reasoningEffort = null)
    {
        Directory.CreateDirectory(directory);
        var payload = new Dictionary<string, object?>
        {
            ["id"] = id,
            ["name"] = name,
            ["description"] = "test",
            ["promptTemplate"] = "Prompt {selected}",
            ["model"] = model,
            ["replaceSelection"] = true,
            ["order"] = order,
            ["temperature"] = temperature,
            ["max_tokens"] = maxTokens,
            ["reasoning_effort"] = reasoningEffort
        };

        File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(payload));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
