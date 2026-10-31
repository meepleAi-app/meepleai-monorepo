using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Api.SharedKernel.Services;

/// <summary>
/// Resolves the public front-end URL (#4774). Order, first non-blank value wins:
/// <list type="number">
///   <item><description><c>Frontend:BaseUrl</c>: the key to set (<c>Frontend__BaseUrl</c> in a compose file).</description></item>
///   <item><description><c>App:BaseUrl</c>: the old key, read only so an existing deployment keeps working.</description></item>
///   <item><description><c>FRONTEND_BASE_URL</c>: the variable <c>email.secret</c> carries.</description></item>
///   <item><description>In a local environment only, <c>http://localhost:3000</c>.</description></item>
/// </list>
/// </summary>
/// <remarks>
/// A blank value is not a value: <c>${VAR:-}</c> in a compose file hands the container a variable that is
/// present and empty, and it must fall through to the next source instead of ending the search. Outside a
/// local environment there is no last resort: a made-up <c>localhost</c> link in a production e-mail is
/// worse than a failed send, because nobody sees it fail. A local environment is one where the API runs on
/// the machine of a developer or of a test runner and no link reaches a person: <c>Development</c>,
/// <c>Testing</c>, <c>CI</c> and <c>Integration</c>. Everything else, <c>Staging</c> and <c>Production</c>
/// included, is a deployed one.
/// </remarks>
public sealed class PublicBaseUrlProvider : IPublicBaseUrlProvider
{
    /// <summary>The key to set.</summary>
    public const string PrimaryKey = "Frontend:BaseUrl";

    /// <summary>The key the first generators used. Read-only fallback.</summary>
    public const string LegacyKey = "App:BaseUrl";

    /// <summary>The variable carried by <c>infra/secrets/email.secret</c>.</summary>
    public const string EnvironmentVariableKey = "FRONTEND_BASE_URL";

    /// <summary><see cref="PublicBaseUrlResolution.Source"/> of the local last resort.</summary>
    public const string LocalDefaultSource = "local default";

#pragma warning disable S1075 // URIs should not be hardcoded - the last resort of a local environment, by design
    /// <summary>What a local environment gets when nothing is configured.</summary>
    public const string LocalDefaultUrl = "http://localhost:3000";
#pragma warning restore S1075

    private static readonly string[] OrderedKeys = [PrimaryKey, LegacyKey, EnvironmentVariableKey];

    /// <summary>The environments in which <c>localhost</c> is an acceptable last resort. Compared case-insensitively.</summary>
    private static readonly string[] LocalEnvironmentNames = ["Development", "Testing", "CI", "Integration"];

    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment? _environment;

    /// <param name="configuration">Where the keys are read, at every call.</param>
    /// <param name="environment">
    /// Decides whether <c>localhost</c> is an acceptable last resort. <c>null</c> (a hand-built collection,
    /// a test that has no host) counts as not local.
    /// </param>
    public PublicBaseUrlProvider(IConfiguration configuration, IHostEnvironment? environment = null)
    {
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _environment = environment;
    }

    public PublicBaseUrlResolution Resolve()
    {
        var local = IsLocalEnvironment(_environment);

        foreach (var key in OrderedKeys)
        {
            var url = Normalize(_configuration[key]);
            if (url is not null)
            {
                return new PublicBaseUrlResolution(url, key, local);
            }
        }

        return local
            ? new PublicBaseUrlResolution(LocalDefaultUrl, LocalDefaultSource, LocalEnvironment: true)
            : new PublicBaseUrlResolution(Url: null, Source: null, LocalEnvironment: false);
    }

    public string GetBaseUrl() =>
        Resolve().Url
        ?? throw new InvalidOperationException(
            $"The public URL of the front-end is not configured: set '{PrimaryKey}' "
            + "(environment variable 'Frontend__BaseUrl') to the address people open in the browser, "
            + "for example 'https://meepleai.app'. Links to the front-end cannot be built without it.");

    /// <summary>
    /// A local environment is one where the API runs on the machine of a developer or of a test runner, and
    /// never in front of a person who opens a link: <c>Development</c> (a developer machine), <c>Testing</c>
    /// (the in-process test harness: its hosts clear every configuration source, so no key is there to read),
    /// <c>CI</c> (the hosts the CI workflows start on the runner) and <c>Integration</c> (the integration stack
    /// of <c>compose.integration.yml</c>). <c>Staging</c> and <c>Production</c> are the deployed ones.
    /// </summary>
    private static bool IsLocalEnvironment(IHostEnvironment? environment) =>
        environment is not null && LocalEnvironmentNames.Any(environment.IsEnvironment);

    private static string? Normalize(string? value)
    {
        var trimmed = value?.Trim().TrimEnd('/');
        return string.IsNullOrEmpty(trimmed) ? null : trimmed;
    }
}

/// <summary>Registration of <see cref="IPublicBaseUrlProvider"/>.</summary>
public static class PublicBaseUrlServiceCollectionExtensions
{
    /// <summary>
    /// Idempotent: a bounded context that needs the provider can call it without caring whether the host
    /// already did.
    /// </summary>
    public static IServiceCollection AddPublicBaseUrl(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.TryAddSingleton<IPublicBaseUrlProvider, PublicBaseUrlProvider>();
        return services;
    }
}
