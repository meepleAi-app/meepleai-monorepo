using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

internal class DeleteN8NConfigCommandHandler : ICommandHandler<DeleteN8NConfigCommand, bool>
{
    private readonly IN8NConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteN8NConfigCommandHandler(
        IN8NConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _repository = repository;
        ArgumentNullException.ThrowIfNull(unitOfWork);
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteN8NConfigCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var config = await _repository.GetByIdAsync(command.ConfigId, cancellationToken).ConfigureAwait(false);
        if (config == null)
            return false;

        await _repository.DeleteAsync(config, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return true;
    }
}
