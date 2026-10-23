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
            .ToList() // before reading byId.Values: Remove must have run for every item
            .Concat(byId.Values)
            .ToList();

        return data with { Scripts = data.Scripts with { Scripts = scripts } };
    }

    /// <summary>
    /// Data with <paramref name="overrides"/> applied: a role with an existing id replaces it in place (moving between
    /// roles and fabled if its team changed), a new id is appended to roles or fabled by its team.
    /// </summary>
    public static GameData WithRoles(this GameData data, IEnumerable<Role> overrides)
    {
        var byId = overrides.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);
        var all = data.AllRoles
            .Select(r => byId.Remove(r.Id, out var replacement) ? replacement : r)
            .ToList() // before reading byId.Values: Remove must have run for every item
            .Concat(byId.Values)
            .ToList();

        return data with
        {
            Roles = all.Where(r => r.Team != TeamIds.Fabled).ToList(),
            Fabled = all.Where(r => r.Team == TeamIds.Fabled).ToList(),
        };
    }

    /// <summary>
    /// Data with <paramref name="overrides"/> applied: a team with an existing id replaces it, a new id is added.
    /// Teams are kept sorted by <see cref="TeamInfo.Order"/>.
    /// </summary>
    public static GameData WithTeams(this GameData data, IEnumerable<TeamInfo> overrides)
    {
        var byId = overrides.ToDictionary(t => t.Id, StringComparer.OrdinalIgnoreCase);
        var teams = data.Teams
            .Select(t => byId.Remove(t.Id, out var replacement) ? replacement : t)
            .ToList() // before reading byId.Values: Remove must have run for every item
            .Concat(byId.Values)
            .OrderBy(t => t.Order)
            .ToList();

        return data with { Teams = teams };
    }

    /// <summary>Data with jinx <paramref name="overrides"/> applied: each replaces, adds or removes one owner/other pair.</summary>
    public static GameData WithJinxes(this GameData data, IEnumerable<JinxOverride> overrides)
    {
        var owners = data.Jinxes.Select(o => (o.Id, Jinxes: o.Jinxes.ToList())).ToList();

        foreach (var change in overrides)
        {
            var index = owners.FindIndex(o => string.Equals(o.Id, change.OwnerId, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
            {
                owners.Add((change.OwnerId, []));
                index = owners.Count - 1;
            }

            var jinxes = owners[index].Jinxes;
            var existing = jinxes.FindIndex(j => string.Equals(j.Id, change.Jinx.Id, StringComparison.OrdinalIgnoreCase));
            if (change.Removed)
            {
                if (existing >= 0)
                {
                    jinxes.RemoveAt(existing);
                }
            }
            else if (existing >= 0)
            {
                jinxes[existing] = change.Jinx;
            }
            else
            {
                jinxes.Add(change.Jinx);
            }
        }

        return data with
        {
            Jinxes = owners
                .Where(o => o.Jinxes.Count > 0)
                .Select(o => new RoleJinxes { Id = o.Id, Jinxes = o.Jinxes })
                .ToList(),
        };
    }

    public static TeamInfo? FindTeam(this GameData data, string id) =>
        data.Teams.FirstOrDefault(t => string.Equals(t.Id, id, StringComparison.OrdinalIgnoreCase));

    /// <summary>The team of a role; an undefined team id gets a stand-in so it can still be shown.</summary>
    public static TeamInfo TeamOf(this GameData data, Role role) => data.FindTeam(role.Team) ?? TeamInfo.Unknown(role.Team);

    /// <summary>Roles grouped by team, groups in team order.</summary>
    public static IReadOnlyList<(TeamInfo Team, IReadOnlyList<Role> Roles)> GroupByTeam(this GameData data, IEnumerable<Role> roles) =>
        roles
            .GroupBy(r => r.Team, StringComparer.OrdinalIgnoreCase)
            .Select(g => (Team: data.FindTeam(g.Key) ?? TeamInfo.Unknown(g.Key), Roles: (IReadOnlyList<Role>)g.ToList()))
            .OrderBy(g => g.Team.Order)
            .ToList();

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
