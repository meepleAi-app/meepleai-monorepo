using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderCredentials;
using Api.BoundedContexts.Administration.Domain.Aggregates.ProviderProbeAudit;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.Administration.Infrastructure.Services;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.Middleware.Exceptions;
using Api.Services.Providers.Probe;
using MediatR;
using Microsoft.AspNetCore.DataProtection;

namespace Api.BoundedContexts.Administration.Application.Commands.Providers;

/// <summary>
/// Handles <see cref="RotateProviderKeyCommand"/>. Flow:
/// <list type="number">
///   <item>Authorise: load the requesting user, reject anything other than superadmin.</item>
///   <item>Rate-limit guard: reject if a rotation for this provider happened &lt; 24h ago.</item>
///   <item>Pre-flight probe: validate the new key against the provider before persisting; on
///         non-<see cref="ProbeOutcome.Success"/> outcome throw
///         <see cref="ProviderProbeFailedException"/> (HTTP 502) without touching the DB.</item>
///   <item>Encrypt the plaintext key via <see cref="IDataProtector"/> purpose
///         <c>"ProviderCredentials"</c> and derive the masked <see cref="KeyFingerprint"/>.</item>
///   <item>Deactivate the currently active credential row (if any), insert the new active row
///         linked to the previous one — atomic via <c>[AtomicAudit]</c> on the command.</item>
///   <item><c>SaveChangesAsync</c> commits and dispatches the
///         <see cref="Domain.Aggregates.ProviderCredentials.Events.ProviderKeyRotatedEvent"/>
///         raised by the aggregate factory; the event handler publishes a Redis pub/sub message
///         so every pod invalidates its <see cref="IProviderCredentialResolver"/> cache.</item>
/// </list>
/// Issue #1859.
/// </summary>
internal sealed class RotateProviderKeyCommandHandler
    : IRequestHandler<RotateProviderKeyCommand, RotateProviderKeyResponseDto>
{
    internal static readonly TimeSpan RotationCooldown = TimeSpan.FromHours(24);
    internal const string DataProtectionPurpose = "ProviderCredentials";

    private readonly IProviderCredentialRepository _repository;
    private readonly IUserRepository _userRepository;
    private readonly IProviderProbeExecutorFactory _probeFactory;
    private readonly IDataProtectionProvider _protectionProvider;
    private readonly TimeProvider _timeProvider;
    private readonly ILogger<RotateProviderKeyCommandHandler> _logger;

    public RotateProviderKeyCommandHandler(
        IProviderCredentialRepository repository,
        IUserRepository userRepository,
        IProviderProbeExecutorFactory probeFactory,
        IDataProtectionProvider protectionProvider,
        TimeProvider timeProvider,
        ILogger<RotateProviderKeyCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
        _probeFactory = probeFactory ?? throw new ArgumentNullException(nameof(probeFactory));
        _protectionProvider = protectionProvider ?? throw new ArgumentNullException(nameof(protectionProvider));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<RotateProviderKeyResponseDto> Handle(
        RotateProviderKeyCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var providerName = ProviderName.Create(command.ProviderName);

        // 1. Authz: superadmin only.
        var requester = await _userRepository
            .GetByIdAsync(command.RequestingUserId, cancellationToken)
            .ConfigureAwait(false);

        if (requester is null)
        {
            throw new NotFoundException("User", command.RequestingUserId.ToString());
        }

        if (!requester.Role.IsSuperAdmin())
        {
            _logger.LogWarning(
                "SECURITY: non-superadmin {UserId} (role={Role}) attempted to rotate provider key '{Provider}'",
                command.RequestingUserId, requester.Role.Value, providerName.Value);
            throw new ForbiddenException("Only superadmins can rotate provider keys");
        }

        // 2. Rate-limit guard (1/24h per provider).
        var lastRotation = await _repository
            .GetLastRotationAsync(providerName.Value, cancellationToken)
            .ConfigureAwait(false);
        var now = _timeProvider.GetUtcNow().UtcDateTime;
        if (lastRotation is not null && (now - lastRotation.RotatedAt) < RotationCooldown)
        {
            var nextAllowed = lastRotation.RotatedAt.Add(RotationCooldown);
            throw new ConflictException(
                $"Provider '{providerName.Value}' was rotated less than 24h ago. Next allowed at {nextAllowed:O}");
        }

        // 3. Probe new key (pre-flight). Use the explicit-key overload — we don't want to read
        //    the env var: we're validating the candidate key, not the currently configured one.
        // CLAUDE.md issue #2568: never throw InvalidOperationException (HTTP 500); map to
        // ProviderCredentialNotConfiguredException (HTTP 503) — the allowed-provider whitelist
        // (ProviderName.Allowed) means this branch is only reachable on a DI misconfiguration,
        // semantically equivalent to "the credential infrastructure is not configured".
        var probeExecutor = _probeFactory.GetExecutor(providerName.Value)
            ?? throw new ProviderCredentialNotConfiguredException(providerName.Value);
        var probeResult = await probeExecutor
            .ExecuteAsync(command.ApiKey, expectedModel: null, cancellationToken)
            .ConfigureAwait(false);

        if (probeResult.Outcome != ProbeOutcome.Success)
        {
            var reason = probeResult.ErrorMessage ?? probeResult.ErrorCode ?? "unknown";
            _logger.LogWarning(
                "Probe failed for provider '{Provider}' with new key — rotation aborted. Outcome: {Outcome}, Reason: {Reason}",
                providerName.Value, probeResult.Outcome, reason);
            throw new ProviderProbeFailedException(providerName.Value, reason);
        }

        // 4. Encrypt the key + derive masked fingerprint.
        var protector = _protectionProvider.CreateProtector(DataProtectionPurpose);
        var ciphertext = protector.Protect(command.ApiKey);
        var fingerprint = KeyFingerprint.FromPlaintext(command.ApiKey);

        // 5. Deactivate previous active row + insert new (atomic via [AtomicAudit]).
        var activePrevious = await _repository
            .GetActiveAsync(providerName.Value, cancellationToken)
            .ConfigureAwait(false);
        activePrevious?.Deactivate(_timeProvider);

        var newCredential = ProviderCredential.Create(
            providerName,
            ciphertext,
            fingerprint,
            command.RequestingUserId,
            activePrevious?.Id,
            _timeProvider);

        await _repository.AddAsync(newCredential, cancellationToken).ConfigureAwait(false);
        await _repository.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Provider '{Provider}' key rotated by superadmin {UserId}. New fingerprint: {Fingerprint}",
            providerName.Value, command.RequestingUserId, fingerprint.Value);

        return new RotateProviderKeyResponseDto(
            ProviderName: providerName.Value,
            NewKeyFingerprint: fingerprint.Value,
            RotatedAt: newCredential.RotatedAt,
            PreviousKeyDisabledAt: newCredential.RotatedAt);
    }
}
