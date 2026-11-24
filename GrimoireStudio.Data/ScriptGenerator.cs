namespace GrimoireStudio.Data;

/// <summary>
/// Fills a script at random around the roles already chosen, as script tools do: each counted team gets random roles
/// from a pool until it has the wanted number. Chosen roles always stay; a role that needs another one
/// (<see cref="ScriptAnalysis.Requires"/>) comes with its partner or not at all.
/// </summary>
public static class ScriptGenerator
{
    /// <summary>The counted teams with how many roles of each a full script usually has.</summary>
    public static IReadOnlyList<(string Team, int Count)> Usual { get; } =
    [
        (TeamIds.Townsfolk, 13),
        (TeamIds.Outsider, 4),
        (TeamIds.Minion, 4),
        (TeamIds.Demon, 4),
    ];

    /// <summary>
    /// The chosen roles plus random roles from <paramref name="pool"/>, up to <paramref name="wanted"/> roles of each
    /// team (a team that already has as many is left as it is). Returns the added roles only.
    /// </summary>
    public static IReadOnlyList<Role> Fill(
        GameData data,
        IReadOnlyCollection<Role> chosen,
        IEnumerable<Role> pool,
        IReadOnlyDictionary<string, int> wanted,
        Random random)
    {
        var ids = chosen.Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var counts = wanted.Keys.ToDictionary(team => team, team => chosen.Count(r => r.Team == team));
        var added = new List<Role>();

        bool HasRoom(Role role) => counts.TryGetValue(role.Team, out var count) && count < wanted[role.Team];

        void Add(Role role)
        {
            ids.Add(role.Id);
            counts[role.Team]++;
            added.Add(role);
        }

        // Shuffled once, then taken in order: every team draws from the same random order.
        var candidates = pool
            .Where(r => wanted.ContainsKey(r.Team) && !ids.Contains(r.Id))
            .DistinctBy(r => r.Id, StringComparer.OrdinalIgnoreCase)
            .OrderBy(_ => random.Next())
            .ToList();

        foreach (var role in candidates)
        {
            if (ids.Contains(role.Id) || !HasRoom(role))
            {
                continue;
            }

            // A role with a partner needs room for both (the partner may be on another team).
            if (ScriptAnalysis.Requires.TryGetValue(role.Id, out var partnerId) && !ids.Contains(partnerId))
            {
                if (data.FindRole(partnerId) is not { } partner || !wanted.ContainsKey(partner.Team))
                {
                    continue;
                }

                Add(role);
                if (!HasRoom(partner))
                {
                    // No room for the partner: the role goes back.
                    ids.Remove(role.Id);
                    counts[role.Team]--;
                    added.RemoveAt(added.Count - 1);
                    continue;
                }

                Add(partner);
                continue;
            }

            Add(role);
        }

        return added;
    }
}
