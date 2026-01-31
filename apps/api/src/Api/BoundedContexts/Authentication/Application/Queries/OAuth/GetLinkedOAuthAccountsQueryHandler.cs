using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Authentication.Application.Queries.OAuth;

/// <summary>
/// Handles retrieval of all OAuth accounts linked to a user.
/// Maps domain entities to DTOs with status information.
/// </summary>
internal sealed class GetLinkedOAuthAccountsQueryHandler : IQueryHandler<GetLinkedOAuthAccountsQuery, GetLinkedOAuthAccountsResult>
{
    private readonly IOAuthAccountRepository _oauthAccountRepository;
    private readonly ILogger<GetLinkedOAuthAccountsQueryHandler> _logger;

    public GetLinkedOAuthAccountsQueryHandler(
        IOAuthAccountRepository oauthAccountRepository,
        ILogger<GetLinkedOAuthAccountsQueryHandler> logger)
    {
        _oauthAccountRepository = oauthAccountRepository ?? throw new ArgumentNullException(nameof(oauthAccountRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetLinkedOAuthAccountsResult> Handle(GetLinkedOAuthAccountsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        try
        {
            // Get all OAuth accounts for user
            var accounts = await _oauthAccountRepository.GetByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

            // Map domain entities to DTOs
            var accountDtos = accounts.Select(MapToDto).ToList();

            _logger.LogInformation("Retrieved {Count} OAuth accounts for user {UserId}", accountDtos.Count, query.UserId);

            return new GetLinkedOAuthAccountsResult
            {
                Accounts = accountDtos
            };
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: QUERY HANDLER PATTERN - CQRS query boundary
        // Generic catch handles unexpected infrastructure failures (DB, network)
        // to prevent exception propagation to API layer. Returns empty result on failure.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving OAuth accounts for user {UserId}", query.UserId);
            return new GetLinkedOAuthAccountsResult
            {
                Accounts = new List<OAuthAccountDto>()
            };
        }
#pragma warning restore CA1031
    }

    private static OAuthAccountDto MapToDto(OAuthAccount account)
    {
        return new OAuthAccountDto
        {
            Id = account.Id,
            Provider = account.Provider,
            ProviderUserId = account.ProviderUserId,
            IsTokenExpired = account.IsTokenExpired(),
            SupportsRefresh = account.SupportsRefresh(),
            CreatedAt = account.CreatedAt
        };
    }
}
