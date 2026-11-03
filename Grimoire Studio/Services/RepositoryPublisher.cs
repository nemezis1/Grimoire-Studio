using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GrimoireStudio.Data;
using Microsoft.Extensions.Configuration;

namespace Grimoire_Studio.Services;

/// <summary>
/// Sends approved wiki changes to the repository through the publishing service (the Cloudflare Worker in worker/),
/// and proposals to the admins. Admins sign in with their personal key; it is kept for this tab only (sessionStorage).
/// </summary>
public sealed class RepositoryPublisher(IConfiguration configuration, LocalStore store, HttpClient site)
{
    /// <summary>Address of the publishing service in wwwroot/appsettings.json; empty means sending is not set up.</summary>
    public const string ConfigurationKey = "PublishApi";

    private const string KeyStorage = "grimoire-studio.admin-key";

    private static readonly HttpClient Http = new();

    private string? _key;
    private Task? _load;

    public string ApiUrl { get; } = (configuration[ConfigurationKey] ?? string.Empty).TrimEnd('/');

    public bool IsConfigured => ApiUrl.Length > 0;

    /// <summary>Name of the signed-in admin, or null.</summary>
    public string? AdminName { get; private set; }

    /// <summary>The signed-in admin is an owner (in the Worker's ADMIN_KEYS secret) and manages the other admins.</summary>
    public bool IsOwner { get; private set; }

    /// <summary>Raised after signing in or out.</summary>
    public event Action? SignedInChanged;

    /// <summary>
    /// Signs in again with the key kept for this tab, if any. Every caller waits for the same sign-in, so components
    /// rendered together all see the result.
    /// </summary>
    public Task LoadAsync() => _load ??= IsConfigured ? LoadCoreAsync() : Task.CompletedTask;

    private async Task LoadCoreAsync()
    {
        var key = await store.GetSessionAsync(KeyStorage);
        if (key is not null)
        {
            try
            {
                await SignInAsync(key);
            }
            catch (PublishException)
            {
                await SignOutAsync();
            }
        }
    }

    /// <exception cref="PublishException">The key is wrong or the service is unreachable.</exception>
    public async Task SignInAsync(string key)
    {
        using var request = Request(HttpMethod.Get, "me", key.Trim());
        var reply = await SendAsync<MeReply>(request);
        _key = key.Trim();
        AdminName = reply.Name;
        IsOwner = reply.Owner;
        await store.SetSessionAsync(KeyStorage, _key);
        SignedInChanged?.Invoke();
    }

    public async Task SignOutAsync()
    {
        _key = null;
        AdminName = null;
        IsOwner = false;
        await store.SetSessionAsync(KeyStorage, null);
        SignedInChanged?.Invoke();
    }

    /// <summary>Commits <paramref name="files"/> (paths relative to the data folder) to the repository.</summary>
    /// <exception cref="PublishException">Not signed in, a conflict, or the service refused the commit.</exception>
    public async Task<PublishResult> PublishAsync(string message, string baseVersion, IReadOnlyList<DataFile> files)
    {
        if (_key is null)
        {
            throw new PublishException("Najpierw zaloguj się kluczem administratora.");
        }

        using var request = Request(HttpMethod.Post, "commit", _key);
        request.Content = JsonContent.Create(new
        {
            message,
            baseVersion,
            files = files.Select(f => new { path = $"data/{f.Path}", content = Convert.ToBase64String(f.Content), delete = f.Deleted }),
        });
        return await SendAsync<PublishResult>(request);
    }

    /// <summary>The latest deployments and the data version this site serves now (not the one this tab loaded).</summary>
    /// <exception cref="PublishException">Not signed in, or the service is unreachable.</exception>
    public async Task<DeploymentStatus> GetDeploymentsAsync()
    {
        if (_key is null)
        {
            throw new PublishException("Najpierw zaloguj się kluczem administratora.");
        }

        using var request = Request(HttpMethod.Get, "status", _key);
        var reply = await SendAsync<StatusReply>(request);
        return new DeploymentStatus(reply.Runs, await GetLiveVersionAsync());
    }

