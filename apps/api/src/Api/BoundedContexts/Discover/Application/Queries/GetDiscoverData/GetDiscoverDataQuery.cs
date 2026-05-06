using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Discover.Application.Queries.GetDiscoverData;

internal sealed record GetDiscoverDataQuery(int Limit) : IQuery<DiscoverDto>;
