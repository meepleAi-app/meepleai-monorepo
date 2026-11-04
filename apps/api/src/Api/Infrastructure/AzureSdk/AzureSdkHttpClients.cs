using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using Azure.Core.Pipeline;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// I due <see cref="HttpClient"/> nominati che trasportano gli SDK Azure (spec azure-local §4.5).
/// <para>
/// <c>AzureSdk</c> è pinnato con <c>ConfigureSsrfPin</c> e serve il profilo Azure vero: il pin blocca OGNI
/// indirizzo privato (<c>SsrfPolicy.IsBlocked</c>), quindi non può raggiungere gli emulatori sulla rete
/// compose. Per questo esiste <c>AzureSdkLocal</c>, esente dal pin e scelto SOLO con
/// <c>MEEPLEAI_PROFILE=azure-local</c>. La scelta avviene a runtime in <see cref="CreateTransport"/>.
/// </para>
/// <para>Il client pinnato senza suffissi ammessi non restringerebbe l'host: <see cref="CreateTransport"/>
/// rifiuta quel caso invece di lasciare un default permissivo.</para>
/// </summary>
internal static class AzureSdkHttpClients
{
    public const string PinnedName = "AzureSdk";
    public const string LocalName = "AzureSdkLocal";
    public const string ProfileKey = "MEEPLEAI_PROFILE";
    public const string LocalProfile = "azure-local";
    public const string AllowedHostSuffixesKey = "Azure:Sdk:AllowedHostSuffixes";
    public const string AllowInsecureTlsKey = "AZURE_SDK_ALLOW_INSECURE_TLS";

    public static bool IsLocalProfile(IConfiguration configuration) =>
        string.Equals(configuration[ProfileKey], LocalProfile, StringComparison.OrdinalIgnoreCase);

    public static bool AllowInsecureTls(IConfiguration configuration) =>
        IsLocalProfile(configuration)
        && string.Equals(configuration[AllowInsecureTlsKey], "true", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ReadAllowedHostSuffixes(IConfiguration configuration) =>
        configuration.GetSection(AllowedHostSuffixesKey).Get<string[]>()?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray() ?? [];

    public static IServiceCollection AddAzureSdkHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var suffixes = ReadAllowedHostSuffixes(configuration);
        var allowInsecureTls = AllowInsecureTls(configuration);

        services.AddHttpClient("AzureSdk").ConfigureSsrfPin(MeepleAiMetrics.EgressSinks.AzureSdk, allowedHostSuffixes: suffixes);

        services.AddHttpClient("AzureSdkLocal").ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new SocketsHttpHandler();
            if (allowInsecureTls)
            {
                // Solo per il certificato self-signed del Key Vault emulator, solo nel profilo locale.
#pragma warning disable S4830, CA5359 // Deliberato: solo profilo azure-local + AZURE_SDK_ALLOW_INSECURE_TLS=true (AllowInsecureTls)
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#pragma warning restore S4830, CA5359
            }

            return handler;
        });

        return services;
    }

    /// <summary>Il transport da passare nelle <c>ClientOptions</c> di ogni SDK Azure basato su HTTP.</summary>
    public static HttpClientTransport CreateTransport(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        if (IsLocalProfile(configuration))
        {
            return new HttpClientTransport(factory.CreateClient(LocalName));
        }

        if (ReadAllowedHostSuffixes(configuration).Count == 0)
        {
            throw new InvalidOperationException(
                $"{AllowedHostSuffixesKey} must list the Azure endpoint suffixes (for example .blob.core.windows.net) " +
                $"when {ProfileKey} is not '{LocalProfile}': the pinned AzureSdk client refuses to run without a host allow-list.");
        }

        return new HttpClientTransport(factory.CreateClient(PinnedName));
    }
}
