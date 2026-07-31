using System.Text.Json;
using GroundNotes.Models;
using GroundNotes.Services;
using Xunit;

namespace GroundNotes.Tests;

public sealed class CustomSlashCommandEditorServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "GroundNotes.Tests", Guid.NewGuid().ToString("N"));
    private readonly CustomSlashCommandCatalogService _catalog;
    private readonly CustomSlashCommandEditorService _editor;

    public CustomSlashCommandEditorServiceTests()
    {
        _catalog = new(["help"]);
        _editor = new CustomSlashCommandEditorService(["help"], _catalog);
    }

    [Fact]
    public async Task SaveCustomCommandAsync_PreservesTemplateAndOmitsNullDescription()
    {
        var command = new CustomSlashCommandDefinition("rewrite", "  Rewrite  ", "  line 1\nline 2  ");

        await _editor.SaveCustomCommandAsync(_root, command);

        var json = await File.ReadAllTextAsync(_editor.GetCustomCommandFilePath(_root, "rewrite"));
        Assert.DoesNotContain("description", json, StringComparison.OrdinalIgnoreCase);
        var loaded = Assert.Single((await _catalog.LoadCommandsAsync(_root)).Commands);
        Assert.Equal("Rewrite", loaded.Name);
        Assert.Equal(command.Template, loaded.Template);
    }

    [Fact]
    public async Task SaveCustomCommandAsync_RoundTripsAliasesAndRejectsCollisions()
    {
        await _editor.SaveCustomCommandAsync(_root, new("rewrite", "Rewrite", "template", Aliases: ["r", "rework"]));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _editor.SaveCustomCommandAsync(_root, new("other", "Other", "template", Aliases: ["R"])));

        var loaded = Assert.Single((await _catalog.LoadCommandsAsync(_root)).Commands);
        Assert.Equal(["r", "rework"], loaded.Aliases);
    }

    [Fact]
    public async Task SaveCustomCommandAsync_RenameExcludesOldDefinitionAndPreservesAliases()
    {
        await _editor.SaveCustomCommandAsync(_root, new("rewrite", "Rewrite", "template", Aliases: ["r"]));

        await _editor.SaveCustomCommandAsync(_root, new("revise", "Revise", "new template", Aliases: ["r"]), "rewrite");

        var loaded = Assert.Single((await _catalog.LoadCommandsAsync(_root)).Commands);
        Assert.Equal("revise", loaded.Id);
        Assert.Equal(["r"], loaded.Aliases);
        Assert.False(File.Exists(_editor.GetCustomCommandFilePath(_root, "rewrite")));
    }

    [Fact]
    public async Task SaveCustomCommandAsync_RenameCompletesWhenBackupCleanupFails()
    {
        var deleteAttempts = 0;
        var editor = new CustomSlashCommandEditorService(["help"], _catalog, path =>
        {
            if (path.Contains(".groundnotes-backup-", StringComparison.Ordinal))
            {
                deleteAttempts++;
                throw new IOException("simulated backup cleanup failure");
            }
            File.Delete(path);
        });
        await editor.SaveCustomCommandAsync(_root, new("rewrite", "Rewrite", "template", Aliases: ["r"]));

        await editor.SaveCustomCommandAsync(_root, new("revise", "Revise", "new template", Aliases: ["v"]), "rewrite");

        Assert.True(deleteAttempts > 0);
        var loaded = Assert.Single((await _catalog.LoadCommandsAsync(_root)).Commands);
        Assert.Equal("revise", loaded.Id);
        Assert.Equal("Revise", loaded.Name);
        Assert.Equal("new template", loaded.Template);
        Assert.Equal(["v"], loaded.Aliases);
        Assert.False(File.Exists(editor.GetCustomCommandFilePath(_root, "rewrite")));
        Assert.Contains(Directory.EnumerateFiles(_catalog.GetNotesFolderCommandsDirectory(_root)), path =>
            Path.GetFileName(path).Contains(".groundnotes-backup-", StringComparison.Ordinal));
    }

    [Fact]
    public async Task SaveCustomCommandAsync_RenameRejectsOccupiedTargetFile()
    {
        await _editor.SaveCustomCommandAsync(_root, new("rewrite", "Rewrite", "template"));
        await _editor.SaveCustomCommandAsync(_root, new("revise", "Revise", "template"));

        await Assert.ThrowsAsync<InvalidOperationException>(() => _editor.SaveCustomCommandAsync(_root, new("revise", "Revise", "new"), "rewrite"));
    }

    [Fact]
    public async Task SaveCustomCommandAsync_UpdatesArbitraryFileAndRemovesDuplicates()
    {
        var directory = _catalog.GetNotesFolderCommandsDirectory(_root);
        Directory.CreateDirectory(directory);
        foreach (var file in new[] { "first.json", "second.json" })
        {
            await File.WriteAllTextAsync(Path.Combine(directory, file), JsonSerializer.Serialize(new { id = "rewrite", name = "Old", template = "old" }));
        }

        await _editor.SaveCustomCommandAsync(_root, new("rewrite", "New", "new"));

        Assert.True(File.Exists(Path.Combine(directory, "first.json")));
        Assert.False(File.Exists(Path.Combine(directory, "second.json")));
        Assert.Equal("New", Assert.Single((await _catalog.LoadCommandsAsync(_root)).Commands).Name);
    }

    [Fact]
    public async Task DeleteCustomCommandAsync_DeletesEveryMatchingFile()
    {
        var directory = _catalog.GetNotesFolderCommandsDirectory(_root);
        Directory.CreateDirectory(directory);
        foreach (var file in new[] { "one.json", "two.json" })
            await File.WriteAllTextAsync(Path.Combine(directory, file), JsonSerializer.Serialize(new { id = "remove", name = "Remove", template = "x" }));

        await _editor.DeleteCustomCommandAsync(_root, "REMOVE");

        Assert.Empty(Directory.EnumerateFiles(directory, "*.json"));
    }

    [Fact]
    public async Task SaveCustomCommandAsync_RejectsInvalidAndReservedIds()
    {
        await Assert.ThrowsAsync<InvalidOperationException>(() => _editor.SaveCustomCommandAsync(_root, new("Bad.Id", "Bad", "x")));
        await Assert.ThrowsAsync<InvalidOperationException>(() => _editor.SaveCustomCommandAsync(_root, new("help", "Help", "x")));
    }

    [Fact]
    public void GetCustomCommandFilePath_RejectsPathTraversalAndReservedIds()
    {
        Assert.Throws<InvalidOperationException>(() => _editor.GetCustomCommandFilePath(_root, "../escape"));
        Assert.Throws<InvalidOperationException>(() => _editor.GetCustomCommandFilePath(_root, "help"));
    }

    [Fact]
    public async Task SaveCustomCommandAsync_RejectsMalformedGeneratedFilename()
    {
        var directory = _catalog.GetNotesFolderCommandsDirectory(_root);
        Directory.CreateDirectory(directory);
        await File.WriteAllTextAsync(Path.Combine(directory, "rewrite.json"), "not json");
        await Assert.ThrowsAsync<InvalidOperationException>(() => _editor.SaveCustomCommandAsync(_root, new("rewrite", "Rewrite", "new")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
