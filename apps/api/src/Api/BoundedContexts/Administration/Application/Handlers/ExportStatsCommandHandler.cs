using Api.BoundedContexts.Administration.Application.Commands;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for ExportStatsCommand.
/// Delegates to AdminStatsService (service will be refactored in future iterations).
/// </summary>
public class ExportStatsCommandHandler : ICommandHandler<ExportStatsCommand, string>
{
    private readonly AdminStatsService _adminStatsService;

    public ExportStatsCommandHandler(AdminStatsService adminStatsService)
    {
        _adminStatsService = adminStatsService;
    }

    public async Task<string> Handle(ExportStatsCommand command, CancellationToken cancellationToken)
    {
        var request = new ExportDataRequest(
            Format: command.Format,
            FromDate: command.FromDate,
            ToDate: command.ToDate,
            GameId: command.GameId
        );

        return await _adminStatsService.ExportDashboardDataAsync(request, cancellationToken);
    }
}
