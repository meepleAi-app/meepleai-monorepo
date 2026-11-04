using Api.BoundedContexts.Authentication.Application.DTOs;
using Api.BoundedContexts.KbQuality.Application.Behaviors;
using Api.BoundedContexts.KbQuality.Application.Commands.StartEvaluation;
using Api.BoundedContexts.KbQuality.Application.Services;
using Api.BoundedContexts.KbQuality.Domain.Evaluation.Exceptions;
using Api.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.KbQuality.Unit.Application;

/// <summary>
/// #4166 (ADR-095 D6): il tetto di KbQuality è la quota delle valutazioni dentro il tetto unico.
/// Il behavior legge quanto ne resta dal gate del gateway invece del contatore per tenant di A1.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KbQuality")]
[Trait("Issue", "4166")]
public sealed class EvalCostCapBehaviorTests
{
    private static readonly Guid DocId = Guid.NewGuid();

    private readonly Mock<ILlmBudgetStatus> _budget = new();
    private readonly Mock<IEvaluationCostEstimator> _estimator = new();
    private readonly Mock<IHttpContextAccessor> _httpContext = new();
    private readonly DefaultHttpContext _context = new();
    private int _handlerRuns;

    public EvalCostCapBehaviorTests()
    {
        _httpContext.Setup(a => a.HttpContext).Returns(_context);
        _estimator.Setup(e => e.EstimateAsync(DocId, It.IsAny<CancellationToken>())).ReturnsAsync(0.10m);
        SignedIn(role: "user");
    }

    private EvalCostCapBehavior<StartEvaluationCommand, EvaluationStartedResult> CreateSut() =>
        new(_budget.Object, _estimator.Object, _httpContext.Object,
            NullLogger<EvalCostCapBehavior<StartEvaluationCommand, EvaluationStartedResult>>.Instance);

    private void Remaining(params decimal[] readings)
    {
        var sequence = _budget.SetupSequence(b => b.GetRemainingUsdAsync(RequestSource.Evaluation, It.IsAny<CancellationToken>()));
        foreach (var reading in readings)
        {
            sequence = sequence.ReturnsAsync(reading);
        }
    }

    private void SignedIn(string role)
    {
        var user = new UserDto(
            Id: Guid.NewGuid(), Email: "eval@meepleai.test", DisplayName: "eval", Role: role, Tier: "free",
            CreatedAt: DateTime.UtcNow, IsTwoFactorEnabled: false, TwoFactorEnabledAt: null, Level: 1, ExperiencePoints: 0);
        _context.Items[nameof(SessionStatusDto)] = new SessionStatusDto(
            IsValid: true, Principal: new Principal(user, null), ExpiresAt: DateTime.UtcNow.AddHours(1), LastSeenAt: DateTime.UtcNow);
    }

    private Task<EvaluationStartedResult> HandleAsync(bool overrideCostCap = false) =>
        CreateSut().Handle(
            new StartEvaluationCommand(DocId, GoldsetVersion: null, OverrideCostCap: overrideCostCap),
            _ =>
            {
                _handlerRuns++;
                return Task.FromResult(new EvaluationStartedResult(Guid.NewGuid(), DateTime.UtcNow, 1, DateTime.UtcNow, 0m, 0m));
            },
            TestContext.Current.CancellationToken);

    [Fact]
    public async Task AnEstimateOverWhatIsLeftOfTheEvaluationQuota_IsRefused()
    {
        Remaining(0.05m);

        var act = () => HandleAsync();

        var refused = await act.Should().ThrowAsync<CostCapExceededException>();
        refused.Which.RemainingBudgetUsd.Should().Be(0.05m, "il residuo è quello della quota del gate");
        _handlerRuns.Should().Be(0);
    }

    [Fact]
    public async Task AnEstimateWithinTheQuota_Runs_AndSurfacesWhatIsLeftAfterTheRun()
    {
        Remaining(0.15m, 0.07m);

        await HandleAsync();

        _handlerRuns.Should().Be(1);
        _context.Items[KbQualityHttpItemKeys.CostCapRemaining].Should().Be(0.07m,
            "dopo l'esecuzione il residuo si rilegge dal gate, che ha già contato le chiamate della valutazione");
        _context.Items[KbQualityHttpItemKeys.CostCapEstimate].Should().Be(0.10m);
    }

    [Fact]
    public async Task TheActualCostOfTheRun_ReplacesTheEstimateInTheResponse()
    {
        Remaining(0.15m, 0.14m);
        _estimator.Setup(e => e.EstimateAsync(DocId, It.IsAny<CancellationToken>())).ReturnsAsync(0.10m);

        await CreateSut().Handle(
            new StartEvaluationCommand(DocId, null, false),
            _ =>
            {
                _context.Items[KbQualityHttpItemKeys.ActualCostUsd] = 0.01m;
                return Task.FromResult(new EvaluationStartedResult(Guid.NewGuid(), DateTime.UtcNow, 1, DateTime.UtcNow, 0m, 0m));
            },
            TestContext.Current.CancellationToken);

        _context.Items[KbQualityHttpItemKeys.CostCapEstimate].Should().Be(0.01m);
    }

    [Fact]
    public async Task AnAdminOverride_SkipsTheEstimate()
    {
        SignedIn(role: "admin");
        Remaining(0m, 0m);

        await HandleAsync(overrideCostCap: true);

        _handlerRuns.Should().Be(1, "l'override salta la stima; le chiamate le ferma comunque il gate alla quota");
    }

    [Fact]
    public async Task AnOverrideByANonAdmin_IsRefused()
    {
        Remaining(0m);

        var act = () => HandleAsync(overrideCostCap: true);

        await act.Should().ThrowAsync<UnauthorizedAccessException>();
        _handlerRuns.Should().Be(0);
    }
}
