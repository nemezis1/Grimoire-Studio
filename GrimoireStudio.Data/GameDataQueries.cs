namespace GrimoireStudio.Data;

/// <summary>Read-side lookups over <see cref="GameData"/>. Data is small (~150 roles), so linear scans are fine.</summary>
public static class GameDataQueries
{
    public static Role? FindRole(this GameData data, string id) =>
        data.AllRoles.FirstOrDefault(r => string.Equals(r.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>
    /// Data with <paramref name="overrides"/> applied: a script with an existing id replaces it in place,
    /// a new id is appended.
    /// </summary>
    public static GameData WithScripts(this GameData data, IEnumerable<Script> overrides)
    {
        var byId = overrides.ToDictionary(s => s.Id, StringComparer.OrdinalIgnoreCase);
        var scripts = data.Scripts.Scripts
            .Select(s => byId.Remove(s.Id, out var replacement) ? replacement : s)
            .Concat(byId.Values)
            .ToList();

        return data with { Scripts = data.Scripts with { Scripts = scripts } };
    }

    public static Script? FindScript(this GameData data, string id) =>
        data.Scripts.Scripts.FirstOrDefault(s => string.Equals(s.Id, id, StringComparison.OrdinalIgnoreCase));

    public static IEnumerable<Script> ScriptsWithRole(this GameData data, string roleId) =>
        data.Scripts.Scripts.Where(s => s.Roles.Contains(roleId, StringComparer.OrdinalIgnoreCase));

    /// <summary>Roles of a script in its listed order; unknown ids are skipped.</summary>
    public static IReadOnlyList<Role> RolesOf(this GameData data, Script script) =>
        script.Roles.Select(data.FindRole).OfType<Role>().ToList();

    /// <summary>Roles of the script that wake on the given night, in waking order.</summary>
    public static IReadOnlyList<Role> NightOrder(this GameData data, Script script, bool firstNight) =>
        data.RolesOf(script)
            .Select(r => (Role: r, Order: firstNight ? r.FirstNight : r.OtherNight))
            .Where(x => x.Order > 0)
            .OrderBy(x => x.Order)
            .Select(x => x.Role)
            .ToList();

    /// <summary>
    /// Night order with the first night's minion and demon information steps (when <paramref name="includeInfo"/>).
    /// </summary>
    public static IReadOnlyList<NightStep> NightSteps(this GameData data, Script script, bool firstNight, bool includeInfo = true)
    {
        var steps = data.NightOrder(script, firstNight)
            .Select(r => new NightStep(NightStepKind.Role, firstNight ? r.FirstNight : r.OtherNight, r))
            .ToList();

        if (firstNight && includeInfo)
        {
            steps.Add(new NightStep(NightStepKind.MinionInfo, NightStep.MinionInfoOrder));
            steps.Add(new NightStep(NightStepKind.DemonInfo, NightStep.DemonInfoOrder));
        }

        // OrderBy is stable: roles sharing a number keep their script order.
        return steps.OrderBy(s => s.Order).ToList();
    }

    /// <summary>
    /// Jinxes involving the role in either direction (owned by it, or owned by another role and naming it).
    /// When <paramref name="scope"/> is given, only jinxes with roles on that script are returned.
    /// </summary>
    public static IReadOnlyList<(Role Other, Jinx Jinx)> JinxesFor(this GameData data, string roleId, Script? scope = null)
    {
        var result = new List<(Role, Jinx)>();

        foreach (var owner in data.Jinxes)
        {
            foreach (var jinx in owner.Jinxes)
            {
                string? otherId =
                    string.Equals(owner.Id, roleId, StringComparison.OrdinalIgnoreCase) ? jinx.Id :
                    string.Equals(jinx.Id, roleId, StringComparison.OrdinalIgnoreCase) ? owner.Id :
                    null;

                if (otherId is null || (scope is not null && !scope.Roles.Contains(otherId, StringComparer.OrdinalIgnoreCase)))
                {
                    continue;
                }

                if (data.FindRole(otherId) is { } other)
                {
                    result.Add((other, jinx));
                }
            }
        }

        return result;
    }
}
