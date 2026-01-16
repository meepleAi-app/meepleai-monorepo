using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Service for automatic system configuration during first run.
/// Orchestrates seeding of admin user, test user, and AI models.
/// </summary>
internal sealed class AutoConfigurationService : IAutoConfigurationService
{
    private readonly IUserRepository _userRepository;
    private readonly IMediator _mediator;
    private readonly ILogger<AutoConfigurationService> _logger;

    public AutoConfigurationService(
        IUserRepository userRepository,
        IMediator mediator,
        ILogger<AutoConfigurationService> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            _logger.LogInformation("Auto-configuration completed successfully.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Auto-configuration failed: {Message}", ex.Message);
            throw;
        }
    }
}
