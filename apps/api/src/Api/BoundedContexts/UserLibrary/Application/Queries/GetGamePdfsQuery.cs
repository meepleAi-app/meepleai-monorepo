using Api.BoundedContexts.UserLibrary.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get all PDFs associated with a game in user's library.
/// Returns both user-uploaded custom PDFs and shared catalog PDFs.
/// Issue #3152: Game Detail Split View - PDF selector support
/// </summary>
/// <param name="GameId">The game ID (from SharedGameCatalog)</param>
/// <param name="UserId">The user ID requesting PDFs</param>
internal record GetGamePdfsQuery(
    Guid GameId,
    Guid UserId
) : IRequest<List<GamePdfDto>>;
