using Microsoft.Extensions.Logging;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// Shared context passed to all seed layers.
/// </summary>
public sealed record SeedContext(
    SeedProfile Profile,
    MeepleAiDbContext DbContext,
    IServiceProvider Services,
    ILogger Logger,
    Guid SystemUserId);
