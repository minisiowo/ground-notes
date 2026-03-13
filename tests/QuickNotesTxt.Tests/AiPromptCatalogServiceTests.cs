using System.Text.Json;
using QuickNotesTxt.Services;
using Xunit;

namespace QuickNotesTxt.Tests;

public sealed class AiPromptCatalogServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "QuickNotesTxt.Tests", Guid.NewGuid().ToString("N"));
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

        var prompts = await _service.LoadPromptsAsync(null);

        var prompt = Assert.Single(prompts);
        Assert.True(prompt.IsBuiltIn);
        Assert.Equal("translate", prompt.Id);
    }

    [Fact]
    public async Task LoadPromptsAsync_LoadsCustomPromptsFromNotesFolder()
    {
        WritePrompt(_service.GetNotesFolderPromptsDirectory(_notesDir), "rewrite.json", "rewrite", "Rewrite", 200);

        var prompts = await _service.LoadPromptsAsync(_notesDir);

        var prompt = Assert.Single(prompts);
        Assert.False(prompt.IsBuiltIn);
        Assert.Equal("rewrite", prompt.Id);
    }

    [Fact]
    public async Task LoadPromptsAsync_CustomPromptOverridesBuiltInById()
    {
        WritePrompt(_builtInDir, "translate.json", "translate", "Translate With AI", 100);
        WritePrompt(_service.GetNotesFolderPromptsDirectory(_notesDir), "translate.json", "translate", "My Translate", 5, model: "gpt-4.1");

        var prompts = await _service.LoadPromptsAsync(_notesDir);

        var prompt = Assert.Single(prompts);
        Assert.Equal("My Translate", prompt.Name);
        Assert.False(prompt.IsBuiltIn);
        Assert.Equal("gpt-4.1", prompt.Model);
    }

    [Fact]
    public async Task LoadPromptsAsync_SkipsMalformedJson()
    {
        Directory.CreateDirectory(_builtInDir);
        await File.WriteAllTextAsync(Path.Combine(_builtInDir, "bad.json"), "{ no");

        var prompts = await _service.LoadPromptsAsync(_notesDir);

        Assert.Empty(prompts);
    }

    private static void WritePrompt(string directory, string fileName, string id, string name, int order, string? model = null)
    {
        Directory.CreateDirectory(directory);
        var payload = new
        {
            id,
            name,
            description = "test",
            promptTemplate = "Prompt {selected}",
            model,
            replaceSelection = true,
            order
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
