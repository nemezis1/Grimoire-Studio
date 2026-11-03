namespace Grimoire_Studio.Services;

/// <summary>Items one commit changed, and the commit before it (null for the first one).</summary>
public sealed record CommitChanges(string? Parent, IReadOnlyList<CommitItem> Items);
