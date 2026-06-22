using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands.StagingAllowlist;

/// <summary>
/// Idempotent staging allowlist bootstrap. See <see cref="SeedStagingAllowlistCommand"/> for contract.
/// </summary>
/// <remarks>
/// Migration path from the legacy <c>STAGING_ALLOWED_EMAILS</c> env var:
/// on first staging deploy after #845, this seeder pulls every email from the env var
/// and inserts it into the new DB-backed table. After the first successful run +
/// post-deploy verification, the env var can be removed from
/// <c>infra/secrets/staging-access.secret</c>.
/// </remarks>
internal sealed class SeedStagingAllowlistCommandHandler : ICommandHandler<SeedStagingAllowlistCommand>
{
    private const string BootstrapEmail = "badsworm@gmail.com";

    private readonly IStagingAllowlistRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SeedStagingAllowlistCommandHandler> _logger;

    public SeedStagingAllowlistCommandHandler(
        IStagingAllowlistRepository repository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<SeedStagingAllowlistCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedStagingAllowlistCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_hostEnvironment.IsStaging())
        {
            _logger.LogDebug("Skipping staging_allowlist seed — environment is {Env}", _hostEnvironment.EnvironmentName);
            return;
        }

        var inserted = 0;

        inserted += await EnsureSeededAsync(BootstrapEmail, "Bootstrap seed (#845)", cancellationToken).ConfigureAwait(false);

        // Migration path: pull legacy env-var values into the DB
        var legacy = _configuration["STAGING_ALLOWED_EMAILS"] ?? string.Empty;
        foreach (var email in legacy.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            inserted += await EnsureSeededAsync(
                email,
                "Migrated from STAGING_ALLOWED_EMAILS env var (#845)",
                cancellationToken).ConfigureAwait(false);
        }

        if (inserted > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("Seeded {Count} new staging allowlist entries", inserted);
        }
        else
        {
            _logger.LogDebug("Staging allowlist already seeded — no new entries");
        }
    }

    /// <summary>Returns 1 if a new row was added, 0 if already present.</summary>
    private async Task<int> EnsureSeededAsync(string email, string note, CancellationToken cancellationToken)
    {
        var normalized = StagingAllowlistEntry.NormalizeEmail(email);
        if (await _repository.ExistsByEmailAsync(normalized, cancellationToken).ConfigureAwait(false))
        {
            return 0;
        }

        var entry = StagingAllowlistEntry.Create(normalized, addedByUserId: null, note);
        await _repository.AddAsync(entry, cancellationToken).ConfigureAwait(false);
        return 1;
    }
}
