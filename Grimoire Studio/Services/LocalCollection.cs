using System.Text.Json;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>
/// Items of one kind created or changed in this browser, kept in IndexedDB under <paramref name="key"/> and cached
/// for the session. An item replaces the item with the same <see cref="IdOf"/>.
/// </summary>
public abstract class LocalCollection<T>(LocalStore store, string key)
{
    private List<T>? _items;

    protected LocalStore Store { get; } = store;

    /// <summary>Items loaded so far, for synchronous lookups while rendering (loaded together with the game data).</summary>
    protected IReadOnlyList<T> Loaded => _items ?? [];

    protected abstract string IdOf(T item);

    /// <summary>A copy: later changes to the collection do not alter lists already handed out (e.g. a proposal).</summary>
    public async Task<IReadOnlyList<T>> GetAllAsync() => (await LoadAsync()).ToList();

    public async Task<T?> FindAsync(string id) =>
        (await LoadAsync()).FirstOrDefault(i => string.Equals(IdOf(i), id, StringComparison.OrdinalIgnoreCase));

    /// <returns>false when the browser refused to store (private mode, quota).</returns>
    public async Task<bool> SaveAsync(T item)
    {
        var items = await LoadAsync();
        items.RemoveAll(i => string.Equals(IdOf(i), IdOf(item), StringComparison.OrdinalIgnoreCase));
        items.Add(item);
        return await PersistAsync(items);
    }

    /// <summary>Removes the local version: a new item disappears, an edited one returns to the original.</summary>
    public async Task<bool> RemoveAsync(string id)
    {
        var items = await LoadAsync();
        items.RemoveAll(i => string.Equals(IdOf(i), id, StringComparison.OrdinalIgnoreCase));
        return await PersistAsync(items);
    }

    /// <summary>Replaces all local items (import, clearing).</summary>
    public async Task<bool> ReplaceAllAsync(IEnumerable<T> items)
    {
        var list = await LoadAsync();
        list.Clear();
        list.AddRange(items);
        return await PersistAsync(list);
    }

    private async Task<List<T>> LoadAsync()
    {
        if (_items is not null)
        {
            return _items;
        }

        var json = await Store.GetAsync(key) ?? await Store.MigrateFromLocalStorageAsync(key);
        try
        {
            _items = json is null ? [] : JsonSerializer.Deserialize<List<T>>(json, GameData.JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            _items = [];
        }

        return _items;
    }

    private Task<bool> PersistAsync(List<T> items) => Store.SetAsync(key, JsonSerializer.Serialize(items, GameData.JsonOptions));
}
