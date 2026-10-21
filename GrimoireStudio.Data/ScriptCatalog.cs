namespace GrimoireStudio.Data;

/// <summary>Root object of scripts.json.</summary>
public sealed record ScriptCatalog
{
    public int Version { get; init; }
    public IReadOnlyList<Script> Scripts { get; init; } = [];
}
