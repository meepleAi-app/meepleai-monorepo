using Api.BoundedContexts.Administration.Domain.Aggregates.AlertChannels;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Commands.AlertChannels;

internal sealed class UpsertAlertChannelCommandHandler
    : IRequestHandler<UpsertAlertChannelCommand, AlertChannelUpsertResult>
{
    private readonly IAlertChannelRepository _repository;

    public UpsertAlertChannelCommandHandler(IAlertChannelRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<AlertChannelUpsertResult> Handle(
        UpsertAlertChannelCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var type = AlertChannelTypeExtensions.FromWireValue(request.Type);
        var existing = await _repository.GetByTypeAsync(type, cancellationToken).ConfigureAwait(false);

        AlertChannel aggregate;
        if (existing is null)
        {
            aggregate = AlertChannel.Create(type, request.ConfigJson, request.IsEnabled, request.UpdatedBy);
        }
        else
        {
            // Carry the RowVersion supplied by the client back into the aggregate
            // so the repository can detect a concurrent update via Entry.OriginalValues.
            existing.UpdateConfig(request.ConfigJson, request.IsEnabled, request.UpdatedBy);
            if (!string.IsNullOrEmpty(request.RowVersion))
            {
                var tokenBytes = TryDecodeRowVersion(request.RowVersion);
                if (tokenBytes is not null)
                {
                    // We reconstitute a copy with the client-supplied token —
                    // necessary because UpdateConfig doesn't touch RowVersion.
                    existing = AlertChannel.Reconstitute(
                        existing.Type,
                        existing.ConfigJson,
                        existing.IsEnabled,
                        existing.LastTestedAt,
                        existing.LastTestStatus,
                        existing.LastTestMessage,
                        existing.CreatedAt,
                        existing.UpdatedAt,
                        existing.CreatedBy,
                        existing.UpdatedBy,
                        tokenBytes);
                }
            }
            aggregate = existing;
        }

        try
        {
            await _repository.UpsertAsync(aggregate, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            throw new ConflictException(
                $"Alert channel '{request.Type}' was modified by another admin. Reload and retry.",
                ex);
        }

        // Re-fetch to surface the freshly-bumped RowVersion to the client.
        var refreshed = await _repository.GetByTypeAsync(type, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("AlertChannel", request.Type);

        return new AlertChannelUpsertResult(
            Type: request.Type.ToLowerInvariant(),
            UpdatedAt: refreshed.UpdatedAt,
            RowVersion: Convert.ToBase64String(refreshed.RowVersion ?? Array.Empty<byte>()));
    }

    private static byte[]? TryDecodeRowVersion(string base64)
    {
        try
        {
            return Convert.FromBase64String(base64);
        }
        catch (FormatException)
        {
            return null;
        }
    }
}
