using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

public class OfficialUpdatesTests
{
    private static readonly GameData Data = GameData.Load(DataFolder.Path);

    /// <summary>The repository's data as if published, so a comparison finds only what a test changes.</summary>
    private static OfficialRelease Published(Func<OfficialRole, OfficialRole>? change = null, params OfficialRole[] added)
    {
        var roles = Data.AllRoles.Select(r => new OfficialRole
        {
            Id = r.Id,
            Name = r.NameEn,
            Team = r.Team == TeamIds.Traveler ? "traveller" : r.Team,
            Ability = r.AbilityEn,
            FirstNightReminder = r.FirstNightReminderEn,
            OtherNightReminder = r.OtherNightReminderEn,
            Reminders = r.RemindersEn,
            RemindersGlobal = r.RemindersGlobalEn,
            Setup = r.Setup,
        });

        return new OfficialRelease
        {
            Roles = roles.Select(change ?? (r => r)).Concat(added).ToList(),
            Jinxes = Data.Jinxes.Select(o => new OfficialJinxes { Id = o.Id, Jinx = o.Jinxes.Select(j => new OfficialJinx { Id = j.Id, Reason = j.ReasonEn }).ToList() }).ToList(),
            NightSheet = new OfficialNightSheet
            {
                FirstNight = Order(r => r.FirstNight),
                OtherNight = Order(r => r.OtherNight),
            },
        };
    }

    private static List<string> Order(Func<Role, int> position) =>
        Data.AllRoles.Where(r => position(r) > 0).OrderBy(position).ThenBy(r => r.Id, StringComparer.Ordinal).Select(r => r.Id).ToList();

    private static OfficialSelection Tick(OfficialComparison comparison)
    {
        var selection = new OfficialSelection();
        foreach (var update in comparison.Roles)
        {
            selection.RoleFields[update.Id] = update.Fields.Select(f => f.Key).ToHashSet();
        }

        foreach (var jinx in comparison.Jinxes)
        {
            selection.Jinxes[jinx.Key] = string.Empty;
        }

        selection.Nights.UnionWith(comparison.Nights.Select(n => n.Key));
        return selection;
    }

    [Fact]
    public void Compare_Should_FindNothing_When_PublishedDataMatches()
    {
        var comparison = OfficialUpdates.Compare(Data, Published());

        Assert.Equal(0, comparison.Count);
    }

    [Fact]
    public void Compare_Should_ListAChangedAbility_WithTheCurrentPolishText()
    {
        var comparison = OfficialUpdates.Compare(Data, Published(r => r.Id == "chef" ? r with { Ability = "New chef text." } : r));

        var update = Assert.Single(comparison.Roles);
        var field = Assert.Single(update.Fields);
        Assert.Equal(OfficialField.Ability, field.Key);
        Assert.Equal("New chef text.", field.New);
        Assert.Equal(Data.FindRole("chef")!.AbilityPl, field.Polish);
        Assert.Empty(comparison.Nights);
    }

    [Fact]
    public void Apply_Should_UseTheTypedPolishText_OrKeepTheRolesOwn()
    {
        var comparison = OfficialUpdates.Compare(Data, Published(r => r.Id switch
        {
            "chef" => r with { Ability = "New chef text." },
            "empath" => r with { Ability = "New empath text." },
            _ => r,
        }));
        var selection = Tick(comparison);
        selection.RolePolish["chef"] = new() { [OfficialField.Ability] = "Nowy tekst Kucharza." };

        var changes = OfficialUpdates.Apply(Data, comparison, selection);

        var chef = changes.Roles.Single(r => r.Role.Id == "chef").Role;
        Assert.Equal(("New chef text.", "Nowy tekst Kucharza."), (chef.AbilityEn, chef.AbilityPl));
        var empath = changes.Roles.Single(r => r.Role.Id == "empath").Role;
        Assert.Equal(Data.FindRole("empath")!.AbilityPl, empath.AbilityPl);
    }

