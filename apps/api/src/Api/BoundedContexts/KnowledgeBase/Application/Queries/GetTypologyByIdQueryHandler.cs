using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetTypologyByIdQuery with role-based visibility.
/// Issue #3178: AGT-004 Typology Query Handlers.
/// </summary>
internal sealed class GetTypologyByIdQueryHandler : IRequestHandler<GetTypologyByIdQuery, AgentTypologyDto?>
{
    private readonly IAgentTypologyRepository _repository;

    public GetTypologyByIdQueryHandler(IAgentTypologyRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<AgentTypologyDto?> Handle(
        GetTypologyByIdQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var typology = await _repository.GetByIdAsync(request.TypologyId, cancellationToken).ConfigureAwait(false);

        if (typology == null)
        {
            throw new NotFoundException($"Agent typology not found: {request.TypologyId}");
        }

        // Check visibility based on role
        var canView = request.UserRole.ToUpperInvariant() switch
        {
            "ADMIN" => true, // Admin sees all
            "EDITOR" => typology.Status == TypologyStatus.Approved || // All approved
                        typology.CreatedBy == request.UserId, // Own drafts/pending
            _ => typology.Status == TypologyStatus.Approved // User sees approved only
        };

        if (!canView)
        {
            throw new UnauthorizedAccessException("You do not have permission to view this typology");
        }

        // Map to DTO
        return new AgentTypologyDto(
            Id: typology.Id,
            Name: typology.Name,
            Description: typology.Description,
            BasePrompt: typology.BasePrompt,
            DefaultStrategyName: typology.DefaultStrategy.Name,
            DefaultStrategyParameters: typology.DefaultStrategy.Parameters,
            Status: typology.Status.ToString(),
            CreatedBy: typology.CreatedBy,
            ApprovedBy: typology.ApprovedBy,
            CreatedAt: typology.CreatedAt,
            ApprovedAt: typology.ApprovedAt,
            IsDeleted: typology.IsDeleted
        );
    }
}
