namespace Grimoire_Studio.Services;

/// <summary>Word-by-word difference of two texts, to show what a new text changed (e.g. one word of an ability).</summary>
public static class WordDiff
{
    /// <summary>One run of words: kept in both texts, only in the old one or only in the new one.</summary>
    public enum Kind
    {
        Same,
        Removed,
        Added,
    }

    /// <summary>The new text as runs of words, with the removed words of the old one where they were.</summary>
    public static IReadOnlyList<(string Text, Kind Kind)> Compare(string old, string @new)
    {
        var a = Words(old);
        var b = Words(@new);

        // Longest common subsequence of words, then walked from the start.
        var lengths = new int[a.Count + 1, b.Count + 1];
        for (var i = a.Count - 1; i >= 0; i--)
        {
            for (var j = b.Count - 1; j >= 0; j--)
            {
                lengths[i, j] = a[i] == b[j] ? lengths[i + 1, j + 1] + 1 : Math.Max(lengths[i + 1, j], lengths[i, j + 1]);
            }
        }

        var runs = new List<(string Text, Kind Kind)>();
        void Add(string word, Kind kind)
        {
            if (runs.Count > 0 && runs[^1].Kind == kind)
            {
                runs[^1] = (runs[^1].Text + word, kind);
            }
            else
            {
                runs.Add((word, kind));
            }
        }

        int x = 0, y = 0;
        while (x < a.Count && y < b.Count)
        {
            if (a[x] == b[y])
            {
                Add(a[x++], Kind.Same);
                y++;
            }
            else if (lengths[x + 1, y] >= lengths[x, y + 1])
            {
                Add(a[x++], Kind.Removed);
            }
            else
            {
                Add(b[y++], Kind.Added);
            }
        }

        while (x < a.Count)
        {
            Add(a[x++], Kind.Removed);
        }

        while (y < b.Count)
        {
            Add(b[y++], Kind.Added);
        }

        return runs;
    }

    /// <summary>Words with the spaces after them, so the runs join back into the text.</summary>
    private static List<string> Words(string text) =>
        System.Text.RegularExpressions.Regex.Matches(text, @"\S+\s*").Select(m => m.Value).ToList();
}
