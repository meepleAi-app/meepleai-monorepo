using Api.BoundedContexts.Administration.Application.Queries.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

public class GetAlertRuleByIdQueryHandler : IRequestHandler<GetAlertRuleByIdQuery, AlertRuleDto?>
{
    private readonly IAlertRuleRepository _repository;

    public GetAlertRuleByIdQueryHandler(IAlertRuleRepository repository) => _repository = repository;

    public async Task<AlertRuleDto?> Handle(GetAlertRuleByIdQuery request, CancellationToken ct)
    {
        var rule = await _repository.GetByIdAsync(request.Id, ct).ConfigureAwait(false);
        return rule == null ? null : new AlertRuleDto(rule.Id, rule.Name, rule.AlertType, rule.Severity.ToDisplayString(), rule.Threshold.Value, rule.Threshold.Unit, rule.Duration.Minutes, rule.IsEnabled, rule.Description, rule.CreatedAt, rule.UpdatedAt);
    }
}
