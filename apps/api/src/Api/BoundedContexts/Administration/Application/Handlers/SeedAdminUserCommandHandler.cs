using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for SeedAdminUserCommand.
/// Creates initial admin user from admin.secret configuration.
/// Idempotent: Only executes if no admin user exists.
/// </summary>
internal sealed class SeedAdminUserCommandHandler : ICommandHandler<SeedAdminUserCommand>
{
    private readonly IUserRepository _userRepository;
    private readonly IUserAiConsentRepository _consentRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedAdminUserCommandHandler> _logger;

    public SeedAdminUserCommandHandler(
        IUserRepository userRepository,
        IUserAiConsentRepository consentRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SeedAdminUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _consentRepository = consentRepository ?? throw new ArgumentNullException(nameof(consentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedAdminUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check idempotency: Skip if admin user already exists
        var adminCount = await _userRepository.CountAdminsAsync(cancellationToken).ConfigureAwait(false);

        if (adminCount > 0)
        {
            _logger.LogInformation("Admin user already exists. Skipping seed.");
            return;
        }

        // Read admin credentials from configuration
        // Issue #2152: Fallback to Environment.GetEnvironmentVariable for values
        // set by SecretLoader after IConfiguration was built
        var adminEmail = _configuration["INITIAL_ADMIN_EMAIL"]
            ?? Environment.GetEnvironmentVariable("INITIAL_ADMIN_EMAIL");
        var adminPassword = SecretsHelper.GetSecretOrValue(
            _configuration,
            "ADMIN_PASSWORD",
            _logger,
            required: false
        ) ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD");
        var adminDisplayName = _configuration["INITIAL_ADMIN_DISPLAY_NAME"]
            ?? Environment.GetEnvironmentVariable("INITIAL_ADMIN_DISPLAY_NAME")
            ?? "System Administrator";

        // Validate credentials
        if (string.IsNullOrWhiteSpace(adminEmail))
            throw new InvalidOperationException("INITIAL_ADMIN_EMAIL is not configured");

        if (string.IsNullOrWhiteSpace(adminPassword))
            throw new InvalidOperationException("ADMIN_PASSWORD is not configured");

        if (adminPassword.Length < 8)
            throw new InvalidOperationException("ADMIN_PASSWORD must be at least 8 characters");

        if (!adminPassword.Any(char.IsUpper))
            throw new InvalidOperationException("ADMIN_PASSWORD must contain at least one uppercase letter");

        if (!adminPassword.Any(char.IsDigit))
            throw new InvalidOperationException("ADMIN_PASSWORD must contain at least one digit");

        // Create domain objects
        var email = new Email(adminEmail);
        var passwordHash = PasswordHash.Create(adminPassword);
        var role = Role.SuperAdmin;

        // Create admin user
        var adminUser = new User(
            id: Guid.NewGuid(),
            email: email,
            displayName: adminDisplayName,
            passwordHash: passwordHash,
            role: role
        );

        // Issue #372: Seeded admin must have verified email to avoid 403 on /auth/me
        adminUser.VerifyEmail();

        // Persist
        await _userRepository.AddAsync(adminUser, cancellationToken).ConfigureAwait(false);

        // E2E fix: Auto-create AI consent for admin user so AI features work out of the box
        var aiConsent = UserAiConsent.Create(
            adminUser.Id,
            consentedToAiProcessing: true,
            consentedToExternalProviders: true,
            consentVersion: "1.0-admin-seed");
        await _consentRepository.AddAsync(aiConsent, cancellationToken).ConfigureAwait(false);

        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin user seeded successfully with AI consent: {Email}", adminEmail);
    }
}
