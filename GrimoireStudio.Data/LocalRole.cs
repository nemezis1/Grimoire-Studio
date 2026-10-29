namespace GrimoireStudio.Data;

/// <summary>A role saved in this browser, with its own icon (data URL) when one was uploaded.</summary>
public sealed record LocalRole(Role Role, string? IconUrl);
