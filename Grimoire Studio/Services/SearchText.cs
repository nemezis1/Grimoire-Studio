using System.Globalization;
using System.Text;

namespace Grimoire_Studio.Services;

/// <summary>Case- and diacritic-insensitive matching, so "jedza" finds "Jędza".</summary>
public static class SearchText
{
    public static string Fold(string value)
    {
        var decomposed = value.ToLowerInvariant().Replace('ł', 'l').Normalize(NormalizationForm.FormD);
        var builder = new StringBuilder(decomposed.Length);

        foreach (var c in decomposed)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(c);
            }
        }

        return builder.ToString();
    }

    public static bool Matches(string query, params string[] fields)
    {
        var folded = Fold(query.Trim());
        return folded.Length == 0 || fields.Any(f => Fold(f).Contains(folded, StringComparison.Ordinal));
    }
}
