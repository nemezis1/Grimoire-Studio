using Bunit;
using Grimoire_Studio.Generator;
using Grimoire_Studio.Services;
using GrimoireStudio.Data;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.JSInterop;

namespace GrimoireStudio.App.Tests;

/// <summary>
/// A bUnit context with the app's services as in Program.cs, over the repository's data folder and a fake browser.
/// The publishing service is not configured, so proposals and wiki changes stay in the (fake) browser.
/// </summary>
public abstract class AppTestContext : BunitContext
{
    protected AppTestContext()
    {
        Services.AddSingleton<IJSRuntime>(Browser);
        Services.AddSingleton<IConfiguration>(new ConfigurationBuilder().Build());
        Services.AddScoped(_ => new HttpClient(new DataFolderHandler()) { BaseAddress = new Uri("http://localhost/") });
        Services.AddScoped<LocalStore>();
        Services.AddScoped<ChangeSets>();
        Services.AddScoped<Proposals>();
        Services.AddScoped<TeamText>();
        Services.AddScoped<GameDataClient>();
        Services.AddScoped<TokenRenderer>();
        Services.AddScoped<RepositoryPublisher>();
        Services.AddScoped<OutdatedCheck>();
    }

    protected FakeBrowser Browser { get; } = new();

    /// <summary>The data folder as the site serves it, with its teams handed to <see cref="TeamText"/>.</summary>
    protected GameData LoadData()
    {
        var data = GameData.Load(DataFolderHandler.DataPath);
        Services.GetRequiredService<TeamText>().Use(data.Teams);
        return data;
    }

    protected T Service<T>()
        where T : notnull => Services.GetRequiredService<T>();
}
