namespace Api.Services.Pdf;

/// <summary>Configurazione del provider <c>azureblob</c> (spec azure-local §4.1).</summary>
internal sealed class AzureBlobStorageOptions
{
    /// <summary>Connection string con chiave condivisa (Azurite o account vero). Alternativa a <see cref="AccountUrl"/>.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>URL dell'account (<c>https://&lt;account&gt;.blob.core.windows.net</c>) con <c>DefaultAzureCredential</c>.</summary>
    public string? AccountUrl { get; init; }

    public required string ContainerName { get; init; }

    /// <summary>
    /// Host pubblico con cui riscrivere scheme/host/porta degli URL SAS (come <c>S3_PUBLIC_ENDPOINT</c>, #3498):
    /// la firma SAS copre risorsa e parametri, non l'host.
    /// </summary>
    public string? PublicBlobEndpoint { get; init; }

    public int SasExpirySeconds { get; init; } = 3600;

    /// <summary>Estratti dalla connection string: con entrambi la SAS è firmata con la chiave condivisa, altrimenti con la delega utente.</summary>
    public string? AccountName { get; init; }
    public string? AccountKey { get; init; }
}
