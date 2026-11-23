using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Npgsql;
using Xunit;
using Kind = Api.BoundedContexts.SharedGameCatalog.Application.Services.ShareRequestApprovalContributions.Kind;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// #5139 — which contribution an approval records. The paths through the real approvals and database are in
/// <c>ShareRequestDomainEventsIntegrationTests</c>; these cover the kinds that one does not reach, a cover first.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class ShareRequestApprovalContributionsTests
{
    private readonly Mock<IContributorRepository> _repository = new();
    private readonly List<Contributor> _added = [];
    private readonly Guid _userId = Guid.NewGuid();
    private readonly Guid _gameId = Guid.NewGuid();

    public ShareRequestApprovalContributionsTests()
    {
        _repository
            .Setup(r => r.AddAsync(It.IsAny<Contributor>(), It.IsAny<CancellationToken>()))
            .Callback<Contributor, CancellationToken>((c, _) => _added.Add(c))
            .Returns(Task.CompletedTask);
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ACover_IsAMetadataUpdate()
    {
        var record = await RecordOneAsync(Kind.Cover, []);

        record.Type.Should().Be(ContributionRecordType.MetadataUpdate);
    }

    [Fact]
    public async Task ContentWithoutDocuments_IsAContentEnhancement_WithoutGameData()
    {
        var record = await RecordOneAsync(Kind.Content, []);

        record.Type.Should().Be(ContributionRecordType.ContentEnhancement);
        record.IncludesGameData.Should().BeFalse();
    }

    [Fact]
    public async Task ContentWithDocuments_IsADocumentAddition_OfThoseDocuments()
    {
        var copies = new[] { Guid.NewGuid(), Guid.NewGuid() };

        var record = await RecordOneAsync(Kind.Content, copies);

        record.Type.Should().Be(ContributionRecordType.DocumentAddition);
        record.DocumentIds.Should().Equal(copies);
    }

    [Fact]
    public async Task TheGameItself_OnAGameWithAPrimaryAlready_IsAContentEnhancement_WithGameData_AndNotPrimary()
    {
        _repository.Setup(r => r.HasPrimaryContributorAsync(_gameId, It.IsAny<CancellationToken>())).ReturnsAsync(true);

        var record = await RecordOneAsync(Kind.NewGame, []);

        record.Type.Should().Be(ContributionRecordType.ContentEnhancement);
        record.IncludesGameData.Should().BeTrue();
        _added.Should().ContainSingle().Which.IsPrimaryContributor.Should().BeFalse();
    }

    [Fact]
    public async Task AnExistingContributor_GetsTheNextVersion_AndIsNotAddedAgain()
    {
        var existing = Contributor.CreatePrimaryWithInitialSubmission(_userId, _gameId, Guid.NewGuid());
        _repository
            .Setup(r => r.GetByUserAndSharedGameAsync(_userId, _gameId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        IReadOnlyCollection<ContributionRecord>? saved = null;
        _repository
            .Setup(r => r.AddContributionsAsync(existing, It.IsAny<IReadOnlyCollection<ContributionRecord>>(), It.IsAny<CancellationToken>()))
            .Callback<Contributor, IReadOnlyCollection<ContributionRecord>, CancellationToken>((_, records, _) => saved = records)
            .Returns(Task.CompletedTask);
        var contributions = new ShareRequestApprovalContributions(_repository.Object);

        await contributions.RecordAsync(Request(), _gameId, Kind.Content, [Guid.NewGuid()], Ct);
        await contributions.AddToUnitOfWorkAsync(Ct);

        _added.Should().BeEmpty();
        saved.Should().ContainSingle().Which.Version.Should().Be(2);
    }

    [Fact]
    public void KindOf_ANewGame_IsTheGameItself() =>
        ShareRequestApprovalContributions.KindOf(Request()).Should().Be(Kind.NewGame);

    [Fact]
    public void KindOf_AdditionalContent_IsContent() =>
        ShareRequestApprovalContributions.KindOf(
                ShareRequest.Create(_userId, _gameId, ContributionType.AdditionalContent, targetSharedGameId: _gameId))
            .Should().Be(Kind.Content);

    // Review of #5142: two approvals of one author on one game at once both look the contributor up before either
    // commits; the second save hits the unique index. A 500 before, a 409 the admin can retry now.
    [Fact]
    public async Task SaveWithTheApproval_AConcurrentApprovalOfTheSameAuthorAndGame_IsAConflict_AndEvictsNothing()
    {
        var (unitOfWork, cache) = (new Mock<IUnitOfWork>(), new Mock<ISharedGameCacheInvalidator>());
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolation("ix_contributors_user_shared_game_unique"));
        var contributions = new ShareRequestApprovalContributions(_repository.Object);
        await contributions.RecordAsync(Request(), _gameId, Kind.NewGame, [], Ct);

        var act = () => contributions.SaveWithTheApprovalAsync(unitOfWork.Object, cache.Object, Ct);

        await act.Should().ThrowAsync<ConflictException>();
        cache.Verify(c => c.EvictContributorsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("ix_contributors_user_shared_game_unique")]
    [InlineData("ix_contributors_one_primary_per_shared_game_unique")]
    [InlineData("ix_contribution_records_contributor_version_unique")]
    public void IsAConcurrentApproval_TheIndexesAReadThenWriteCannotSee(string constraint) =>
        ShareRequestApprovalContributions.IsAConcurrentApproval(UniqueViolation(constraint)).Should().BeTrue();

    [Fact]
    public async Task SaveWithTheApproval_AnotherUniqueViolation_IsNotTurnedIntoAConflict()
    {
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(UniqueViolation("ux_share_requests_one_proposal_per_private_game"));
        var contributions = new ShareRequestApprovalContributions(_repository.Object);

        var act = () => contributions.SaveWithTheApprovalAsync(unitOfWork.Object, Mock.Of<ISharedGameCacheInvalidator>(), Ct);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task SaveWithTheApproval_EvictsTheContributorsOfTheGamesItTouched_AfterTheSave()
    {
        var order = new List<string>();
        var unitOfWork = new Mock<IUnitOfWork>();
        unitOfWork
            .Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .Callback(() => order.Add("save"))
            .ReturnsAsync(1);
        var cache = new Mock<ISharedGameCacheInvalidator>();
        IReadOnlyCollection<Guid>? evicted = null;
        cache
            .Setup(c => c.EvictContributorsAsync(It.IsAny<IReadOnlyCollection<Guid>>(), It.IsAny<CancellationToken>()))
            .Callback<IReadOnlyCollection<Guid>, CancellationToken>((games, _) =>
            {
                order.Add("evict");
                evicted = games.ToList();
            })
            .Returns(Task.CompletedTask);
        var otherGame = Guid.NewGuid();
        var contributions = new ShareRequestApprovalContributions(_repository.Object);
        await contributions.RecordAsync(Request(), _gameId, Kind.NewGame, [], Ct);
        await contributions.RecordAsync(Request(), otherGame, Kind.Content, [], Ct);

        await contributions.SaveWithTheApprovalAsync(unitOfWork.Object, cache.Object, Ct);

        order.Should().Equal("save", "evict");
        evicted.Should().BeEquivalentTo([_gameId, otherGame]);
    }

    private static DbUpdateException UniqueViolation(string constraint) =>
        new("duplicate key", new PostgresException(
            "duplicate key value violates unique constraint", "ERROR", "ERROR", "23505", constraintName: constraint));

    // A new contributor, saved with its whole history: the one record this approval gave it.
    private async Task<ContributionRecord> RecordOneAsync(Kind kind, IReadOnlyList<Guid> documentIds)
    {
        var contributions = new ShareRequestApprovalContributions(_repository.Object);
        await contributions.RecordAsync(Request(), _gameId, kind, documentIds, Ct);
        await contributions.AddToUnitOfWorkAsync(Ct);
        return _added.Should().ContainSingle().Subject.Contributions.Should().ContainSingle().Subject;
    }

    private ShareRequest Request() => ShareRequest.Create(_userId, _gameId, ContributionType.NewGame);
}
