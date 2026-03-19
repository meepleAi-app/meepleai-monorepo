using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for CreateAgentTypologyCommand.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal sealed class CreateAgentTypologyCommandHandler : IRequestHandler<CreateAgentTypologyCommand, AgentTypologyDto>
{
    private readonly IAgentTypologyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<CreateAgentTypologyCommandHandler> _logger;

    public CreateAgentTypologyCommandHandler(
        IAgentTypologyRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<CreateAgentTypologyCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentTypologyDto> Handle(
        CreateAgentTypologyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Create default strategy value object
            var defaultStrategy = AgentStrategy.Custom(
                request.DefaultStrategyName,
                request.DefaultStrategyParameters);

            // Create domain entity (starts as Draft)
            var typology = new AgentTypology(
                id: Guid.NewGuid(),
                name: request.Name,
                description: request.Description,
                basePrompt: request.BasePrompt,
                defaultStrategy: defaultStrategy,
                createdBy: request.CreatedBy,
                status: TypologyStatus.Draft);

            // Persist
            await _repository.AddAsync(typology, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Created agent typology '{Name}' with ID {TypologyId} by user {UserId}",
                typology.Name,
                typology.Id,
                request.CreatedBy);

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
            _logger.LogWarning(ex, "Invalid input for CreateAgentTypologyCommand");
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
            _logger.LogError(ex, "Error creating agent typology '{Name}'", request.Name);
            throw;
        }
    }
}
