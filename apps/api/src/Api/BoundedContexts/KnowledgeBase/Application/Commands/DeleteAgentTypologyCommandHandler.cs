using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for DeleteAgentTypologyCommand.
/// Performs soft delete (sets IsDeleted=true).
/// Issue #3176: AGT-002 Typology CRUD Commands.
/// </summary>
internal sealed class DeleteAgentTypologyCommandHandler : IRequestHandler<DeleteAgentTypologyCommand>
{
    private readonly IAgentTypologyRepository _repository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteAgentTypologyCommandHandler> _logger;

    public DeleteAgentTypologyCommandHandler(
        IAgentTypologyRepository repository,
        IUnitOfWork unitOfWork,
        ILogger<DeleteAgentTypologyCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Handle(
        DeleteAgentTypologyCommand request,
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

            // Apply domain logic (soft delete)
            typology.Delete();

            // Persist
            await _repository.UpdateAsync(typology, cancellationToken).ConfigureAwait(false);
            await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Soft deleted agent typology '{Name}' with ID {TypologyId}",
                typology.Name,
                typology.Id);
        }
        catch (NotFoundException ex)
        {
            _logger.LogWarning(ex, "Agent typology {TypologyId} not found", request.Id);
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
            _logger.LogError(ex, "Error deleting agent typology {TypologyId}", request.Id);
            throw;
        }
    }
}
