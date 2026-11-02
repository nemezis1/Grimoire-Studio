namespace Grimoire_Studio.Services;

/// <summary>Changes of one item, newest first; <see cref="Complete"/> is false when older commits were not looked at.</summary>
public sealed record ItemHistory(IReadOnlyList<ItemChange> Entries, bool Complete);
