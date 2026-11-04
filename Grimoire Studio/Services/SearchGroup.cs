namespace Grimoire_Studio.Services;

/// <summary>One kind of search result; <see cref="MoreHref"/> lists every match on that kind's page.</summary>
public sealed record SearchGroup(string Label, IReadOnlyList<SearchHit> Hits, string? MoreHref);
