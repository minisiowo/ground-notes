using GroundNotes.Services;

namespace GroundNotes.Editors;

internal static class MarkdownSlashCommandCatalog
{
    public static IReadOnlyList<MarkdownSlashCommand> All { get; } =
    [
        new("bold", "Bold", "Wrap selection with **bold**", SlashCommandAction.Bold, ["b"]),
        new("italic", "Italic", "Wrap selection with *italic*", SlashCommandAction.Italic, ["i"]),
        new("code", "Inline Code", "Wrap selection with `code`", SlashCommandAction.InlineCode, ["k", "inline"]),
        new("codeblock", "Code Block", "Wrap selection with a fenced code block", SlashCommandAction.CodeBlock, ["block", "fence", "pre"]),
        new("task", "Task List", "Toggle selected lines as tasks", SlashCommandAction.TaskList, ["todo", "checkbox"]),
        new("bullet", "Bullet List", "Toggle selected lines as bullets", SlashCommandAction.BulletList, ["list", "ul"]),
        new("table", "Table", "Insert an editable Markdown table", SlashCommandAction.Table, ["grid"]),
        new("table-format", "Format Table", "Align the Markdown table at the caret", SlashCommandAction.FormatTable, ["format-table", "align-table"]),
        new("h1", "Heading 1", "Toggle heading level 1", SlashCommandAction.Heading1, ["heading1"]),
        new("h2", "Heading 2", "Toggle heading level 2", SlashCommandAction.Heading2, ["heading2"]),
        new("h3", "Heading 3", "Toggle heading level 3", SlashCommandAction.Heading3, ["heading3"]),
    ];

    public static MarkdownSlashTrigger? TryGetTrigger(string text, int caretOffset)
    {
        var caret = Math.Clamp(caretOffset, 0, text.Length);
        var slashIndex = caret - 1;

        while (slashIndex >= 0)
        {
            var ch = text[slashIndex];
            if (ch == '/')
            {
                break;
            }

            if (char.IsWhiteSpace(ch))
            {
                return null;
            }

            slashIndex--;
        }

        if (slashIndex < 0 || text[slashIndex] != '/')
        {
            return null;
        }

        if (slashIndex > 0)
        {
            var previous = text[slashIndex - 1];
            if (!char.IsWhiteSpace(previous) && previous is not '(' and not '[' and not '{')
            {
                return null;
            }
        }

        var query = text[(slashIndex + 1)..caret];
        if (query.Any(char.IsWhiteSpace))
        {
            return null;
        }

        return new MarkdownSlashTrigger(slashIndex, caret - slashIndex, query);
    }

    public static IReadOnlyList<MarkdownSlashCommand> Filter(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return All;
        }

        return All
            .Select((command, index) => new
            {
                Command = command,
                Index = index,
                Score = ScoreMatch(command, query)
            })
            .Where(result => result.Score is not null)
            .OrderByDescending(result => result.Score)
            .ThenBy(result => result.Index)
            .Select(result => result.Command)
            .ToList();
    }

    private static int? ScoreMatch(MarkdownSlashCommand command, string query)
    {
        return command.Aliases
            .Prepend(command.Label)
            .Prepend(command.Id)
            .Select(candidate => TextMatchScorer.Score(candidate, query))
            .Where(score => score is not null)
            .Select(score => score!.Value)
            .DefaultIfEmpty(int.MinValue)
            .Max() is var score && score != int.MinValue
                ? score
                : null;
    }
}

internal readonly record struct MarkdownSlashTrigger(int Start, int Length, string Query);

internal sealed record MarkdownSlashCommand(string Id, string Label, string Description, SlashCommandAction Action, IReadOnlyList<string> Aliases);

internal enum SlashCommandAction
{
    Bold,
    Italic,
    InlineCode,
    CodeBlock,
    TaskList,
    BulletList,
    Table,
    FormatTable,
    Heading1,
    Heading2,
    Heading3,
}
