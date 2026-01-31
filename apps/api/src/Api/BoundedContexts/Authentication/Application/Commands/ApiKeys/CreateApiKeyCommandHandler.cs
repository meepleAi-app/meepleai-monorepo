using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Authentication.Application.Commands;

/// <summary>
/// Handles API key creation for a user.
/// </summary>
internal class CreateApiKeyCommandHandler : ICommandHandler<CreateApiKeyCommand, CreateApiKeyResponse>
{
    private readonly IApiKeyRepository _apiKeyRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateApiKeyCommandHandler(
        IApiKeyRepository apiKeyRepository,
        IUnitOfWork unitOfWork)
    {
        _apiKeyRepository = apiKeyRepository ?? throw new ArgumentNullException(nameof(apiKeyRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<CreateApiKeyResponse> Handle(CreateApiKeyCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        // Create API key (returns entity + plaintext key)
        var apiKeyId = Guid.NewGuid();
        var (apiKey, plaintextKey) = ApiKey.Create(
            id: apiKeyId,
            userId: command.UserId,
            keyName: command.KeyName,
            scopes: command.Scopes,
            expiresAt: command.ExpiresAt,
            metadata: command.Metadata
        );

        // Persist API key
        await _apiKeyRepository.AddAsync(apiKey, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Return response with plaintext key (only time it's visible)
        return new CreateApiKeyResponse(
            Id: apiKey.Id,
            KeyName: apiKey.KeyName,
            KeyPrefix: apiKey.KeyPrefix,
            PlaintextKey: plaintextKey,
            Scopes: apiKey.Scopes,
            CreatedAt: apiKey.CreatedAt,
            ExpiresAt: apiKey.ExpiresAt
        );
    }
}
