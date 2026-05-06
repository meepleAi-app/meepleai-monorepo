using Api.BoundedContexts.Authentication.Application.Queries.GetTopContributors;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Unit tests for <see cref="GetTopContributorsHandler"/>.
/// Issue #728 — Discover dashboard top contributors sub-query.
///
/// Covers:
/// - Ranking by equal-weight sum: kbUploads + distinct agent definition sessions
/// - Tie ordering (descending total, then DisplayName ASC)
/// - Exclusion of zero-contribution users
/// - Distinct agent count de-duplication by AgentDefinitionId
/// - Exclusion of suspended users
/// - Limit clamping (1–20)
/// - Constructor null-argument guards
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public sealed class GetTopContributorsHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly Mock<ILogger<GetTopContributorsHandler>> _loggerMock;
    private readonly GetTopContributorsHandler _handler;

    public GetTopContributorsHandlerTests()
    {
        _dbContext = TestDbContextFactory.CreateInMemoryDbContext();
        _loggerMock = new Mock<ILogger<GetTopContributorsHandler>>();
        _handler = new GetTopContributorsHandler(_dbContext, _loggerMock.Object);
    }

    // ── Ranking formula ────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RanksByEqualWeightSum_KbUploadsAndDistinctAgentSessions()
    {
        // Arrange
        var u1 = SeedUser("Alice");   // 5 kb + 3 distinct agents = 8
        var u2 = SeedUser("Bob");     // 0 kb + 8 distinct agents = 8 (tie with u1)
        var u3 = SeedUser("Charlie"); // 3 kb + 2 distinct agents = 5
        SeedUser("NoContrib");        // 0 contributions → excluded

        for (int i = 0; i < 5; i++) SeedKbUpload(u1);
        for (int i = 0; i < 3; i++) SeedAgentSession(u1, Guid.NewGuid());

        for (int i = 0; i < 8; i++) SeedAgentSession(u2, Guid.NewGuid());

        for (int i = 0; i < 3; i++) SeedKbUpload(u3);
        for (int i = 0; i < 2; i++) SeedAgentSession(u3, Guid.NewGuid());

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3, "zero-contribution user must be excluded");
        result[0].ContributionCount.Should().Be(8);
        result[1].ContributionCount.Should().Be(8);
        result[2].ContributionCount.Should().Be(5);
        result[2].UserId.Should().Be(u3);
    }

    [Fact]
    public async Task Handle_TiedContributors_OrderedByDisplayNameAscending()
    {
        // Arrange — two users with identical totals; "Alice" should come before "Bob"
        var uAlice = SeedUser("Alice");
        var uBob = SeedUser("Bob");

        SeedKbUpload(uAlice);
        SeedKbUpload(uBob);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(2);
        result[0].DisplayName.Should().Be("Alice");
        result[1].DisplayName.Should().Be("Bob");
    }

    // ── Zero-contribution exclusion ────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesZeroContributionUsers()
    {
        // Arrange
        SeedUser("NoContributions");
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Should().BeEmpty();
    }

    // ── Distinct agent count ────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_CountsDistinctAgentDefinitionIds_NotSessionCount()
    {
        // Arrange — 5 sessions with the SAME AgentDefinitionId → should count as 1
        var user = SeedUser("Marco");
        var singleAgentDefId = Guid.NewGuid();
        for (int i = 0; i < 5; i++) SeedAgentSession(user, singleAgentDefId);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Single().AgentCount.Should().Be(1);
        result.Single().ContributionCount.Should().Be(1); // 0 kb + 1 distinct agent
    }

    [Fact]
    public async Task Handle_MultipleDistinctAgentDefinitions_CountedCorrectly()
    {
        // Arrange — 3 distinct AgentDefinitionIds, 2 sessions each → agentCount = 3
        var user = SeedUser("Julia");
        for (int i = 0; i < 3; i++)
        {
            var defId = Guid.NewGuid();
            SeedAgentSession(user, defId);
            SeedAgentSession(user, defId); // duplicate session, same def
        }
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Single().AgentCount.Should().Be(3);
        result.Single().KbUploadCount.Should().Be(0);
        result.Single().ContributionCount.Should().Be(3);
    }

    // ── Suspended users ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_ExcludesSuspendedUsers()
    {
        // Arrange
        var activeUser = SeedUser("ActiveUser");
        var suspendedUser = SeedUser("SuspendedUser", isSuspended: true);

        SeedKbUpload(activeUser);
        SeedKbUpload(suspendedUser); // suspended user's uploads must not appear
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        result.Single().DisplayName.Should().Be("ActiveUser");
    }

    // ── Limit clamping ─────────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_RespectsLimit()
    {
        // Arrange — seed 5 users, each with 1 upload
        for (int i = 0; i < 5; i++)
        {
            var u = SeedUser($"User{i}");
            SeedKbUpload(u);
        }
        await _dbContext.SaveChangesAsync();

        // Act — request only 3
        var result = await _handler.Handle(
            new GetTopContributorsQuery(3), CancellationToken.None);

        // Assert
        result.Should().HaveCount(3);
    }

    [Fact]
    public async Task Handle_ClampsLimitToMaximumTwenty()
    {
        // Arrange — seed 25 users
        for (int i = 0; i < 25; i++)
        {
            var u = SeedUser($"User{i:00}");
            SeedKbUpload(u);
        }
        await _dbContext.SaveChangesAsync();

        // Act — request 100 (above max)
        var result = await _handler.Handle(
            new GetTopContributorsQuery(100), CancellationToken.None);

        // Assert
        result.Should().HaveCount(20);
    }

    // ── DTO field mapping ──────────────────────────────────────────────────────

    [Fact]
    public async Task Handle_MapsAllDtoFields_Correctly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "test@example.com",
            DisplayName = "Detailed User",
            AvatarUrl = "https://example.com/avatar.png"
        });

        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = userId,
            IsPublic = true,
            FileName = "rulebook.pdf",
            FilePath = "/tmp/rulebook.pdf",
            DocumentCategory = "Rulebook"
        });

        var agentDefId = Guid.NewGuid();
        _dbContext.AgentSessions.Add(new AgentSessionEntity
        {
            Id = Guid.NewGuid(),
            AgentDefinitionId = agentDefId,
            UserId = userId,
            GameSessionId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            CurrentGameStateJson = "{}",
            IsActive = true,
            StartedAt = DateTime.UtcNow
        });

        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Should().HaveCount(1);
        var dto = result.Single();
        dto.UserId.Should().Be(userId);
        dto.DisplayName.Should().Be("Detailed User");
        dto.AvatarUrl.Should().Be("https://example.com/avatar.png");
        dto.KbUploadCount.Should().Be(1);
        dto.AgentCount.Should().Be(1);
        dto.ContributionCount.Should().Be(2); // 1 kb + 1 agent
    }

    [Fact]
    public async Task Handle_NullDisplayName_ReturnsEmptyString()
    {
        // Arrange — user with no DisplayName set
        var userId = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = userId,
            Email = "nodisplay@example.com",
            DisplayName = null
        });
        SeedKbUpload(userId);
        await _dbContext.SaveChangesAsync();

        // Act
        var result = await _handler.Handle(
            new GetTopContributorsQuery(10), CancellationToken.None);

        // Assert
        result.Single().DisplayName.Should().Be(string.Empty);
    }

    // ── Constructor null guards ────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullDbContext_ThrowsArgumentNullException()
    {
        var act = () => new GetTopContributorsHandler(null!, _loggerMock.Object);
        act.Should().Throw<ArgumentNullException>().WithParameterName("dbContext");
    }

    [Fact]
    public void Constructor_NullLogger_ThrowsArgumentNullException()
    {
        var act = () => new GetTopContributorsHandler(_dbContext, null!);
        act.Should().Throw<ArgumentNullException>().WithParameterName("logger");
    }

    [Fact]
    public async Task Handle_NullQuery_ThrowsArgumentNullException()
    {
        var act = async () => await _handler.Handle(null!, CancellationToken.None);
        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ── Seed helpers ───────────────────────────────────────────────────────────

    private Guid SeedUser(string displayName, bool isSuspended = false)
    {
        var id = Guid.NewGuid();
        _dbContext.Users.Add(new UserEntity
        {
            Id = id,
            Email = $"{displayName.ToLowerInvariant().Replace(" ", ".")}@test.com",
            DisplayName = displayName,
            IsSuspended = isSuspended
        });
        return id;
    }

    private void SeedKbUpload(Guid uploaderId)
    {
        _dbContext.PdfDocuments.Add(new PdfDocumentEntity
        {
            Id = Guid.NewGuid(),
            UploadedByUserId = uploaderId,
            IsPublic = true,
            FileName = $"doc-{Guid.NewGuid():N}.pdf",
            FilePath = $"/tmp/{Guid.NewGuid():N}.pdf",
            DocumentCategory = "Rulebook"
        });
    }

    private void SeedAgentSession(Guid userId, Guid agentDefId)
    {
        _dbContext.AgentSessions.Add(new AgentSessionEntity
        {
            Id = Guid.NewGuid(),
            AgentDefinitionId = agentDefId,
            UserId = userId,
            GameSessionId = Guid.NewGuid(),
            GameId = Guid.NewGuid(),
            CurrentGameStateJson = "{}",
            IsActive = true,
            StartedAt = DateTime.UtcNow
        });
    }

    public void Dispose() => _dbContext.Dispose();
}
