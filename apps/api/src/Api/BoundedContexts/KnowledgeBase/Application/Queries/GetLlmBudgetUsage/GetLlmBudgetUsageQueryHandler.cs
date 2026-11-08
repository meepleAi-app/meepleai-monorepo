using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetLlmBudgetUsage;

/// <summary>
/// #4169: legge il consumo da <see cref="ILlmBudgetStatus"/>, cioè dal gate del gateway LLM, e non
/// dal registro costi: così la pagina mostra la spesa con cui il gate decide, comprese le chiamate
/// in corso.
/// </summary>
internal sealed class GetLlmBudgetUsageQueryHandler : IQueryHandler<GetLlmBudgetUsageQuery, LlmBudgetUsageDto>
{
    private readonly ILlmBudgetStatus _budgetStatus;

    public GetLlmBudgetUsageQueryHandler(ILlmBudgetStatus budgetStatus)
    {
        _budgetStatus = budgetStatus ?? throw new ArgumentNullException(nameof(budgetStatus));
    }

    public async Task<LlmBudgetUsageDto> Handle(GetLlmBudgetUsageQuery request, CancellationToken cancellationToken)
    {
        var usage = await _budgetStatus.GetUsageAsync(cancellationToken).ConfigureAwait(false);
        return usage is null
            ? new LlmBudgetUsageDto(Available: false, Day: null, Today: null, Month: null, IsDegraded: false)
            : new LlmBudgetUsageDto(Available: true, usage.Day, usage.Today, usage.Month, usage.IsDegraded);
    }
}
