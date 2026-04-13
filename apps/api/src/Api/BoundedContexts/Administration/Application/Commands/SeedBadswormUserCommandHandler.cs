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
/// Handler for SeedBadswormUserCommand.
/// Creates demo user (badsworm@alice.it) with password from SEED_BADSWORM_PASSWORD secret.
/// Idempotent: Only executes if user doesn't exist.
/// Runs in all environments (Dev, Staging, Prod) when SEED_BADSWORM_PASSWORD secret is present.
/// Remove or leave the secret absent in environments where this account should not exist.
/// </summary>
internal sealed class SeedBadswormUserCommandHandler : ICommandHandler<SeedBadswormUserCommand>
{
    private const string BadswormEmail = "badsworm@alice.it";
    private const string BadswormDisplayName = "Badsworm";

    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SeedBadswormUserCommandHandler> _logger;

    public SeedBadswormUserCommandHandler(
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IConfiguration configuration,
        ILogger<SeedBadswormUserCommandHandler> logger)
    {
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(SeedBadswormUserCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var password = SecretsHelper.GetSeedBadswormPassword(_configuration, _logger);
        if (string.IsNullOrWhiteSpace(password))
        {
            _logger.LogWarning("SEED_BADSWORM_PASSWORD not configured — skipping badsworm user seed");
            return;
        }
        if (password.Length < 8)
        {
            _logger.LogWarning("SEED_BADSWORM_PASSWORD is too short (min 8 chars) — skipping badsworm user seed");
            return;
        }

        var emailValue = new Email(BadswormEmail);
        var existingUser = await _userRepository.GetByEmailAsync(emailValue, cancellationToken).ConfigureAwait(false);
        if (existingUser != null)
        {
            _logger.LogInformation("Badsworm user already exists. Skipping seed.");
            return;
        }

        var user = new User(
            id: Guid.NewGuid(),
            email: emailValue,
            displayName: BadswormDisplayName,
            passwordHash: PasswordHash.Create(password),
            role: Role.User
        );
        user.VerifyEmail(); // Developer account: pre-verified, no email flow required

        await _userRepository.AddAsync(user, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Badsworm user seeded successfully: {Email}", DataMasking.MaskEmail(BadswormEmail));
    }
}
