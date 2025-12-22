namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Result of PDF processing cancellation command.
/// </summary>
/// <param name="Success">Whether the cancellation was successful</param>
/// <param name="Message">Result message for the client</param>
/// <param name="ErrorCode">Optional error code if cancellation failed</param>
internal record CancelProcessingResult(
    bool Success,
    string Message,
    string? ErrorCode = null
);
