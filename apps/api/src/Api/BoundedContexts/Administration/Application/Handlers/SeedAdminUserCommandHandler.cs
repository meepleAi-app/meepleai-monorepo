using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
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
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedAdminUserCommandHandler> _logger;

    public SeedAdminUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SeedAdminUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
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
        var adminEmail = _configuration["INITIAL_ADMIN_EMAIL"];
        var adminPassword = SecretsHelper.GetSecretOrValue(
            _configuration,
            "ADMIN_PASSWORD",
            _logger,
            required: false
        );
        var adminDisplayName = _configuration["INITIAL_ADMIN_DISPLAY_NAME"] ?? "System Administrator";

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
        var role = Role.Admin;

        // Create admin user
        var adminUser = new User(
            id: Guid.NewGuid(),
            email: email,
            displayName: adminDisplayName,
            passwordHash: passwordHash,
            role: role
        );

        // Persist
        await _userRepository.AddAsync(adminUser, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Admin user seeded successfully: {Email}", adminEmail);
    }
}
