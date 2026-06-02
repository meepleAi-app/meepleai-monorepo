namespace Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;

public sealed class InvalidGoldsetVersionException : Exception
{
    public string RequestedVersion { get; }
    public IReadOnlyList<string> AvailableVersions { get; }

    public InvalidGoldsetVersionException(string requestedVersion, IReadOnlyList<string> availableVersions)
        : base($"Goldset '{requestedVersion}' not registered. Available: [{string.Join(", ", availableVersions)}]")
    {
        RequestedVersion = requestedVersion;
        AvailableVersions = availableVersions;
    }
}
