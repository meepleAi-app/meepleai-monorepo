using System;

namespace Api.Models;

public record LogEntryResponse(
    DateTime Timestamp,
    string Level,
    string Message,
    string? RequestId,
    string? UserId,
    string? GameId
);
