using System.Text.Json;

namespace Grimoire_Studio.Services;

/// <summary>
/// A role, script, team or jinx one commit changed (kind "role", "script", "team" or "jinx"; a jinx id is "owner/other"):
/// the item before and after as stored in the data files (null when it did not exist), and whether a role's icon changed.
/// </summary>
public sealed record CommitItem(string Kind, string Id, JsonElement? Before, JsonElement? After, bool IconChanged);
