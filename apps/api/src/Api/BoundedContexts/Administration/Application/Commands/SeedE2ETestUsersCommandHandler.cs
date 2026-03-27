using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Infrastructure.Security;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SeedE2ETestUsersCommand.
/// Creates E2E test users for Playwright tests with credentials from secrets.
/// Idempotent: Skips users that already exist.
/// </summary>
internal sealed class SeedE2ETestUsersCommandHandler : ICommandHandler<SeedE2ETestUsersCommand>
{
    private static readonly (string Email, string DisplayName, Role Role)[] NonAdminTestUsers =
    [
        ("editor@meepleai.dev", "E2E Editor User", Role.Editor),
        ("user@meepleai.dev", "E2E Regular User", Role.User),
    ];

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedE2ETestUsersCommandHandler> _logger;

    public SeedE2ETestUsersCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SeedE2ETestUsersCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedE2ETestUsersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var testPassword = SecretsHelper.GetSeedTestPassword(_configuration, _logger);
        if (string.IsNullOrWhiteSpace(testPassword))
        {
            _logger.LogWarning("SEED_TEST_PASSWORD not configured — skipping E2E test user seed");
            return;
        }
        if (testPassword.Length < 8)
        {
            _logger.LogWarning("SEED_TEST_PASSWORD is too short (min 8 chars) — skipping E2E user seed");
            return;
        }

        var usersCreated = 0;

        // Admin E2E user
        var adminEmail = _configuration["INITIAL_ADMIN_EMAIL"]
            ?? Environment.GetEnvironmentVariable("INITIAL_ADMIN_EMAIL")
            ?? _configuration["ADMIN_EMAIL"]
            ?? Environment.GetEnvironmentVariable("ADMIN_EMAIL");

        if (!string.IsNullOrWhiteSpace(adminEmail))
        {
            var email = new Email(adminEmail);
            var existingAdmin = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

            if (existingAdmin != null)
            {
                _logger.LogInformation("E2E admin user already exists: {Email}. Skipping.", DataMasking.MaskEmail(adminEmail));
            }
            else
            {
                var adminPassword = SecretsHelper.GetSecretOrValue(_configuration, "ADMIN_PASSWORD", _logger, required: false)
                    ?? Environment.GetEnvironmentVariable("ADMIN_PASSWORD")
                    ?? testPassword;

                var adminUser = new User(
                    id: Guid.NewGuid(),
                    email: email,
                    displayName: "E2E Admin User",
                    passwordHash: PasswordHash.Create(adminPassword),
                    role: Role.Admin
                );

                await _userRepository.AddAsync(adminUser, cancellationToken).ConfigureAwait(false);
                usersCreated++;
                _logger.LogInformation("E2E admin user created: {Email} with role Admin", DataMasking.MaskEmail(adminEmail));
            }
        }
        else
        {
            _logger.LogWarning("INITIAL_ADMIN_EMAIL not configured — skipping E2E admin user seed");
        }

        // Non-admin E2E users
        foreach (var (emailStr, displayName, role) in NonAdminTestUsers)
        {
            var email = new Email(emailStr);
            var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

            if (existingUser != null)
            {
                _logger.LogInformation("E2E test user already exists: {Email}. Skipping.", DataMasking.MaskEmail(emailStr));
                continue;
            }

            var user = new User(
                id: Guid.NewGuid(),
                email: email,
                displayName: displayName,
                passwordHash: PasswordHash.Create(testPassword),
                role: role
            );

            await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
            usersCreated++;
            _logger.LogInformation("E2E test user created: {Email} with role {Role}", DataMasking.MaskEmail(emailStr), role);
        }

        if (usersCreated > 0)
        {
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("E2E test users seeded successfully. Created {Count} users.", usersCreated);
        }
        else
        {
            _logger.LogInformation("All E2E test users already exist. No changes made.");
        }
    }
}
