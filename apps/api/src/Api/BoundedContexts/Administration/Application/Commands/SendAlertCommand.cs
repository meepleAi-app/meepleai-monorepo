using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to send an alert through configured channels (Email, Slack, PagerDuty).
/// </summary>
public record SendAlertCommand(
    string AlertType,
    string Severity,
    string Message,
    Dictionary<string, object>? Metadata = null
) : ICommand<AlertDto>;
