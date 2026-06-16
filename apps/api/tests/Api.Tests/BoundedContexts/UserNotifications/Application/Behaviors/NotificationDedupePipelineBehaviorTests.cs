using Api.BoundedContexts.UserNotifications.Application.Behaviors;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Behaviors;

[Trait("Category", TestCategories.Unit)]
public sealed class NotificationDedupePipelineBehaviorTests
{
    private static NotificationDedupePipelineBehavior<DummyRequest, DummyResponse> CreateSut() =>
        new(NullLogger<NotificationDedupePipelineBehavior<DummyRequest, DummyResponse>>.Instance);

    // ─── Predicate (extracted for unit testability without PostgresException) ──

    [Theory]
    [InlineData("23505", "UX_notifications_user_source_event_id", true)]
    [InlineData("23505", "ux_notifications_user_source_event_id", false)] // case-sensitive
    [InlineData("23505", "IX_some_other_unique_index", false)]
    [InlineData("23503", "UX_notifications_user_source_event_id", false)] // FK violation, not unique
    [InlineData("23505", null, false)]
    [InlineData(null, "UX_notifications_user_source_event_id", false)]
    [InlineData(null, null, false)]
    public void IsNotificationDedupViolation_PredicateBehavior(
        string? sqlState, string? constraintName, bool expected)
    {
        var result = NotificationDedupePipelineBehavior<DummyRequest, DummyResponse>
            .IsNotificationDedupViolation(sqlState, constraintName);

        result.Should().Be(expected);
    }

    // ─── Behavior pipeline ────────────────────────────────────────────────

    [Fact]
    public async Task Handle_HandlerSucceeds_ReturnsResponseUnchanged()
    {
        var sut = CreateSut();
        var expectedResponse = new DummyResponse("ok");
        RequestHandlerDelegate<DummyResponse> next = _ => Task.FromResult(expectedResponse);

        var result = await sut.Handle(new DummyRequest(), next, TestContext.Current.CancellationToken);

        result.Should().BeSameAs(expectedResponse);
    }

    [Fact]
    public async Task Handle_HandlerThrowsDbUpdateExceptionWithoutPostgresInner_Rethrows()
    {
        var sut = CreateSut();
        RequestHandlerDelegate<DummyResponse> next = _ =>
            throw new DbUpdateException("plain wrapper", new InvalidOperationException("not postgres"));

        var act = async () => await sut.Handle(new DummyRequest(), next, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Handle_HandlerThrowsArbitraryException_Rethrows()
    {
        var sut = CreateSut();
        RequestHandlerDelegate<DummyResponse> next = _ =>
            throw new InvalidOperationException("arbitrary domain failure");

        var act = async () => await sut.Handle(new DummyRequest(), next, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<InvalidOperationException>();
    }

    // ─── Construction ──────────────────────────────────────────────────────

    [Fact]
    public void Constructor_NullLogger_Throws()
    {
        var act = () => new NotificationDedupePipelineBehavior<DummyRequest, DummyResponse>(null!);

        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public async Task Handle_NullNextDelegate_Throws()
    {
        var sut = CreateSut();

        var act = async () => await sut.Handle(new DummyRequest(), null!, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    // ─── Test doubles ──────────────────────────────────────────────────────

    public sealed record DummyRequest : IRequest<DummyResponse>;

    public sealed record DummyResponse(string Value);

    // ─── Notes on coverage ─────────────────────────────────────────────────
    //
    // The catch-and-swallow path (DbUpdateException wrapping a PostgresException with
    // SQLSTATE 23505 + matching ConstraintName) cannot be exercised in a pure unit test
    // because PostgresException.ConstraintName is derived from the Npgsql internal wire
    // message (read-only post-construction in Npgsql 10.x). The predicate
    // IsNotificationDedupViolation(sqlState, constraintName) above covers the matching
    // logic in isolation; an integration test against a real Postgres (Testcontainers)
    // can verify the end-to-end catch path when the dispatcher integration lands as a
    // follow-up PR.
}
