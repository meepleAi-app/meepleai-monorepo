using Api.Infrastructure;
using Api.Infrastructure.Entities;

namespace Api.Tests.TestHelpers;

/// <summary>
/// One user per role, with the role as the users table stores it (lowercase, the Role value object). For the tests of
/// who receives an alert (#5113, #5119): the ones that seeded only an "admin" could not see a superadmin left out.
/// </summary>
internal static class UserRoleSeeds
{
    public static readonly string[] Roles = ["user", "editor", "creator", "admin", "superadmin"];

    public static async Task<IReadOnlyDictionary<string, Guid>> SeedOnePerRoleAsync(MeepleAiDbContext db)
    {
        var ids = new Dictionary<string, Guid>(StringComparer.Ordinal);
        foreach (var role in Roles)
        {
            var id = Guid.NewGuid();
            ids[role] = id;
            db.Users.Add(new UserEntity
            {
                Id = id,
                Email = $"{role}-{id:N}@test.com",
                PasswordHash = "hash",
                Role = role,
                CreatedAt = DateTime.UtcNow,
            });
        }

        await db.SaveChangesAsync();
        return ids;
    }
}
