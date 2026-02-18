using Api.BoundedContexts.Administration.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Budget;

/// <summary>
/// Query to get user's credit budget status
/// Self-access only (users can only query their own budget)
/// </summary>
public sealed record GetUserBudgetQuery(Guid UserId) : IQuery<UserBudgetStatus>;

/// <summary>
/// Handler for GetUserBudgetQuery with HybridCache integration
/// </summary>
internal sealed class GetUserBudgetQueryHandler : IQueryHandler<GetUserBudgetQuery, UserBudgetStatus>
{
    private readonly IUserBudgetService _budgetService;
    private readonly ILogger<GetUserBudgetQueryHandler> _logger;

    public GetUserBudgetQueryHandler(
        IUserBudgetService budgetService,
        ILogger<GetUserBudgetQueryHandler> logger)
    {
        _budgetService = budgetService ?? throw new ArgumentNullException(nameof(budgetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<UserBudgetStatus> Handle(GetUserBudgetQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Retrieving budget status for user {UserId}", query.UserId);

        return await _budgetService.GetUserBudgetAsync(query.UserId, cancellationToken).ConfigureAwait(false);
    }
}
