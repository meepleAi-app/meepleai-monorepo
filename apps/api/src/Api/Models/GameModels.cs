using System;

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

public record CreateGameRequest(
    string Name,
    string? GameId = null
);

public record GameResponse(
    string Id,
    string Name,
    DateTime CreatedAt
);
