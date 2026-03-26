using Api.BoundedContexts.Administration.Application.Commands;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Handler for ExportStatsCommand.
/// Delegates to AdminStatsService (service will be refactored in future iterations).
/// </summary>
internal class ExportStatsCommandHandler : ICommandHandler<ExportStatsCommand, string>
{
    private readonly IAdminStatsService _adminStatsService;

    public ExportStatsCommandHandler(IAdminStatsService adminStatsService)
    {
        _adminStatsService = adminStatsService ?? throw new ArgumentNullException(nameof(adminStatsService));
    }

    public async Task<string> Handle(ExportStatsCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var request = new ExportDataRequest(
            Format: command.Format,
            FromDate: command.FromDate,
            ToDate: command.ToDate,
            GameId: command.GameId
        );

        return await _adminStatsService.ExportDashboardDataAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
