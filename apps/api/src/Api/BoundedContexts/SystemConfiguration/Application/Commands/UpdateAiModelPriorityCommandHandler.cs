using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Commands;

public sealed class UpdateAiModelPriorityCommandHandler : ICommandHandler<UpdateAiModelPriorityCommand>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly MeepleAiDbContext _db;

    public UpdateAiModelPriorityCommandHandler(
        IAiModelConfigurationRepository repository,
        MeepleAiDbContext db)
    {
        _repository = repository;
        _db = db;
    }

    public async Task Handle(UpdateAiModelPriorityCommand request, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"AI model {request.Id} not found");

        model.UpdatePriority(request.NewPriority);
        await _repository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
