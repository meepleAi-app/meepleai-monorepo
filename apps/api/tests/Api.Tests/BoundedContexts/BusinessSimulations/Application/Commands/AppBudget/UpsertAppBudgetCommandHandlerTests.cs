using Api.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using AppBudgetAggregate = Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets.AppBudget;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Commands.AppBudget;

/// <summary>
/// Unit tests for UpsertAppBudgetCommandHandler (Issue #1838 SP5 F4-C5).
/// Covers create vs update branches, RowVersion plumbing, and 409 translation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class UpsertAppBudgetCommandHandlerTests
{
    private readonly Mock<IAppBudgetRepository> _repo = new();

    private UpsertAppBudgetCommandHandler CreateHandler() => new(_repo.Object);

    [Fact]
    public async Task Handle_WhenBudgetMissing_CreatesNewAggregate()
    {
        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppBudgetAggregate?)null)
            .ReturnsAsync(Reconstituted(rowVersion: new byte[] { 1, 2, 3 }));

        var capture = new List<AppBudgetAggregate>();
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<AppBudgetAggregate, CancellationToken>((b, _) => capture.Add(b))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 1500m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            RowVersion: null,
            UpdatedBy: "admin");

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        capture.Should().HaveCount(1);
        capture[0].MonthlyLimit.Amount.Should().Be(1500m);
        capture[0].CreatedBy.Should().Be("admin");
        result.RowVersion.Should().Be(Convert.ToBase64String(new byte[] { 1, 2, 3 }));
    }

    [Fact]
    public async Task Handle_WhenBudgetExists_UpdatesAndPropagatesRowVersion()
    {
        var existingRowVersion = new byte[] { 5, 5, 5 };
        var existing = Reconstituted(rowVersion: existingRowVersion);

        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .ReturnsAsync(Reconstituted(rowVersion: new byte[] { 6, 6, 6 }));

        var capture = new List<AppBudgetAggregate>();
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<AppBudgetAggregate, CancellationToken>((b, _) => capture.Add(b))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 2000m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 70,
            CriticalThresholdPct: 90,
            RowVersion: Convert.ToBase64String(existingRowVersion),
            UpdatedBy: "admin2");

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        capture.Should().HaveCount(1);
        capture[0].MonthlyLimit.Amount.Should().Be(2000m);
        capture[0].AlertThresholdPct.Should().Be(70);
        capture[0].CriticalThresholdPct.Should().Be(90);
        capture[0].UpdatedBy.Should().Be("admin2");
        capture[0].RowVersion.Should().BeEquivalentTo(existingRowVersion,
            "Handler must reconstitute with client-supplied token for EF concurrency check");
        result.RowVersion.Should().Be(Convert.ToBase64String(new byte[] { 6, 6, 6 }));
    }

    [Fact]
    public async Task Handle_WithMalformedBase64RowVersion_FallsBackToServerToken()
    {
        var existing = Reconstituted(rowVersion: new byte[] { 1 });
        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .ReturnsAsync(Reconstituted(rowVersion: new byte[] { 2 }));
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 1500m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            RowVersion: "***not-base64***",
            UpdatedBy: "admin");

        // Should not throw — handler silently falls back to existing aggregate's token
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);
        result.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_OnConcurrencyConflict_Throws409()
    {
        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Reconstituted(rowVersion: new byte[] { 1 }));

        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Conflict"));

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 2000m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            RowVersion: Convert.ToBase64String(new byte[] { 1 }),
            UpdatedBy: "admin");

        var act = () => CreateHandler().Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*modified by another admin*");
    }

    [Fact]
    public async Task Handle_WhenRefetchReturnsNull_ThrowsNotFound()
    {
        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync((AppBudgetAggregate?)null)
            .ReturnsAsync((AppBudgetAggregate?)null);

        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 1500m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            RowVersion: null,
            UpdatedBy: "admin");

        var act = () => CreateHandler().Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static AppBudgetAggregate Reconstituted(byte[] rowVersion) =>
        AppBudgetAggregate.Reconstitute(
            id: Guid.NewGuid(),
            monthlyLimit: Money.Create(1000m, "USD"),
            alertThresholdPct: 80,
            criticalThresholdPct: 95,
            isEnabled: true,
            createdAt: DateTime.UtcNow,
            updatedAt: DateTime.UtcNow,
            createdBy: "seed",
            updatedBy: "seed",
            rowVersion: rowVersion);
}
