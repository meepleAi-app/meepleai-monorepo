using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Unit tests for ContributionRecord value object.
/// </summary>
public class ContributionRecordTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsContributionRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var type = ContributionRecordType.DocumentAddition;
        var description = "Added rulebook PDF";
        var version = 2;

        // Act
        var record = ContributionRecord.Create(
            contributorId,
            type,
            description,
            version);

        // Assert
        record.Should().NotBeNull();
        record.Id.Should().NotBe(Guid.Empty);
        record.ContributorId.Should().Be(contributorId);
        record.Type.Should().Be(type);
        record.Description.Should().Be(description);
        record.Version.Should().Be(version);
        record.ContributedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        record.ShareRequestId.Should().BeNull();
        record.DocumentIds.Should().BeEmpty();
        record.IncludesGameData.Should().BeFalse();
        record.IncludesMetadata.Should().BeFalse();
    }

    [Fact]
    public void Create_WithAllOptionalParameters_ReturnsContributionRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var type = ContributionRecordType.ContentEnhancement;
        var description = "Enhanced game content";
        var version = 3;
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };

        // Act
        var record = ContributionRecord.Create(
            contributorId,
            type,
            description,
            version,
            shareRequestId,
            documentIds,
            includesGameData: true,
            includesMetadata: true);

        // Assert
        record.ShareRequestId.Should().Be(shareRequestId);
        record.DocumentIds.Should().BeEquivalentTo(documentIds);
        record.IncludesGameData.Should().BeTrue();
        record.IncludesMetadata.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyContributorId_ThrowsArgumentException()
    {
        // Arrange
        var contributorId = Guid.Empty;

        // Act
        var act = () => ContributionRecord.Create(
            contributorId,
            ContributionRecordType.DocumentAddition,
            "Test description",
            1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("contributorId")
            .WithMessage("*ContributorId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithInvalidDescription_ThrowsArgumentException(string? description)
    {
        // Arrange
        var contributorId = Guid.NewGuid();

        // Act
        var act = () => ContributionRecord.Create(
            contributorId,
            ContributionRecordType.DocumentAddition,
            description!,
            1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description is required*");
    }

    [Fact]
    public void Create_WithDescriptionExceeding1000Characters_ThrowsArgumentException()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var description = new string('a', 1001);

        // Act
        var act = () => ContributionRecord.Create(
            contributorId,
            ContributionRecordType.DocumentAddition,
            description,
            1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("description")
            .WithMessage("*Description cannot exceed 1000 characters*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Create_WithInvalidVersion_ThrowsArgumentException(int version)
    {
        // Arrange
        var contributorId = Guid.NewGuid();

        // Act
        var act = () => ContributionRecord.Create(
            contributorId,
            ContributionRecordType.DocumentAddition,
            "Test description",
            version);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("version")
            .WithMessage("*Version must be at least 1*");
    }

    [Fact]
    public void Create_TrimsDescription()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var description = "  Test description with whitespace  ";

        // Act
        var record = ContributionRecord.Create(
            contributorId,
            ContributionRecordType.DocumentAddition,
            description,
            1);

        // Assert
        record.Description.Should().Be("Test description with whitespace");
    }

    #endregion

    #region CreateInitialSubmission Tests

    [Fact]
    public void CreateInitialSubmission_WithValidParameters_ReturnsCorrectRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var record = ContributionRecord.CreateInitialSubmission(
            contributorId,
            shareRequestId,
            documentIds);

        // Assert
        record.Type.Should().Be(ContributionRecordType.InitialSubmission);
        record.Description.Should().Be("Initial game submission");
        record.Version.Should().Be(1);
        record.ShareRequestId.Should().Be(shareRequestId);
        record.DocumentIds.Should().BeEquivalentTo(documentIds);
        record.IncludesGameData.Should().BeTrue();
        record.IncludesMetadata.Should().BeTrue();
    }

    [Fact]
    public void CreateInitialSubmission_WithoutDocumentIds_ReturnsRecordWithEmptyDocuments()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var shareRequestId = Guid.NewGuid();

        // Act
        var record = ContributionRecord.CreateInitialSubmission(
            contributorId,
            shareRequestId);

        // Assert
        record.DocumentIds.Should().BeEmpty();
    }

    #endregion

    #region CreateDocumentAddition Tests

    [Fact]
    public void CreateDocumentAddition_WithValidParameters_ReturnsCorrectRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var version = 2;
        var documentIds = new List<Guid> { Guid.NewGuid(), Guid.NewGuid() };
        var description = "Added player aid cards";
        var shareRequestId = Guid.NewGuid();

        // Act
        var record = ContributionRecord.CreateDocumentAddition(
            contributorId,
            version,
            documentIds,
            description,
            shareRequestId);

        // Assert
        record.Type.Should().Be(ContributionRecordType.DocumentAddition);
        record.Version.Should().Be(version);
        record.DocumentIds.Should().BeEquivalentTo(documentIds);
        record.Description.Should().Be(description);
        record.ShareRequestId.Should().Be(shareRequestId);
        record.IncludesGameData.Should().BeFalse();
        record.IncludesMetadata.Should().BeFalse();
    }

    [Fact]
    public void CreateDocumentAddition_WithNullDocumentIds_ThrowsArgumentException()
    {
        // Arrange
        var contributorId = Guid.NewGuid();

        // Act
        var act = () => ContributionRecord.CreateDocumentAddition(
            contributorId,
            1,
            null!,
            "Test description");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("documentIds")
            .WithMessage("*DocumentIds must contain at least one document*");
    }

    [Fact]
    public void CreateDocumentAddition_WithEmptyDocumentIds_ThrowsArgumentException()
    {
        // Arrange
        var contributorId = Guid.NewGuid();

        // Act
        var act = () => ContributionRecord.CreateDocumentAddition(
            contributorId,
            1,
            new List<Guid>(),
            "Test description");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("documentIds")
            .WithMessage("*DocumentIds must contain at least one document*");
    }

    #endregion

    #region CreateMetadataUpdate Tests

    [Fact]
    public void CreateMetadataUpdate_WithValidParameters_ReturnsCorrectRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var version = 3;
        var description = "Updated player count";
        var shareRequestId = Guid.NewGuid();

        // Act
        var record = ContributionRecord.CreateMetadataUpdate(
            contributorId,
            version,
            description,
            shareRequestId);

        // Assert
        record.Type.Should().Be(ContributionRecordType.MetadataUpdate);
        record.Version.Should().Be(version);
        record.Description.Should().Be(description);
        record.ShareRequestId.Should().Be(shareRequestId);
        record.DocumentIds.Should().BeEmpty();
        record.IncludesGameData.Should().BeFalse();
        record.IncludesMetadata.Should().BeTrue();
    }

    #endregion

    #region CreateContentEnhancement Tests

    [Fact]
    public void CreateContentEnhancement_WithAllOptions_ReturnsCorrectRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var version = 4;
        var description = "Major content update";
        var shareRequestId = Guid.NewGuid();
        var documentIds = new List<Guid> { Guid.NewGuid() };

        // Act
        var record = ContributionRecord.CreateContentEnhancement(
            contributorId,
            version,
            description,
            shareRequestId,
            documentIds,
            includesGameData: true,
            includesMetadata: true);

        // Assert
        record.Type.Should().Be(ContributionRecordType.ContentEnhancement);
        record.Version.Should().Be(version);
        record.Description.Should().Be(description);
        record.ShareRequestId.Should().Be(shareRequestId);
        record.DocumentIds.Should().BeEquivalentTo(documentIds);
        record.IncludesGameData.Should().BeTrue();
        record.IncludesMetadata.Should().BeTrue();
    }

    [Fact]
    public void CreateContentEnhancement_WithMinimalOptions_ReturnsCorrectRecord()
    {
        // Arrange
        var contributorId = Guid.NewGuid();
        var version = 2;
        var description = "Minor enhancement";

        // Act
        var record = ContributionRecord.CreateContentEnhancement(
            contributorId,
            version,
            description);

        // Assert
        record.Type.Should().Be(ContributionRecordType.ContentEnhancement);
        record.ShareRequestId.Should().BeNull();
        record.DocumentIds.Should().BeEmpty();
        record.IncludesGameData.Should().BeFalse();
        record.IncludesMetadata.Should().BeFalse();
    }

    #endregion

    #region ContributionRecordType Tests

    [Theory]
    [InlineData(ContributionRecordType.InitialSubmission, 0)]
    [InlineData(ContributionRecordType.DocumentAddition, 1)]
    [InlineData(ContributionRecordType.MetadataUpdate, 2)]
    [InlineData(ContributionRecordType.ContentEnhancement, 3)]
    public void ContributionRecordType_HasCorrectValues(ContributionRecordType type, int expectedValue)
    {
        // Assert
        ((int)type).Should().Be(expectedValue);
    }

    #endregion
}