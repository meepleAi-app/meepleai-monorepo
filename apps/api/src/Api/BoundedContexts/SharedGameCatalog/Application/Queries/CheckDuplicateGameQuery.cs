using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to check for duplicate games using both exact BggId matching and fuzzy title similarity.
/// Used in the PDF wizard flow to warn users about potential duplicates before import.
/// Issue #4158: Backend - Duplicate Detection Enhancement
/// </summary>
/// <param name="Title">Game title to check for fuzzy duplicates</param>
/// <param name="BggId">Optional BGG ID to check for exact duplicates</param>
public record CheckDuplicateGameQuery(
    string Title,
    int? BggId) : IRequest<DuplicateCheckResultDto>;
