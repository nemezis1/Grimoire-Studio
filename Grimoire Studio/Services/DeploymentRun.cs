namespace Grimoire_Studio.Services;

/// <summary>One run of the Build workflow, which builds and deploys the site after a push to the main branch.</summary>
public sealed record DeploymentRun(
    string Sha,
    string Title,
    string Author,
    string Status,
    string? Conclusion,
    DateTimeOffset StartedAt,
    DateTimeOffset UpdatedAt,
    string Url)
{
    public string ShortSha => Sha.Length > 7 ? Sha[..7] : Sha;

    public bool IsRunning => Status != "completed";

    public bool Succeeded => Conclusion == "success";

    /// <summary>"w toku", "wdrożone", "błąd"… for the admin panel.</summary>
    public string StatusText => IsRunning
        ? Status == "in_progress" ? "w toku" : "w kolejce"
        : Conclusion switch
        {
            "success" => "wdrożone",
            "failure" => "błąd",
            "cancelled" => "anulowane",
            _ => Conclusion ?? "zakończone",
        };
}
