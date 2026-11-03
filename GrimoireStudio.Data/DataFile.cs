namespace GrimoireStudio.Data;

/// <summary>A file of the data folder to write; <see cref="Path"/> is relative to the folder and uses '/'.</summary>
public sealed record DataFile(string Path, byte[] Content)
{
    /// <summary>The file is removed from the data folder (an icon of a removed role); <see cref="Content"/> is empty.</summary>
    public bool Deleted { get; init; }
}
