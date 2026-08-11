using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Infrastructure.Repositories;

/// <summary>
/// EF Core implementation of <see cref="IProviderCredentialRepository"/>.
/// Issue #1859.
/// </summary>
internal sealed class ProviderCredentialRepository : IProviderCredentialRepository
{
    private readonly MeepleAiDbContext _dbContext;

    public ProviderCredentialRepository(MeepleAiDbContext dbContext)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
    }

    public Task<ProviderCredential?> GetActiveAsync(string providerName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        // Comparing the whole ProviderName value object (mapped via a HasConversion
        // value converter) translates to `provider_name = @p`; comparing `.Value`
        // does NOT translate and throws at runtime. Providers outside the allowed
        // set have no DB row → return null so callers fall back to env-var keys.
        var normalized = providerName.ToLowerInvariant();
        if (!ProviderName.Allowed.Contains(normalized))
            return Task.FromResult<ProviderCredential?>(null);
        var target = ProviderName.Create(normalized);
        return _dbContext.ProviderCredentials
            .AsTracking()
            .FirstOrDefaultAsync(c => c.ProviderName == target && c.IsActive, ct);
    }

    public Task<ProviderCredential?> GetLastRotationAsync(string providerName, CancellationToken ct)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(providerName);
        var normalized = providerName.ToLowerInvariant();
        if (!ProviderName.Allowed.Contains(normalized))
            return Task.FromResult<ProviderCredential?>(null);
        var target = ProviderName.Create(normalized);
        return _dbContext.ProviderCredentials
            .AsNoTracking()
            .Where(c => c.ProviderName == target)
            .OrderByDescending(c => c.RotatedAt)
            .FirstOrDefaultAsync(ct);
    }

    public async Task AddAsync(ProviderCredential credential, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(credential);
        await _dbContext.ProviderCredentials.AddAsync(credential, ct).ConfigureAwait(false);
    }

    public Task SaveChangesAsync(CancellationToken ct) => _dbContext.SaveChangesAsync(ct);
}
