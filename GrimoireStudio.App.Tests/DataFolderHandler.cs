using System.Net;
using GrimoireStudio.Data;

namespace GrimoireStudio.App.Tests;

/// <summary>Serves the repository's data folder at <c>data/</c>, as the site does; the data version is fixed.</summary>
public sealed class DataFolderHandler : HttpMessageHandler
{
    public const string Version = "test-version";

    public static string DataPath { get; } = FindDataFolder();

    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var path = request.RequestUri!.AbsolutePath.TrimStart('/');
        HttpResponseMessage response;
        if (path == $"data/{GameData.VersionFile}")
        {
            response = new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(Version) };
        }
        else if (path.StartsWith("data/", StringComparison.Ordinal) && File.Exists(Path.Combine(DataPath, path[5..])))
        {
            response = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new ByteArrayContent(File.ReadAllBytes(Path.Combine(DataPath, path[5..]))),
            };
            response.Content.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
        }
        else
        {
            response = new HttpResponseMessage(HttpStatusCode.NotFound);
        }

        return Task.FromResult(response);
    }

    private static string FindDataFolder()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = Path.Combine(dir.FullName, "data");
            if (File.Exists(Path.Combine(candidate, GameData.RolesFile)))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Repository data folder not found.");
    }
}
