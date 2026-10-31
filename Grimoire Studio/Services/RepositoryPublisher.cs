using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using GrimoireStudio.Data;
using Microsoft.Extensions.Configuration;

namespace Grimoire_Studio.Services;

/// <summary>
/// Sends approved wiki changes to the repository through the publishing service (the Cloudflare Worker in worker/).
/// Admins sign in with their personal key; it is kept for this tab only (sessionStorage).
/// </summary>
public sealed class RepositoryPublisher(IConfiguration configuration, LocalStore store)
{
    /// <summary>Address of the publishing service in wwwroot/appsettings.json; empty means sending is not set up.</summary>
    public const string ConfigurationKey = "PublishApi";

    private const string KeyStorage = "grimoire-studio.admin-key";

    private static readonly HttpClient Http = new();

    private string? _key;
    private bool _loaded;

    public string ApiUrl { get; } = (configuration[ConfigurationKey] ?? string.Empty).TrimEnd('/');

    public bool IsConfigured => ApiUrl.Length > 0;

    /// <summary>Name of the signed-in admin, or null.</summary>
    public string? AdminName { get; private set; }

    /// <summary>Signs in again with the key kept for this tab, if any.</summary>
    public async Task LoadAsync()
    {
        if (_loaded || !IsConfigured)
        {
            return;
        }

        _loaded = true;
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
        await store.SetSessionAsync(KeyStorage, _key);
    }

    public async Task SignOutAsync()
    {
        _key = null;
        AdminName = null;
        await store.SetSessionAsync(KeyStorage, null);
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
            files = files.Select(f => new { path = $"data/{f.Path}", content = Convert.ToBase64String(f.Content) }),
        });
        return await SendAsync<PublishResult>(request);
    }

    private HttpRequestMessage Request(HttpMethod method, string path, string key)
    {
        var request = new HttpRequestMessage(method, $"{ApiUrl}/{path}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", key);
        return request;
    }

    private static async Task<T> SendAsync<T>(HttpRequestMessage request)
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
                return await response.Content.ReadFromJsonAsync<T>(JsonSerializerOptions.Web)
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

    private sealed record MeReply(string Name);

    private sealed record ErrorReply(string? Error, IReadOnlyList<string>? Conflicts);
}
