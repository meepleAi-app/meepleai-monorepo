using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Convenzione dei nomi (spec azure-local §4.2): i secret portano i nomi delle variabili d'ambiente che il repo
/// già usa, con <c>-</c> al posto di <c>_</c> (Key Vault non ammette l'underscore); <c>--</c> resta il separatore
/// di sezione come nel manager di default. <c>OPENROUTER-API-KEY</c> → <c>OPENROUTER_API_KEY</c>,
/// <c>Embedding--Provider</c> → <c>Embedding:Provider</c>. Le chiavi CamelCase con trattino interno non sono
/// esprimibili: usare il nome senza trattini (<c>Seq--ApiKey</c>).
/// </summary>
internal sealed class EnvironmentStyleKeyVaultSecretManager : KeyVaultSecretManager
{
    public override string GetKey(KeyVaultSecret secret) =>
        secret.Name.Replace("--", ":", StringComparison.Ordinal).Replace('-', '_');
}
