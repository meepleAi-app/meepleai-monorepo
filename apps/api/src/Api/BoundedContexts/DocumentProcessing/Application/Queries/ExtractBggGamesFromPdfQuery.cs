using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// Query to extract BGG games list from PDF document.
/// Parses CSV format: "Nome Gioco,ID BGG"
/// </summary>
internal sealed record ExtractBggGamesFromPdfQuery(
    string PdfFilePath
) : IQuery<List<BggGameDto>>;
