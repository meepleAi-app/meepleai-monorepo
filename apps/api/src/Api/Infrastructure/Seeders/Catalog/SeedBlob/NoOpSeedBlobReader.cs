namespace Api.Infrastructure.Seeders.Catalog.SeedBlob;

/// <summary>
/// Fallback ISeedBlobReader used when SEED_BUCKET_* env vars are absent
/// (e.g., local dev without a configured seed bucket). Does not log per call;
/// PdfSeeder checks IsConfigured and emits a single informational message.
/// </summary>
internal sealed class NoOpSeedBlobReader : ISeedBlobReader
{
    public bool IsConfigured => false;

    public Task<Stream> OpenReadAsync(string blobKey, CancellationToken ct) =>
        throw new InvalidOperationException(
            "Seed bucket not configured. Set SEED_BUCKET_NAME / SEED_BUCKET_ENDPOINT / "
            + "SEED_BUCKET_ACCESS_KEY / SEED_BUCKET_SECRET_KEY to enable PDF seeding.");

    public Task<bool> ExistsAsync(string blobKey, CancellationToken ct) =>
        Task.FromResult(false);
}
