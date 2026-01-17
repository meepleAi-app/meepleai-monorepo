using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Handlers;

public sealed class DeleteAiModelConfigCommandHandler : ICommandHandler<DeleteAiModelConfigCommand>
{
    private readonly IAiModelConfigurationRepository _repository;
    private readonly MeepleAiDbContext _db;

    public DeleteAiModelConfigCommandHandler(
        IAiModelConfigurationRepository repository,
        MeepleAiDbContext db)
    {
        _repository = repository;
        _db = db;
    }

    public async Task Handle(DeleteAiModelConfigCommand request, CancellationToken cancellationToken)
    {
        var model = await _repository.GetByIdAsync(request.Id, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"AI model {request.Id} not found");

        await _repository.DeleteAsync(model, cancellationToken).ConfigureAwait(false);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }
}
