using Api.BoundedContexts.SystemConfiguration.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SystemConfiguration.Application.Queries;

/// <summary>
/// Query to get a single AI model configuration by ID
/// </summary>
/// <remarks>
/// Issue #2567: Admin endpoint for retrieving specific AI model
/// </remarks>
internal sealed record GetAiModelByIdQuery(Guid Id) : IQuery<AiModelDto>;
