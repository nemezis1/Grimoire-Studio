namespace GrimoireStudio.Data;

/// <summary>A jinx added, changed or removed in the published data, compared with the wiki's.</summary>
public sealed record OfficialJinxUpdate
{
    /// <summary>The role the wiki stores the jinx under (the published owner for a new jinx).</summary>
    public required string OwnerId { get; init; }

    public required string OtherId { get; init; }

    /// <summary>The wiki's English reason; empty for a new jinx.</summary>
    public string Old { get; init; } = string.Empty;

    /// <summary>The published reason; empty for a removed jinx.</summary>
    public string New { get; init; } = string.Empty;

    /// <summary>The wiki's Polish reason; empty for a new jinx.</summary>
    public string Polish { get; init; } = string.Empty;

    public bool IsNew => Old.Length == 0;

    public bool IsRemoved => New.Length == 0;

    /// <summary>The pair in either order, as <see cref="JinxOverride.Key"/> would name it.</summary>
    public string Key => $"{OwnerId}/{OtherId}".ToLowerInvariant();
}
