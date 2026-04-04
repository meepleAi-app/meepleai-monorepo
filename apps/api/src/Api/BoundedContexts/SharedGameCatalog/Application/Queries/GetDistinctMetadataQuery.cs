using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

internal record DistinctMetadataDto(
    List<string> Categories,
    List<string> Mechanics,
    List<string> Designers,
    List<string> Publishers);

internal record GetDistinctMetadataQuery() : IRequest<DistinctMetadataDto>;
