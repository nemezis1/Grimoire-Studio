using GrimoireStudio.Data;

namespace Grimoire_Studio.Services;

/// <summary>A role saved in this browser, with its own icon (data URL) when one was uploaded.</summary>
public sealed record LocalRole(Role Role, string? IconUrl);
