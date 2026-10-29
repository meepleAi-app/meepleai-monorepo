using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Queries.GetUserPermissions;
using Api.BoundedContexts.Administration.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// GetUserPermissionsHandler serves what the frontend asks of /api/v1/permissions/me.
/// #4268: it used to also serve a library limit (50 games for a free user) that nothing enforces;
/// the limit that is enforced is the plan's TierLimits.MaxLibraryGames.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class GetUserPermissionsHandlerTests : IDisposable
{
    // The minimal API serializes with the web defaults (camelCase).
    private static readonly JsonSerializerOptions WebJson = new(JsonSerializerDefaults.Web);

    private readonly MeepleAiDbContext _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly GetUserPermissionsHandler _handler;

    public GetUserPermissionsHandlerTests()
    {
        _handler = new GetUserPermissionsHandler(_dbContext, new PermissionRegistry());
    }

    public void Dispose() => _dbContext.Dispose();

    [Fact]
    public async Task Handle_FreeUser_ReturnsTierRoleStatusAndTheFeaturesOfThatUser()
    {
        var userId = await SeedUserAsync(tier: "free", role: "user");

        var response = await _handler.Handle(new GetUserPermissionsQuery(userId), TestContext.Current.CancellationToken);

        response.Tier.Should().Be("free");
        response.Role.Should().Be("user");
        response.Status.Should().Be(UserAccountStatus.Active);
        response.AccessibleFeatures.Should().Contain("wishlist");
        response.AccessibleFeatures.Should().NotContain("analytics.view");
    }

    [Fact]
    public async Task Handle_PayloadHasNoLibraryLimit_AmongItsMembers()
    {
        var userId = await SeedUserAsync(tier: "free", role: "user");

        var response = await _handler.Handle(new GetUserPermissionsQuery(userId), TestContext.Current.CancellationToken);

        // Closed list on purpose: a new member of this payload is a decision, not an accident.
        using var json = JsonDocument.Parse(JsonSerializer.Serialize(response, WebJson));
        json.RootElement.EnumerateObject().Select(p => p.Name)
            .Should().BeEquivalentTo("tier", "role", "status", "accessibleFeatures");
    }

    private async Task<Guid> SeedUserAsync(string tier, string role)
    {
        var user = new UserEntity
        {
            Id = Guid.NewGuid(),
            Email = $"{Guid.NewGuid():N}@example.com",
            PasswordHash = "hash",
            Role = role,
            Tier = tier,
            CreatedAt = DateTime.UtcNow
        };
        _dbContext.Users.Add(user);
        await _dbContext.SaveChangesAsync(TestContext.Current.CancellationToken);
        return user.Id;
    }
}
