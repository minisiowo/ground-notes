using System.Text.Json;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class CustomSlashCommandCatalogServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly CustomSlashCommandCatalogService _service;

    public CustomSlashCommandCatalogServiceTests() => _service = new(["help", "summarize"]);

    [Fact]
    public async Task LoadCommandsAsync_LoadsAndOrdersValidCommands()
    {
        Write("z.json", "zeta", "Same", "  exact\n", 1, " desc ");
        Write("a.json", "alpha", "same", "template", 1);

        var result = await _service.LoadCommandsAsync(_root);

        Assert.Equal(["alpha", "zeta"], result.Commands.Select(command => command.Id));
        Assert.Equal("  exact\n", result.Commands[1].Template);
        Assert.Equal("desc", result.Commands[1].Description);
        Assert.Empty(result.Warnings);
    }

    [Fact]
    public async Task LoadCommandsAsync_SkipsInvalidReservedAndDuplicateCommands()
    {
        Write("one.json", "valid", "Valid", "template");
        Write("two.json", "VALID", "Duplicate", "template");
        Write("three.json", "help", "Reserved", "template");
        Write("four.json", "Bad.Id", "Invalid", "template");
        Directory.CreateDirectory(_service.GetNotesFolderCommandsDirectory(_root));
        await File.WriteAllTextAsync(Path.Combine(_service.GetNotesFolderCommandsDirectory(_root), "bad.json"), "{ no");

        var result = await _service.LoadCommandsAsync(_root);

        Assert.Equal("valid", Assert.Single(result.Commands).Id);
        Assert.Equal(4, result.Warnings.Count);
    }

    [Fact]
    public async Task LoadCommandsAsync_LoadsAliasesAndOldJson()
    {
        Write("old.json", "old", "Old", "template");
        Write("alias.json", "rewrite", "Rewrite", "template", aliases: ["r", "rework"]);

        var result = await _service.LoadCommandsAsync(_root);

        Assert.Null(result.Commands.Single(command => command.Id == "old").Aliases);
        Assert.Equal(["r", "rework"], result.Commands.Single(command => command.Id == "rewrite").Aliases);
    }

    [Fact]
    public async Task LoadCommandsAsync_UsesOrdinalFirstFileAndTypedCollisionWarning()
    {
        Write("b.json", "first", "First", "template", aliases: ["shared"]);
        Write("a.json", "second", "Second", "template", aliases: ["shared"]);

        var result = await _service.LoadCommandsAsync(_root);

        Assert.Equal("second", Assert.Single(result.Commands).Id);
        var warning = Assert.Single(result.Warnings);
        Assert.Equal(Path.Combine(_service.GetNotesFolderCommandsDirectory(_root), "b.json"), warning.Path);
        Assert.Contains("shared", warning.Reason);
    }

    private void Write(string fileName, string id, string name, string template, int order = 0, string? description = null, string[]? aliases = null)
    {
        var directory = _service.GetNotesFolderCommandsDirectory(_root);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, fileName), JsonSerializer.Serialize(new { id, name, template, description, order, aliases }));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
