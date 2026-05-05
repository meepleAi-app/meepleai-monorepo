using Api.BoundedContexts.Authentication.Application.Commands.Waitlist;
using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Handlers;

/// <summary>
/// Unit tests for <see cref="JoinWaitlistCommandHandler"/>.
/// Spec §3.5 (2026-04-27-v2-migration-wave-a-2-join.md) — RED phase.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class JoinWaitlistCommandHandlerTests
{
    private readonly Mock<IWaitlistEntryRepository> _mockRepository;
    private readonly Mock<IUnitOfWork> _mockUnitOfWork;
    private readonly Mock<ILogger<JoinWaitlistCommandHandler>> _mockLogger;
    private readonly JoinWaitlistCommandHandler _handler;

    public JoinWaitlistCommandHandlerTests()
    {
        _mockRepository = new Mock<IWaitlistEntryRepository>();
        _mockUnitOfWork = new Mock<IUnitOfWork>();
        _mockLogger = new Mock<ILogger<JoinWaitlistCommandHandler>>();

        _handler = new JoinWaitlistCommandHandler(
            _mockRepository.Object,
            _mockUnitOfWork.Object,
            _mockLogger.Object);
    }

    private static JoinWaitlistCommand BuildCommand(
        string email = "alice@example.com",
        string? name = "Alice",
        string gameId = "g-azul",
        string? gameOther = null,
        bool optIn = false) =>
        new(email, name, gameId, gameOther, optIn);

    [Fact]
    public async Task Handle_WithNewEmail_CreatesEntryAtPosition1()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.IsAlreadyOnList.Should().BeFalse();
        result.Position.Should().Be(1);
        result.EstimatedWeeks.Should().Be(1);

        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e =>
                e.Email == "alice@example.com" &&
                e.Position == 1 &&
                e.GamePreferenceId == "g-azul"),
                It.IsAny<CancellationToken>()),
            Times.Once);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingMaxPosition_AssignsNextSequentialPosition()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(42);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.Position.Should().Be(43);
        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e => e.Position == 43), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_WithExistingEmail_ReturnsAlreadyOnListWithExistingPosition()
    {
        var existing = WaitlistEntry.Create(
            email: "alice@example.com",
            name: "Alice",
            gamePreferenceId: "g-azul",
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 17);

        _mockRepository
            .Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.IsAlreadyOnList.Should().BeTrue();
        result.Position.Should().Be(17);
        result.EstimatedWeeks.Should().Be(1);

        _mockRepository.Verify(
            r => r.AddAsync(It.IsAny<WaitlistEntry>(), It.IsAny<CancellationToken>()),
            Times.Never);
        _mockUnitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_NormalizesEmailToLowercaseBeforeLookup()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        await _handler.Handle(BuildCommand(email: "Alice@EXAMPLE.COM"), CancellationToken.None);

        _mockRepository.Verify(
            r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()),
            Times.Once);
        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e => e.Email == "alice@example.com"), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_DuplicateUppercaseEmail_DetectsAlreadyOnList()
    {
        var existing = WaitlistEntry.Create(
            email: "alice@example.com",
            name: null,
            gamePreferenceId: "g-azul",
            gamePreferenceOther: null,
            newsletterOptIn: false,
            position: 5);

        _mockRepository
            .Setup(r => r.GetByEmailAsync("alice@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing);

        var result = await _handler.Handle(BuildCommand(email: "ALICE@example.com"), CancellationToken.None);

        result.IsAlreadyOnList.Should().BeTrue();
        result.Position.Should().Be(5);
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(50, 1)]
    [InlineData(100, 1)]
    [InlineData(101, 2)]
    [InlineData(200, 2)]
    [InlineData(247, 3)]
    [InlineData(1000, 10)]
    public async Task Handle_ComputesEstimatedWeeksAsCeilingOfPositionDivided100(int maxPosition, int expectedWeeks)
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(maxPosition - 1);

        var result = await _handler.Handle(BuildCommand(), CancellationToken.None);

        result.Position.Should().Be(maxPosition);
        result.EstimatedWeeks.Should().Be(expectedWeeks);
    }

    [Fact]
    public async Task Handle_PreservesNewsletterOptInTrue()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        await _handler.Handle(BuildCommand(optIn: true), CancellationToken.None);

        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e => e.NewsletterOptIn), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_PreservesNewsletterOptInFalseAsDefault()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        await _handler.Handle(BuildCommand(optIn: false), CancellationToken.None);

        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e => !e.NewsletterOptIn), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StoresGameOtherFreeText()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        await _handler.Handle(
            BuildCommand(gameId: "g-other", gameOther: "Terraforming Mars"),
            CancellationToken.None);

        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e =>
                e.GamePreferenceId == "g-other" &&
                e.GamePreferenceOther == "Terraforming Mars"),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_StoresNullName_WhenNameOmitted()
    {
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((int?)null);

        await _handler.Handle(BuildCommand(name: null), CancellationToken.None);

        _mockRepository.Verify(
            r => r.AddAsync(It.Is<WaitlistEntry>(e => e.Name == null), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task Handle_AssignsMonotonicPositionsAcrossSequentialCalls()
    {
        // Simulate 3 sequential inserts where repository max-position grows
        var positions = new Queue<int?>(new int?[] { null, 1, 2 });
        _mockRepository
            .Setup(r => r.GetByEmailAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((WaitlistEntry?)null);
        _mockRepository
            .Setup(r => r.GetMaxPositionAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(() => positions.Dequeue());

        var r1 = await _handler.Handle(BuildCommand(email: "a@x.com"), CancellationToken.None);
        var r2 = await _handler.Handle(BuildCommand(email: "b@x.com"), CancellationToken.None);
        var r3 = await _handler.Handle(BuildCommand(email: "c@x.com"), CancellationToken.None);

        r1.Position.Should().Be(1);
        r2.Position.Should().Be(2);
        r3.Position.Should().Be(3);
    }
}
