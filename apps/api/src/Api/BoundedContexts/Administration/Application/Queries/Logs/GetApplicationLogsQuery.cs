using Api.BoundedContexts.Administration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048
namespace Api.BoundedContexts.Administration.Application.Queries.Logs;

internal record GetApplicationLogsQuery(
    string? Search, string? Level, string? Source, string? CorrelationId,
    DateTime? From, DateTime? To, int Count = 50, string? AfterId = null
) : IQuery<GetApplicationLogsResponse>;

public sealed record GetApplicationLogsResponse(
    IReadOnlyList<ApplicationLogDto> Items,
    int? RemainingCount,
    string? LastId);
