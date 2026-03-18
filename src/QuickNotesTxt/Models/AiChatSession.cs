namespace QuickNotesTxt.Models;

public sealed record AiChatSession
{
    public List<AiChatMessage> Messages { get; init; } = [];

    public AiChatSession WithAppended(string role, string content)
    {
        var messages = new List<AiChatMessage>(Messages)
        {
            new(role, content)
        };

        return this with { Messages = messages };
    }
}
