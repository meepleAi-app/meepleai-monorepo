using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.ExportSharedGamesTracking;

internal sealed record ExportSharedGamesTrackingQuery : IQuery<byte[]>;
