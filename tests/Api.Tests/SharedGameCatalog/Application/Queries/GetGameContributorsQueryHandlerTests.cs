using Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetGameContributors;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using FluentAssertions;
using FluentValidation.TestHelper;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.SharedGameCatalog.Application.Queries;

/// <summary>
/// Unit tests for GetGameContributorsQuery and validator.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
public sealed class GetGameContributorsQueryValidatorTests
{
    private readonly GetGameContributorsQueryValidator _validator = new();

    [Fact]
    public void Validate_WithValidGameId_ShouldPass()
    {
        // Arrange
        var query = new GetGameContributorsQuery(Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_ShouldFail()
    {
        // Arrange
        var query = new GetGameContributorsQuery(Guid.Empty);

        // Act
        var result = _validator.TestValidate(query);

        // Assert
        result.ShouldHaveValidationErrorFor(q => q.SharedGameId);
    }
}

/// <summary>
/// Integration tests for ContributorRepository.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
public sealed class ContributorRepositoryTests : IAsyncLifetime
{
    private readonly DbContextOptions<MeepleAiDbContext> _options;
    private MeepleAiDbContext _context = null!;
    private readonly Guid _gameId = Guid.NewGuid();
    private readonly Guid _userId1 = Guid.NewGuid();
    private readonly Guid _userId2 = Guid.NewGuid();

    public ContributorRepositoryTests()
    {
        _options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
    }

    public async Task InitializeAsync()
    {
        _context = new MeepleAiDbContext(_options);
        await SeedTestDataAsync();
    }

    public async Task DisposeAsync()
    {
        await _context.DisposeAsync();
    }

    [Fact]
    public async Task GetBySharedGameAsync_ReturnsContributorsOrderedByPrimaryThenCount()
    {
        // Arrange
        var repository = new ContributorRepository(_context);

        // Act
        var contributors = await repository.GetBySharedGameAsync(_gameId, CancellationToken.None);

        // Assert
        contributors.Should().HaveCount(2);
        contributors[0].IsPrimaryContributor.Should().BeTrue();
        contributors[0].UserId.Should().Be(_userId1);
        contributors[1].IsPrimaryContributor.Should().BeFalse();
        contributors[1].UserId.Should().Be(_userId2);
    }

    [Fact]
    public async Task GetByUserIdAsync_ReturnsPaginatedResults()
    {
        // Arrange
        var repository = new ContributorRepository(_context);

        // Act
        var (contributors, totalCount) = await repository.GetByUserIdAsync(
            _userId1,
            pageNumber: 1,
            pageSize: 10,
            CancellationToken.None);

        // Assert
        contributors.Should().HaveCount(1);
        totalCount.Should().Be(1);
        contributors[0].UserId.Should().Be(_userId1);
    }

    [Fact]
    public async Task GetByIdAsync_WithValidId_ReturnsContributor()
    {
        // Arrange
        var repository = new ContributorRepository(_context);
        var contributors = await repository.GetBySharedGameAsync(_gameId, CancellationToken.None);
        var contributorId = contributors[0].Id;

        // Act
        var contributor = await repository.GetByIdAsync(contributorId, CancellationToken.None);

        // Assert
        contributor.Should().NotBeNull();
        contributor!.Id.Should().Be(contributorId);
    }

    [Fact]
    public async Task GetByIdAsync_WithInvalidId_ReturnsNull()
    {
        // Arrange
        var repository = new ContributorRepository(_context);

        // Act
        var contributor = await repository.GetByIdAsync(Guid.NewGuid(), CancellationToken.None);

        // Assert
        contributor.Should().BeNull();
    }

    private async Task SeedTestDataAsync()
    {
        // Create users
        var user1 = new UserEntity
        {
            Id = _userId1,
            Email = "primary@test.com",
            DisplayName = "Primary Contributor",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };

        var user2 = new UserEntity
        {
            Id = _userId2,
            Email = "secondary@test.com",
            DisplayName = "Secondary Contributor",
            Role = "user",
            CreatedAt = DateTime.UtcNow
        };

        await _context.Users.AddRangeAsync(user1, user2);

        // Create contributors
        var contributor1 = new ContributorEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId1,
            SharedGameId = _gameId,
            IsPrimaryContributor = true,
            CreatedAt = DateTime.UtcNow.AddDays(-10)
        };

        var contributor2 = new ContributorEntity
        {
            Id = Guid.NewGuid(),
            UserId = _userId2,
            SharedGameId = _gameId,
            IsPrimaryContributor = false,
            CreatedAt = DateTime.UtcNow.AddDays(-5)
        };

        // Add contribution records
        var contribution1 = new ContributionRecordEntity
        {
            Id = Guid.NewGuid(),
            ContributorId = contributor1.Id,
            Type = (int)ContributionRecordType.InitialSubmission,
            Description = "Initial submission",
            Version = 1,
            ContributedAt = DateTime.UtcNow.AddDays(-10),
            IncludesGameData = true,
            IncludesMetadata = true
        };

        var contribution2 = new ContributionRecordEntity
        {
            Id = Guid.NewGuid(),
            ContributorId = contributor2.Id,
            Type = (int)ContributionRecordType.MetadataUpdate,
            Description = "Updated metadata",
            Version = 2,
            ContributedAt = DateTime.UtcNow.AddDays(-5),
            IncludesMetadata = true
        };

        contributor1.Contributions.Add(contribution1);
        contributor2.Contributions.Add(contribution2);

        await _context.Set<ContributorEntity>().AddRangeAsync(contributor1, contributor2);
        await _context.Set<ContributionRecordEntity>().AddRangeAsync(contribution1, contribution2);

        await _context.SaveChangesAsync();
    }
}
