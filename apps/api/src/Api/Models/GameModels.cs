using System;

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

internal record CreateGameRequest(
    string Name,
    string? GameId = null
);

internal record GameResponse(
    string Id,
    string Name,
    DateTime CreatedAt
);
