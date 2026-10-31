namespace Grimoire_Studio.Services;

/// <summary>An admin added by an owner in the admin panel.</summary>
public sealed record AdminEntry(string Name, DateTimeOffset AddedAt, string AddedBy);
