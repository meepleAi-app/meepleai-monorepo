using System;

namespace Api.Models;

public record CreateGameRequest(
    string TenantId,
    string Name,
    string? GameId = null
);

public record GameResponse(
    string Id,
    string Name,
    DateTime CreatedAt
);
