using GroundNotes.Editors;
using Xunit;

namespace GroundNotes.Tests;

public sealed class MarkdownSlashCommandCatalogTests
{
    [Fact]
    public void TryGetTrigger_ReturnsQueryAtCaret()
    {
        var trigger = MarkdownSlashCommandCatalog.TryGetTrigger("hello /bo", "hello /bo".Length);

        var value = Assert.IsType<MarkdownSlashTrigger>(trigger);
        Assert.Equal(6, value.Start);
        Assert.Equal(3, value.Length);
        Assert.Equal("bo", value.Query);
    }

    [Fact]
    public void TryGetTrigger_RejectsSlashInsideWord()
    {
        var trigger = MarkdownSlashCommandCatalog.TryGetTrigger("path/to", "path/to".Length);

        Assert.Null(trigger);
    }

    [Fact]
    public void Filter_MatchesAliasesAndLabels()
    {
        var results = MarkdownSlashCommandCatalog.Filter("todo");

        var command = Assert.Single(results);
        Assert.Equal("task", command.Id);
    }

    [Fact]
    public void Filter_IncludesCodeBlockCommand()
    {
        var results = MarkdownSlashCommandCatalog.Filter("fence");

        var command = Assert.Single(results);
        Assert.Equal("codeblock", command.Id);
    }
}
