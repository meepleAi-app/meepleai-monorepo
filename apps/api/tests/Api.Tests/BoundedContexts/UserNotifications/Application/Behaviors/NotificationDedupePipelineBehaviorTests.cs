using System.Reflection;
using Api.BoundedContexts.UserNotifications.Application.Behaviors;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Application.Behaviors;

[Trait("Category", TestCategories.Unit)]
public sealed class NotificationDedupePipelineBehaviorTests
{
    private static NotificationDedupePipelineBehavior<DummyRequest, DummyResponse> CreateSut() =>
        new(NullLogger<NotificationDedupePipelineBehavior<DummyRequest, DummyResponse>>.Instance);

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
    public async Task Handle_HandlerThrowsUniqueViolationOnDedupConstraint_SwallowsAndReturnsDefault()
    {
        var sut = CreateSut();
        RequestHandlerDelegate<DummyResponse> next = _ =>
            throw BuildDedupViolation();

        var result = await sut.Handle(new DummyRequest(), next, TestContext.Current.CancellationToken);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_HandlerThrowsUniqueViolationOnDifferentConstraint_Rethrows()
    {
        var sut = CreateSut();
        RequestHandlerDelegate<DummyResponse> next = _ =>
            throw BuildDbUpdateException(sqlState: "23505", constraintName: "IX_some_other_unique_index");

        var act = async () => await sut.Handle(new DummyRequest(), next, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
    }

    [Fact]
    public async Task Handle_HandlerThrowsDifferentPostgresErrorCode_Rethrows()
    {
        var sut = CreateSut();
        RequestHandlerDelegate<DummyResponse> next = _ =>
            throw BuildDbUpdateException(sqlState: "23503", constraintName: NotificationDedupePipelineBehavior<DummyRequest, DummyResponse>.NotificationDedupConstraintName);

        var act = async () => await sut.Handle(new DummyRequest(), next, TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<DbUpdateException>();
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

    // ─── Helpers ──────────────────────────────────────────────────────────

    private static DbUpdateException BuildDedupViolation() =>
        BuildDbUpdateException(
            sqlState: "23505",
            constraintName: NotificationDedupePipelineBehavior<DummyRequest, DummyResponse>.NotificationDedupConstraintName);

    private static DbUpdateException BuildDbUpdateException(string sqlState, string constraintName)
    {
        // PostgresException.ConstraintName is derived from the internal _msg field
        // (ErrorOrNoticeMessage); it is not settable via the public ctor. Reflection
        // is used here to inject the ConstraintName for race-simulation tests.
        var pgEx = new PostgresException(
            messageText: "duplicate key value violates unique constraint",
            severity: "ERROR",
            invariantSeverity: "ERROR",
            sqlState: sqlState);

        var msgField = typeof(PostgresException).GetField("_msg",
            BindingFlags.NonPublic | BindingFlags.Instance);
        msgField.Should().NotBeNull("PostgresException._msg internal field must exist " +
            "(check Npgsql version compatibility if this fails)");

        var msg = msgField!.GetValue(pgEx);
        msg.Should().NotBeNull();

        var constraintProp = msg!.GetType().GetProperty("ConstraintName",
            BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
        constraintProp.Should().NotBeNull("ErrorOrNoticeMessage.ConstraintName must exist");
        constraintProp!.SetValue(msg, constraintName);

        return new DbUpdateException("simulated 23505", pgEx);
    }

    // ─── Test doubles ──────────────────────────────────────────────────────

    public sealed record DummyRequest : IRequest<DummyResponse>;

    public sealed record DummyResponse(string Value);
}
