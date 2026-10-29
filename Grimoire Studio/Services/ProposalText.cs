using System.Globalization;
using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>Polish labels for proposals.</summary>
public static class ProposalText
{
    /// <summary>"1 zmiana", "3 zmiany", "5 zmian".</summary>
    public static string Changes(int count) => count switch
    {
        1 => "1 zmiana",
        _ when count % 10 is >= 2 and <= 4 && count % 100 is < 12 or > 14 => $"{count} zmiany",
        _ => $"{count} zmian",
    };

    /// <summary>Warning for changes made on another data version than <paramref name="current"/>; null when not.</summary>
    public static string? Outdated(LocalBackup changes, string current) => changes.IsOutdated(current)
        ? $"Zmiany powstały na wcześniejszej wersji danych ({Version(changes.BaseVersion)}, obecna: {Version(current)}). "
            + "Sprawdź, czy nikt w międzyczasie nie zmienił tych samych pozycji."
        : null;

    /// <summary>A data version for people: the build time in local time, or the id as it is.</summary>
    public static string Version(string version) =>
        DateTimeOffset.TryParse(version, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var time)
            ? time.ToLocalTime().ToString("g")
            : version;

    public static string Status(Proposal proposal) => proposal.Status switch
    {
        ProposalStatus.Approved when proposal.ApprovedCount < proposal.Changes.Count =>
            $"zatwierdzone częściowo ({proposal.ApprovedCount} z {proposal.Changes.Count})",
        ProposalStatus.Approved => "zatwierdzone",
        ProposalStatus.Rejected => "odrzucone",
        _ => "czeka na zatwierdzenie",
    };
}
