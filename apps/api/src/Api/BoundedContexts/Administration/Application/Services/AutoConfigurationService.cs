using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Seeders;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Service for automatic system configuration during first run.
/// Orchestrates seeding of admin user, test user, AI models, shared games, badges, and rate limits.
/// </summary>
internal sealed class AutoConfigurationService : IAutoConfigurationService
{
    private readonly IUserRepository _userRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<AutoConfigurationService> _logger;
    private readonly MeepleAiDbContext _dbContext;
    private readonly IBggApiService _bggApiService;

    public AutoConfigurationService(
        IUserRepository userRepository,
        IMediator mediator,
        ILogger<AutoConfigurationService> logger,
        MeepleAiDbContext dbContext,
        IBggApiService bggApiService)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
    }

    public async Task<bool> IsFirstRunAsync(CancellationToken cancellationToken = default)
    {
        var hasUsers = await _userRepository.HasAnyUsersAsync(cancellationToken).ConfigureAwait(false);
        return !hasUsers;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        var isFirstRun = await IsFirstRunAsync(cancellationToken).ConfigureAwait(false);

        if (!isFirstRun)
        {
            _logger.LogInformation("Not first run. Skipping auto-configuration.");
            return;
        }

        _logger.LogInformation("First run detected. Starting auto-configuration...");

        try
        {
            // Seed admin user (from admin.secret)
            _logger.LogInformation("Seeding admin user...");
            await _mediator.Send(new SeedAdminUserCommand(), cancellationToken).ConfigureAwait(false);

            // Seed test user (hardcoded demo credentials)
            _logger.LogInformation("Seeding test user...");
            await _mediator.Send(new SeedTestUserCommand(), cancellationToken).ConfigureAwait(false);

            // Seed AI models (OpenRouter + Ollama)
            _logger.LogInformation("Seeding AI models...");
            await _mediator.Send(new SeedAiModelsCommand(), cancellationToken).ConfigureAwait(false);

            // Seed shared games, badges, and rate limits (requires admin user)
            await SeedSharedGamesAndRelatedDataAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Auto-configuration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-configuration failed: {Message}", ex.Message);
            throw;
        }
    }

    private async Task SeedSharedGamesAndRelatedDataAsync(CancellationToken cancellationToken)
    {
        // Get admin user for system operations (created in previous step)
        var adminUser = await _dbContext.Users
            .FirstOrDefaultAsync(u => u.Role == "admin", cancellationToken)
            .ConfigureAwait(false);

        if (adminUser == null)
        {
            _logger.LogWarning("Admin user not found. Skipping SharedGame, Badge, and RateLimitConfig seeding.");
            return;
        }

        // Seed SharedGameCatalog from BGG data
        _logger.LogInformation("Seeding shared games from BGG...");
        await SharedGameSeeder.SeedSharedGamesAsync(
            _dbContext,
            _bggApiService,
            adminUser.Id,
            _logger,
            cancellationToken).ConfigureAwait(false);

        // Seed predefined badges (ISSUE-2731)
        _logger.LogInformation("Seeding badges...");
        await BadgeSeeder.SeedBadgesAsync(
            _dbContext,
            _logger,
            cancellationToken).ConfigureAwait(false);

        // Seed default rate limit configurations (ISSUE-2809)
        _logger.LogInformation("Seeding rate limit configurations...");
        await RateLimitConfigSeeder.SeedRateLimitConfigsAsync(
            _dbContext,
            _logger,
            cancellationToken).ConfigureAwait(false);
    }
}
