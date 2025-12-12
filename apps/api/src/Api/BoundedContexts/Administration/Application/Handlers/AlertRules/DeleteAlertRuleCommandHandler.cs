using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

public class DeleteAlertRuleCommandHandler : IRequestHandler<DeleteAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _repository;

    public DeleteAlertRuleCommandHandler(IAlertRuleRepository repository) => _repository = repository;

    public async Task<Unit> Handle(DeleteAlertRuleCommand request, CancellationToken ct)
    {
        await _repository.DeleteAsync(request.Id, ct);
        return Unit.Value;
    }
}
