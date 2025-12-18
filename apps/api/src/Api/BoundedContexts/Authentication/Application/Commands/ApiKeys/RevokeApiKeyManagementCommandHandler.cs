using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Commands;

internal class RevokeApiKeyManagementCommandHandler : ICommandHandler<RevokeApiKeyManagementCommand, bool>
{
    private readonly MeepleAiDbContext _db;
    private readonly ApiKeyAuthenticationService _authService;
    private readonly ILogger<RevokeApiKeyManagementCommandHandler> _logger;

    public RevokeApiKeyManagementCommandHandler(
        MeepleAiDbContext db,
        ApiKeyAuthenticationService authService,
        ILogger<RevokeApiKeyManagementCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(RevokeApiKeyManagementCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        if (!Guid.TryParse(command.KeyId, out var keyGuid) || !Guid.TryParse(command.UserId, out var userGuid))
        {
            return false;
        }

        // Check that the user owns the key before revoking
        var apiKey = await _db.ApiKeys.AsNoTracking().FirstOrDefaultAsync(k => k.Id == keyGuid && k.UserId == userGuid, cancellationToken).ConfigureAwait(false);
        if (apiKey == null)
        {
            _logger.LogWarning("API key revocation failed: key not found or unauthorized. KeyId: {KeyId}, UserId: {UserId}", command.KeyId, command.UserId);
            return false;
        }

        return await _authService.RevokeApiKeyAsync(command.KeyId, command.UserId, cancellationToken).ConfigureAwait(false);
    }
}
