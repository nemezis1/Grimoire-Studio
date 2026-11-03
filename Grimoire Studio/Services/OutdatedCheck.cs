using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Whether changes made on an older data version touch items that changed since: compares the items at the version
/// the changes were made on (read from the repository) with the current data.
/// </summary>
public sealed class OutdatedCheck(RepositoryPublisher publisher, GameDataClient client)
{
    /// <summary>The data at each version asked for so far; null when the repository could not tell.</summary>
    private readonly Dictionary<string, Task<GameData?>> _versions = [];

    /// <summary>
    /// A warning naming the items changed both here and in the data since <paramref name="changes"/> were made, or null
    /// when there are none. Without access to the repository (not signed in, or the version is not a commit) the
    /// warning only says the changes are older.
    /// </summary>
    public async Task<string?> WarningAsync(LocalBackup changes, GameData current)
    {
        if (!changes.IsOutdated(current.Version))
        {
            return null;
        }

        await publisher.LoadAsync();
        if (publisher.AdminName is null ||await DataAtAsync(changes.BaseVersion) is not { } made)
        {
            return ProposalText.Outdated(changes, current.Version);
        }

        var conflicts = ItemConflicts.Find(made, current, changes);
        if (conflicts.Count == 0)
        {
            return null;
        }

        var names = conflicts.Select(key =>
        {
            // Items removed since are named from the older data.
            var (kind, id) = ItemLabel.Split(key);
            return ItemConflicts.Json(current, key) is null ? ItemLabel.Of(made, kind, id) : ItemLabel.Of(current, kind, id);
        });
        return $"Od czasu powstania tych zmian ktoś zmienił w wiki te same pozycje: {string.Join(", ", names)}. "
            + "Sprawdź, czy ich nie nadpiszesz.";
    }

    private Task<GameData?> DataAtAsync(string version)
    {
        if (!_versions.TryGetValue(version, out var data))
        {
            data = _versions[version] = LoadAsync(version);
        }

        return data;
    }

    private async Task<GameData?> LoadAsync(string version)
    {
        try
        {
            return await publisher.GetDataAsync(await client.GetOriginalAsync(), version);
        }
        catch (PublishException)
        {
            // Not a commit (e.g. a local build) or the service is unreachable; asked again on the next page load.
            _versions.Remove(version);
            return null;
        }
    }
}
