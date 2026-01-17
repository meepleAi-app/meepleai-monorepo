using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public sealed class ToggleAiModelActiveCommandHandler : ICommandHandler<ToggleAiModelActiveCommand>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly MeepleAiDbContext _db;

    public ToggleAiModelActiveCommandHandler(
        IAiModelConfigurationRepository repository,
        MeepleAiDbContext db)
    {
        _repository = repository;
        _db = db;
    }

    public async Task Handle(ToggleAiModelActiveCommand request, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"AI model {request.Id} not found");

        model.SetActive(request.IsActive);
        await _repository.UpdateAsync(model, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