    [Fact]
    public void Apply_Should_AddANewRole_WithUntranslatedTextMarked_AndPlaceItInTheNight()
    {
        var banshee2 = new OfficialRole
        {
            Id = "newcomer",
            Name = "Newcomer",
            Team = "townsfolk",
            Ability = "You start knowing something.",
            FirstNightReminder = "Show something.",
            Reminders = ["Known"],
        };
        var release = Published(added: banshee2);
        var chefAt = release.NightSheet.FirstNight.ToList().IndexOf("chef");
        release = release with
        {
            NightSheet = release.NightSheet with { FirstNight = [.. release.NightSheet.FirstNight.Take(chefAt + 1), "newcomer", .. release.NightSheet.FirstNight.Skip(chefAt + 1)] },
        };

        var comparison = OfficialUpdates.Compare(Data, release);
        Assert.True(Assert.Single(comparison.Roles).IsNew);
        var night = Assert.Single(comparison.Nights);
        Assert.Equal(["newcomer"], night.Added);

        var selection = Tick(comparison);
        selection.RolePolish["newcomer"] = new() { [OfficialField.Name] = "Nowicjusz" };
        var changes = OfficialUpdates.Apply(Data, comparison, selection);

        var role = changes.Roles.Single(r => r.Role.Id == "newcomer").Role;
        Assert.Equal("Nowicjusz", role.NamePl);
        Assert.Equal("«You start knowing something.»", role.AbilityPl);
        Assert.Equal(["«Known»"], role.RemindersPl);
        Assert.True(role.IsOfficial);

        var chef = Data.FindRole("chef")!.FirstNight;
        var next = Data.AllRoles.Where(r => r.FirstNight > chef).Min(r => r.FirstNight);
        Assert.InRange(role.FirstNight, chef + 1, next - 1);
    }

    [Fact]
    public void Apply_Should_LeaveTheNewRoleOutOfTheNight_When_OnlyTheOrderIsTaken()
    {
        var release = Published(added: new OfficialRole { Id = "newcomer", Name = "Newcomer", Team = "townsfolk", Ability = "X." });
        release = release with { NightSheet = release.NightSheet with { FirstNight = ["newcomer", .. release.NightSheet.FirstNight] } };
        var comparison = OfficialUpdates.Compare(Data, release);
        var selection = new OfficialSelection();
        selection.Nights.Add("night:first");

        var changes = OfficialUpdates.Apply(Data, comparison, selection);

        Assert.Empty(changes.Roles);
    }

    [Fact]
    public void Compare_Should_FindChangedAddedAndRemovedJinxes()
    {
        var owner = Data.Jinxes.First(o => o.Jinxes.Count > 1);
        var release = Published();
        var first = owner.Jinxes[0];
        var dropped = owner.Jinxes[1];
        release = release with
        {
            Jinxes = release.Jinxes
                .Select(o => o.Id != owner.Id ? o : o with
                {
                    Jinx = o.Jinx.Where(j => j.Id != dropped.Id).Select(j => j.Id == first.Id ? j with { Reason = "Changed." } : j).ToList(),
                })
                .Append(new OfficialJinxes { Id = "chef", Jinx = [new OfficialJinx { Id = "imp", Reason = "Brand new." }] })
                .ToList(),
        };

        var jinxes = OfficialUpdates.Compare(Data, release).Jinxes;

        var changed = jinxes.Single(j => j.OtherId == first.Id && j.OwnerId == owner.Id);
        Assert.Equal(("Changed.", first.ReasonPl), (changed.New, changed.Polish));
        Assert.Contains(jinxes, j => j.IsNew && j.OwnerId == "chef" && j.OtherId == "imp");
        Assert.Contains(jinxes, j => j.IsRemoved && j.OtherId == dropped.Id);
    }

    [Fact]
    public void IconUrls_Should_TryTheTeamsLookFirst_InTheEditionFolder()
    {
        var imp = OfficialRelease.IconUrls(new OfficialRole { Id = "imp", Team = "demon", Edition = "tb" });
        Assert.Equal("https://raw.githubusercontent.com/ThePandemoniumInstitute/botc-release/main/resources/characters/tb/imp_e.webp", imp[0]);

        Assert.EndsWith("/carousel/steward_g.webp", OfficialRelease.IconUrls(new OfficialRole { Id = "steward", Team = "townsfolk", Edition = "carousel" })[0]);
        Assert.EndsWith("/fabled/angel.webp", OfficialRelease.IconUrls(new OfficialRole { Id = "angel", Team = "fabled", Edition = "fabled" })[0]);
        Assert.EndsWith("/tb/thief.webp", OfficialRelease.IconUrls(new OfficialRole { Id = "thief", Team = "traveller", Edition = "tb" })[0]);
    }

    [Fact]
    public void Renumber_Should_KeepPositionsInOrder_AndPlaceTheOthersBetween()
    {
        Assert.Equal([10, 15, 20, 30], OfficialUpdates.Renumber([10, 0, 20, 30]));
        Assert.Equal([10, 20, 40, 50], OfficialUpdates.Renumber([10, 20, 40, 30]));
        Assert.Equal([10, 10, 15, 20], OfficialUpdates.Renumber([10, 10, 0, 20]));
        Assert.Equal([10, 20, 30], OfficialUpdates.Renumber([1, 2, 0, 3]).Take(3));
    }
}
