using Api.BoundedContexts.SharedGameCatalog.Application.Commands.UpdateGameTranslation;
using Api.BoundedContexts.SharedGameCatalog.Application.Exceptions;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.UpdateGameTranslation;

/// <summary>
/// Unit tests for <see cref="UpdateGameTranslationCommandHandler"/>.
/// Issue #2339 — sub-PR 1/3 Wave 3 (Task 10).
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class UpdateGameTranslationCommandHandlerTests
{
    private static readonly DateTimeOffset SampleNow =
        new(2026, 6, 15, 12, 0, 0, TimeSpan.Zero);

    private readonly Mock<ISharedGameTranslationRepository> _translationRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();
    private readonly TimeProvider _clock;
    private readonly UpdateGameTranslationCommandHandler _sut;

    public UpdateGameTranslationCommandHandlerTests()
    {
        _clock = new FakeTimeProvider(SampleNow);
        _sut = new UpdateGameTranslationCommandHandler(
            _translationRepo.Object, _uow.Object, _clock);
    }

    [Fact]
    public async Task Handle_HappyPath_MutatesAndSaves()
    {
        var gameId = Guid.NewGuid();
        var actor = Guid.NewGuid();
        var existing = MakeActiveTranslation(gameId, "it", "Vecchio titolo");

        _translationRepo
            .Setup(r => r.GetByGameIdAndLocaleAsync(gameId, "it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(1);

        var cmd = new UpdateGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Nuovo titolo",
            Description: "Nuova descrizione",
            Xmin: 9876u,
            ActorUserId: actor);

        var result = await _sut.Handle(cmd, CancellationToken.None);

        result.Should().Be(MediatR.Unit.Value);
        existing.Title.Should().Be("Nuovo titolo");
        existing.Description.Should().Be("Nuova descrizione");
        existing.UpdatedAt.Should().Be(SampleNow);
        existing.UpdatedBy.Should().Be(actor);
        existing.Xmin.Should().Be(9876u);
        _translationRepo.Verify(
            r => r.UpdateAsync(existing, It.IsAny<CancellationToken>()),
            Times.Once);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_TranslationNotFound_ThrowsTranslationNotFoundException()
    {
        var gameId = Guid.NewGuid();
        _translationRepo
            .Setup(r => r.GetByGameIdAndLocaleAsync(gameId, "it", It.IsAny<CancellationToken>()))
            .ReturnsAsync((SharedGameTranslation?)null);

        var cmd = new UpdateGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "Foo",
            Description: null,
            Xmin: 1u,
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<TranslationNotFoundException>();
        thrown.Which.GameId.Should().Be(gameId);
        thrown.Which.Locale.Should().Be("it");
        _translationRepo.Verify(
            r => r.UpdateAsync(It.IsAny<SharedGameTranslation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ConcurrentEdit_DbUpdateConcurrencyExceptionBubbles()
    {
        var gameId = Guid.NewGuid();
        var existing = MakeActiveTranslation(gameId, "it", "title");

        _translationRepo
            .Setup(r => r.GetByGameIdAndLocaleAsync(gameId, "it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);
        _uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException(
                "Database operation expected to affect 1 row(s) but actually affected 0 row(s)."));

        var cmd = new UpdateGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "stale title",
            Description: null,
            Xmin: 1u,
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<DbUpdateConcurrencyException>();
    }

    [Fact]
    public async Task Handle_SoftDeletedTranslation_DomainGuardThrows()
    {
        var gameId = Guid.NewGuid();
        var existing = MakeActiveTranslation(gameId, "it", "title");
        existing.SoftDelete(deletedBy: null, now: SampleNow.AddHours(-1));

        _translationRepo
            .Setup(r => r.GetByGameIdAndLocaleAsync(gameId, "it", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var cmd = new UpdateGameTranslationCommand(
            GameId: gameId,
            Locale: "it",
            Title: "anything",
            Description: null,
            Xmin: 1u,
            ActorUserId: Guid.NewGuid());

        var act = async () => await _sut.Handle(cmd, CancellationToken.None);

        // SharedGameTranslation.UpdateTitle enforces the not-deleted invariant.
        await act.Should().ThrowAsync<InvalidOperationException>();
        _translationRepo.Verify(
            r => r.UpdateAsync(It.IsAny<SharedGameTranslation>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _uow.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    private static SharedGameTranslation MakeActiveTranslation(
        Guid gameId, string localeCode, string title) =>
        SharedGameTranslation.Create(
            sharedGameId: gameId,
            locale: Locale.Create(localeCode),
            title: title,
            description: null,
            source: TranslationSource.Manual,
            createdBy: Guid.NewGuid(),
            now: SampleNow.AddDays(-1));

    private sealed class FakeTimeProvider : TimeProvider
    {
        private readonly DateTimeOffset _now;
        public FakeTimeProvider(DateTimeOffset now) { _now = now; }
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
