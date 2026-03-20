using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the SharedGameDocument entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 29
/// </summary>
[Trait("Category", "Unit")]
public sealed class SharedGameDocumentTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidData_ReturnsDocument()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var doc = SharedGameDocument.Create(
            sharedGameId,
            pdfDocumentId,
            SharedGameDocumentType.Rulebook,
            "1.0",
            createdBy);

        // Assert
        doc.Id.Should().NotBe(Guid.Empty);
        doc.SharedGameId.Should().Be(sharedGameId);
        doc.PdfDocumentId.Should().Be(pdfDocumentId);
        doc.DocumentType.Should().Be(SharedGameDocumentType.Rulebook);
        doc.Version.Should().Be("1.0");
        doc.CreatedBy.Should().Be(createdBy);
        doc.IsActive.Should().BeFalse();
        doc.Tags.Should().BeEmpty();
        doc.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void Create_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGameDocument.Create(
            Guid.Empty,
            Guid.NewGuid(),
            SharedGameDocumentType.Rulebook,
            "1.0",
            Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*")
            .WithParameterName("sharedGameId");
    }

    [Fact]
    public void Create_WithEmptyPdfDocumentId_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGameDocument.Create(
            Guid.NewGuid(),
            Guid.Empty,
            SharedGameDocumentType.Rulebook,
            "1.0",
            Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PdfDocumentId cannot be empty*")
            .WithParameterName("pdfDocumentId");
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act
        var action = () => SharedGameDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SharedGameDocumentType.Rulebook,
            "1.0",
            Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*")
            .WithParameterName("createdBy");
    }

    [Theory]
    [InlineData(SharedGameDocumentType.Rulebook)]
    [InlineData(SharedGameDocumentType.Errata)]
    [InlineData(SharedGameDocumentType.Homerule)]
    public void Create_WithDifferentDocumentTypes_SetsCorrectType(SharedGameDocumentType docType)
    {
        // Act
        var doc = SharedGameDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            docType,
            "1.0",
            Guid.NewGuid());

        // Assert
        doc.DocumentType.Should().Be(docType);
    }

    [Fact]
    public void Create_WithTags_AddsTags()
    {
        // Arrange
        var tags = new[] { "variant", "house-rule", "advanced" };

        // Act
        var doc = SharedGameDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SharedGameDocumentType.Homerule,
            "1.0",
            Guid.NewGuid(),
            tags);

        // Assert
        doc.Tags.Should().HaveCount(3);
        doc.Tags.Should().Contain("variant");
        doc.Tags.Should().Contain("house-rule");
        doc.Tags.Should().Contain("advanced");
    }

    #endregion

    #region SetAsActive Tests

    [Fact]
    public void SetAsActive_SetsIsActiveTrue()
    {
        // Arrange
        var doc = CreateRulebookDocument();
        doc.IsActive.Should().BeFalse();

        // Act
        doc.SetAsActive();

        // Assert
        doc.IsActive.Should().BeTrue();
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_SetsIsActiveFalse()
    {
        // Arrange
        var doc = CreateRulebookDocument();
        doc.SetAsActive();
        doc.IsActive.Should().BeTrue();

        // Act
        doc.Deactivate();

        // Assert
        doc.IsActive.Should().BeFalse();
    }

    #endregion

    #region AddTag Tests

    [Fact]
    public void AddTag_ToHomeruleDocument_AddsTag()
    {
        // Arrange
        var doc = CreateHomeruleDocument();

        // Act
        doc.AddTag("variant");

        // Assert
        doc.Tags.Should().Contain("variant");
    }

    [Fact]
    public void AddTag_ToNonHomeruleDocument_ThrowsInvalidOperationException()
    {
        // Arrange
        var doc = CreateRulebookDocument();

        // Act
        var action = () => doc.AddTag("variant");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Tags are only allowed for Homerule documents*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddTag_WithEmptyTag_ThrowsArgumentException(string? tag)
    {
        // Arrange
        var doc = CreateHomeruleDocument();

        // Act
        var action = () => doc.AddTag(tag!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Tag cannot be empty*")
            .WithParameterName("tag");
    }

    [Fact]
    public void AddTag_NormalizesTag()
    {
        // Arrange
        var doc = CreateHomeruleDocument();

        // Act
        doc.AddTag("  My House Rule  ");

        // Assert
        doc.Tags.Should().Contain("my-house-rule");
    }

    [Fact]
    public void AddTag_RemovesSpecialCharacters()
    {
        // Arrange
        var doc = CreateHomeruleDocument();

        // Act
        doc.AddTag("Variant #1!");

        // Assert
        doc.Tags.Should().Contain("variant-1");
    }

    [Fact]
    public void AddTag_WithExceedingMaxLength_ThrowsArgumentException()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        var longTag = new string('a', 51);

        // Act
        var action = () => doc.AddTag(longTag);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Tag cannot exceed 50 characters*")
            .WithParameterName("tag");
    }

    [Fact]
    public void AddTag_WithTagContainingOnlySpecialChars_ThrowsArgumentException()
    {
        // Arrange
        var doc = CreateHomeruleDocument();

        // Act
        var action = () => doc.AddTag("!@#$%");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Tag contains no valid characters*")
            .WithParameterName("tag");
    }

    [Fact]
    public void AddTag_WhenMaxTagsReached_ThrowsInvalidOperationException()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        for (int i = 1; i <= 10; i++)
        {
            doc.AddTag($"tag{i}");
        }

        // Act
        var action = () => doc.AddTag("tag11");

        // Assert
        action.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot add more than 10 tags*");
    }

    [Fact]
    public void AddTag_WithDuplicateTag_DoesNotAddDuplicate()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("variant");

        // Act
        doc.AddTag("variant");

        // Assert
        doc.Tags.Should().HaveCount(1);
    }

    [Fact]
    public void AddTag_WithNormalizedDuplicate_DoesNotAddDuplicate()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("My Tag");

        // Act
        doc.AddTag("MY TAG");

        // Assert
        doc.Tags.Should().HaveCount(1);
    }

    #endregion

    #region RemoveTag Tests

    [Fact]
    public void RemoveTag_WithExistingTag_RemovesTag()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("variant");
        doc.Tags.Should().Contain("variant");

        // Act
        doc.RemoveTag("variant");

        // Assert
        doc.Tags.Should().NotContain("variant");
    }

    [Fact]
    public void RemoveTag_WithNonExistingTag_DoesNothing()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("variant");
        var initialCount = doc.Tags.Count;

        // Act
        doc.RemoveTag("nonexistent");

        // Assert
        doc.Tags.Count.Should().Be(initialCount);
    }

    [Fact]
    public void RemoveTag_NormalizesBeforeRemoving()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("my-tag");

        // Act
        doc.RemoveTag("  My Tag  ");

        // Assert
        doc.Tags.Should().NotContain("my-tag");
    }

    #endregion

    #region ClearTags Tests

    [Fact]
    public void ClearTags_RemovesAllTags()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("tag1");
        doc.AddTag("tag2");
        doc.AddTag("tag3");
        doc.Tags.Should().HaveCount(3);

        // Act
        doc.ClearTags();

        // Assert
        doc.Tags.Should().BeEmpty();
    }

    #endregion

    #region HasTag Tests

    [Fact]
    public void HasTag_WithExistingTag_ReturnsTrue()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("variant");

        // Act
        var result = doc.HasTag("variant");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasTag_WithNonExistingTag_ReturnsFalse()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("variant");

        // Act
        var result = doc.HasTag("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasTag_NormalizesBeforeChecking()
    {
        // Arrange
        var doc = CreateHomeruleDocument();
        doc.AddTag("my-tag");

        // Act
        var result = doc.HasTag("  My Tag  ");

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var documentType = SharedGameDocumentType.Errata;
        var version = "2.0";
        var isActive = true;
        var tags = new List<string> { "errata-v2" };
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var createdBy = Guid.NewGuid();

        // Act
        var doc = new SharedGameDocument(
            id,
            sharedGameId,
            pdfDocumentId,
            documentType,
            version,
            isActive,
            tags,
            createdAt,
            createdBy);

        // Assert
        doc.Id.Should().Be(id);
        doc.SharedGameId.Should().Be(sharedGameId);
        doc.PdfDocumentId.Should().Be(pdfDocumentId);
        doc.DocumentType.Should().Be(documentType);
        doc.Version.Should().Be(version);
        doc.IsActive.Should().Be(isActive);
        doc.Tags.Should().Contain("errata-v2");
        doc.CreatedAt.Should().Be(createdAt);
        doc.CreatedBy.Should().Be(createdBy);
    }

    [Fact]
    public void InternalConstructor_WithNullTags_CreatesEmptyList()
    {
        // Act
        var doc = new SharedGameDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            Guid.NewGuid(),
            SharedGameDocumentType.Rulebook,
            "1.0",
            false,
            null,
            DateTime.UtcNow,
            Guid.NewGuid());

        // Assert
        doc.Tags.Should().BeEmpty();
    }

    #endregion

    #region Helper Methods

    private static SharedGameDocument CreateRulebookDocument()
    {
        return SharedGameDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SharedGameDocumentType.Rulebook,
            "1.0",
            Guid.NewGuid());
    }

    private static SharedGameDocument CreateHomeruleDocument()
    {
        return SharedGameDocument.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            SharedGameDocumentType.Homerule,
            "1.0",
            Guid.NewGuid());
    }

    #endregion
}