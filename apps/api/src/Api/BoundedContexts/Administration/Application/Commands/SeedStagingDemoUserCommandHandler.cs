using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Creates a demo user on staging for external testing/demos.
/// Only runs in Staging environment. Idempotent.
/// </summary>
internal sealed class SeedStagingDemoUserCommandHandler : ICommandHandler<SeedStagingDemoUserCommand>
{
    private const string DefaultDisplayName = "Demo User";

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<SeedStagingDemoUserCommandHandler> _logger;

    public SeedStagingDemoUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        IHostEnvironment hostEnvironment,
        ILogger<SeedStagingDemoUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _hostEnvironment = hostEnvironment ?? throw new ArgumentNullException(nameof(hostEnvironment));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedStagingDemoUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        if (!_hostEnvironment.IsStaging())
        {
            _logger.LogDebug("Skipping staging demo user seed — not in Staging environment");
            return;
        }

        var demoEmail = _configuration["STAGING_DEMO_EMAIL"]
            ?? Environment.GetEnvironmentVariable("STAGING_DEMO_EMAIL");
        var demoPassword = SecretsHelper.GetSecretOrValue(_configuration, "STAGING_DEMO_PASSWORD", _logger, required: false)
            ?? Environment.GetEnvironmentVariable("STAGING_DEMO_PASSWORD");

        if (string.IsNullOrWhiteSpace(demoEmail) || string.IsNullOrWhiteSpace(demoPassword))
        {
            _logger.LogWarning("STAGING_DEMO_EMAIL or STAGING_DEMO_PASSWORD not configured — skipping");
            return;
        }

        var email = new Email(demoEmail);
        var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);
        if (existingUser != null)
        {
            _logger.LogInformation("Staging demo user already exists: {Email}. Skipping.", DataMasking.MaskEmail(demoEmail));
            return;
        }

        var demoUser = new User(
            id: Guid.NewGuid(),
            email: email,
            displayName: DefaultDisplayName,
            passwordHash: PasswordHash.Create(demoPassword),
            role: Role.User
        );
        demoUser.VerifyEmail();

        await _userRepository.AddAsync(demoUser, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Staging demo user seeded: {Email}", DataMasking.MaskEmail(demoEmail));
    }
}
