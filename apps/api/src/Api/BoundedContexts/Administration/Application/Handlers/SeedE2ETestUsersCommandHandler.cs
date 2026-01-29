using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for SeedE2ETestUsersCommand.
/// Creates E2E test users for Playwright tests with predictable credentials.
/// Idempotent: Skips users that already exist.
/// </summary>
internal sealed class SeedE2ETestUsersCommandHandler : ICommandHandler<SeedE2ETestUsersCommand>
{
    private const string TestPassword = "Demo123!";

    private static readonly (string Email, string DisplayName, Role Role)[] TestUsers =
    [
        ("admin@meepleai.dev", "E2E Admin User", Role.Admin),
        ("editor@meepleai.dev", "E2E Editor User", Role.Editor),
        ("user@meepleai.dev", "E2E Regular User", Role.User),
    ];

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeedE2ETestUsersCommandHandler> _logger;

    public SeedE2ETestUsersCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<SeedE2ETestUsersCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedE2ETestUsersCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var passwordHash = PasswordHash.Create(TestPassword);
        var usersCreated = 0;

        foreach (var (emailStr, displayName, role) in TestUsers)
        {
            var email = new Email(emailStr);
            var existingUser = await _userRepository.GetByEmailAsync(email, cancellationToken).ConfigureAwait(false);

            if (existingUser != null)
            {
                _logger.LogInformation("E2E test user already exists: {Email}. Skipping.", emailStr);
                continue;
            }

            var user = new User(
                id: Guid.NewGuid(),
                email: email,
                displayName: displayName,
                passwordHash: passwordHash,
                role: role
            );

            await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
            usersCreated++;

            _logger.LogInformation("E2E test user created: {Email} with role {Role}", emailStr, role);
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
