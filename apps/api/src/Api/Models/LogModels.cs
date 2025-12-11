using System;

#pragma warning disable MA0048 // File name must match type name - Contains related domain models
namespace Api.Models;

public record LogEntryResponse(
    DateTime Timestamp,
    string Level,
    string Message,
    string? RequestId,
    string? UserId,
    string? GameId
);
