namespace Grimoire_Studio.Services;

/// <summary>Items one commit changed.</summary>
public sealed record CommitChanges(IReadOnlyList<CommitItem> Items);
