using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Returns the PDF indexing/processing status for a game owned by the given user.
/// Issue #4943: Enables frontend polling for KB-ready state.
/// </summary>
/// <param name="GameId">The private game whose PDF status is requested.</param>
/// <param name="UserId">The authenticated user — must own the game.</param>
internal record GetGamePdfIndexingStatusQuery(
    Guid GameId,
    Guid UserId) : IQuery<PdfIndexingStatusDto>;
