namespace Grimoire_Studio.Services;

/// <summary>
/// Change proposals for the wiki. With the publishing service set up they go to the server, where every admin sees
/// them, and this browser keeps a copy of the ones it sent (to show how they were decided). Without it, and for the
/// admins not signed in, they stay in this browser.
/// </summary>
public sealed class Proposals(LocalStore store, RepositoryPublisher publisher) : LocalCollection<Proposal>(store, "grimoire-studio.proposals")
{
    protected override string IdOf(Proposal item) => item.Id;

    /// <summary>Raised after an admin decided, changed or removed a proposal here.</summary>
    public event Action? Changed;

    /// <summary>How long the server's list of pending proposals may miss a change (KV listings lag behind writes).</summary>
    private static readonly TimeSpan ListingDelay = TimeSpan.FromMinutes(2);

    /// <summary>Proposals sent, decided or removed here lately: whether each is pending now, and when that changed.</summary>
    private readonly Dictionary<string, (bool Pending, DateTimeOffset At)> _recent = [];

    /// <summary>Proposals go to the server (otherwise they stay in this browser).</summary>
    public bool OnServer => publisher.IsConfigured;

    /// <summary>Sends a proposal; returns it as stored.</summary>
    /// <exception cref="PublishException">The server refused it or is unreachable.</exception>
    /// <exception cref="IOException">The browser refused to store it.</exception>
    public async Task<Proposal> SubmitAsync(Proposal proposal)
    {
        var stored = OnServer ? await publisher.SubmitProposalAsync(proposal) : proposal;
        Remember(stored.Id, pending: true);
        if (!await SaveAsync(stored) && !OnServer)
        {
            throw new IOException("Przeglądarka nie pozwoliła zapisać zgłoszenia (tryb prywatny lub brak miejsca).");
        }

        return stored;
    }

    /// <summary>
    /// Updates this browser's copies of its proposals sent to the server that are not settled yet (waiting for a
    /// decision, or approved and not in the repository yet). Proposals the server no longer has keep the last state.
    /// </summary>
    public async Task RefreshMineAsync()
    {
        if (!OnServer)
        {
            return;
        }

        foreach (var mine in await GetAllAsync())
        {
            if (mine.Status == ProposalStatus.Rejected || mine.PublishedIn is not null)
            {
                continue;
            }

            try
            {
                if (await publisher.GetProposalAsync(mine.Id) is { } current)
                {
                    await SaveAsync(current);
                }
            }
            catch (PublishException)
            {
                // Offline: the copy stays as it was.
                return;
            }
        }
    }

    /// <summary>
    /// Proposals for the admin panel: those on the server (when signed in) and those only in this browser (sent
    /// before the server kept them, or added from files while signed out).
    /// </summary>
    /// <exception cref="PublishException">The server refused the list.</exception>
    public async Task<IReadOnlyList<Proposal>> GetForAdminAsync()
    {
        var local = await GetAllAsync();
        if (publisher.AdminName is null)
        {
            return local;
        }

        var server = await publisher.GetProposalsAsync();
        var ids = server.Select(p => p.Id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return server.Concat(local.Where(p => !ids.Contains(p.Id))).ToList();
    }

    /// <summary>An admin's change of a proposal: on the server when signed in, and in this browser's copy if it has one.</summary>
    /// <exception cref="PublishException">The server refused it.</exception>
    public async Task UpdateAsync(Proposal proposal)
    {
        Remember(proposal.Id, proposal.Status == ProposalStatus.Pending);
        if (publisher.AdminName is not null)
        {
            await publisher.SaveProposalAsync(proposal);
            if (await FindAsync(proposal.Id) is null)
            {
                Changed?.Invoke();
                return;
            }
        }

        await SaveAsync(proposal);
        Changed?.Invoke();
    }

    /// <exception cref="PublishException">The server refused it.</exception>
    public async Task DeleteAsync(string id)
    {
        Remember(id, pending: false);
        if (publisher.AdminName is not null)
        {
            await publisher.DeleteProposalAsync(id);
        }

        await RemoveAsync(id);
        Changed?.Invoke();
    }

    /// <summary>
    /// Number of proposals waiting for a decision: the server's list (<paramref name="serverIds"/>) with the changes
    /// made here that it may not show yet.
    /// </summary>
    public int PendingCount(IEnumerable<string> serverIds)
    {
        var pending = serverIds.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var since = DateTimeOffset.Now - ListingDelay;
        foreach (var (id, (isPending, at)) in _recent.Where(r => r.Value.At >= since))
        {
            if (isPending)
            {
                pending.Add(id);
            }
            else
            {
                pending.Remove(id);
            }
        }

        return pending.Count;
    }

    private void Remember(string id, bool pending) => _recent[id] = (pending, DateTimeOffset.Now);
}
