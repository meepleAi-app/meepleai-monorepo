using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Gets detailed job information including steps and log entries.
/// Issue #4731: Queue queries.
/// </summary>
internal record GetJobDetailQuery(Guid JobId) : IQuery<ProcessingJobDetailDto>;
