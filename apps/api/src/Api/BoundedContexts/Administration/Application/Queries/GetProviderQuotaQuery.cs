using Api.Models;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

internal sealed record GetProviderQuotaQuery(string ProviderName) : IQuery<ProviderQuotaDto>;
