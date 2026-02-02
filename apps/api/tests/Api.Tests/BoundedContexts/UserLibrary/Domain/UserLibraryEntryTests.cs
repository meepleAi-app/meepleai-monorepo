using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Domain.Events;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Domain;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class UserLibraryEntryTests
{
    [Fact]
    public void AssociatePrivatePdf_WithValidPdfId_SetsPrivatePdfId()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        var pdfId = Guid.NewGuid();

        // Act
        entry.AssociatePrivatePdf(pdfId);

        // Assert
        entry.PrivatePdfId.Should().Be(pdfId);
    }

    [Fact]
    public void AssociatePrivatePdf_WithValidPdfId_RaisesPrivatePdfAssociatedEvent()
    {
        // Arrange
        var entryId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var entry = new UserLibraryEntry(entryId, userId, gameId);
        entry.ClearDomainEvents(); // Clear GameAddedToLibraryEvent from constructor
        var pdfId = Guid.NewGuid();

        // Act
        entry.AssociatePrivatePdf(pdfId);

        // Assert
        var domainEvent = entry.DomainEvents.Should().ContainSingle()
            .Which.Should().BeOfType<PrivatePdfAssociatedEvent>().Subject;
        domainEvent.LibraryEntryId.Should().Be(entryId);
        domainEvent.UserId.Should().Be(userId);
        domainEvent.GameId.Should().Be(gameId);
        domainEvent.PdfDocumentId.Should().Be(pdfId);
        domainEvent.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void AssociatePrivatePdf_WithEmptyGuid_ThrowsArgumentException()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var act = () => entry.AssociatePrivatePdf(Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*PDF document ID cannot be empty*");
    }

    [Fact]
    public void HasPrivatePdf_WhenPrivatePdfIdIsSet_ReturnsTrue()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AssociatePrivatePdf(Guid.NewGuid());

        // Act
        var result = entry.HasPrivatePdf;

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasPrivatePdf_WhenPrivatePdfIdIsNull_ReturnsFalse()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = entry.HasPrivatePdf;

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ResetPdfToShared_ClearsPrivatePdfId()
    {
        // Arrange
        var entry = new UserLibraryEntry(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        entry.AssociatePrivatePdf(Guid.NewGuid());

        // Act
        entry.ResetPdfToShared();

        // Assert
        entry.PrivatePdfId.Should().BeNull();
        entry.HasPrivatePdf.Should().BeFalse();
    }
}
