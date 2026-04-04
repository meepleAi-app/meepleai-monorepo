using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

/// <summary>
/// Handler for deleting an AI model configuration
/// </summary>
/// <remarks>
/// Issue #2567: DELETE /api/v1/admin/ai-models/{id} endpoint handler
/// Note: Hard delete. Primary models cannot be deleted.
/// </remarks>
internal sealed class DeleteAiModelCommandHandler : ICommandHandler<DeleteAiModelCommand, bool>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteAiModelCommandHandler(
        IAiModelConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<bool> Handle(DeleteAiModelCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var model = await _repository.GetByIdAsync(command.Id, cancellationToken).ConfigureAwait(false);

        if (model is null)
        {
            throw new NotFoundException("AiModel", command.Id.ToString());
        }

        // Business rule: Cannot delete primary model
        if (model.IsPrimary)
        {
            throw new ConflictException("Cannot delete primary AI model. Set another model as primary first.");
        }

        await _repository.DeleteAsync(model, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
