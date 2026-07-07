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
/// Covers create vs update branches, xmin plumbing, and 409 translation.
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
            .ReturnsAsync(Reconstituted(xmin: 123u));

        var capture = new List<AppBudgetAggregate>();
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<AppBudgetAggregate, CancellationToken>((b, _) => capture.Add(b))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 1500m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            Xmin: null,
            UpdatedBy: "admin");

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        capture.Should().HaveCount(1);
        capture[0].MonthlyLimit.Amount.Should().Be(1500m);
        capture[0].CreatedBy.Should().Be("admin");
        result.Xmin.Should().Be(123u);
    }

    [Fact]
    public async Task Handle_WhenBudgetExists_UpdatesAndPropagatesXmin()
    {
        const uint existingXmin = 555u;
        var existing = Reconstituted(xmin: existingXmin);

        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .ReturnsAsync(Reconstituted(xmin: 666u));

        var capture = new List<AppBudgetAggregate>();
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<AppBudgetAggregate, CancellationToken>((b, _) => capture.Add(b))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 2000m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 70,
            CriticalThresholdPct: 90,
            Xmin: existingXmin,
            UpdatedBy: "admin2");

        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        capture.Should().HaveCount(1);
        capture[0].MonthlyLimit.Amount.Should().Be(2000m);
        capture[0].AlertThresholdPct.Should().Be(70);
        capture[0].CriticalThresholdPct.Should().Be(90);
        capture[0].UpdatedBy.Should().Be("admin2");
        capture[0].Xmin.Should().Be(existingXmin,
            "Handler must carry the client-supplied token for the EF concurrency check");
        result.Xmin.Should().Be(666u);
    }

    [Fact]
    public async Task Handle_WithNullXminOnUpdate_UsesServerToken()
    {
        var existing = Reconstituted(xmin: 42u);
        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(existing)
            .ReturnsAsync(Reconstituted(xmin: 43u));

        var capture = new List<AppBudgetAggregate>();
        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .Callback<AppBudgetAggregate, CancellationToken>((b, _) => capture.Add(b))
            .Returns(Task.CompletedTask);

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 1500m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            Xmin: null,
            UpdatedBy: "admin");

        // Should not throw — with no client token the handler keeps the
        // server-loaded aggregate's token.
        var result = await CreateHandler().Handle(cmd, CancellationToken.None);

        result.Should().NotBeNull();
        capture.Should().HaveCount(1);
        capture[0].Xmin.Should().Be(42u,
            "with no client token the handler keeps the server-loaded xmin");
    }

    [Fact]
    public async Task Handle_OnConcurrencyConflict_Throws409()
    {
        _repo.SetupSequence(r => r.GetCurrentAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Reconstituted(xmin: 1u));

        _repo.Setup(r => r.UpsertAsync(It.IsAny<AppBudgetAggregate>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new DbUpdateConcurrencyException("Conflict"));

        var cmd = new UpsertAppBudgetCommand(
            MonthlyLimitAmount: 2000m,
            MonthlyLimitCurrency: "USD",
            AlertThresholdPct: 80,
            CriticalThresholdPct: 95,
            Xmin: 1u,
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
            Xmin: null,
            UpdatedBy: "admin");

        var act = () => CreateHandler().Handle(cmd, CancellationToken.None);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    private static AppBudgetAggregate Reconstituted(uint xmin) =>
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
            xmin: xmin);
}
