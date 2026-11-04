using Api.SharedKernel.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Hosting;

namespace Api.Tests.TestHelpers;

/// <summary>
/// #4774: the real <see cref="PublicBaseUrlProvider"/> over a configuration the test controls. A test that
/// exercises a link generator builds the provider here instead of mocking it: what the test asserts is the
/// link the real resolution order produces.
/// </summary>
internal static class PublicBaseUrlTestSupport
{
    /// <summary>The environment of a deployed host: no <c>localhost</c> last resort.</summary>
    public const string DeployedEnvironment = "Production";

    public static PublicBaseUrlProvider ProviderFor(
        IConfiguration configuration, string environmentName = DeployedEnvironment) =>
        new(configuration, new FakeHostEnvironment(environmentName));

    /// <summary>A provider over the given keys, in a deployed environment unless told otherwise.</summary>
    public static PublicBaseUrlProvider ProviderFor(
        IReadOnlyDictionary<string, string?> values, string environmentName = DeployedEnvironment) =>
        ProviderFor(new ConfigurationBuilder().AddInMemoryCollection(values).Build(), environmentName);

    /// <summary>
    /// A provider with no configuration at all, in <c>Development</c>: it answers <c>http://localhost:3000</c>.
    /// For tests that render a link but are not about its base.
    /// </summary>
    public static PublicBaseUrlProvider LocalProvider() =>
        ProviderFor(new ConfigurationBuilder().Build(), "Development");

    /// <summary>A provider that answers <paramref name="baseUrl"/> from <c>Frontend:BaseUrl</c>.</summary>
    public static PublicBaseUrlProvider ProviderAt(string baseUrl) =>
        ProviderFor(new Dictionary<string, string?> { [PublicBaseUrlProvider.PrimaryKey] = baseUrl });

    private sealed class FakeHostEnvironment(string environmentName) : IHostEnvironment
    {
        public string EnvironmentName { get; set; } = environmentName;

        public string ApplicationName { get; set; } = "Api.Tests";

        public string ContentRootPath { get; set; } = AppContext.BaseDirectory;

        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
    }
}
