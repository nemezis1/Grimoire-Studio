namespace Grimoire_Studio.Generator;

/// <summary>One token on the print sheet. Its settings are edited in place while it is selected.</summary>
public sealed class SheetItem(TokenSettings settings, string? roleId)
{
    public Guid Id { get; } = Guid.NewGuid();

    public TokenSettings Settings { get; } = settings;

    /// <summary>Role the token was made from, so editing can show its reminder buttons again.</summary>
    public string? RoleId { get; set; } = roleId;

    /// <summary>Object URL of the rendered token (print resolution); null until rendered.</summary>
    public string? ImageUrl { get; set; }

    /// <summary>Snapshot of <see cref="Settings"/> that <see cref="ImageUrl"/> was rendered from.</summary>
    public string? RenderedState { get; set; }
}
