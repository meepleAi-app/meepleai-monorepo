using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public class DeleteConfigurationCommandHandler : ICommandHandler<DeleteConfigurationCommand, bool>
{
    private readonly IConfigurationRepository _configurationRepository;
    private readonly IUnitOfWork _unitOfWork;

    public DeleteConfigurationCommandHandler(
        IConfigurationRepository configurationRepository,
        IUnitOfWork unitOfWork)
    {
        _configurationRepository = configurationRepository ?? throw new ArgumentNullException(nameof(configurationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<bool> Handle(DeleteConfigurationCommand command, CancellationToken cancellationToken)
    {
        var config = await _configurationRepository.GetByIdAsync(command.ConfigId, cancellationToken);

        if (config == null)
            return false;

        // Mark as deleted to raise domain event
        config.MarkAsDeleted();

        await _configurationRepository.DeleteAsync(config, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        return true;
    }
}
