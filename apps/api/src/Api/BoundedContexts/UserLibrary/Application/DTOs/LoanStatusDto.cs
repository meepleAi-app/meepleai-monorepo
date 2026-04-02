namespace Api.BoundedContexts.UserLibrary.Application.DTOs;

internal record LoanStatusDto(
    bool IsOnLoan,
    string? BorrowerInfo,
    DateTime? LoanedSince
);
