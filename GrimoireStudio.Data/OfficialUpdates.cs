namespace GrimoireStudio.Data;

/// <summary>
/// Compares the data The Pandemonium Institute publishes with the wiki's, and turns the differences the admin picks
/// into wiki changes. English texts come from the published data; Polish ones from the admin, or else the role keeps
/// its own, and a text with no translation yet is the English one in « » (the mark for text to review).
/// </summary>
public static class OfficialUpdates
{
    /// <summary>The published team id as the wiki names it.</summary>
    public static string Team(string official) => official switch
    {
        "traveller" => TeamIds.Traveler,
        _ => official,
    };

    /// <summary>English text with no Polish translation yet, marked for review.</summary>
    public static string Untranslated(string english) => $"«{english}»";

    public static OfficialComparison Compare(GameData wiki, OfficialRelease release)
    {
        var roles = release.Roles
            .Select(official => wiki.FindRole(official.Id) is { } current
                ? new OfficialRoleUpdate { Official = official, Current = current, Fields = ChangedFields(current, official) }
                : new OfficialRoleUpdate { Official = official, Fields = NewFields(official) })
            .Where(u => u.Fields.Count > 0)
            .ToList();

        var known = wiki.AllRoles.Select(r => r.Id).Concat(release.Roles.Select(r => r.Id)).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nights = new[] { true, false }
            .Select(first => NightUpdate(wiki, release, first, known))
            .OfType<OfficialNightUpdate>()
            .ToList();

        return new OfficialComparison { Roles = roles, Jinxes = JinxUpdates(wiki, release), Nights = nights };
    }

    /// <summary>The wiki changes for the picked differences (roles, jinxes and night positions).</summary>
    public static LocalBackup Apply(GameData wiki, OfficialComparison comparison, OfficialSelection selection)
    {
        var roles = new Dictionary<string, Role>(StringComparer.OrdinalIgnoreCase);
        foreach (var update in comparison.Roles)
        {
            var role = update.Current ?? NewRole(wiki, update.Official);
            var changed = false;
            foreach (var field in update.Fields.Where(f => selection.IsTicked(update.Id, f.Key)))
            {
                role = ApplyField(role, field, selection.PolishFor(update.Id, field.Key));
                changed = true;
            }

            if (changed)
            {
                roles[role.Id] = role;
            }
        }

        foreach (var night in comparison.Nights.Where(n => selection.Nights.Contains(n.Key)))
        {
            Role? Find(string id) => roles.TryGetValue(id, out var role) ? role : wiki.FindRole(id);
            int Position(Role role) => night.FirstNight ? role.FirstNight : role.OtherNight;

            // Roles new here wake only when they are taken too.
            var order = night.New.Where(id => Find(id) is not null).ToList();
            var numbers = Renumber(order.Select(id => Position(Find(id)!)).ToList());
            var positions = order.Select((id, i) => (id, numbers[i])).Concat(night.Old.Except(order).Select(id => (id, 0)));
            foreach (var (id, position) in positions)
            {
                if (Find(id) is { } role && Position(role) != position)
                {
                    roles[id] = night.FirstNight ? role with { FirstNight = position } : role with { OtherNight = position };
                }
            }
        }

        var jinxes = comparison.Jinxes
            .Where(j => selection.Jinxes.ContainsKey(j.Key))
            .Select(j => new JinxOverride
            {
                OwnerId = j.OwnerId,
                Removed = j.IsRemoved,
                Jinx = new Jinx
                {
                    Id = j.OtherId,
                    ReasonEn = j.IsRemoved ? j.Old : j.New,
                    ReasonPl = j.IsRemoved ? j.Polish : Polish(selection.Jinxes[j.Key], j.Polish, j.New),
                },
            })
            .ToList();

        return new LocalBackup
        {
            Roles = roles.Values.Select(r => new LocalRole(r, selection.Icons.GetValueOrDefault(r.Id))).ToList(),
            Jinxes = jinxes,
        };
    }

