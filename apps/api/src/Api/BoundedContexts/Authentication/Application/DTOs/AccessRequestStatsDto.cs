namespace Api.BoundedContexts.Authentication.Application.DTOs;

public record AccessRequestStatsDto(
    int Pending,
    int Approved,
    int Rejected,
    int Total);
