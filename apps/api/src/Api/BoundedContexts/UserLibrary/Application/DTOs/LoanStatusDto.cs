namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

/// <summary>
/// DTO representing the loan status of a game in the user's library.
/// </summary>
internal record LoanStatusDto(
    bool IsOnLoan,
    string? BorrowerInfo,
    DateTime? LoanedSince
);
