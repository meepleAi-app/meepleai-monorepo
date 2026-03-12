using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Seeders;
using Api.Infrastructure.Seeders.Catalog;
using Api.Infrastructure.Seeders.Core;
using Api.Services;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
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
    private readonly IEmbeddingService _embeddingService;
    private readonly IConfiguration _configuration;

    public AutoConfigurationService(
        IUserRepository userRepository,
        IMediator mediator,
        ILogger<AutoConfigurationService> logger,
        MeepleAiDbContext dbContext,
        IBggApiService bggApiService,
        IEmbeddingService embeddingService,
        IConfiguration configuration)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _bggApiService = bggApiService ?? throw new ArgumentNullException(nameof(bggApiService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<bool> IsFirstRunAsync(CancellationToken cancellationToken = default)
    {
        var hasUsers = await _userRepository.HasAnyUsersAsync(cancellationToken).ConfigureAwait(false);
        return !hasUsers;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting auto-configuration check...");

        try
        {
            // Seed users only if no users exist
            var hasUsers = await _userRepository.HasAnyUsersAsync(cancellationToken).ConfigureAwait(false);
            if (!hasUsers)
            {
                _logger.LogInformation("No users found. Seeding users...");

                // Seed admin user (from admin.secret)
                _logger.LogInformation("Seeding admin user...");
                await _mediator.Send(new SeedAdminUserCommand(), cancellationToken).ConfigureAwait(false);

                // Seed test user (hardcoded demo credentials)
                _logger.LogInformation("Seeding test user...");
                await _mediator.Send(new SeedTestUserCommand(), cancellationToken).ConfigureAwait(false);

                // Seed E2E test users (admin, editor, user with Demo123! password)
                _logger.LogInformation("Seeding E2E test users...");
                await _mediator.Send(new SeedE2ETestUsersCommand(), cancellationToken).ConfigureAwait(false);

                // Seed AI models (OpenRouter + Ollama)
                _logger.LogInformation("Seeding AI models...");
                await _mediator.Send(new SeedAiModelsCommand(), cancellationToken).ConfigureAwait(false);
            }
            else
            {
                _logger.LogInformation("Users already exist. Checking E2E test users...");
                // Always ensure E2E test users exist (idempotent - handler checks each user)
                await _mediator.Send(new SeedE2ETestUsersCommand(), cancellationToken).ConfigureAwait(false);
            }

            // Always check and seed games, badges, and rate limits if missing
            await SeedSharedGamesAndRelatedDataAsync(cancellationToken).ConfigureAwait(false);

            // Seed agent definitions for Playground POC (idempotent - skips if any exist)
            _logger.LogInformation("Seeding agent definitions...");
            await _mediator.Send(new SeedAgentDefinitionsCommand(), cancellationToken).ConfigureAwait(false);

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

        // Seed Catan POC Agent with full domain entities (Issue #4667)
        // Must run BEFORE StrategyPatternSeeder: creates GameEntity "Catan" needed for strategy pattern lookup
        _logger.LogInformation("Seeding Catan POC Agent...");
        await CatanPocAgentSeeder.SeedAsync(
            _dbContext,
            _logger,
            cancellationToken: cancellationToken).ConfigureAwait(false);

        // Seed strategy patterns for AI agent decision-making (Issue #3493, #3956, #3984)
        var seedingEnabled = _configuration.GetValue("Seeding:EnableStrategyPatterns", true);
        if (seedingEnabled)
        {
            _logger.LogInformation("Seeding strategy patterns for common game openings...");
            await StrategyPatternSeeder.SeedAsync(
                _dbContext,
                _logger,
                _embeddingService,
                cancellationToken).ConfigureAwait(false);
        }
        else
        {
            _logger.LogInformation("Strategy pattern seeding disabled via configuration");
        }

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

        // Seed default feature flag configurations (Issue #3674)
        _logger.LogInformation("Seeding feature flag configurations...");
        await FeatureFlagSeeder.SeedFeatureFlagsAsync(
            _dbContext,
            adminUser.Id,
            _logger,
            cancellationToken).ConfigureAwait(false);

        // Seed PDF rulebooks from data/rulebook/ directory (idempotent)
        _logger.LogInformation("Seeding PDF rulebooks...");
        await PdfRulebookSeeder.SeedRulebooksAsync(
            _dbContext,
            adminUser.Id,
            _logger,
            _configuration["PDF_STORAGE_PATH"],
            cancellationToken).ConfigureAwait(false);
    }
}
