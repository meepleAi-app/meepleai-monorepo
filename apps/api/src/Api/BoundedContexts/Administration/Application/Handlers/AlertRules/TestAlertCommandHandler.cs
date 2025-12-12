using Api.BoundedContexts.Administration.Application.Commands.AlertRules;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.Administration.Application.Handlers.AlertRules;

public class TestAlertCommandHandler : IRequestHandler<TestAlertCommand, bool>
{
    private readonly IAlertingService _alertingService;

    public TestAlertCommandHandler(IAlertingService alertingService) => _alertingService = alertingService;

    public async Task<bool> Handle(TestAlertCommand request, CancellationToken ct)
    {
        try
        {
            await _alertingService.SendAlertAsync(request.AlertType, "Critical", $"Test alert for {request.AlertType} via {request.Channel}", new Dictionary<string, object> { ["test"] = true, ["channel"] = request.Channel }, ct);
            return true;
        }
        catch { return false; }
    }
}
