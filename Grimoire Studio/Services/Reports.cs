namespace Grimoire_Studio.Services;

/// <summary>
/// Bug reports and ideas. With the publishing service set up they go to the server, where every admin sees and answers
/// them, and this browser keeps a copy of the ones it sent (to show their status and the reply). Without it they stay in
/// this browser.
/// </summary>
public sealed class Reports(LocalStore store, RepositoryPublisher publisher) : LocalCollection<Report>(store, "grimoire-studio.reports")
{
    protected override string IdOf(Report item) => item.Id;

    /// <summary>Raised after an admin answered or removed a report here.</summary>
    public event Action? Changed;

    public bool OnServer => publisher.IsConfigured;

    /// <summary>Sends a report; returns it as stored.</summary>
    /// <exception cref="PublishException">The server refused it or is unreachable.</exception>
    /// <exception cref="IOException">The browser refused to store it.</exception>
    public async Task<Report> SubmitAsync(Report report, string? website = null)
    {
        var stored = OnServer
            ? await publisher.SubmitReportAsync(report, website)
            : report with { Id = Guid.NewGuid().ToString("N"), CreatedAt = DateTimeOffset.Now, Status = Report.New, Reply = string.Empty };
        if (!await SaveAsync(stored) && !OnServer)
        {
            throw new IOException("Przeglądarka nie pozwoliła zapisać zgłoszenia (tryb prywatny lub brak miejsca).");
        }

        return stored;
    }

    /// <summary>Updates this browser's copies of its reports from the server; offline, they keep the last state.</summary>
    public async Task RefreshMineAsync()
    {
        if (!OnServer)
        {
            return;
        }

        foreach (var mine in await GetAllAsync())
        {
            try
            {
                if (await publisher.GetReportAsync(mine.Id) is { } current)
                {
                    await SaveAsync(current);
                }
            }
            catch (PublishException)
            {
                return;
            }
        }
    }

    /// <summary>Reports for the admin panel: the server's when signed in, otherwise those in this browser.</summary>
    /// <exception cref="PublishException">The server refused the list.</exception>
    public async Task<IReadOnlyList<Report>> GetForAdminAsync() =>
        publisher.AdminName is null ? await GetAllAsync() : await publisher.GetReportsAsync();

    /// <summary>An admin's answer: on the server when signed in, and in this browser's copy if it has one.</summary>
    /// <exception cref="PublishException">The server refused it.</exception>
    public async Task<Report> AnswerAsync(Report report, string status, string reply)
    {
        var answered = publisher.AdminName is not null
            ? await publisher.AnswerReportAsync(report.Id, status, reply)
            : report with { Status = status, Reply = reply.Trim(), UpdatedAt = DateTimeOffset.Now };
        if (publisher.AdminName is null || await FindAsync(report.Id) is not null)
        {
            await SaveAsync(answered);
        }

        Changed?.Invoke();
        return answered;
    }

    /// <exception cref="PublishException">The server refused it.</exception>
    public async Task DeleteAsync(string id)
    {
        if (publisher.AdminName is not null)
        {
            await publisher.DeleteReportAsync(id);
        }

        await RemoveAsync(id);
        Changed?.Invoke();
    }

    /// <summary>Number of reports no admin has looked at yet (0 when not signed in or offline).</summary>
    public async Task<int> OpenCountAsync()
    {
        if (publisher.AdminName is null)
        {
            return (await GetAllAsync()).Count(r => r.Status == Report.New);
        }

        try
        {
            return await publisher.GetOpenReportsAsync();
        }
        catch (PublishException)
        {
            return 0;
        }
    }
}
