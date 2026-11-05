namespace Api.SharedKernel.Services;

/// <summary>
/// #4774: at startup, a public front-end URL that is missing or points at this machine is written to the log
/// as an error, instead of being found in a guest's mailbox. It never stops the host: a wrong value would
/// then block a deploy, and the next release is the cheaper place to fix a value. Same principle as the
/// <c>OPENROUTER_DEFAULT_MODEL</c> check in <c>Program.cs</c> and <c>AgentProfileStartupCheck</c>.
/// </summary>
internal static class PublicBaseUrlStartupCheck
{
    /// <returns><c>true</c> when the value is usable where this host runs.</returns>
    internal static bool LogIfMisconfigured(IPublicBaseUrlProvider provider, ILogger logger)
    {
        ArgumentNullException.ThrowIfNull(provider);
        ArgumentNullException.ThrowIfNull(logger);

        var resolution = provider.Resolve();

        if (resolution.LocalEnvironment)
        {
            logger.LogInformation(
                "Public front-end URL: {Url} (from {Source})", resolution.Url, resolution.Source);
            return true;
        }

        if (resolution.Url is null)
        {
            logger.LogError(
                "The public front-end URL is not configured: invitation e-mails, share links, e-mail buttons and OAuth "
                + "redirects cannot be built and will fail. Set '{Key}' (environment variable 'Frontend__BaseUrl') "
                + "to the address people open in the browser.",
                PublicBaseUrlProvider.PrimaryKey);
            return false;
        }

        if (PointsAtThisMachine(resolution.Url))
        {
            logger.LogError(
                "The public front-end URL is '{Url}' (from {Source}) and points at this machine: every link the API "
                + "writes for people to open will lead nowhere for them. Set '{Key}' (environment variable "
                + "'Frontend__BaseUrl') to the public address.",
                resolution.Url,
                resolution.Source,
                PublicBaseUrlProvider.PrimaryKey);
            return false;
        }

        logger.LogInformation(
            "✓ Public front-end URL: {Url} (from {Source})", resolution.Url, resolution.Source);
        return true;
    }

    /// <summary><c>localhost</c>, <c>127.0.0.1</c> or any other loopback address, with or without a port.</summary>
    internal static bool PointsAtThisMachine(string url)
    {
        if (Uri.TryCreate(url, UriKind.Absolute, out var uri) && !string.IsNullOrEmpty(uri.Host))
        {
            return uri.IsLoopback
                || uri.Host.Equals("localhost", StringComparison.OrdinalIgnoreCase)
                || uri.Host.EndsWith(".localhost", StringComparison.OrdinalIgnoreCase);
        }

        // Not a URL at all (a bare "localhost:3000" reads as scheme "localhost"): the substring still tells.
        return url.Contains("localhost", StringComparison.OrdinalIgnoreCase)
            || url.Contains("127.0.0.1", StringComparison.Ordinal);
    }
}