    /// <summary>
    /// Positions for roles in waking order from their positions now (0: not waking). The longest run already in order
    /// keeps its numbers, the others go between their neighbours; when there is no room, the order is numbered anew.
    /// </summary>
    public static IReadOnlyList<int> Renumber(IReadOnlyList<int> current)
    {
        var keep = LongestIncreasing(current);
        var result = new int[current.Count];
        var previous = 0;
        var index = 0;
        while (index < current.Count)
        {
            if (keep.Contains(index))
            {
                previous = result[index] = current[index];
                index++;
                continue;
            }

            // A run of roles to place: between the previous kept position and the next one.
            var end = index;
            while (end < current.Count && !keep.Contains(end))
            {
                end++;
            }

            var count = end - index;
            var next = end < current.Count ? current[end] : previous + (10 * (count + 1));
            if (next - previous <= count)
            {
                return current.Select((_, i) => (i + 1) * 10).ToList();
            }

            for (var j = 1; j <= count; j++)
            {
                result[index + j - 1] = previous + ((next - previous) * j / (count + 1));
            }

            index = end;
        }

        return result;
    }

    private static Role NewRole(GameData wiki, OfficialRole official) => new()
    {
        Id = official.Id,
        NameEn = official.Name,
        NamePl = string.Empty,
        AbilityEn = string.Empty,
        AbilityPl = string.Empty,
        Team = Team(official.Team),
        Edition = wiki.FindScript(official.Edition) is null ? string.Empty : official.Edition,
        IsOfficial = true,
    };

    private static Role ApplyField(Role role, OfficialField field, string? polish) => field.Key switch
    {
        OfficialField.Name => role with { NameEn = field.New, NamePl = Polish(polish, field.Polish, field.New) },
        OfficialField.Ability => role with { AbilityEn = field.New, AbilityPl = Polish(polish, field.Polish, field.New) },
        OfficialField.FirstNightReminder => role with
        {
            FirstNightReminderEn = field.New,
            FirstNightReminderPl = Polish(polish, field.Polish, field.New),
        },
        OfficialField.OtherNightReminder => role with
        {
            OtherNightReminderEn = field.New,
            OtherNightReminderPl = Polish(polish, field.Polish, field.New),
        },
        OfficialField.Reminders => role with { RemindersEn = Lines(field.New), RemindersPl = PolishList(polish ?? field.Polish, field.New) },
        OfficialField.RemindersGlobal => role with
        {
            RemindersGlobalEn = Lines(field.New),
            RemindersGlobalPl = PolishList(polish ?? field.Polish, field.New),
        },
        OfficialField.Team => role with { Team = field.New },
        OfficialField.Setup => role with { Setup = field.New == Yes },
        OfficialField.Official => role with { IsOfficial = true },
        _ => role,
    };

    /// <summary>The admin's Polish text, else the role's own, else the English one marked for review; empty with no English.</summary>
    private static string Polish(string? typed, string? current, string english) =>
        english.Length == 0 ? string.Empty
        : !string.IsNullOrWhiteSpace(typed) ? typed.Trim()
        : !string.IsNullOrWhiteSpace(current) ? current
        : Untranslated(english);

    private static List<string> PolishList(string? polish, string english)
    {
        var typed = Lines(polish ?? string.Empty);
        return Lines(english).Select((item, i) => Polish(i < typed.Count ? typed[i] : null, null, item)).ToList();
    }

    private const string Yes = "tak";
    private const string No = "nie";

