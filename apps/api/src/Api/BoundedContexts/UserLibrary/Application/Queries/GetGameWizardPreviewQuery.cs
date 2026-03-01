using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.UserLibrary.Application.Queries;

/// <summary>
/// Query to get combined game preview data for the collection wizard.
/// Issue #4823: Backend Game Preview API - Unified Wizard Data Endpoint
/// Epic #4817: User Collection Wizard
/// </summary>
internal record GetGameWizardPreviewQuery(
    Guid GameId,
    string Source,
    Guid UserId
) : IQuery<GameWizardPreviewDto>;
