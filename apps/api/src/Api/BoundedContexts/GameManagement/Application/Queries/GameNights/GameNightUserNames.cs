using Api.Infrastructure;
using Api.SharedKernel.Domain.Validation;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// The names of a night's people as every guest sees them: the organizer, the players who answered. One reading for
/// the queries of the night, which each had their own copy with <c>DisplayName ?? Email</c> — the address of a user
/// without a name went to every guest (#4780). The rule is <see cref="PlayerDisplayNames.ForOthers"/>, and the
/// fallback is the name of someone without a usable one: <see cref="PlayerDisplayNames.Organizer"/> for the
/// organizers of a list, <see cref="PlayerDisplayNames.Player"/> for the players of a roster.
/// </summary>
internal static class GameNightUserNames
{
    public static async Task<Dictionary<Guid, string>> LoadAsync(
        MeepleAiDbContext context, IReadOnlyCollection<Guid> userIds, string fallback, CancellationToken cancellationToken)
    {
        var users = await context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.ToDictionary(u => u.Id, u => PlayerDisplayNames.ForOthers(u.DisplayName, u.Email, fallback));
    }
}
