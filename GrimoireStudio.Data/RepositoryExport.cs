using System.Text;

namespace GrimoireStudio.Data;

/// <summary>
/// Turns a change set (e.g. the wiki changes) into files of the data folder: the JSON files that change, uploaded role
/// icons as <c>icons/&lt;id&gt;.png</c> and uploaded script pictures as <c>editions/&lt;script id&gt;.png</c>.
/// </summary>
public static class RepositoryExport
{
    /// <summary>Uploaded pictures are scaled down in the browser (icons 256 px, script pictures 512 px); this is a safety cap.</summary>
    public const int MaxImageBytes = 1024 * 1024;

    private static readonly byte[] PngSignature = [0x89, (byte)'P', (byte)'N', (byte)'G', 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Files that differ once <paramref name="changes"/> are applied to <paramref name="original"/>.</summary>
    /// <exception cref="InvalidDataException">An uploaded picture is not a PNG, is too big, or its file name is taken.</exception>
    public static IReadOnlyList<DataFile> ChangedFiles(GameData original, LocalBackup changes)
    {
        var updated = changes.ApplyTo(original);
        var files = new List<DataFile>();

        foreach (var role in changes.Roles.Where(r => r.IconUrl is not null))
        {
            files.Add(new($"{GameData.IconsFolder}/{role.Role.Id}.png", DecodePng(role.IconUrl!, $"Ikona roli {role.Role.Id}")));
        }

        // A script picture is saved under the script's id, which then is its icon name (no custom icon).
        var scripts = new List<Script>();
        foreach (var script in updated.Scripts.Scripts)
        {
            if (script.Image is null)
            {
                scripts.Add(script);
                continue;
            }

            if (updated.Scripts.Scripts.Any(s => s.Id != script.Id && s.IconName == script.Id))
            {
                throw new InvalidDataException(
                    $"Obrazek skryptu {script.Id}: plik {GameData.EditionsFolder}/{script.Id}.png jest ikoną innego skryptu.");
            }

            files.Add(new($"{GameData.EditionsFolder}/{script.Id}.png", DecodePng(script.Image, $"Obrazek skryptu {script.Id}")));
            scripts.Add(script with { Image = null, CustomIcon = string.Empty });
        }

        updated = updated with { Scripts = updated.Scripts with { Scripts = scripts } };

        var before = DataFiles.Serialize(original);
        foreach (var (name, json) in DataFiles.Serialize(updated))
        {
            if (before[name] != json)
            {
                files.Add(new(name, Encoding.UTF8.GetBytes(json)));
            }
        }

        return files;
    }

    /// <exception cref="InvalidDataException">Not a base64 PNG data URL, or bigger than <see cref="MaxImageBytes"/>.</exception>
    public static byte[] DecodePng(string dataUrl, string what)
    {
        const string prefix = "data:image/png;base64,";
        byte[] bytes;
        try
        {
            bytes = dataUrl.StartsWith(prefix, StringComparison.Ordinal)
                ? Convert.FromBase64String(dataUrl[prefix.Length..])
                : [];
        }
        catch (FormatException)
        {
            bytes = [];
        }

        if (!bytes.AsSpan().StartsWith(PngSignature))
        {
            throw new InvalidDataException($"{what}: to nie jest obrazek PNG.");
        }

        return bytes.Length <= MaxImageBytes
            ? bytes
            : throw new InvalidDataException($"{what}: obrazek ma {bytes.Length / 1024} KB, limit to {MaxImageBytes / 1024} KB.");
    }
}
