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

    /// <summary>Keys of the items the admin turned down; the rest waits while the proposal is pending.</summary>
    public IReadOnlyList<string> RejectedKeys { get; init; } = [];

    /// <summary>Number of items approved (when only some were accepted).</summary>
    public int ApprovedCount { get; init; }

    /// <summary>Items still waiting for a decision (none once the whole proposal is decided).</summary>
    public IReadOnlySet<string> UndecidedKeys() => Status == ProposalStatus.Pending
        ? Changes.Keys().Except(ApprovedKeys).Except(RejectedKeys).ToHashSet()
        : new HashSet<string>();

    /// <summary>Some items are in the wiki and not sent to the repository yet.</summary>
    public bool IsUnsent => Status != ProposalStatus.Rejected && ApprovedCount > 0 && PublishedIn is null;

    /// <summary>An approved item taken back out of the wiki before sending: it waits for a decision again.</summary>
    public Proposal Reopen(string key)
    {
        var approved = ApprovedKeys.Where(k => k != key).ToList();
        return this with { ApprovedKeys = approved, ApprovedCount = approved.Count, Status = ProposalStatus.Pending };
    }

    /// <summary>
    /// The proposal after deciding <paramref name="keys"/>: approved or rejected. It stays pending while other items
    /// wait; <paramref name="before"/> (the items as the wiki had them) joins <see cref="Before"/>.
    /// </summary>
    public Proposal Decide(IReadOnlySet<string> keys, bool approve, LocalBackup before)
    {
        var approved = approve ? ApprovedKeys.Union(keys).ToList() : ApprovedKeys.ToList();
        var rejected = approve ? RejectedKeys.ToList() : RejectedKeys.Union(keys).ToList();
        var waiting = Changes.Keys().Except(approved).Except(rejected).Any();
        return this with
        {
            ApprovedKeys = approved,
            RejectedKeys = rejected,
            ApprovedCount = approved.Count,
            Status = waiting ? ProposalStatus.Pending : approved.Count > 0 ? ProposalStatus.Approved : ProposalStatus.Rejected,
            DecidedAt = DateTimeOffset.Now,
            // New approved items go out with the next commit.
            PublishedIn = approve ? null : PublishedIn,
            Before = Before is null ? before : before with
            {
                Roles = Before.Roles.Concat(before.Roles).DistinctBy(r => r.Role.Id).ToList(),
                Scripts = Before.Scripts.Concat(before.Scripts).DistinctBy(s => s.Id).ToList(),
                Teams = Before.Teams.Concat(before.Teams).DistinctBy(t => t.Id).ToList(),
                Jinxes = Before.Jinxes.Concat(before.Jinxes).DistinctBy(j => j.Key).ToList(),
            },
        };
    }

    /// <summary>
    /// The changed items as the wiki had them when the proposal was decided (new items are missing), so the history can
    /// still show the differences once the changes are in the data folder; null for proposals decided before it was kept.
    /// </summary>
    public LocalBackup? Before { get; init; }

    /// <summary>Data the history compares the changes with: <see cref="Before"/>, or <paramref name="fallback"/> without it.</summary>
    public GameData DecidedBase(GameData fallback) => Before?.ApplyTo(new GameData()) ?? fallback;
}