    private static List<OfficialField> NewFields(OfficialRole official)
    {
        var fields = new List<OfficialField>
        {
            Text(OfficialField.Name, "Nazwa", string.Empty, official.Name, string.Empty),
            Text(OfficialField.Ability, "Zdolność", string.Empty, official.Ability, string.Empty),
            new() { Key = OfficialField.Team, Label = "Drużyna", New = Team(official.Team) },
        };

        if (official.Setup)
        {
            fields.Add(new() { Key = OfficialField.Setup, Label = "Przygotowanie gry", Old = No, New = Yes });
        }

        if (official.FirstNightReminder.Length > 0)
        {
            fields.Add(Text(OfficialField.FirstNightReminder, "Tekst pierwszej nocy", string.Empty, official.FirstNightReminder, string.Empty));
        }

        if (official.OtherNightReminder.Length > 0)
        {
            fields.Add(Text(OfficialField.OtherNightReminder, "Tekst pozostałych nocy", string.Empty, official.OtherNightReminder, string.Empty));
        }

        if (official.Reminders.Count > 0)
        {
            fields.Add(List(OfficialField.Reminders, "Znaczniki", [], official.Reminders, []));
        }

        if (official.RemindersGlobal.Count > 0)
        {
            fields.Add(List(OfficialField.RemindersGlobal, "Znaczniki globalne", [], official.RemindersGlobal, []));
        }

        return fields;
    }

    private static List<OfficialField> ChangedFields(Role current, OfficialRole official)
    {
        var fields = new List<OfficialField>
        {
            Text(OfficialField.Name, "Nazwa", current.NameEn, official.Name, current.NamePl),
            Text(OfficialField.Ability, "Zdolność", current.AbilityEn, official.Ability, current.AbilityPl),
            Text(OfficialField.FirstNightReminder, "Tekst pierwszej nocy", current.FirstNightReminderEn, official.FirstNightReminder, current.FirstNightReminderPl),
            Text(OfficialField.OtherNightReminder, "Tekst pozostałych nocy", current.OtherNightReminderEn, official.OtherNightReminder, current.OtherNightReminderPl),
            List(OfficialField.Reminders, "Znaczniki", current.RemindersEn, official.Reminders, current.RemindersPl),
            List(OfficialField.RemindersGlobal, "Znaczniki globalne", current.RemindersGlobalEn, official.RemindersGlobal, current.RemindersGlobalPl),
            new() { Key = OfficialField.Team, Label = "Drużyna", Old = current.Team, New = Team(official.Team) },
            new() { Key = OfficialField.Setup, Label = "Przygotowanie gry", Old = current.Setup ? Yes : No, New = official.Setup ? Yes : No },
            new() { Key = OfficialField.Official, Label = "Oficjalna", Old = current.IsOfficial == true ? Yes : No, New = Yes },
        };

        return fields.Where(f => f.Old != f.New).ToList();
    }

    private static OfficialField Text(string key, string label, string old, string @new, string polish) =>
        new() { Key = key, Label = label, Old = old.Trim(), New = @new.Trim(), Polish = polish };

    /// <summary>A reminder list; the Polish names of tokens kept from before stay with them.</summary>
    private static OfficialField List(string key, string label, IReadOnlyList<string> old, IReadOnlyList<string> @new, IReadOnlyList<string> polish)
    {
        var translated = @new.Select(item =>
        {
            var index = old.ToList().FindIndex(o => string.Equals(o, item, StringComparison.OrdinalIgnoreCase));
            return index >= 0 && index < polish.Count ? polish[index] : string.Empty;
        });

        return new()
        {
            Key = key,
            Label = label,
            Old = string.Join('\n', old),
            New = string.Join('\n', @new),
            Polish = string.Join('\n', translated),
        };
    }

    private static List<string> Lines(string text) =>
        text.Split('\n', StringSplitOptions.TrimEntries).Where(line => line.Length > 0).ToList();

