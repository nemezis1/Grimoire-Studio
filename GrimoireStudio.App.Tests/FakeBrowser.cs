using Microsoft.JSInterop;

namespace GrimoireStudio.App.Tests;

/// <summary>
/// Stands in for the browser behind <c>js/localStore.js</c>: IndexedDB, localStorage and sessionStorage kept in
/// dictionaries, so changes, proposals and the admin switches survive between calls as they would in a tab. Every other
/// call (Blazor's own, downloads) succeeds with the default result.
/// </summary>
public sealed class FakeBrowser : IJSRuntime, IJSObjectReference
{
    public Dictionary<string, string> Db { get; } = [];
    public Dictionary<string, string> Local { get; } = [];
    public Dictionary<string, string> Session { get; } = [];

    /// <summary>Names of the files the page offered for download.</summary>
    public List<string> Downloads { get; } = [];

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args) =>
        InvokeAsync<TValue>(identifier, CancellationToken.None, args);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
    {
        args ??= [];
        string Arg(int i) => args[i]?.ToString() ?? string.Empty;

        object? result = identifier switch
        {
            // Every module import gets this object, which then answers the module's functions.
            "import" => this,
            "dbGet" => Db.GetValueOrDefault(Arg(0)),
            "dbSet" => Set(Db, Arg(0), Arg(1)),
            "getItem" => Local.GetValueOrDefault(Arg(0)),
            "setItem" => Set(Local, Arg(0), Arg(1)),
            "removeItem" => Local.Remove(Arg(0)),
            "sessionGet" => Session.GetValueOrDefault(Arg(0)),
            "sessionSet" => args[1] is null ? Session.Remove(Arg(0)) : Set(Session, Arg(0), Arg(1)),
            "downloadText" or "downloadBytes" => Record(Arg(0)),
            _ => null,
        };

        return ValueTask.FromResult(result is TValue value ? value : default!);
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static bool Set(Dictionary<string, string> store, string key, string value)
    {
        store[key] = value;
        return true;
    }

    private bool Record(string fileName)
    {
        Downloads.Add(fileName);
        return true;
    }
}
