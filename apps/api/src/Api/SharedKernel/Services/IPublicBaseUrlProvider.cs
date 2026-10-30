namespace Api.SharedKernel.Services;

/// <summary>
/// #4774: the one place that says what the public URL of the web front-end is. Every link the backend
/// writes for a person to open (invitations, share links, e-mail buttons, OAuth redirects) takes its base
/// from here, so a deployment configures one value and every link follows it.
/// </summary>
/// <remarks>
/// Before this existed, eight generators read <c>App:BaseUrl</c> (which no deployment sets, and which
/// <c>appsettings.json</c> filled with <c>localhost:3000</c>) while the verification e-mail read
/// <c>Frontend:BaseUrl</c> and then <c>FRONTEND_BASE_URL</c>: the same value under two keys with two
/// different fallbacks, and a link to the developer's machine as the silent outcome.
/// </remarks>
public interface IPublicBaseUrlProvider
{
    /// <summary>
    /// Reads the sources in order and never throws: the startup check needs to describe a missing value,
    /// not fail on it. Read at every call, so a reloaded configuration is picked up.
    /// </summary>
    PublicBaseUrlResolution Resolve();

    /// <summary>
    /// The public base URL without a trailing slash, ready to be followed by <c>/path</c>.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    /// Nothing is configured and the environment is not a local one. The message names the key to set.
    /// </exception>
    string GetBaseUrl();
}

/// <summary>What <see cref="IPublicBaseUrlProvider.Resolve"/> found.</summary>
/// <param name="Url">The URL without trailing slash, or <c>null</c> when no source holds a value.</param>
/// <param name="Source">
/// The configuration key that supplied <paramref name="Url"/>, or
/// <see cref="PublicBaseUrlProvider.LocalDefaultSource"/>; <c>null</c> when <paramref name="Url"/> is.
/// </param>
/// <param name="LocalEnvironment">
/// The host runs on a developer machine or in the test harness, where <c>localhost</c> is a legitimate
/// public URL. Everywhere else a URL that points at this machine is a misconfiguration.
/// </param>
public sealed record PublicBaseUrlResolution(string? Url, string? Source, bool LocalEnvironment);
