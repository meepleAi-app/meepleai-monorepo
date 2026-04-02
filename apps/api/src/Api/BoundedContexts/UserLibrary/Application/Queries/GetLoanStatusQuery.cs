using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

internal record GetLoanStatusQuery(Guid UserId, Guid GameId) : IQuery<LoanStatusDto?>;
