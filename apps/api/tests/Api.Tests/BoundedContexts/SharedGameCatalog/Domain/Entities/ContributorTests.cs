using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Unit tests for Contributor entity.
/// </summary>
public class ContributorTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsContributor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var isPrimary = true;

        // Act
        var contributor = Contributor.Create(userId, sharedGameId, isPrimary);

        // Assert
        contributor.Should().NotBeNull();
        contributor.Id.Should().NotBe(Guid.Empty);
        contributor.UserId.Should().Be(userId);
        contributor.SharedGameId.Should().Be(sharedGameId);
        contributor.IsPrimaryContributor.Should().Be(isPrimary);
        contributor.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        contributor.ModifiedAt.Should().BeNull();
        contributor.Contributions.Should().BeEmpty();
        contributor.ContributionCount.Should().Be(0);
        contributor.LatestVersion.Should().Be(0);
    }

    [Fact]
    public void Create_WithNonPrimaryContributor_ReturnsContributor()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();

        // Act
        var contributor = Contributor.Create(userId, sharedGameId, isPrimary: false);

        // Assert
        contributor.IsPrimaryContributor.Should().BeFalse();
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.Empty;
        var sharedGameId = Guid.NewGuid();

        // Act
        var act = () => Contributor.Create(userId, sharedGameId, isPrimary: true);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.Empty;

        // Act
        var act = () => Contributor.Create(userId, sharedGameId, isPrimary: true);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("sharedGameId")
            .WithMessage("*SharedGameId cannot be empty*");
    }

    #endregion

    #region CreatePrimaryWithInitialSubmission Tests

    [Fact]
    public void CreatePrimaryWithInitialSubmission_ReturnsContributorWithInitialContribution()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var contributor = Contributor.CreatePrimaryWithInitialSubmission(
            userId,
            sharedGameId,
            shareRequestId,
            documentIds);

        // Assert
        contributor.IsPrimaryContributor.Should().BeTrue();
        contributor.ContributionCount.Should().Be(1);
        contributor.LatestVersion.Should().Be(1);

        var contribution = contributor.Contributions.First();
        contribution.Type.Should().Be(ContributionRecordType.InitialSubmission);
        contribution.ShareRequestId.Should().Be(shareRequestId);
        contribution.DocumentIds.Should().BeEquivalentTo(documentIds);
        contribution.IncludesGameData.Should().BeTrue();
        contribution.IncludesMetadata.Should().BeTrue();
    }

    [Fact]
    public void CreatePrimaryWithInitialSubmission_WithoutDocuments_ReturnsContributorWithEmptyDocuments()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();

        // Act
        var contributor = Contributor.CreatePrimaryWithInitialSubmission(
            userId,
            sharedGameId,
            shareRequestId);

        // Assert
        contributor.Contributions.First().DocumentIds.Should().BeEmpty();
    }

    #endregion

    #region AddContribution Tests

    [Fact]
    public void AddContribution_WithValidRecord_AddsToContributions()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var record = ContributionRecord.Create(
            contributor.Id,
            ContributionRecordType.DocumentAddition,
            "Added rulebook",
            1);

        // Act
        contributor.AddContribution(record);

        // Assert
        contributor.ContributionCount.Should().Be(1);
        contributor.Contributions.Should().Contain(record);
        contributor.ModifiedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void AddContribution_WithNullRecord_ThrowsArgumentNullException()
    {
        // Arrange
        var contributor = CreateTestContributor();

        // Act
        var act = () => contributor.AddContribution(null!);

        // Assert
        act.Should().Throw<ArgumentNullException>()
            .WithParameterName("record");
    }

    [Fact]
    public void AddContribution_WithMismatchedContributorId_ThrowsArgumentException()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var wrongContributorId = Guid.NewGuid();
        var record = ContributionRecord.Create(
            wrongContributorId,
            ContributionRecordType.DocumentAddition,
            "Added rulebook",
            1);

        // Act
        var act = () => contributor.AddContribution(record);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("record")
            .WithMessage("*Contribution record must belong to this contributor*");
    }

    #endregion

    #region RecordDocumentAddition Tests

    [Fact]
    public void RecordDocumentAddition_CreatesContributionWithCorrectVersion()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var documentIds = new List<Guid> { Guid.NewGuid() };
        var description = "Added player aid";

        // Act
        var record = contributor.RecordDocumentAddition(documentIds, description);

        // Assert
        record.Type.Should().Be(ContributionRecordType.DocumentAddition);
        record.Version.Should().Be(1); // First contribution
        record.DocumentIds.Should().BeEquivalentTo(documentIds);
        record.Description.Should().Be(description);
        contributor.ContributionCount.Should().Be(1);
        contributor.LatestVersion.Should().Be(1);
    }

    [Fact]
    public void RecordDocumentAddition_IncrementsVersion()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var contributor = Contributor.CreatePrimaryWithInitialSubmission(
            userId, sharedGameId, shareRequestId);

        var documentIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var record = contributor.RecordDocumentAddition(documentIds, "Second contribution");

        // Assert
        record.Version.Should().Be(2); // After initial submission (version 1)
        contributor.LatestVersion.Should().Be(2);
    }

    [Fact]
    public void RecordDocumentAddition_WithShareRequestId_IncludesIt()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var documentIds = new List<Guid> { Guid.NewGuid() };
        var shareRequestId = Guid.NewGuid();

        // Act
        var record = contributor.RecordDocumentAddition(
            documentIds,
            "Added via share request",
            shareRequestId);

        // Assert
        record.ShareRequestId.Should().Be(shareRequestId);
    }

    #endregion

    #region RecordMetadataUpdate Tests

    [Fact]
    public void RecordMetadataUpdate_CreatesCorrectContribution()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var description = "Updated player count to 2-6";

        // Act
        var record = contributor.RecordMetadataUpdate(description);

        // Assert
        record.Type.Should().Be(ContributionRecordType.MetadataUpdate);
        record.Description.Should().Be(description);
        record.IncludesMetadata.Should().BeTrue();
        record.IncludesGameData.Should().BeFalse();
        contributor.ContributionCount.Should().Be(1);
    }

    [Fact]
    public void RecordMetadataUpdate_WithShareRequestId_IncludesIt()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var shareRequestId = Guid.NewGuid();

        // Act
        var record = contributor.RecordMetadataUpdate(
            "Updated metadata",
            shareRequestId);

        // Assert
        record.ShareRequestId.Should().Be(shareRequestId);
    }

    #endregion

    #region RecordContentEnhancement Tests

    [Fact]
    public void RecordContentEnhancement_WithAllOptions_CreatesCorrectContribution()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var description = "Major content enhancement";
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var record = contributor.RecordContentEnhancement(
            description,
            shareRequestId,
            documentIds,
            includesGameData: true,
            includesMetadata: true);

        // Assert
        record.Type.Should().Be(ContributionRecordType.ContentEnhancement);
        record.Description.Should().Be(description);
        record.ShareRequestId.Should().Be(shareRequestId);
        record.DocumentIds.Should().BeEquivalentTo(documentIds);
        record.IncludesGameData.Should().BeTrue();
        record.IncludesMetadata.Should().BeTrue();
    }

    [Fact]
    public void RecordContentEnhancement_WithMinimalOptions_CreatesCorrectContribution()
    {
        // Arrange
        var contributor = CreateTestContributor();
        var description = "Minor enhancement";

        // Act
        var record = contributor.RecordContentEnhancement(description);

        // Assert
        record.Type.Should().Be(ContributionRecordType.ContentEnhancement);
        record.ShareRequestId.Should().BeNull();
        record.DocumentIds.Should().BeEmpty();
        record.IncludesGameData.Should().BeFalse();
        record.IncludesMetadata.Should().BeFalse();
    }

    #endregion

    #region GetContributionsByType Tests

    [Fact]
    public void GetContributionsByType_ReturnsFilteredContributions()
    {
        // Arrange
        var contributor = CreateTestContributor();
        contributor.RecordDocumentAddition(new List<Guid> { Guid.NewGuid() }, "Doc 1");
        contributor.RecordMetadataUpdate("Meta 1");
        contributor.RecordDocumentAddition(new List<Guid> { Guid.NewGuid() }, "Doc 2");
        contributor.RecordMetadataUpdate("Meta 2");

        // Act
        var documentAdditions = contributor.GetContributionsByType(ContributionRecordType.DocumentAddition).ToList();
        var metadataUpdates = contributor.GetContributionsByType(ContributionRecordType.MetadataUpdate).ToList();

        // Assert
        documentAdditions.Should().HaveCount(2);
        metadataUpdates.Should().HaveCount(2);
        (documentAdditions.All(c => c.Type == ContributionRecordType.DocumentAddition)).Should().BeTrue();
        (metadataUpdates.All(c => c.Type == ContributionRecordType.MetadataUpdate)).Should().BeTrue();
    }

    [Fact]
    public void GetContributionsByType_WithNoMatches_ReturnsEmpty()
    {
        // Arrange
        var contributor = CreateTestContributor();
        contributor.RecordDocumentAddition(new List<Guid> { Guid.NewGuid() }, "Doc 1");

        // Act
        var results = contributor.GetContributionsByType(ContributionRecordType.MetadataUpdate);

        // Assert
        results.Should().BeEmpty();
    }

    #endregion

    #region GetLatestContribution Tests

    [Fact]
    public void GetLatestContribution_ReturnsNullWhenNoContributions()
    {
        // Arrange
        var contributor = CreateTestContributor();

        // Act
        var latest = contributor.GetLatestContribution();

        // Assert
        latest.Should().BeNull();
    }

    [Fact]
    public void GetLatestContribution_ReturnsMostRecentContribution()
    {
        // Arrange
        var contributor = CreateTestContributor();
        contributor.RecordDocumentAddition(new List<Guid> { Guid.NewGuid() }, "First");

        // Small delay to ensure different timestamps
        Thread.Sleep(10);

        contributor.RecordMetadataUpdate("Second");

        Thread.Sleep(10);

        var lastContribution = contributor.RecordContentEnhancement("Third");

        // Act
        var latest = contributor.GetLatestContribution();

        // Assert
        latest.Should().NotBeNull();
        latest!.Description.Should().Be("Third");
        latest.Id.Should().Be(lastContribution.Id);
    }

    #endregion

    #region LatestVersion Tests

    [Fact]
    public void LatestVersion_WithNoContributions_ReturnsZero()
    {
        // Arrange
        var contributor = CreateTestContributor();

        // Assert
        contributor.LatestVersion.Should().Be(0);
    }

    [Fact]
    public void LatestVersion_ReturnsMaxVersionNumber()
    {
        // Arrange
        var contributor = CreateTestContributor();
        contributor.RecordDocumentAddition(new List<Guid> { Guid.NewGuid() }, "V1");
        contributor.RecordMetadataUpdate("V2");
        contributor.RecordContentEnhancement("V3");

        // Assert
        contributor.LatestVersion.Should().Be(3);
    }

    #endregion

    #region ContributionCount Tests

    [Fact]
    public void ContributionCount_ReflectsNumberOfContributions()
    {
        // Arrange
        var contributor = CreateTestContributor();

        // Assert initial
        contributor.ContributionCount.Should().Be(0);

        // Add contributions
        contributor.RecordDocumentAddition(new List<Guid> { Guid.NewGuid() }, "First");
        contributor.ContributionCount.Should().Be(1);

        contributor.RecordMetadataUpdate("Second");
        contributor.ContributionCount.Should().Be(2);

        contributor.RecordContentEnhancement("Third");
        contributor.ContributionCount.Should().Be(3);
    }

    #endregion

    #region Helper Methods

    private static Contributor CreateTestContributor()
    {
        return Contributor.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            isPrimary: false);
    }

    #endregion
}
