using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// The header search: roles, scripts, jinxes and teams by name (Polish or English, without diacritics), best matches
/// first. A name starting with the query beats a word starting with it, which beats the query anywhere; a query of 4+
/// letters also finds a word with one letter wrong, so "zolnirz" still finds "Żołnierz".
/// </summary>
public static class SiteSearch
{
    public const string RolesGroup = "Role";
    public const string ScriptsGroup = "Skrypty";
    public const string JinxesGroup = "Jinxy";
    public const string TeamsGroup = "Drużyny";

    /// <summary>Hits per group; the group's "more" link shows the rest on its list page.</summary>
    public const int PerGroup = 6;

    private const int NoMatch = int.MaxValue;

    public static IReadOnlyList<SearchGroup> Find(GameData data, string query)
    {
        var q = SearchText.Fold(query.Trim());
        if (q.Length == 0)
        {
            return [];
        }

        var escaped = Uri.EscapeDataString(query.Trim());

        var roles = Rank(data.AllRoles, r => Score(q, Clean(r.NamePl), r.NameEn, r.Id), r => Clean(r.NamePl))
            .Select(x => SearchHit.For(x.Item))
            .ToList();

        var scripts = Rank(data.Scripts.Scripts, s => Score(q, s.Name, s.Id), s => s.Name)
            .Select(x => SearchHit.For(x.Item))
            .ToList();

        var jinxes = Rank(
                data.Jinxes.SelectMany(o => o.Jinxes.Select(j => (Owner: data.FindRole(o.Id), Other: data.FindRole(j.Id), Jinx: j)))
                    .Where(x => x.Owner is not null && x.Other is not null),
                x => Math.Min(
                    Score(q, Clean(x.Owner!.NamePl), x.Owner.NameEn, x.Owner.Id),
                    Score(q, Clean(x.Other!.NamePl), x.Other.NameEn, x.Other.Id)),
                x => $"{Clean(x.Owner!.NamePl)} {Clean(x.Other!.NamePl)}")
            .Select(x => SearchHit.For(x.Item.Owner!, x.Item.Other!, x.Item.Jinx))
            .ToList();

        var teams = Rank(data.Teams, t => Score(q, t.PluralPl, t.NamePl, t.PluralEn, t.NameEn, t.Id), t => t.PluralPl)
            .Select(x => SearchHit.For(x.Item))
            .ToList();

        return new[]
        {
            new SearchGroup(RolesGroup, roles, $"roles?q={escaped}"),
            new SearchGroup(ScriptsGroup, scripts, null),
            new SearchGroup(JinxesGroup, jinxes, $"jinxes?q={escaped}"),
            new SearchGroup(TeamsGroup, teams, null),
        }
        .Where(g => g.Hits.Count > 0)
        .ToList();
    }

    /// <summary>Machine translations are wrapped in « »; they should not change the order or the match.</summary>
    private static string Clean(string name) => name.Trim('«', '»', ' ');

    private static IEnumerable<(T Item, int Score)> Rank<T>(IEnumerable<T> items, Func<T, int> score, Func<T, string> name) =>
        items
            .Select(item => (Item: item, Score: score(item)))
            .Where(x => x.Score != NoMatch)
            .OrderBy(x => x.Score)
            .ThenBy(x => name(x.Item), StringComparer.Create(new System.Globalization.CultureInfo("pl-PL"), true));

    /// <summary>Best (lowest) score of the query against any of the names; <see cref="NoMatch"/> when none matches.</summary>
    private static int Score(string q, params string[] names) => names.Min(name => ScoreOne(q, SearchText.Fold(name)));

    private static int ScoreOne(string q, string name)
    {
        if (name.Length == 0)
        {
            return NoMatch;
        }

        if (name == q)
        {
            return 0;
        }

        if (name.StartsWith(q, StringComparison.Ordinal))
        {
            return 1;
        }

        var words = name.Split([' ', '-', '\'', '’', '(', ')', ','], StringSplitOptions.RemoveEmptyEntries);
        if (words.Any(w => w.StartsWith(q, StringComparison.Ordinal)))
        {
            return 2;
        }

        if (name.Contains(q, StringComparison.Ordinal))
        {
            return 3;
        }

        // One letter wrong, missing or extra at the start of a word.
        return q.Length >= 4 && words.Any(w => NearPrefix(q, w)) ? 4 : NoMatch;
    }

    /// <summary>The word starts with something at most one edit away from <paramref name="q"/>.</summary>
    private static bool NearPrefix(string q, string word) =>
        new[] { q.Length - 1, q.Length, q.Length + 1 }
            .Where(n => n > 0 && n <= word.Length)
            .Any(n => WithinOneEdit(q, word[..n]));

    private static bool WithinOneEdit(string a, string b)
    {
        if (Math.Abs(a.Length - b.Length) > 1)
        {
            return false;
        }

        var i = 0;
        while (i < a.Length && i < b.Length && a[i] == b[i])
        {
            i++;
        }

        if (a.Length == b.Length)
        {
            return a[(Math.Min(i + 1, a.Length))..] == b[(Math.Min(i + 1, b.Length))..];
        }

        return a.Length > b.Length ? a[(i + 1)..] == b[i..] : a[i..] == b[(i + 1)..];
    }
}