    private static List<OfficialJinxUpdate> JinxUpdates(GameData wiki, OfficialRelease release)
    {
        static string Pair(string a, string b) =>
            string.Compare(a, b, StringComparison.OrdinalIgnoreCase) <= 0 ? $"{a}/{b}".ToLowerInvariant() : $"{b}/{a}".ToLowerInvariant();

        var ours = wiki.Jinxes
            .SelectMany(o => o.Jinxes.Select(j => (Owner: o.Id, Jinx: j)))
            .GroupBy(x => Pair(x.Owner, x.Jinx.Id))
            .ToDictionary(g => g.Key, g => g.First());
        var official = release.Jinxes
            .SelectMany(o => o.Jinx.Select(j => (Owner: o.Id, Jinx: j)))
            .GroupBy(x => Pair(x.Owner, x.Jinx.Id))
            .ToDictionary(g => g.Key, g => g.First());

        var updates = new List<OfficialJinxUpdate>();
        foreach (var (pair, (owner, jinx)) in official)
        {
            if (!ours.TryGetValue(pair, out var mine))
            {
                updates.Add(new() { OwnerId = owner, OtherId = jinx.Id, New = jinx.Reason.Trim() });
            }
            else if (mine.Jinx.ReasonEn.Trim() != jinx.Reason.Trim())
            {
                updates.Add(new()
                {
                    OwnerId = mine.Owner,
                    OtherId = mine.Jinx.Id,
                    Old = mine.Jinx.ReasonEn.Trim(),
                    New = jinx.Reason.Trim(),
                    Polish = mine.Jinx.ReasonPl,
                });
            }
        }

        // A jinx the publisher dropped; jinxes with roles made here are the wiki's own.
        foreach (var (pair, (owner, jinx)) in ours.Where(x => !official.ContainsKey(x.Key)))
        {
            if (wiki.FindRole(owner)?.IsOfficial == true && wiki.FindRole(jinx.Id)?.IsOfficial == true)
            {
                updates.Add(new() { OwnerId = owner, OtherId = jinx.Id, Old = jinx.ReasonEn.Trim(), Polish = jinx.ReasonPl });
            }
        }

        return updates;
    }

    private static OfficialNightUpdate? NightUpdate(GameData wiki, OfficialRelease release, bool first, IReadOnlySet<string> known)
    {
        int Position(Role role) => first ? role.FirstNight : role.OtherNight;

        var old = wiki.AllRoles.Where(r => Position(r) > 0).OrderBy(Position).ThenBy(r => r.Id, StringComparer.Ordinal).Select(r => r.Id).ToList();
        var sheet = (first ? release.NightSheet.FirstNight : release.NightSheet.OtherNight).Where(known.Contains).ToList();

        // Roles made here keep their place: after the role they follow now.
        var unofficial = wiki.AllRoles.Where(r => r.IsOfficial != true).Select(r => r.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var merged = sheet.ToList();
        var last = -1;
        foreach (var id in old)
        {
            var at = merged.FindIndex(m => string.Equals(m, id, StringComparison.OrdinalIgnoreCase));
            if (at >= 0)
            {
                last = at;
            }
            else if (unofficial.Contains(id))
            {
                merged.Insert(++last, id);
            }
        }

        // Roles sharing a position may wake in any order among themselves: the published order fits when the same roles
        // wake and their positions do not go down along it.
        var positions = merged.Select(id => wiki.FindRole(id) is { } role ? Position(role) : 0).ToList();
        var fits = merged.ToHashSet(StringComparer.OrdinalIgnoreCase).SetEquals(old)
            && positions.Zip(positions.Skip(1)).All(p => p.First <= p.Second);
        return fits ? null : new() { FirstNight = first, Old = old, New = merged };
    }

    /// <summary>
    /// Indexes of the longest non-decreasing run of positive values (in order, not necessarily adjacent): roles sharing
    /// a position, as the travellers at dusk, keep it.
    /// </summary>
    private static HashSet<int> LongestIncreasing(IReadOnlyList<int> values)
    {
        var length = new int[values.Count];
        var previous = new int[values.Count];
        var best = -1;
        for (var i = 0; i < values.Count; i++)
        {
            previous[i] = -1;
            if (values[i] <= 0)
            {
                continue;
            }

            length[i] = 1;
            for (var j = 0; j < i; j++)
            {
                if (values[j] > 0 && values[j] <= values[i] && length[j] + 1 > length[i])
                {
                    length[i] = length[j] + 1;
                    previous[i] = j;
                }
            }

            if (best < 0 || length[i] > length[best])
            {
                best = i;
            }
        }

        var keep = new HashSet<int>();
        for (var i = best; i >= 0; i = previous[i])
        {
            keep.Add(i);
        }

        return keep;
    }
}
