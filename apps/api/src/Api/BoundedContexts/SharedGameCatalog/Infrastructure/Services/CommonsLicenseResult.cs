namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Services;

/// <summary>
/// Outcome of <see cref="IWikimediaCommonsClient.FetchLicenseAsync"/>.
/// Phase B foundation for issue #1823 L2 cover enrichment.
/// </summary>
/// <param name="RawLicense">
/// The raw <c>extmetadata.LicenseShortName</c> value returned by the Commons
/// API (e.g. <c>"CC BY-SA 4.0"</c>, <c>"CC0"</c>, <c>"Public domain"</c>).
/// <c>null</c> when the file is missing, the metadata is absent, or the request
/// failed (see <see cref="NotAvailable"/>).
/// </param>
/// <param name="IsWhitelisted">
/// <c>true</c> when <see cref="RawLicense"/> matches the DEC-3c whitelist
/// (PD / CC0 / CC-BY / CC-BY-SA, any version) per
/// <see cref="LicenseValidator.IsWhitelisted"/>. Always <c>false</c> when
/// <see cref="RawLicense"/> is <c>null</c>.
/// </param>
/// <param name="Attribution">
/// The plain-text artist credit derived from <c>extmetadata.Artist</c>, intended
/// for persistence in <c>shared_games.cover_attribution</c>. <c>null</c> when
/// the Artist field is missing (typical for CC0 / public domain works) or when
/// the file lookup failed.
/// </param>
internal sealed record CommonsLicenseResult(string? RawLicense, bool IsWhitelisted, string? Attribution)
{
    /// <summary>
    /// Sentinel result used when the Commons API call failed, the file is
    /// missing, or the response did not include the required metadata fields.
    /// Callers (M8 orchestrator) treat this as "skip cover enrichment for this
    /// game" — not as an exception.
    /// </summary>
    public static CommonsLicenseResult NotAvailable() => new(null, false, null);

    /// <summary>
    /// Successful result with the raw license string, the whitelist verdict, and
    /// an optional attribution string.
    /// </summary>
    /// <param name="rawLicense">Raw <c>extmetadata.LicenseShortName</c> value.</param>
    /// <param name="whitelisted">Whitelist verdict from <see cref="LicenseValidator.IsWhitelisted"/>.</param>
    /// <param name="attribution">Plain-text artist credit, or <c>null</c> when absent.</param>
    public static CommonsLicenseResult Found(string rawLicense, bool whitelisted, string? attribution) =>
        new(rawLicense, whitelisted, attribution);
}
