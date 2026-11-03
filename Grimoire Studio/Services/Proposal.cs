using GrimoireStudio.Data;

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

    /// <summary>Commit that took the approved changes to the repository; null until they are sent.</summary>
    public string? PublishedIn { get; init; }

    /// <summary>Keys (<see cref="LocalBackup.Keys"/>) of the items the admin accepted.</summary>
    public IReadOnlyList<string> ApprovedKeys { get; init; } = [];

    /// <summary>Number of items approved (when only some were accepted).</summary>
    public int ApprovedCount { get; init; }

    /// <summary>
    /// The changed items as the wiki had them when the proposal was decided (new items are missing), so the history can
    /// still show the differences once the changes are in the data folder; null for proposals decided before it was kept.
    /// </summary>
    public LocalBackup? Before { get; init; }

    /// <summary>Data the history compares the changes with: <see cref="Before"/>, or <paramref name="fallback"/> without it.</summary>
    public GameData DecidedBase(GameData fallback) => Before?.ApplyTo(new GameData()) ?? fallback;
}
