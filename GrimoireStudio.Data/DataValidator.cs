using System.Text.RegularExpressions;

namespace GrimoireStudio.Data;

/// <summary>
/// Checks cross-file consistency of <see cref="GameData"/>. Returns human-readable errors; empty means valid.
/// </summary>
public static partial class DataValidator
{
    [GeneratedRegex("^[a-z0-9]+$")]
    private static partial Regex IdPattern();

    public static IReadOnlyList<string> Validate(GameData data, string? iconsDirectory = null)
    {
        var errors = new List<string>();
        var roleIds = new HashSet<string>(StringComparer.Ordinal);

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

            ValidateRole(role, errors);

            if (iconsDirectory is not null && !File.Exists(Path.Combine(iconsDirectory, $"{role.Id}.png")))
            {
                errors.Add($"Role '{role.Id}': missing icon {role.Id}.png.");
            }
        }

        foreach (var role in data.Roles.Where(r => r.Team == Team.Fabled))
        {
            errors.Add($"Role '{role.Id}': fabled belongs in {GameData.FabledFile}.");
        }

        foreach (var role in data.Fabled.Where(r => r.Team != Team.Fabled))
        {
            errors.Add($"Role '{role.Id}': {GameData.FabledFile} may only contain team 'fabled'.");
        }

        ValidateScripts(data, roleIds, errors);
        ValidateJinxes(data, roleIds, errors);

        return errors;
    }

    private static void ValidateRole(Role role, List<string> errors)
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

        if (!Enum.IsDefined(role.Team))
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
        foreach (var owner in data.Jinxes)
        {
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
