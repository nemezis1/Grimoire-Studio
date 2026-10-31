namespace Grimoire_Studio.Services;

/// <summary>Reply of the publishing service: the new commit, or <see cref="Changed"/> false when the files were already so.</summary>
public sealed record PublishResult(bool Changed, string Sha, string? Url, IReadOnlyList<string>? Files);
