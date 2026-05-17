using Api.BoundedContexts.SystemConfiguration.Application.Commands.UpdateStatusBanner;
using Api.BoundedContexts.SystemConfiguration.Application.Queries;
using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Enums;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Application.Handlers;

/// <summary>
/// Issue #1089: Mock-based unit tests for status banner CQRS handlers.
/// Exercises the public API surface end-to-end (Get public / Get admin / Update).
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class StatusBannerHandlerTests
{
    private readonly Mock<IIncidentBannerRepository> _repo = new();

    [Fact]
    public async Task GetPublic_ReturnsNull_WhenInactive()
    {
        var state = IncidentBannerState.Create("hello", BannerSeverity.Info, isActive: false, null, null, null);
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handler = new GetPublicStatusBannerQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetPublicStatusBannerQuery(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetPublic_ReturnsResponse_WhenActiveAndWithinWindow()
    {
        var state = IncidentBannerState.Create("scheduled maintenance", BannerSeverity.Warning, true, null, null, "admin");
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handler = new GetPublicStatusBannerQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetPublicStatusBannerQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result!.Message.Should().Be("scheduled maintenance");
        result.Severity.Should().Be("Warning");
        result.MessageId.Should().NotBe(Guid.Empty);
    }

    [Fact]
    public async Task GetPublic_ReturnsNull_WhenActiveButOutsideWindow()
    {
        // EndsAt already passed → not visible.
        var state = IncidentBannerState.Create(
            "x", BannerSeverity.Info, true,
            startsAt: null,
            endsAt: DateTime.UtcNow.AddHours(-1),
            updatedBy: null);
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handler = new GetPublicStatusBannerQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetPublicStatusBannerQuery(), TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAdmin_ReturnsFullStateEvenWhenInactive()
    {
        var state = IncidentBannerState.Create("draft", BannerSeverity.Critical, false, null, null, "editor");
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(state);

        var handler = new GetAdminStatusBannerQueryHandler(_repo.Object);
        var result = await handler.Handle(new GetAdminStatusBannerQuery(), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        result.Message.Should().Be("draft");
        result.Severity.Should().Be("Critical");
        result.IsActive.Should().BeFalse();
        result.UpdatedBy.Should().Be("editor");
    }

    [Fact]
    public async Task Update_MutatesStateAndPersists()
    {
        var existing = IncidentBannerState.Create("old", BannerSeverity.Info, false, null, null, null);
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var handler = new UpdateStatusBannerCommandHandler(_repo.Object, NullLogger<UpdateStatusBannerCommandHandler>.Instance);
        var cmd = new UpdateStatusBannerCommand("incident in progress", "Critical", true, null, null, "admin@example.com");

        var result = await handler.Handle(cmd, TestContext.Current.CancellationToken);

        result.Message.Should().Be("incident in progress");
        result.Severity.Should().Be("Critical");
        result.IsActive.Should().BeTrue();
        result.UpdatedBy.Should().Be("admin@example.com");

        _repo.Verify(r => r.UpdateAsync(It.Is<IncidentBannerState>(s =>
            s.Message == "incident in progress" &&
            s.Severity == BannerSeverity.Critical &&
            s.IsActive &&
            s.UpdatedBy == "admin@example.com"),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Update_AcceptsLowercaseSeverity()
    {
        var existing = IncidentBannerState.Create("x", BannerSeverity.Info, false, null, null, null);
        _repo.Setup(r => r.GetAsync(It.IsAny<CancellationToken>())).ReturnsAsync(existing);

        var handler = new UpdateStatusBannerCommandHandler(_repo.Object, NullLogger<UpdateStatusBannerCommandHandler>.Instance);
        var cmd = new UpdateStatusBannerCommand("hi", "warning", true, null, null, "admin");

        var result = await handler.Handle(cmd, TestContext.Current.CancellationToken);
        result.Severity.Should().Be("Warning");
    }
}
