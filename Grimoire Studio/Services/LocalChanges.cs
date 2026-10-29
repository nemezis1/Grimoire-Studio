using System.Text.Json;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// One set of changes over the data folder kept in this browser (see <see cref="ChangeSets"/>): roles, scripts, teams
/// and jinxes, with export to and import from a file.
/// </summary>
public sealed class LocalChanges(LocalStore store, string prefix)
{
    private static readonly JsonSerializerOptions FileOptions = new(GameData.JsonOptions) { WriteIndented = true };

    public LocalRoles Roles { get; } = new(store, prefix);
    public LocalScripts Scripts { get; } = new(store, prefix);
    public LocalTeams Teams { get; } = new(store, prefix);
    public LocalJinxes Jinxes { get; } = new(store, prefix);

    private string BaseVersionKey => $"{prefix}-base";

    public async Task<LocalBackup> GetAllAsync() => new()
    {
        ExportedAt = DateTimeOffset.Now,
        BaseVersion = await store.GetAsync(BaseVersionKey) ?? string.Empty,
        Roles = await Roles.GetAllAsync(),
        Scripts = await Scripts.GetAllAsync(),
        Teams = await Teams.GetAllAsync(),
        Jinxes = await Jinxes.GetAllAsync(),
    };

    /// <summary>Data with this change set applied on top of <paramref name="data"/>.</summary>
    public async Task<GameData> ApplyToAsync(GameData data) => (await GetAllAsync()).ApplyTo(data);

    public async Task<string> ExportAsync() => JsonSerializer.Serialize(await GetAllAsync(), FileOptions);

    /// <exception cref="FormatException">The text is not an export of local changes.</exception>
    public static LocalBackup Parse(string json)
    {
        LocalBackup? backup;
        try
        {
            backup = JsonSerializer.Deserialize<LocalBackup>(json, GameData.JsonOptions);
        }
        catch (JsonException e)
        {
            throw new FormatException($"Niepoprawny JSON: {e.Message}");
        }

        return backup is not null && backup.Format == LocalBackup.FormatName
            ? backup
            : throw new FormatException("To nie jest plik z eksportu zmian Grimoire Studio.");
    }

    /// <summary>
    /// Reads an export. With <paramref name="replace"/> the whole change set is replaced, otherwise items from the file
    /// replace items with the same id and the rest stays.
    /// </summary>
    /// <returns>Number of imported items.</returns>
    /// <exception cref="FormatException">The file is not an export of local changes, or saving failed.</exception>
    public async Task<int> ImportAsync(string json, bool replace)
    {
        var backup = Parse(json);
        return await MergeAsync(backup, replace)
            ? backup.Count
            : throw new FormatException("Przeglądarka nie pozwoliła zapisać zmian (tryb prywatny lub brak miejsca).");
    }

    /// <summary>Adds <paramref name="changes"/> to this set; an item replaces the item with the same id.</summary>
    /// <returns>false when the browser refused to store.</returns>
    public async Task<bool> MergeAsync(LocalBackup changes, bool replace = false)
    {
        var current = replace ? new LocalBackup() : await GetAllAsync();
        var baseVersion = current.Count == 0 && changes.BaseVersion.Length > 0 ? changes.BaseVersion : current.BaseVersion;
        return await store.SetAsync(BaseVersionKey, baseVersion)
            & await Roles.ReplaceAllAsync(Merge(current.Roles, changes.Roles, r => r.Role.Id))
            & await Scripts.ReplaceAllAsync(Merge(current.Scripts, changes.Scripts, s => s.Id))
            & await Teams.ReplaceAllAsync(Merge(current.Teams, changes.Teams, t => t.Id))
            & await Jinxes.ReplaceAllAsync(Merge(current.Jinxes, changes.Jinxes, j => j.Key));
    }

    /// <summary>Removes one item by its <see cref="LocalBackup.Keys"/> key: a new item disappears, a changed one is restored.</summary>
    public Task<bool> RemoveAsync(string key)
    {
        var (kind, id) = (key[..key.IndexOf(':')], key[(key.IndexOf(':') + 1)..]);
        return kind switch
        {
            "role" => Roles.RemoveAsync(id),
            "script" => Scripts.RemoveAsync(id),
            "team" => Teams.RemoveAsync(id),
            "jinx" => Jinxes.RemoveAsync(id),
            _ => Task.FromResult(false),
        };
    }

    /// <summary>
    /// Drops items that no longer differ from <paramref name="baseData"/> (saved without changes, or the same change is
    /// already in the base), so lists of changes show only real differences. Also keeps
    /// <see cref="LocalBackup.BaseVersion"/>: taken from <paramref name="baseData"/> when changes appear, cleared when
    /// none are left.
    /// </summary>
    public async Task PruneAsync(GameData baseData)
    {
        var all = await GetAllAsync();
        var stale = all.Roles
            .Where(r => r.IconUrl is null && baseData.FindRole(r.Role.Id) is { } b && Same(b, r.Role))
            .Select(r => LocalBackup.RoleKey(r.Role.Id))
            .Concat(all.Scripts
                .Where(s => baseData.FindScript(s.Id) is { } b && Same(b, s))
                .Select(s => LocalBackup.ScriptKey(s.Id)))
            .Concat(all.Teams
                .Where(t => baseData.FindTeam(t.Id) is { } b && Same(b, t))
                .Select(t => LocalBackup.TeamKey(t.Id)))
            .Concat(all.Jinxes
                .Where(j => IsUnchanged(baseData, j))
                .Select(LocalBackup.JinxKey))
            .ToList();

        foreach (var key in stale)
        {
            await RemoveAsync(key);
        }

        var left = all.Count - stale.Count;
        var baseVersion = left == 0 ? string.Empty : all.BaseVersion.Length > 0 ? all.BaseVersion : baseData.Version;
        if (baseVersion != all.BaseVersion)
        {
            await store.SetAsync(BaseVersionKey, baseVersion);
        }
    }

    private static bool Same<T>(T a, T b) =>
        JsonSerializer.Serialize(a, GameData.JsonOptions) == JsonSerializer.Serialize(b, GameData.JsonOptions);

    private static bool IsUnchanged(GameData baseData, JinxOverride change)
    {
        var existing = baseData.Jinxes
            .Where(o => string.Equals(o.Id, change.OwnerId, StringComparison.OrdinalIgnoreCase))
            .SelectMany(o => o.Jinxes)
            .FirstOrDefault(j => string.Equals(j.Id, change.Jinx.Id, StringComparison.OrdinalIgnoreCase));

        return change.Removed
            ? existing is null
            : existing is not null
                && existing.ReasonPl.Trim() == change.Jinx.ReasonPl.Trim()
                && existing.ReasonEn.Trim() == change.Jinx.ReasonEn.Trim();
    }

    public async Task<bool> ClearAsync() =>
        await store.SetAsync(BaseVersionKey, string.Empty)
            & await Roles.ReplaceAllAsync([])
            & await Scripts.ReplaceAllAsync([])
            & await Teams.ReplaceAllAsync([])
            & await Jinxes.ReplaceAllAsync([]);

    private static List<T> Merge<T>(IEnumerable<T> current, IEnumerable<T> incoming, Func<T, string> id)
    {
        var incomingList = incoming.ToList();
        var incomingIds = incomingList.Select(id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.Where(i => !incomingIds.Contains(id(i))).Concat(incomingList).ToList();
    }
}
