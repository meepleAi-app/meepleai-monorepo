using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.SharedKernel.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Security;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for SeedTestUserCommand.
/// Creates demo test user (Test@meepleai.com) with password from SEED_TEST_PASSWORD secret.
/// Idempotent: Only executes if test user doesn't exist.
/// </summary>
internal sealed class SeedTestUserCommandHandler : ICommandHandler<SeedTestUserCommand>
{
    private const string TestEmail = "Test@meepleai.com";
    private const string TestDisplayName = "Test User";

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedTestUserCommandHandler> _logger;

    public SeedTestUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SeedTestUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedTestUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var testPassword = SecretsHelper.GetSeedTestPassword(_configuration, _logger);
        if (string.IsNullOrWhiteSpace(testPassword))
        {
            _logger.LogWarning("SEED_TEST_PASSWORD not configured — skipping test user seed");
            return;
        }
        if (testPassword.Length < 8)
        {
            _logger.LogWarning("SEED_TEST_PASSWORD is too short (min 8 chars) — skipping test user seed");
            return;
        }

        var testEmailValue = new Email(TestEmail);
        var existingUser = await _userRepository.GetByEmailAsync(testEmailValue, cancellationToken).ConfigureAwait(false);
        if (existingUser != null)
        {
            _logger.LogInformation("Test user already exists. Skipping seed.");
            return;
        }

        var testUser = new User(
            id: Guid.NewGuid(),
            email: new Email(TestEmail),
            displayName: TestDisplayName,
            passwordHash: PasswordHash.Create(testPassword),
            role: Role.User
        );

        await _userRepository.AddAsync(testUser, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Test user seeded successfully: {Email}", DataMasking.MaskEmail(TestEmail));
    }
}
