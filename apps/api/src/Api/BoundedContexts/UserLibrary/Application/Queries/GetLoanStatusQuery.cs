using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to retrieve loan status of a specific game in the user's library.
/// </summary>
internal record GetLoanStatusQuery(Guid UserId, Guid GameId) : IQuery<LoanStatusDto?>;
