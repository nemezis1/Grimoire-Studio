using System.Text.RegularExpressions;

namespace GrimoireStudio.Data;

/// <summary>
/// Checks cross-file consistency of <see cref="GameData"/>. Returns human-readable errors; empty means valid.
/// </summary>
public static partial class DataValidator
{
    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex IdPattern();

    /// <summary>Role and script ids: lowercase letters and digits only.</summary>
    public static bool IsValidId(string id) => IdPattern().IsMatch(id);

    public static IReadOnlyList<string> Validate(GameData data, string? iconsDirectory = null)
    {
        var errors = new List<string>();
        var roleIds = new HashSet<string>(StringComparer.Ordinal);
        var teamIds = data.Teams.Select(t => t.Id).ToHashSet(StringComparer.Ordinal);

        foreach (var team in data.Teams.GroupBy(t => t.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"Team '{team.Key}': duplicate id.");
        }

        foreach (var team in data.Teams.Where(t => !IdPattern().IsMatch(t.Id)))
        {
            errors.Add($"Team '{team.Id}': id must be lowercase letters and digits only.");
        }

        foreach (var role in data.AllRoles)
        {
            if (!IdPattern().IsMatch(role.Id))
            {
                errors.Add($"Role '{role.Id}': id must be lowercase letters and digits only.");
            }

            if (!roleIds.Add(role.Id))
            {
                errors.Add($"Role '{role.Id}': duplicate id.");
            }

            ValidateRole(role, teamIds, errors);

            if (iconsDirectory is not null && !File.Exists(Path.Combine(iconsDirectory, $"{role.Id}.png")))
            {
                errors.Add($"Role '{role.Id}': missing icon {role.Id}.png.");
            }
        }

        foreach (var role in data.Roles.Where(r => r.Team == TeamIds.Fabled))
        {
            errors.Add($"Role '{role.Id}': fabled belongs in {GameData.FabledFile}.");
        }

        foreach (var role in data.Fabled.Where(r => r.Team != TeamIds.Fabled))
        {
            errors.Add($"Role '{role.Id}': {GameData.FabledFile} may only contain team 'fabled'.");
        }

        ValidateScripts(data, roleIds, errors);
        ValidateJinxes(data, roleIds, errors);

        return errors;
    }

    private static void ValidateRole(Role role, HashSet<string> teamIds, List<string> errors)
    {
        void Require(string value, string field)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                errors.Add($"Role '{role.Id}': {field} is empty.");
            }
        }

        Require(role.NameEn, nameof(Role.NameEn));
        Require(role.NamePl, nameof(Role.NamePl));
        Require(role.AbilityEn, nameof(Role.AbilityEn));
        Require(role.AbilityPl, nameof(Role.AbilityPl));

        if (!teamIds.Contains(role.Team))
        {
            errors.Add($"Role '{role.Id}': unknown team.");
        }

        if (role.FirstNight < 0 || role.OtherNight < 0)
        {
            errors.Add($"Role '{role.Id}': night order cannot be negative.");
        }

        if (role.RemindersEn.Count != role.RemindersPl.Count)
        {
            errors.Add($"Role '{role.Id}': {role.RemindersEn.Count} EN reminders vs {role.RemindersPl.Count} PL.");
        }

        if (role.RemindersGlobalEn.Count != role.RemindersGlobalPl.Count)
        {
            errors.Add($"Role '{role.Id}': {role.RemindersGlobalEn.Count} EN global reminders vs {role.RemindersGlobalPl.Count} PL.");
        }
    }

    private static void ValidateScripts(GameData data, HashSet<string> roleIds, List<string> errors)
    {
        var scriptIds = new HashSet<string>(StringComparer.Ordinal);

        foreach (var script in data.Scripts.Scripts)
        {
            if (!scriptIds.Add(script.Id))
            {
                errors.Add($"Script '{script.Id}': duplicate id.");
            }

            foreach (var group in script.Roles.GroupBy(id => id).Where(g => g.Count() > 1))
            {
                errors.Add($"Script '{script.Id}': role '{group.Key}' listed {group.Count()} times.");
            }

            foreach (var id in script.Roles.Where(id => !roleIds.Contains(id)).Distinct())
            {
                errors.Add($"Script '{script.Id}': unknown role '{id}'.");
            }
        }

        foreach (var role in data.Roles.Where(r => r.Edition.Length > 0 && !scriptIds.Contains(r.Edition)))
        {
            errors.Add($"Role '{role.Id}': unknown edition '{role.Edition}'.");
        }
    }

    private static void ValidateJinxes(GameData data, HashSet<string> roleIds, List<string> errors)
    {
        foreach (var owner in data.Jinxes.GroupBy(o => o.Id).Where(g => g.Count() > 1))
        {
            errors.Add($"Jinx owner '{owner.Key}': listed {owner.Count()} times.");
        }

        foreach (var owner in data.Jinxes)
        {
            foreach (var pair in owner.Jinxes.GroupBy(j => j.Id).Where(g => g.Count() > 1))
            {
                errors.Add($"Jinx '{owner.Id}' / '{pair.Key}': listed {pair.Count()} times.");
            }

            if (!roleIds.Contains(owner.Id))
            {
                errors.Add($"Jinx owner '{owner.Id}': unknown role.");
            }

            foreach (var jinx in owner.Jinxes)
            {
                if (!roleIds.Contains(jinx.Id))
                {
                    errors.Add($"Jinx '{owner.Id}' / '{jinx.Id}': unknown role.");
                }

                if (string.IsNullOrWhiteSpace(jinx.ReasonEn) || string.IsNullOrWhiteSpace(jinx.ReasonPl))
                {
                    errors.Add($"Jinx '{owner.Id}' / '{jinx.Id}': reason is empty.");
                }
            }
        }
    }
}
