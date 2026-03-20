using Api.BoundedContexts.Administration.Application.Commands;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SeedTestUserCommand.
/// Creates demo test user (Test@meepleai.com / Demo123!).
/// Idempotent: Only executes if test user doesn't exist.
/// </summary>
internal sealed class SeedTestUserCommandHandler : ICommandHandler<SeedTestUserCommand>
{
    private const string TestEmail = "Test@meepleai.com";
    private const string TestPassword = "Demo123!";
    private const string TestDisplayName = "Test User";

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<SeedTestUserCommandHandler> _logger;

    public SeedTestUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        ILogger<SeedTestUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedTestUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check idempotency: Skip if test user already exists
        var testEmailValue = new Email(TestEmail);
        var existingUser = await _userRepository.GetByEmailAsync(testEmailValue, cancellationToken).ConfigureAwait(false);

        if (existingUser != null)
        {
            _logger.LogInformation("Test user already exists. Skipping seed.");
            return;
        }

        // Create domain objects
        var email = new Email(TestEmail);
        var passwordHash = PasswordHash.Create(TestPassword);
        var role = Role.User;

        // Create test user
        var testUser = new User(
            id: Guid.NewGuid(),
            email: email,
            displayName: TestDisplayName,
            passwordHash: passwordHash,
            role: role
        );

        // Persist
        await _userRepository.AddAsync(testUser, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Test user seeded successfully: {Email}", TestEmail);
    }
}
