namespace GrimoireStudio.Data;

/// <summary>A file of the data folder to write; <see cref="Path"/> is relative to the folder and uses '/'.</summary>
public sealed record DataFile(string Path, byte[] Content);
