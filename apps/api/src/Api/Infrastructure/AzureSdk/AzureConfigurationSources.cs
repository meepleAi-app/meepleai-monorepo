using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Sorgenti <see cref="IConfiguration"/> Azure (spec azure-local §4.2). Girano PRIMA del container DI, quindi non
/// passano dal client <c>AzureSdk</c> pinnato: gli endpoint vengono da configurazione statica (mai da input
/// utente), la stessa condizione con cui il gate di egress esenta OpenRouter o Resend. Con le variabili assenti o
/// vuote non viene aggiunta alcuna sorgente. Le chiavi lette da qui VINCONO sulle variabili d'ambiente
/// (aggiunte dopo); <c>SecretLoader</c> aggiunge solo le chiavi ancora assenti.
/// </summary>
internal static class AzureConfigurationSources
{
    public const string AppConfigConnectionStringKey = "AZURE_APPCONFIG_CONNECTION_STRING";
    public const string AppConfigEndpointKey = "AZURE_APPCONFIG_ENDPOINT";
    public const string KeyVaultUriKey = "AZURE_KEYVAULT_URI";
    public const string KeyVaultAnonymousKey = "AZURE_KEYVAULT_ANONYMOUS";
    private const string AnonymousToken = "anonymous";

    public static IConfigurationBuilder AddAzureConfigurationSources(this IConfigurationBuilder builder, IConfiguration settings)
    {
        var appConfigConnectionString = settings[AppConfigConnectionStringKey];
        var appConfigEndpoint = settings[AppConfigEndpointKey];
        if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
        {
            // HMAC (emulatore o store con chiavi di accesso): funziona anche su http.
            builder.AddAzureAppConfiguration(options => options
                .Connect(appConfigConnectionString.Trim())
                .Select(KeyFilter.Any, LabelFilter.Null));
        }
        else if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
        {
            // Azure vero: identità Entra, endpoint https.
            var endpoint = ParseAbsoluteUri(appConfigEndpoint, AppConfigEndpointKey);
            builder.AddAzureAppConfiguration(options => options
                .Connect(endpoint, new DefaultAzureCredential())
                .Select(KeyFilter.Any, LabelFilter.Null));
        }

        var keyVaultUri = settings[KeyVaultUriKey];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            var vault = ParseAbsoluteUri(keyVaultUri, KeyVaultUriKey);
            var clientOptions = new SecretClientOptions { DisableChallengeResourceVerification = true };
            if (AzureSdkHttpClients.AllowInsecureTls(settings))
            {
                // Solo profilo azure-local + opt-in esplicito: certificato self-signed del Key Vault emulator.
                var handler = new SocketsHttpHandler();
#pragma warning disable S4830, CA5359 // Deliberato: solo profilo azure-local + opt-in (AllowInsecureTls)
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
#pragma warning restore S4830, CA5359
                clientOptions.Transport = new HttpClientTransport(new HttpClient(handler));
            }

            var anonymous = string.Equals(settings[KeyVaultAnonymousKey], "true", StringComparison.OrdinalIgnoreCase);
            var client = new SecretClient(vault, anonymous ? new StaticTokenCredential(AnonymousToken) : new DefaultAzureCredential(), clientOptions);
            builder.AddAzureKeyVault(client, new EnvironmentStyleKeyVaultSecretManager());
        }

        return builder;
    }

    private static Uri ParseAbsoluteUri(string value, string key)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{key} must be an absolute URL, got '{value}'");
        }

        return uri;
    }
}
