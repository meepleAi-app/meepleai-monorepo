using Api.BoundedContexts.Administration.Application.Queries.AlertRules;
using Api.BoundedContexts.Administration.Domain.Aggregates.AlertRules;
using Api.BoundedContexts.Administration.Domain.Repositories;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

internal class GetAllAlertRulesQueryHandler : IRequestHandler<GetAllAlertRulesQuery, List<AlertRuleDto>>
{
    private readonly IAlertRuleRepository _repository;

    public GetAllAlertRulesQueryHandler(IAlertRuleRepository repository) => _repository = repository;

    public async Task<List<AlertRuleDto>> Handle(GetAllAlertRulesQuery request, CancellationToken ct)
    {
        var rules = await _repository.GetAllAsync(ct).ConfigureAwait(false);
        return rules.Select(r => new AlertRuleDto(r.Id, r.Name, r.AlertType, r.Severity.ToDisplayString(), r.Threshold.Value, r.Threshold.Unit, r.Duration.Minutes, r.IsEnabled, r.Description, r.CreatedAt, r.UpdatedAt)).ToList();
    }
}
