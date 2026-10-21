using GrimoireStudio.Data;

namespace GrimoireStudio.Data.Tests;

/// <summary>Locates the repository's data folder from the test output directory.</summary>
internal static class DataFolder
{
    public static string Path { get; } = Find();

    public static string Icons => System.IO.Path.Combine(Path, "icons");

    private static string Find()
    {
        for (var dir = new DirectoryInfo(AppContext.BaseDirectory); dir is not null; dir = dir.Parent)
        {
            var candidate = System.IO.Path.Combine(dir.FullName, "data");
            if (File.Exists(System.IO.Path.Combine(candidate, GameData.RolesFile)))
            {
                return candidate;
            }
        }

        throw new DirectoryNotFoundException("Repository data folder not found.");
    }
}
