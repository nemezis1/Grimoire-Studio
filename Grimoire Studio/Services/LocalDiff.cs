using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Short descriptions of what a local change differs in from the data folder, for the "my changes" page.</summary>
public static class LocalDiff
{
    public static IReadOnlyList<string> Role(Role original, LocalRole local)
    {
        var role = local.Role;
        var changes = new List<string>();
        void Check(bool changed, string label)
        {
            if (changed)
            {
                changes.Add(label);
            }
        }

        Check(role.NamePl != original.NamePl || role.NameEn != original.NameEn, "nazwa");
        Check(role.Team != original.Team, "drużyna");
        Check(role.Edition != original.Edition, "edycja");
        Check(role.Setup != original.Setup, "wpływ na przygotowanie");
        Check(role.AbilityPl != original.AbilityPl || role.AbilityEn != original.AbilityEn, "zdolność");
        Check(role.FirstNight != original.FirstNight || role.OtherNight != original.OtherNight, "kolejność nocy");
        Check(role.FirstNightReminderPl != original.FirstNightReminderPl
            || role.FirstNightReminderEn != original.FirstNightReminderEn
            || role.OtherNightReminderPl != original.OtherNightReminderPl
            || role.OtherNightReminderEn != original.OtherNightReminderEn, "tekst dla narratora");
        Check(!role.RemindersPl.SequenceEqual(original.RemindersPl)
            || !role.RemindersEn.SequenceEqual(original.RemindersEn)
            || !role.RemindersGlobalPl.SequenceEqual(original.RemindersGlobalPl)
            || !role.RemindersGlobalEn.SequenceEqual(original.RemindersGlobalEn), "znaczniki");
        Check(local.IconUrl is not null, "ikona");
        return changes;
    }

    public static IReadOnlyList<string> Script(Script original, Script local)
    {
        var changes = new List<string>();
        if (local.Name != original.Name)
        {
            changes.Add($"nazwa: {original.Name} → {local.Name}");
        }

        var added = local.Roles.Except(original.Roles, StringComparer.OrdinalIgnoreCase).Count();
        var removed = original.Roles.Except(local.Roles, StringComparer.OrdinalIgnoreCase).Count();
        if (added > 0 || removed > 0)
        {
            changes.Add($"role: +{added} / −{removed}");
        }
        else if (!local.Roles.SequenceEqual(original.Roles, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add("kolejność ról");
        }

        return changes;
    }

    public static IReadOnlyList<string> Team(TeamInfo original, TeamInfo local)
    {
        var changes = new List<string>();
        if (local.NamePl != original.NamePl || local.NameEn != original.NameEn
            || local.PluralPl != original.PluralPl || local.PluralEn != original.PluralEn)
        {
            changes.Add("nazwa");
        }

        if (!string.Equals(local.Color, original.Color, StringComparison.OrdinalIgnoreCase))
        {
            changes.Add("kolor");
        }

        if (local.Order != original.Order)
        {
            changes.Add("kolejność");
        }

        if (local.Extra != original.Extra)
        {
            changes.Add(local.Extra ? "poza liczbą postaci" : "w liczbie postaci");
        }

        return changes;
    }
}
