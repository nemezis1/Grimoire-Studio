namespace Grimoire_Studio.Services;

/// <summary>A commit of wiki changes sent by an admin: who approved them and whose proposals they hold.</summary>
public sealed record WikiCommit(
    string Sha,
    string Url,
    DateTimeOffset Date,
    string Author,
    string Title,
    string? ApprovedBy,
    string? Proposals)
{
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;
}
