using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

internal class EnableAlertRuleCommandHandler : IRequestHandler<EnableAlertRuleCommand, Unit>
{
    private readonly IAlertRuleRepository _repository;

    public EnableAlertRuleCommandHandler(IAlertRuleRepository repository) => _repository = repository;

    public async Task<Unit> Handle(EnableAlertRuleCommand request, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(request);
        var rule = await _repository.GetByIdAsync(request.Id, ct).ConfigureAwait(false);
        if (rule == null) throw new InvalidOperationException($"AlertRule {request.Id} not found");

        if (rule.IsEnabled) rule.Disable(request.UpdatedBy);
        else rule.Enable(request.UpdatedBy);

        await _repository.UpdateAsync(rule, ct).ConfigureAwait(false);
        return Unit.Value;
    }
}
