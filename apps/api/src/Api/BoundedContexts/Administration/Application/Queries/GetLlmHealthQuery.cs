using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get LLM provider health status.
/// ISSUE-962 (BGAI-020): Admin endpoint for provider health monitoring
/// </summary>
internal record GetLlmHealthQuery : IQuery<LlmHealthStatusDto>;
