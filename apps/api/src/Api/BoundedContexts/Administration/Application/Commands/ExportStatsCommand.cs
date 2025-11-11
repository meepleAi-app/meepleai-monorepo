using MediatR;

namespace Api.BoundedContexts.Administration.Application.Commands;

/// <summary>
/// Command to export dashboard data in CSV or JSON format.
/// </summary>
public record ExportStatsCommand(
    string Format, // "csv" or "json"
    DateTime? FromDate = null,
    DateTime? ToDate = null,
    string? GameId = null
) : IRequest<string>;
