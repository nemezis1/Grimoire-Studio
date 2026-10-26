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

    public static string Status(Proposal proposal) => proposal.Status switch
    {
        ProposalStatus.Approved when proposal.ApprovedCount < proposal.Changes.Count =>
            $"zatwierdzone częściowo ({proposal.ApprovedCount} z {proposal.Changes.Count})",
        ProposalStatus.Approved => "zatwierdzone",
        ProposalStatus.Rejected => "odrzucone",
        _ => "czeka na zatwierdzenie",
    };
}
