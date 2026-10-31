namespace Grimoire_Studio.Services;

/// <summary>The latest deployments, newest first, and the data version the site serves right now.</summary>
public sealed record DeploymentStatus(IReadOnlyList<DeploymentRun> Runs, string LiveVersion)
{
    public bool AnyRunning => Runs.Any(r => r.IsRunning);
}
