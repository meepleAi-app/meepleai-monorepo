using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Commands;

internal class DeleteApiKeyCommandHandler : ICommandHandler<DeleteApiKeyCommand, bool>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<DeleteApiKeyCommandHandler> _logger;

    public DeleteApiKeyCommandHandler(
        MeepleAiDbContext db,
        ILogger<DeleteApiKeyCommandHandler> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<bool> Handle(DeleteApiKeyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(command);
        if (!Guid.TryParse(command.KeyId, out var keyGuid))
        {
            return false;
        }

        var apiKey = await _db.ApiKeys.FirstOrDefaultAsync(k => k.Id == keyGuid, cancellationToken).ConfigureAwait(false);
        if (apiKey == null)
        {
            _logger.LogWarning("API key deletion failed: key not found. KeyId: {KeyId}", command.KeyId);
            return false;
        }

        _db.ApiKeys.Remove(apiKey);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("API key deleted. KeyId: {KeyId}, DeletedBy: {DeletedBy}", command.KeyId, command.UserId);

        return true;
    }
}
