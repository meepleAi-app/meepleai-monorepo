using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Resources;

/// <summary>
/// Query to retrieve pgvector vector store metrics.
/// Issue #3695: Resources Monitoring - Vector store metrics
/// </summary>
internal record GetVectorStoreMetricsQuery : IQuery<VectorStoreMetricsDto>;
