namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1903 M7.2 — runtime kill-switch for the admin catalog seed pipeline.
/// When the flag is disabled:
/// <list type="bullet">
/// <item><c>CatalogSeedFetchJob</c> early-returns (no provider calls, no DB
/// reads), so a stuck pipeline can be parked without redeploy.</item>
/// <item>The admin HTTP endpoints under <c>/api/v1/admin/catalog/seeds</c>
/// short-circuit with <c>503 Service Unavailable</c> via an endpoint filter,
/// surfacing the kill-switch to the UI immediately.</item>
/// </list>
/// </summary>
/// <remarks>
/// <para>Backed by the runtime configuration store
/// (<c>IConfigurationService</c>) under key
/// <see cref="FlagKey"/>. Mirrors the <c>RegistrationMode</c> /
/// <c>Registration:PublicEnabled</c> pattern referenced in CLAUDE.md — same
/// fail-closed semantics so a DB outage cannot accidentally enable the pipeline
/// (catalog seed touches third-party APIs subject to BGG ToS, spec §8.5.6).</para>
/// <para>Default value <c>false</c>: shipping with the flag flipped on by
/// default would risk silent BGG hits on first deploy. Operators must
/// explicitly opt in via <c>/admin/config</c> (or seed the config row in the
/// staging DB) before the pipeline becomes active.</para>
/// </remarks>
public interface ICatalogSeedFeatureFlag
{
    /// <summary>
    /// Runtime configuration key under which the boolean flag is stored.
    /// Exposed for use by admin config UI / seed migrations / diagnostic
    /// tooling.
    /// </summary>
    public const string FlagKey = "admin.catalog-seed.enabled";

    /// <summary>
    /// Returns <c>true</c> if the catalog seed pipeline is enabled in the
    /// running environment. Fail-closed: any error (config not found, DB
    /// unreachable, deserialization failure) yields <c>false</c>.
    /// </summary>
    Task<bool> IsEnabledAsync(CancellationToken ct = default);
}
