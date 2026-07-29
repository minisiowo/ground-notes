namespace GroundNotes.Services;

internal static class TextMatchScorer
{
    public static int? Score(string candidate, string searchText)
    {
        if (string.IsNullOrWhiteSpace(candidate) || string.IsNullOrWhiteSpace(searchText))
        {
            return null;
        }

        var candidateText = candidate.ToLowerInvariant();
        var queryText = searchText.ToLowerInvariant();

        if (candidateText == queryText)
        {
            return 10_000 - candidateText.Length;
        }

        var substringIndex = candidateText.IndexOf(queryText, StringComparison.Ordinal);
        if (substringIndex == 0)
        {
            return 8_000 - (candidateText.Length * 2);
        }

        if (substringIndex > 0)
        {
            return 7_000 - (substringIndex * 40) - candidateText.Length;
        }

        var score = 1_000 - candidateText.Length;
        var previousMatchIndex = -1;

        foreach (var queryCharacter in queryText)
        {
            var matchIndex = candidateText.IndexOf(queryCharacter, previousMatchIndex + 1);
            if (matchIndex < 0)
            {
                return null;
            }

            score += 100;

            if (matchIndex == 0)
            {
                score += 120;
            }
            else if (IsWordBoundary(candidate[matchIndex - 1]))
            {
                score += 60;
            }

            if (previousMatchIndex >= 0)
            {
                var gap = matchIndex - previousMatchIndex - 1;
                if (gap == 0)
                {
                    score += 90;
                }
                else
                {
                    score -= gap * 8;
                }
            }

            previousMatchIndex = matchIndex;
        }

        return score;
    }

    private static bool IsWordBoundary(char character)
    {
        return character is ' ' or '-' or '_' or '/' or '\\' or '.';
    }
}
