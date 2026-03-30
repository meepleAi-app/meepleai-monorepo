using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for ProposeAgentTypologyCommand.
/// Issue #3177: AGT-003 Editor Proposal Commands.
/// </summary>
internal sealed class ProposeAgentTypologyCommandHandler : IRequestHandler<ProposeAgentTypologyCommand, AgentTypologyDto>
{
    private readonly IAgentTypologyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ProposeAgentTypologyCommandHandler> _logger;

    public ProposeAgentTypologyCommandHandler(
        IAgentTypologyRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ProposeAgentTypologyCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentTypologyDto> Handle(
        ProposeAgentTypologyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Create default strategy value object
            var defaultStrategy = AgentStrategy.HybridSearch();

            // Create domain entity (Editor proposals always start as Draft)
            var typology = new AgentTypology(
                id: Guid.NewGuid(),
                name: request.Name,
                description: request.Description,
                basePrompt: request.BasePrompt,
                defaultStrategy: defaultStrategy,
                createdBy: request.ProposedBy,
                status: TypologyStatus.Draft);

            // Persist
            await _repository.AddAsync(typology, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Editor proposed agent typology '{Name}' with ID {TypologyId} by user {UserId}",
                typology.Name,
                typology.Id,
                request.ProposedBy);

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
                IsDeleted: typology.IsDeleted);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid input for ProposeAgentTypologyCommand");
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific ArgumentException is handled above
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Error proposing agent typology '{Name}'", request.Name);
            throw;
        }
    }
}
