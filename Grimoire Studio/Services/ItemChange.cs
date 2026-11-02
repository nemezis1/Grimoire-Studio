using System.Text.Json;

namespace Grimoire_Studio.Services;

/// <summary>
/// One commit that changed a role, script or jinx: the item before and after (as stored in the data files; null when it
/// did not exist), and whether a role's icon changed.
/// </summary>
public sealed record ItemChange(
    string Sha,
    string Url,
    DateTimeOffset Date,
    string Author,
    string Title,
    string? ApprovedBy,
    string? Proposals,
    JsonElement? Before,
    JsonElement? After,
    bool IconChanged)
{
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
}
