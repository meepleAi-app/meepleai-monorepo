using Api.BoundedContexts.Administration.Domain.Entities;
using Api.Infrastructure.Entities.Administration;

namespace Api.BoundedContexts.Administration.Infrastructure.Persistence;

/// <summary>
/// Mapper between <see cref="StagingAllowlistEntity"/> (persistence) and
/// <see cref="StagingAllowlistEntry"/> (domain).
/// </summary>
internal static class StagingAllowlistEntryMapper
{
    public static StagingAllowlistEntry Reconstitute(StagingAllowlistEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);

        return StagingAllowlistEntry.Reconstitute(
            id: entity.Id,
            email: entity.Email,
            addedByUserId: entity.AddedByUserId,
            addedAt: entity.AddedAt,
            note: entity.Note,
            isDeleted: entity.IsDeleted,
            deletedAt: entity.DeletedAt,
            deletedByUserId: entity.DeletedByUserId);
    }
}