    private async Task<string> GetLiveVersionAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"data/{GameData.VersionFile}");
            request.Headers.CacheControl = new CacheControlHeaderValue { NoCache = true };
            using var response = await site.SendAsync(request);
            return response.IsSuccessStatusCode ? (await response.Content.ReadAsStringAsync()).Trim() : string.Empty;
        }
        catch (HttpRequestException)
        {
            return string.Empty;
        }
    }

    /// <summary>Admins added by the owners (owners themselves are not listed). Owner only.</summary>
    /// <exception cref="PublishException">Not an owner, or the service refused.</exception>
    public async Task<IReadOnlyList<AdminEntry>> GetAdminsAsync()
    {
        using var request = Request(HttpMethod.Get, "admins", RequireKey());
        return (await SendAsync<AdminsReply>(request)).Admins;
    }

    /// <summary>Adds an admin; returns their new key, shown only this once.</summary>
    /// <exception cref="PublishException">Not an owner, the name is taken, or the service refused.</exception>
    public async Task<string> AddAdminAsync(string name)
    {
        using var request = Request(HttpMethod.Post, "admins", RequireKey());
        request.Content = JsonContent.Create(new { name });
        return (await SendAsync<NewAdminReply>(request)).Key;
    }

    /// <exception cref="PublishException">Not an owner, or no such admin.</exception>
    public async Task RemoveAdminAsync(string name)
    {
        using var request = Request(HttpMethod.Delete, $"admins/{Uri.EscapeDataString(name)}", RequireKey());
        await SendAsync<JsonElement>(request);
    }

    /// <summary>Wiki changes sent by admins, newest first, read from the repository history.</summary>
    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task<IReadOnlyList<WikiCommit>> GetChangesAsync()
    {
        using var request = Request(HttpMethod.Get, "changes", RequireKey());
        return (await SendAsync<ChangesReply>(request)).Changes;
    }

    /// <summary>Commits that changed one item (kind "role", "script" or "jinx"; <paramref name="file"/> picks roles/fabled).</summary>
    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task<ItemHistory> GetItemHistoryAsync(string kind, string id, string? file = null)
    {
        var query = $"item-history?kind={Uri.EscapeDataString(kind)}&id={Uri.EscapeDataString(id)}"
            + (file is null ? null : $"&file={Uri.EscapeDataString(file)}");
        using var request = Request(HttpMethod.Get, query, RequireKey());
        return await SendAsync<ItemHistory>(request);
    }

    /// <summary>Roles, scripts, teams and jinxes one commit changed.</summary>
    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task<CommitChanges> GetCommitChangesAsync(string sha)
    {
        using var request = Request(HttpMethod.Get, $"commit-changes?sha={Uri.EscapeDataString(sha)}", RequireKey());
        return await SendAsync<CommitChanges>(request);
    }

    private string RequireKey() => _key ?? throw new PublishException("Najpierw zaloguj się kluczem administratora.");

    /// <summary>Sends a proposal for the admins (anyone may); returns it as stored, with the id it is read back by.</summary>
    /// <exception cref="PublishException">The service refused it (too many proposals, too big) or is unreachable.</exception>
    public async Task<Proposal> SubmitProposalAsync(Proposal proposal)
    {
        using var request = Request(HttpMethod.Post, "proposals", null);
        request.Content = JsonContent.Create(
            new { proposal.Author, proposal.Comment, proposal.Changes }, options: GameData.JsonOptions);
        return await SendAsync<Proposal>(request, GameData.JsonOptions);
    }

    /// <summary>Number of proposals waiting for a decision.</summary>
    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task<int> GetPendingCountAsync()
    {
        using var request = Request(HttpMethod.Get, "proposals?pending", RequireKey());
        return (await SendAsync<PendingReply>(request)).Pending;
    }

    /// <summary>A proposal as the admins left it; null when it is gone (removed, or expired after the decision).</summary>
    /// <exception cref="PublishException">The service is unreachable.</exception>
    public async Task<Proposal?> GetProposalAsync(string id)
    {
        using var request = Request(HttpMethod.Get, $"proposals/{Uri.EscapeDataString(id)}", null);
        try
        {
            return await SendAsync<Proposal>(request, GameData.JsonOptions);
        }
        catch (PublishException ex) when (ex.Status == System.Net.HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    /// <summary>Proposals on the server; ones this version of the app cannot read are left out.</summary>
    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task<IReadOnlyList<Proposal>> GetProposalsAsync()
    {
        using var request = Request(HttpMethod.Get, "proposals", RequireKey());
        var reply = await SendAsync<ProposalsReply>(request);
        var proposals = new List<Proposal>();
        foreach (var item in reply.Proposals)
        {
            try
            {
                if (item.Deserialize<Proposal>(GameData.JsonOptions) is { } proposal)
                {
                    proposals.Add(proposal);
                }
            }
            catch (JsonException)
            {
                // Sent by hand or by another version: not shown rather than hiding all the others.
            }
        }

        return proposals;
    }

    /// <summary>Stores an admin's version of a proposal on the server (a decision, or one added from a file).</summary>
    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task SaveProposalAsync(Proposal proposal)
    {
        using var request = Request(HttpMethod.Put, $"proposals/{Uri.EscapeDataString(proposal.Id)}", RequireKey());
        request.Content = JsonContent.Create(proposal, options: GameData.JsonOptions);
        await SendAsync<JsonElement>(request);
    }

    /// <exception cref="PublishException">Not signed in, or the service refused.</exception>
    public async Task DeleteProposalAsync(string id)
    {
        using var request = Request(HttpMethod.Delete, $"proposals/{Uri.EscapeDataString(id)}", RequireKey());
        await SendAsync<JsonElement>(request);
    }

    /// <summary>
    /// The data folder as the repository has it at <paramref name="version"/> (a commit), or at the head of the branch
    /// without it; <see cref="GameData.Version"/> is the commit. Files missing there are taken from <paramref name="fallback"/>.
    /// </summary>
    /// <exception cref="PublishException">Not signed in, an unknown version, or the service refused.</exception>
    public async Task<GameData> GetDataAsync(GameData fallback, string? version = null)
    {
        var path = version is null ? "data" : $"data?ref={Uri.EscapeDataString(version)}";
        using var request = Request(HttpMethod.Get, path, RequireKey());
        var reply = await SendAsync<DataReply>(request);

        T? Read<T>(string file)
            where T : class =>
            reply.Files.GetValueOrDefault(file) is { } text ? JsonSerializer.Deserialize<T>(text, GameData.JsonOptions) : null;

        try
        {
            return new GameData
            {
                Roles = Read<List<Role>>(GameData.RolesFile) ?? fallback.Roles,
                Fabled = Read<List<Role>>(GameData.FabledFile) ?? fallback.Fabled,
                Scripts = Read<ScriptCatalog>(GameData.ScriptsFile) ?? fallback.Scripts,
                Jinxes = Read<List<RoleJinxes>>(GameData.JinxesFile) ?? fallback.Jinxes,
                Teams = Read<List<TeamInfo>>(GameData.TeamsFile) ?? fallback.Teams,
                Version = reply.Sha,
            };
        }
        catch (JsonException)
        {
            throw new PublishException("Pliki danych w repozytorium mają nieznany format.");
        }
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string? key)
    {
        var request = new HttpRequestMessage(method, $"{ApiUrl}/{path}");
        if (key is not null)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        }

        return request;
    }

    private static async Task<T> SendAsync<T>(HttpRequestMessage request, JsonSerializerOptions? options = null)
    {
        HttpResponseMessage response;
        try
        {
            response = await Http.SendAsync(request);
        }
        catch (HttpRequestException)
        {
            throw new PublishException("Nie udało się połączyć z usługą zapisu.");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                return await response.Content.ReadFromJsonAsync<T>(options ?? JsonSerializerOptions.Web)
                    ?? throw new PublishException("Usługa zapisu zwróciła pustą odpowiedź.");
            }

            ErrorReply? error = null;
            try
            {
                error = await response.Content.ReadFromJsonAsync<ErrorReply>(JsonSerializerOptions.Web);
            }
            catch (JsonException)
            {
                // Not one of the service's replies (e.g. a proxy error page).
            }

            var message = error?.Error ?? $"Usługa zapisu odpowiedziała błędem {(int)response.StatusCode}.";
            if (error?.Conflicts is { Count: > 0 } conflicts)
            {
                message += " Zmienione pliki: " + string.Join(", ", conflicts) + ".";
            }

            throw new PublishException(message, response.StatusCode);
        }
    }

    private sealed record MeReply(string Name, bool Owner);

    private sealed record ChangesReply(IReadOnlyList<WikiCommit> Changes);

    private sealed record AdminsReply(IReadOnlyList<AdminEntry> Admins);

    private sealed record NewAdminReply(string Name, string Key);

    private sealed record StatusReply(IReadOnlyList<DeploymentRun> Runs);

    private sealed record ErrorReply(string? Error, IReadOnlyList<string>? Conflicts);

    private sealed record PendingReply(int Pending);

    private sealed record ProposalsReply(IReadOnlyList<JsonElement> Proposals);

    private sealed record DataReply(string Sha, IReadOnlyDictionary<string, string?> Files);
}
