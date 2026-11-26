namespace GrimoireStudio.Data;

/// <summary>A published role that is new here or differs in some fields (<see cref="Fields"/>).</summary>
public sealed record OfficialRoleUpdate
{
    public required OfficialRole Official { get; init; }

    /// <summary>The role as the wiki has it; null for a role new here.</summary>
    public Role? Current { get; init; }

    public required IReadOnlyList<OfficialField> Fields { get; init; }

    public string Id => Official.Id;

    public bool IsNew => Current is null;
}
