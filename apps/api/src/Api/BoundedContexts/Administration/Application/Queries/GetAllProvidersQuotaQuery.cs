using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query for aggregated quota across all quota-capable providers (SupportedProviderNames).
/// Issue #3043.
/// </summary>
internal sealed record GetAllProvidersQuotaQuery() : IQuery<IReadOnlyList<ProviderQuotaDto>>;
