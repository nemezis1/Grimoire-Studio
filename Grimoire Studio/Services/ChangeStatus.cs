using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Where the changes of this browser stand, by item key (<see cref="LocalBackup.Keys"/>): only personal, proposed and
/// waiting for an admin, or in the wiki but not sent to the repository yet. Shown as badges on the lists.
/// </summary>
public sealed class ChangeStatus
{
    private readonly HashSet<string> _personal;
    private readonly HashSet<string> _pending;
    private readonly HashSet<string> _wiki;

    private ChangeStatus(IEnumerable<string> personal, IEnumerable<string> pending, IEnumerable<string> wiki)
    {
        _personal = Set(personal);
        _pending = Set(pending);
        _wiki = Set(wiki);
    }

    public static ChangeStatus Empty { get; } = new([], [], []);

    /// <summary>Reads both change sets (whatever the editors save to now) and the proposals waiting for a decision.</summary>
    public static async Task<ChangeStatus> LoadAsync(GameDataClient client, ChangeSets changes, Proposals proposals)
    {
        await client.GetAsync(); // drops saved-but-unchanged items first

        return new(
            (await changes.Personal.GetAllAsync()).Keys(),
            (await proposals.GetAllAsync()).Where(p => p.Status == ProposalStatus.Pending).SelectMany(p => p.Changes.Keys()),
            (await changes.Wiki.GetAllAsync()).Keys());
    }

    /// <summary>The furthest stage of the item: in the wiki, then proposed, then only personal.</summary>
    public ChangeStage Of(string key) =>
        _wiki.Contains(key) ? ChangeStage.Wiki :
        _pending.Contains(key) ? ChangeStage.Pending :
        _personal.Contains(key) ? ChangeStage.Personal :
        ChangeStage.None;

    /// <summary>Marks an item just saved (e.g. imported) without reading everything again.</summary>
    public void Add(string key, bool wiki) => (wiki ? _wiki : _personal).Add(key);

    private static HashSet<string> Set(IEnumerable<string> keys) => keys.ToHashSet(StringComparer.OrdinalIgnoreCase);
}
