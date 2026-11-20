namespace Grimoire_Studio.Services;

/// <summary>
/// A bug report or an idea for the site, sent by anyone; admins answer it with a status and a reply. Content changes
/// (roles, translations) are proposed with the editors instead.
/// </summary>
public sealed record Report
{
    public const string Bug = "bug";
    public const string Idea = "idea";

    public const string New = "new";
    public const string InProgress = "progress";
    public const string Done = "done";
    public const string Rejected = "rejected";

    /// <summary>The statuses in the order an admin moves a report through them.</summary>
    public static IReadOnlyList<string> Statuses { get; } = [New, InProgress, Done, Rejected];

    public required string Id { get; init; }

    /// <summary><see cref="Bug"/> or <see cref="Idea"/>.</summary>
    public required string Kind { get; init; }

    public required string Title { get; init; }
    public required string Description { get; init; }

    /// <summary>Signature; may be empty.</summary>
    public string Author { get; init; } = string.Empty;

    /// <summary>Address of the page the report was sent from; may be empty.</summary>
    public string Page { get; init; } = string.Empty;

    /// <summary>Browser, window and data version, when the sender agreed to add them.</summary>
    public string Technical { get; init; } = string.Empty;

    public DateTimeOffset CreatedAt { get; init; }

    public string Status { get; init; } = New;

    /// <summary>The admins' answer, shown to the sender.</summary>
    public string Reply { get; init; } = string.Empty;

    public DateTimeOffset? UpdatedAt { get; init; }
    public string? UpdatedBy { get; init; }

    public bool IsClosed => Status is Done or Rejected;

    public static string KindLabel(string kind) => kind == Idea ? "Pomysł" : "Błąd";

    public static string StatusLabel(string status) => status switch
    {
        InProgress => "w trakcie",
        Done => "zrobione",
        Rejected => "odrzucone",
        _ => "nowe",
    };
}
