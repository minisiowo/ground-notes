using GroundNotes.Editors;
using GroundNotes.Models;
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
    public void Filter_MatchesCustomAliasesAndMapsThemToTheCommand()
    {
        var command = Assert.Single(MarkdownSlashCommandCatalog.Filter(
            "rewrite",
            [new CustomSlashCommandDefinition("transform", "Transform", "x", Aliases: ["rewrite"])]));

        Assert.Equal("transform", command.Id);
        Assert.Equal(["rewrite"], command.Aliases);
    }

    [Fact]
    public void Filter_RanksExactAndPrefixMatchesAheadOfSubstrings()
    {
        var results = MarkdownSlashCommandCatalog.Filter("code");

        Assert.Equal(new[] { "code", "codeblock" }, results.Select(command => command.Id).ToArray());
    }

    [Fact]
    public void Filter_SupportsCompactSubsequenceMatches()
    {
        var results = MarkdownSlashCommandCatalog.Filter("cdb");

        Assert.Equal("codeblock", Assert.Single(results).Id);
    }

    [Fact]
    public void Filter_IncludesCodeBlockCommand()
    {
        var results = MarkdownSlashCommandCatalog.Filter("fence");

        var command = Assert.Single(results);
        Assert.Equal("codeblock", command.Id);
    }

    [Fact]
    public void Filter_IncludesTableInsertionAndFormattingCommands()
    {
        Assert.Equal("table", MarkdownSlashCommandCatalog.Filter("grid").Single().Id);
        Assert.Equal("table-format", MarkdownSlashCommandCatalog.Filter("align-table").Single().Id);
    }

    [Fact]
    public void Filter_AppendsCustomCommandsInConfiguredOrder()
    {
        var commands = MarkdownSlashCommandCatalog.Filter(
            "",
            [
                new CustomSlashCommandDefinition("zeta", "Zeta", "z", Order: 1),
                new CustomSlashCommandDefinition("alpha", "Alpha", "a", Order: 1),
            ]);

        Assert.Equal(
            new[] { "bold", "italic", "code", "codeblock", "task", "bullet", "table", "table-format", "h1", "h2", "h3", "alpha", "zeta" },
            commands.Select(command => command.Id).ToArray());
        Assert.Equal(SlashCommandAction.InsertTemplate, commands[^1].Action);
        Assert.Equal("z", commands[^1].Template);
    }

    [Fact]
    public void Filter_IgnoresReservedAndDuplicateCustomIds()
    {
        var commands = MarkdownSlashCommandCatalog.Filter(
            "",
            [
                new CustomSlashCommandDefinition("BOLD", "Reserved", "reserved"),
                new CustomSlashCommandDefinition("custom", "First", "first"),
                new CustomSlashCommandDefinition("CUSTOM", "Duplicate", "duplicate"),
            ]);

        Assert.Equal("First", Assert.Single(commands.Where(command => command.Id == "custom")).Label);
    }

    [Fact]
    public void Filter_RejectsTokenCollisionsAcrossCustomCommands()
    {
        var commands = MarkdownSlashCommandCatalog.Filter(
            "",
            [
                new CustomSlashCommandDefinition("first", "First", "1", Aliases: ["shared"]),
                new CustomSlashCommandDefinition("shared", "IdCollision", "2"),
                new CustomSlashCommandDefinition("second", "AliasCollision", "3", Aliases: ["first"]),
                new CustomSlashCommandDefinition("third", "AliasAliasCollision", "4", Aliases: ["shared"]),
            ]);

        Assert.Equal("first", Assert.Single(commands.Where(command => command.Id == "first")).Id);
        Assert.DoesNotContain(commands, command => command.Id is "shared" or "second" or "third");
    }

    [Fact]
    public void Filter_RejectsInvalidCustomAliasesWithoutThrowing()
    {
        var commands = MarkdownSlashCommandCatalog.Filter(
            "",
            [new CustomSlashCommandDefinition("custom", "Custom", "x", Aliases: ["not valid"])]);

        Assert.DoesNotContain(commands, command => command.Id == "custom");
    }

    [Fact]
    public void Filter_UsesCustomDescriptionOrFallback()
    {
        var commands = MarkdownSlashCommandCatalog.Filter(
            "",
            [
                new CustomSlashCommandDefinition("described", "Described", "one", "Description"),
                new CustomSlashCommandDefinition("fallback", "Fallback", "two"),
            ]);

        Assert.Equal("Description", commands.Single(command => command.Id == "described").Description);
        Assert.Equal("Insert custom template", commands.Single(command => command.Id == "fallback").Description);
    }
}
