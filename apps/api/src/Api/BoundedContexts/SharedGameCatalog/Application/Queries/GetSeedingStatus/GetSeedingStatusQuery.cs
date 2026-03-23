using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetSeedingStatus;

public sealed record SeedingGameDto(
    Guid Id,
    int? BggId,
    string Title,
    int GameDataStatus,
    string GameDataStatusName,
    int GameStatus,
    string GameStatusName,
    bool HasUploadedPdf,
    bool IsRagReady,
    DateTime CreatedAt);

internal sealed record GetSeedingStatusQuery : IQuery<List<SeedingGameDto>>;
