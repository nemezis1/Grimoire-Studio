namespace Grimoire_Studio.Services;

/// <summary>Change proposals for the wiki, kept in this browser until the wiki moves to GitHub.</summary>
public sealed class Proposals(LocalStore store) : LocalCollection<Proposal>(store, "grimoire-studio.proposals")
{
    protected override string IdOf(Proposal item) => item.Id;
}
