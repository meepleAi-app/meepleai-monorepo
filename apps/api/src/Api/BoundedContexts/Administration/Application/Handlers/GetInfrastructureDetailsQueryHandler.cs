using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Issue #894: Handler for infrastructure details query.
/// Delegates to IInfrastructureDetailsService for orchestration.
/// </summary>
public class GetInfrastructureDetailsQueryHandler : IRequestHandler<GetInfrastructureDetailsQuery, InfrastructureDetails>
{
    private readonly IInfrastructureDetailsService _detailsService;
    private readonly ILogger<GetInfrastructureDetailsQueryHandler> _logger;

    public GetInfrastructureDetailsQueryHandler(
        IInfrastructureDetailsService detailsService,
        ILogger<GetInfrastructureDetailsQueryHandler> logger)
    {
        _detailsService = detailsService ?? throw new ArgumentNullException(nameof(detailsService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<InfrastructureDetails> Handle(
        GetInfrastructureDetailsQuery request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("Handling GetInfrastructureDetailsQuery");

        try
        {
            var details = await _detailsService.GetDetailsAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Infrastructure details retrieved successfully. Overall state: {State}",
                details.Overall.State);

            return details;
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // HANDLER PATTERN: Log details query failures before propagating.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to handle GetInfrastructureDetailsQuery");
            throw;
        }
#pragma warning restore S2139
    }
}
