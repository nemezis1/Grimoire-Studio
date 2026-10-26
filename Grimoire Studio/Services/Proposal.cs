namespace Grimoire_Studio.Services;

/// <summary>Changes sent for the wiki, waiting for the admin's decision (or already decided).</summary>
public sealed record Proposal
{
    public required string Id { get; init; }
    public required string Author { get; init; }
    public string Comment { get; init; } = string.Empty;
    public DateTimeOffset CreatedAt { get; init; }
    public required LocalBackup Changes { get; init; }

    public ProposalStatus Status { get; init; } = ProposalStatus.Pending;
    public DateTimeOffset? DecidedAt { get; init; }

    /// <summary>Keys (<see cref="LocalBackup.Keys"/>) of the items the admin accepted.</summary>
    public IReadOnlyList<string> ApprovedKeys { get; init; } = [];

    /// <summary>Number of items approved (when only some were accepted).</summary>
    public int ApprovedCount { get; init; }
}
