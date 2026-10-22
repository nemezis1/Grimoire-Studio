namespace GrimoireStudio.Data;

/// <summary>
/// One step of a night: a waking role, or the storyteller's minion / demon information on the first night
/// (<see cref="Role"/> is null for those).
/// </summary>
public sealed record NightStep(NightStepKind Kind, int Order, Role? Role = null)
{
    /// <summary>First-night positions of the information steps; the role data leaves gaps for them (50 → 70, 80 → 100).</summary>
    public const int MinionInfoOrder = 60;
    public const int DemonInfoOrder = 90;
}
