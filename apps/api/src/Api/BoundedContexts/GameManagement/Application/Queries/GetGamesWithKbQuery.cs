using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Query to retrieve all games that have at least one indexed knowledge base document
/// for a given user. Used by the rulebook upload flow to show which games already have PDFs.
/// </summary>
internal record GetGamesWithKbQuery(Guid UserId) : IQuery<List<GameWithKbDto>>;

/// <summary>
/// Lightweight DTO representing a game that has at least one knowledge base document.
/// </summary>
internal record GameWithKbDto(
    Guid GameId,
    string GameName,
    int PdfCount,
    string? LatestPdfStatus);
