using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

internal class DeleteAlertRuleCommandHandler : IRequestHandler<DeleteAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _repository;

    public DeleteAlertRuleCommandHandler(IAlertRuleRepository repository) =>
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));

    public async Task<Unit> Handle(DeleteAlertRuleCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        await _repository.DeleteAsync(request.Id, cancellationToken).ConfigureAwait(false);
        return Unit.Value;
    }
}

