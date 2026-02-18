using Api.BoundedContexts.Administration.Application.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Budget;

/// <summary>
/// Query to get admin budget overview (OpenRouter + App budgets)
/// Admin-only access
/// </summary>
public sealed record GetAdminBudgetOverviewQuery : IQuery<AdminBudgetOverview>;

/// <summary>
/// Handler for GetAdminBudgetOverviewQuery with caching
/// </summary>
internal sealed class GetAdminBudgetOverviewQueryHandler : IQueryHandler<GetAdminBudgetOverviewQuery, AdminBudgetOverview>
{
    private readonly IAdminBudgetService _adminBudgetService;
    private readonly ILogger<GetAdminBudgetOverviewQueryHandler> _logger;

    public GetAdminBudgetOverviewQueryHandler(
        IAdminBudgetService adminBudgetService,
        ILogger<GetAdminBudgetOverviewQueryHandler> logger)
    {
        _adminBudgetService = adminBudgetService ?? throw new ArgumentNullException(nameof(adminBudgetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AdminBudgetOverview> Handle(GetAdminBudgetOverviewQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation("Admin retrieving budget overview");

        return await _adminBudgetService.GetBudgetOverviewAsync(cancellationToken).ConfigureAwait(false);
    }
}
