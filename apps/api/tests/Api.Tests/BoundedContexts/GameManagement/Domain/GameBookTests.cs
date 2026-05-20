using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Exceptions;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Domain;

public class GameBookTests
{
    private static readonly Guid AdminId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid UserId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid GameId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid PdfId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly GameRef SharedRef = GameRef.Shared(GameId);

    [Fact]
    public void CreateCommunity_WithValidInputs_SetsOwnerUserIdNull()
    {
        var book = GameBook.CreateCommunity(
            SharedRef, "Press Start",
            GameBookRole.Tutorial | GameBookRole.Setup,
            ParagraphScheme.None, "en",
            sequentialRead: false,
            kbSourceDocId: PdfId,
            physicalOnly: false,
            createdBy: AdminId);

        Assert.Null(book.OwnerUserId);
        Assert.Equal("Press Start", book.DisplayName);
        Assert.Equal(GameBookRole.Tutorial | GameBookRole.Setup, book.Roles);
        Assert.Equal(PdfId, book.KbSourceDocId);
        Assert.False(book.PhysicalOnly);
    }

    [Fact]
    public void CreatePersonal_WithValidInputs_SetsOwnerUserId()
    {
        var book = GameBook.CreatePersonal(
            SharedRef, UserId, "My House Rules",
            GameBookRole.RulesReference,
            ParagraphScheme.None, "it",
            sequentialRead: false,
            kbSourceDocId: PdfId,
            physicalOnly: false);

        Assert.Equal(UserId, book.OwnerUserId);
    }

    [Fact]
    public void Create_PhysicalOnlyTrueWithKbSource_ThrowsCoherenceException()
    {
        Assert.Throws<GameBookPhysicalCoherenceException>(() =>
            GameBook.CreateCommunity(
                SharedRef, "Storybook",
                GameBookRole.Narrative,
                ParagraphScheme.ParagraphNumber, "en",
                sequentialRead: true,
                kbSourceDocId: PdfId,         // INCOHERENT
                physicalOnly: true,           // INCOHERENT
                createdBy: AdminId));
    }

    [Fact]
    public void Create_EmptyRoles_ThrowsArgumentException()
    {
        Assert.Throws<ArgumentException>(() =>
            GameBook.CreateCommunity(
                SharedRef, "Empty roles",
                GameBookRole.None,            // INVALID
                ParagraphScheme.None, "en",
                sequentialRead: false,
                kbSourceDocId: null,
                physicalOnly: true,
                createdBy: AdminId));
    }

    [Fact]
    public void AttachKbSource_OnPhysicalOnlyBook_ThrowsCoherenceException()
    {
        var book = GameBook.CreateCommunity(
            SharedRef, "Cartaceo", GameBookRole.Narrative,
            ParagraphScheme.ParagraphNumber, "en", true, null, true, AdminId);

        Assert.Throws<GameBookPhysicalCoherenceException>(() =>
            book.AttachKbSource(PdfId, AdminId));
    }

    [Fact]
    public void SoftDelete_SetsIsDeletedAndDeletedAt()
    {
        var book = GameBook.CreateCommunity(
            SharedRef, "X", GameBookRole.Tutorial, ParagraphScheme.None,
            "en", false, null, true, AdminId);

        book.SoftDelete(AdminId);

        Assert.True(book.IsDeleted);
        Assert.NotNull(book.DeletedAt);
    }
}
