using Api.BoundedContexts.WorkflowIntegration.Application.Commands;
using Api.BoundedContexts.WorkflowIntegration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.WorkflowIntegration.Application.Handlers;

public class DeleteN8nConfigCommandHandler : ICommandHandler<DeleteN8nConfigCommand, bool>
{
    private readonly IN8nConfigurationRepository _repository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteN8nConfigCommandHandler(
        IN8nConfigurationRepository repository,
        IUnitOfWork unitOfWork)
    {
        _repository = repository;
        _unitOfWork = unitOfWork;
    }

    public async Task<bool> Handle(DeleteN8nConfigCommand command, CancellationToken cancellationToken)
    {
        var config = await _repository.GetByIdAsync(command.ConfigId, cancellationToken);
        if (config == null)
            return false;

        await _repository.DeleteAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
