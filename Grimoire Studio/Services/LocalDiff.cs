using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Short descriptions of what a local change differs in from the data folder, for the "my changes" page.</summary>
public static class LocalDiff
{
    /// <summary>
    /// Changed fields of a role with old and new values; for a new role (<paramref name="original"/> null) every filled
    /// field with no old value. <paramref name="beforeIcon"/> is the icon shown before the change.
    /// </summary>
    public static IReadOnlyList<FieldChange> RoleDetails(Role? original, LocalRole local, string beforeIcon, Func<string, string> teamName)
    {
        var role = local.Role;
        var changes = new List<FieldChange>();
        void Add(string label, string? before, string after)
        {
            if (before != after && (original is not null || after.Length > 0))
            {
                changes.Add(new FieldChange(label, original is null ? null : before, after));
            }
        }

        Add("Nazwa (PL)", original?.NamePl, role.NamePl);
        Add("Nazwa (EN)", original?.NameEn, role.NameEn);
        Add("Drużyna", original is null ? null : teamName(original.Team), teamName(role.Team));
        Add("Edycja", original?.Edition, role.Edition);
        Add("Zmienia przygotowanie", original is null ? null : YesNo(original.Setup), YesNo(role.Setup));
        Add("Zdolność (PL)", original?.AbilityPl, role.AbilityPl);
        Add("Zdolność (EN)", original?.AbilityEn, role.AbilityEn);
        Add("Pierwsza noc", original is null ? null : Night(original.FirstNight), Night(role.FirstNight));
        Add("Pozostałe noce", original is null ? null : Night(original.OtherNight), Night(role.OtherNight));
        Add("Narrator, pierwsza noc (PL)", original?.FirstNightReminderPl, role.FirstNightReminderPl);
        Add("Narrator, pierwsza noc (EN)", original?.FirstNightReminderEn, role.FirstNightReminderEn);
        Add("Narrator, pozostałe noce (PL)", original?.OtherNightReminderPl, role.OtherNightReminderPl);
        Add("Narrator, pozostałe noce (EN)", original?.OtherNightReminderEn, role.OtherNightReminderEn);
        Add("Znaczniki (PL)", original is null ? null : List(original.RemindersPl), List(role.RemindersPl));
        Add("Znaczniki (EN)", original is null ? null : List(original.RemindersEn), List(role.RemindersEn));
        Add("Znaczniki globalne (PL)", original is null ? null : List(original.RemindersGlobalPl), List(role.RemindersGlobalPl));
        Add("Znaczniki globalne (EN)", original is null ? null : List(original.RemindersGlobalEn), List(role.RemindersGlobalEn));

        if (local.IconUrl is { } icon)
        {
            changes.Add(new FieldChange("Ikona", original is null ? null : beforeIcon, icon, IsImage: true));
        }

        return changes;
    }

    public static IReadOnlyList<FieldChange> ScriptDetails(Script? original, Script local, Func<string, string> roleName)
    {
        var changes = new List<FieldChange>();
        if (original is null)
        {
            changes.Add(new FieldChange("Nazwa", null, local.Name));
            changes.Add(new FieldChange("Role", null, string.Join(", ", local.Roles.Select(roleName))));
            changes.Add(new FieldChange("Obrazek", null, GameDataClient.EditionIconUrl(local), IsImage: true));
            return changes;
        }

        if (original.Name != local.Name)
        {
            changes.Add(new FieldChange("Nazwa", original.Name, local.Name));
        }

        var added = local.Roles.Except(original.Roles, StringComparer.OrdinalIgnoreCase).Select(roleName).ToList();
        var removed = original.Roles.Except(local.Roles, StringComparer.OrdinalIgnoreCase).Select(roleName).ToList();
        if (added.Count > 0)
        {
            changes.Add(new FieldChange("Dodane role", string.Empty, string.Join(", ", added)));
        }

        if (removed.Count > 0)
        {
            changes.Add(new FieldChange("Usunięte role", string.Join(", ", removed), string.Empty));
        }

        if (added.Count == 0 && removed.Count == 0 && !local.Roles.SequenceEqual(original.Roles, StringComparer.OrdinalIgnoreCase))
        {
            changes.Add(new FieldChange("Kolejność ról", string.Join(", ", original.Roles.Select(roleName)), string.Join(", ", local.Roles.Select(roleName))));
        }

        if (GameDataClient.EditionIconUrl(original) != GameDataClient.EditionIconUrl(local))
        {
            changes.Add(new FieldChange("Obrazek", GameDataClient.EditionIconUrl(original), GameDataClient.EditionIconUrl(local), IsImage: true));
        }

        return changes;
    }

    public static IReadOnlyList<FieldChange> TeamDetails(TeamInfo? original, TeamInfo local)
    {
        var changes = new List<FieldChange>();
        void Add(string label, string? before, string after)
        {
            if (before != after)
            {
                changes.Add(new FieldChange(label, original is null ? null : before, after));
            }
        }

        Add("Nazwa (PL)", original?.NamePl, local.NamePl);
        Add("Liczba mnoga (PL)", original?.PluralPl, local.PluralPl);
        Add("Nazwa (EN)", original?.NameEn, local.NameEn);
        Add("Liczba mnoga (EN)", original?.PluralEn, local.PluralEn);
        Add("Kolor", original?.Color, local.Color);
        Add("Kolejność", original?.Order.ToString(), local.Order.ToString());
        Add("Poza liczbą postaci", original is null ? null : YesNo(original.Extra), YesNo(local.Extra));
        return changes;
    }

    public static IReadOnlyList<FieldChange> JinxDetails(Jinx? original, JinxOverride local)
    {
        if (local.Removed)
        {
            return [new FieldChange("Zasada (PL)", original?.ReasonPl ?? string.Empty, "(usunięty)")];
        }

        var changes = new List<FieldChange>();
        if (original?.ReasonPl.Trim() != local.Jinx.ReasonPl.Trim())
        {
            changes.Add(new FieldChange("Zasada (PL)", original?.ReasonPl, local.Jinx.ReasonPl));
        }

        if (original?.ReasonEn.Trim() != local.Jinx.ReasonEn.Trim())
        {
            changes.Add(new FieldChange("Zasada (EN)", original?.ReasonEn, local.Jinx.ReasonEn));
        }

        return changes;
    }

    private static string YesNo(bool value) => value ? "tak" : "nie";

    private static string Night(int order) => order > 0 ? $"#{order}" : "nie budzi się";

    private static string List(IReadOnlyList<string> items) => string.Join(" · ", items);

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

        if (GameDataClient.EditionIconUrl(original) != GameDataClient.EditionIconUrl(local))
        {
            changes.Add("obrazek");
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
