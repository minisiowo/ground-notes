using System.Text.Json;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class AiPromptEditorServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly string _builtInDir;
    private readonly string _notesDir;
    private readonly AiPromptCatalogService _catalogService;
    private readonly AiPromptEditorService _editorService;

    public AiPromptEditorServiceTests()
    {
        _builtInDir = Path.Combine(_tempRoot, "Assets", "AiPrompts");
        _notesDir = Path.Combine(_tempRoot, "notes");
        _catalogService = new AiPromptCatalogService(_builtInDir);
        _editorService = new AiPromptEditorService(_catalogService);
    }

    [Fact]
    public async Task SaveCustomPromptAsync_WritesPromptThatCatalogCanLoad()
    {
        var prompt = new AiPromptDefinition(
            "summarize",
            "Summarize",
            "Summarize: {selected}",
            "test",
            "gpt-5.6-luna",
            true,
            50,
            false,
            null,
            null,
            "low");

        await _editorService.SaveCustomPromptAsync(_notesDir, prompt);

        var result = await _catalogService.LoadPromptsAsync(_notesDir);
        var loaded = Assert.Single(result.Prompts);
        Assert.False(loaded.IsBuiltIn);
        Assert.Equal("summarize", loaded.Id);
        Assert.Equal("gpt-5.6-luna", loaded.Model);
        Assert.Equal("low", loaded.ReasoningEffort);
    }

    [Fact]
    public async Task SaveCustomPromptAsync_OmitsBuiltInFlagAndDefaultModelFields()
    {
        var prompt = new AiPromptDefinition(
            "use-defaults",
            "Use Defaults",
            "Prompt {selected}",
            null,
            null,
            true,
            10,
            true,
            null,
            null,
            null);

        await _editorService.SaveCustomPromptAsync(_notesDir, prompt);

        var json = await File.ReadAllTextAsync(_editorService.GetCustomPromptFilePath(_notesDir, "use-defaults"));
        Assert.DoesNotContain("isBuiltIn", json, StringComparison.Ordinal);
        Assert.DoesNotContain("model", json, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("reasoning_effort", json, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCustomPromptAsync_UpdatesPromptStoredUnderArbitraryFileName()
    {
        var directory = _catalogService.GetNotesFolderPromptsDirectory(_notesDir);
        Directory.CreateDirectory(directory);
        var arbitraryPath = Path.Combine(directory, "custom-file-name.json");
        await File.WriteAllTextAsync(arbitraryPath, JsonSerializer.Serialize(new
        {
            id = "rewrite",
            name = "Old Name",
            promptTemplate = "Old {selected}"
        }));

        await _editorService.SaveCustomPromptAsync(
            _notesDir,
            new AiPromptDefinition("rewrite", "New Name", "New {selected}"));

        Assert.True(File.Exists(arbitraryPath));
        Assert.False(File.Exists(Path.Combine(directory, "rewrite.json")));
        var result = await _catalogService.LoadPromptsAsync(_notesDir);
        Assert.Equal("New Name", Assert.Single(result.Prompts).Name);
    }

    [Fact]
    public async Task DeleteCustomPromptAsync_RemovesPromptStoredUnderArbitraryFileName()
    {
        var directory = _catalogService.GetNotesFolderPromptsDirectory(_notesDir);
        Directory.CreateDirectory(directory);
        var arbitraryPath = Path.Combine(directory, "custom-file-name.json");
        await File.WriteAllTextAsync(arbitraryPath, JsonSerializer.Serialize(new
        {
            id = "delete-me",
            name = "Delete Me",
            promptTemplate = "Prompt {selected}"
        }));

        await _editorService.DeleteCustomPromptAsync(_notesDir, "delete-me");

        Assert.False(File.Exists(arbitraryPath));
    }

    [Fact]
    public async Task SaveCustomPromptAsync_RejectsSanitizedFileNameCollision()
    {
        var directory = _catalogService.GetNotesFolderPromptsDirectory(_notesDir);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "my-prompt.json"), JsonSerializer.Serialize(new
        {
            id = "different-id",
            name = "Different",
            promptTemplate = "Prompt {selected}"
        }));

        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _editorService.SaveCustomPromptAsync(
            _notesDir,
            new AiPromptDefinition("My Prompt", "My Prompt", "Prompt {selected}")));

        Assert.Contains("same filename", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task SaveCustomPromptAsync_RejectsNonFiniteTemperature()
    {
        var exception = await Assert.ThrowsAsync<InvalidOperationException>(() => _editorService.SaveCustomPromptAsync(
            _notesDir,
            new AiPromptDefinition("invalid", "Invalid", "Prompt {selected}", Temperature: double.NaN)));

        Assert.Contains("Temperature", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task DeleteCustomPromptAsync_RemovesPromptFile()
    {
        var prompt = new AiPromptDefinition("delete-me", "Delete Me", "Prompt {selected}");
        await _editorService.SaveCustomPromptAsync(_notesDir, prompt);

        await _editorService.DeleteCustomPromptAsync(_notesDir, prompt.Id);

        var result = await _catalogService.LoadPromptsAsync(_notesDir);
        Assert.Empty(result.Prompts);
    }

    [Fact]
    public void GetCustomPromptFilePath_SanitizesPromptId()
    {
        var path = _editorService.GetCustomPromptFilePath(_notesDir, "My Prompt/One");

        Assert.EndsWith(Path.Combine(".groundnotes", "ai-prompts", "my-prompt-one.json"), path, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
        {
            Directory.Delete(_tempRoot, recursive: true);
        }
    }
}
