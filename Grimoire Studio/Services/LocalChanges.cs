using System.Text.Json;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>All changes saved in this browser, with export to and import from a file.</summary>
public sealed class LocalChanges(LocalRoles roles, LocalScripts scripts, LocalTeams teams, LocalJinxes jinxes)
{
    private static readonly JsonSerializerOptions FileOptions = new(GameData.JsonOptions) { WriteIndented = true };

    public LocalRoles Roles => roles;
    public LocalScripts Scripts => scripts;
    public LocalTeams Teams => teams;
    public LocalJinxes Jinxes => jinxes;

    public async Task<LocalBackup> GetAllAsync() => new()
    {
        ExportedAt = DateTimeOffset.Now,
        Roles = await roles.GetAllAsync(),
        Scripts = await scripts.GetAllAsync(),
        Teams = await teams.GetAllAsync(),
        Jinxes = await jinxes.GetAllAsync(),
    };

    public async Task<string> ExportAsync() => JsonSerializer.Serialize(await GetAllAsync(), FileOptions);

    /// <summary>
    /// Reads an export. With <paramref name="replace"/> everything local is replaced, otherwise items from the file
    /// replace local items with the same id and the rest stays.
    /// </summary>
    /// <returns>Number of imported items.</returns>
    /// <exception cref="FormatException">The file is not an export of local changes.</exception>
    public async Task<int> ImportAsync(string json, bool replace)
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

        if (backup is null || backup.Format != LocalBackup.FormatName)
        {
            throw new FormatException("To nie jest plik z eksportu lokalnych zmian Grimoire Studio.");
        }

        var current = replace ? new LocalBackup() : await GetAllAsync();
        var saved = await roles.ReplaceAllAsync(Merge(current.Roles, backup.Roles, r => r.Role.Id))
            & await scripts.ReplaceAllAsync(Merge(current.Scripts, backup.Scripts, s => s.Id))
            & await teams.ReplaceAllAsync(Merge(current.Teams, backup.Teams, t => t.Id))
            & await jinxes.ReplaceAllAsync(Merge(current.Jinxes, backup.Jinxes, j => j.Key));

        return saved ? backup.Count : throw new FormatException("Przeglądarka nie pozwoliła zapisać zmian (tryb prywatny lub brak miejsca).");
    }

    public async Task<bool> ClearAsync() =>
        await roles.ReplaceAllAsync([])
            & await scripts.ReplaceAllAsync([])
            & await teams.ReplaceAllAsync([])
            & await jinxes.ReplaceAllAsync([]);

    private static List<T> Merge<T>(IEnumerable<T> current, IEnumerable<T> incoming, Func<T, string> id)
    {
        var incomingList = incoming.ToList();
        var incomingIds = incomingList.Select(id).ToHashSet(StringComparer.OrdinalIgnoreCase);
        return current.Where(i => !incomingIds.Contains(id(i))).Concat(incomingList).ToList();
    }
}
