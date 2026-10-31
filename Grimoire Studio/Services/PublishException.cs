using System.Net;

namespace Grimoire_Studio.Services;

/// <summary>The publishing service refused or could not be reached; <see cref="Exception.Message"/> is for the admin.</summary>
public sealed class PublishException(string message, HttpStatusCode? status = null) : Exception(message)
{
    public HttpStatusCode? Status { get; } = status;

    public bool IsConflict => Status == HttpStatusCode.Conflict;
}
