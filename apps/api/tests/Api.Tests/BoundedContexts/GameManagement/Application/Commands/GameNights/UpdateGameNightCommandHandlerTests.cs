using Api.BoundedContexts.GameManagement.Application.Commands.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>
/// #4055. <c>PUT /api/v1/game-nights/{id}</c> rispondeva 500 per qualunque offset diverso da UTC,
/// come la POST: <c>scheduled_at</c> e' <c>timestamptz</c> e Npgsql rifiuta un offset non zero,
/// quindi l'eccezione arrivava a <c>SaveChangesAsync</c>. Misurato sullo stack locale il
/// 2026-10-04: PUT con <c>+01:00</c> → 500, con <c>Z</c> → 204.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4055")]
public sealed class UpdateGameNightCommandHandlerTests
{
    private readonly Mock<IGameNightEventRepository> _repositoryMock = new();
    private readonly Mock<IUnitOfWork> _unitOfWorkMock = new();
    private readonly UpdateGameNightCommandHandler _sut;

    public UpdateGameNightCommandHandlerTests()
    {
        _sut = new UpdateGameNightCommandHandler(_repositoryMock.Object, _unitOfWorkMock.Object);
    }

    private GameNightEvent SeedExisting(Guid organizerId)
    {
        var night = GameNightEvent.Create(
            organizerId,
            "Serata esistente",
            new DateTimeOffset(2026, 11, 1, 18, 0, 0, TimeSpan.Zero));
        _repositoryMock
            .Setup(r => r.GetByIdAsync(night.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(night);
        return night;
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(-5)]
    public async Task Handle_WithNonUtcScheduledAt_PersistsTheSameInstantInUtc(int offsetHours)
    {
        var organizerId = Guid.NewGuid();
        var existing = SeedExisting(organizerId);
        var scheduledAt = new DateTimeOffset(2026, 12, 20, 20, 0, 0, TimeSpan.FromHours(offsetHours));
        GameNightEvent? captured = null;
        _repositoryMock
            .Setup(r => r.UpdateAsync(It.IsAny<GameNightEvent>(), It.IsAny<CancellationToken>()))
            .Callback<GameNightEvent, CancellationToken>((gn, _) => captured = gn)
            .Returns(Task.CompletedTask);

        await _sut.Handle(
            new UpdateGameNightCommand(
                GameNightId: existing.Id,
                UserId: organizerId,
                Title: "Serata aggiornata",
                ScheduledAt: scheduledAt),
            CancellationToken.None);

        captured.Should().NotBeNull();
        captured!.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
        captured.ScheduledAt.Should().Be(scheduledAt, "la normalizzazione non deve spostare l'istante");
        _unitOfWorkMock.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithUtcScheduledAt_IsUnchanged()
    {
        var organizerId = Guid.NewGuid();
        var existing = SeedExisting(organizerId);
        var scheduledAt = new DateTimeOffset(2026, 12, 20, 19, 0, 0, TimeSpan.Zero);

        await _sut.Handle(
            new UpdateGameNightCommand(
                GameNightId: existing.Id,
                UserId: organizerId,
                Title: "Serata aggiornata",
                ScheduledAt: scheduledAt),
            CancellationToken.None);

        existing.ScheduledAt.Should().Be(scheduledAt);
        existing.ScheduledAt.Offset.Should().Be(TimeSpan.Zero);
    }
}
