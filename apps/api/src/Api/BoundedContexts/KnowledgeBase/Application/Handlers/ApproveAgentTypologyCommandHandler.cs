using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for ApproveAgentTypologyCommand.
/// Transitions status from Pending to Approved.
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal sealed class ApproveAgentTypologyCommandHandler : IRequestHandler<ApproveAgentTypologyCommand, AgentTypologyDto>
{
    private readonly IAgentTypologyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<ApproveAgentTypologyCommandHandler> _logger;

    public ApproveAgentTypologyCommandHandler(
        IAgentTypologyRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<ApproveAgentTypologyCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AgentTypologyDto> Handle(
        ApproveAgentTypologyCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        try
        {
            // Retrieve typology
            var typology = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

            if (typology == null)
            {
                throw new NotFoundException("AgentTypology", request.Id.ToString());
            }

            // Apply domain logic (Approve method handles state transition)
            typology.Approve(request.ApprovedBy);

            // Persist
            await _repository.UpdateAsync(typology, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Approved agent typology '{Name}' with ID {TypologyId} by user {UserId}",
                typology.Name,
                typology.Id,
                request.ApprovedBy);

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
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent typology {TypologyId} not found", request.Id);
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Cannot approve agent typology {TypologyId}", request.Id);
            throw;
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid approver for agent typology {TypologyId}", request.Id);
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // HANDLER BOUNDARY: COMMAND HANDLER PATTERN - CQRS handler boundary
        // Specific exceptions handled above
        // Generic catch handles unexpected infrastructure failures (DB, network, memory)
        // to prevent exception propagation to API layer.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Error approving agent typology {TypologyId}", request.Id);
            throw;
        }
    }
}
