using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Application.GameNight;

/// <summary>
/// #4744: the retry that gives an invitation write its second look. The handlers' tests cover what they decide on
/// that second look; these cover the loop itself.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4744")]
public sealed class InvitationWriteRetryTests
{
    private readonly Mock<IDomainEventCollector> _events = new();

    private static DbUpdateConcurrencyException Refused() => new("another writer got there first");

    [Fact]
    public async Task AnAttemptThatSucceeds_RunsOnce_AndClearsNothing()
    {
        var runs = 0;

        await InvitationWriteRetry.RunAsync(_ => { runs++; return Task.CompletedTask; }, _events.Object, CancellationToken.None);

        runs.Should().Be(1);
        _events.Verify(e => e.Clear(), Times.Never);
    }

    [Fact]
    public async Task ARefusalIsFollowedByANewAttempt_ThatSeesTheClearedEvents()
    {
        var runs = 0;
        var clearedBeforeTheSecondRun = false;
        _events.Setup(e => e.Clear()).Callback(() => clearedBeforeTheSecondRun = runs == 1);

        await InvitationWriteRetry.RunAsync(
            _ =>
            {
                runs++;
                return runs == 1 ? Task.FromException(Refused()) : Task.CompletedTask;
            },
            _events.Object,
            CancellationToken.None);

        runs.Should().Be(2);
        clearedBeforeTheSecondRun.Should().BeTrue("the events of the refused attempt go before the next one collects its own");
    }

    [Fact]
    public async Task AfterTheLastAllowedAttempt_TheCallerGetsAConflict_WithTheRefusalAsItsCause()
    {
        var runs = 0;

        var act = () => InvitationWriteRetry.RunAsync(
            _ => { runs++; return Task.FromException(Refused()); }, _events.Object, CancellationToken.None);

        var thrown = await act.Should().ThrowAsync<ConflictException>();
        thrown.Which.InnerException.Should().BeOfType<DbUpdateConcurrencyException>();
        runs.Should().Be(InvitationWriteRetry.MaxAttempts);
    }

    [Fact]
    public async Task AnyOtherException_PropagatesAtOnce_WithoutANewAttempt()
    {
        var runs = 0;

        var act = () => InvitationWriteRetry.RunAsync(
            _ => { runs++; return Task.FromException(new InvalidOperationException("db down")); },
            _events.Object,
            CancellationToken.None);

        await act.Should().ThrowAsync<InvalidOperationException>();
        runs.Should().Be(1);
        _events.Verify(e => e.Clear(), Times.Never);
    }

    [Fact]
    public async Task ThePassedTokenReachesEveryAttempt()
    {
        using var cts = new CancellationTokenSource();
        var seen = new List<CancellationToken>();
        var runs = 0;

        await InvitationWriteRetry.RunAsync(
            ct =>
            {
                seen.Add(ct);
                runs++;
                return runs == 1 ? Task.FromException(Refused()) : Task.CompletedTask;
            },
            _events.Object,
            cts.Token);

        seen.Should().OnlyContain(t => t == cts.Token).And.HaveCount(2);
    }
}
