using System.Text.Json;
using System.Text.Json.Nodes;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.Queries.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Time.Testing;
using Moq;
using Xunit;

using Snapshot = Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot.SessionSnapshot;

namespace Api.Tests.BoundedContexts.GameManagement.Application.Handlers.SessionSnapshot;

/// <summary>
/// #4869 — a snapshot with photos linked to it stores their references next to its data (#5367). A checkpoint keeps
/// them on the root of the state (<c>_attachments</c>); a delta is wrapped in <c>{"_delta": [...], "_attachments":
/// [...]}</c>. Nothing read that envelope: <see cref="JsonDeltaHelper.ApplyDelta"/> took the delta for an array and
/// threw, so every rebuild that went through such a snapshot failed — the state query, the restore, and the creation
/// of the NEXT snapshot, which rebuilds the previous state to work out its delta — until the next checkpoint.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "GameManagement")]
[Trait("Issue", "4869")]
public sealed class SnapshotAttachmentEnvelopeTests
{
    private static readonly Guid SessionId = Guid.NewGuid();

    /// <summary>The state at a turn. Turn 1 alone has a «bonus» key, so a delta worked out from the wrong base state differs.</summary>
    private static string State(int turn)
    {
        var state = new JsonObject { ["turn"] = turn, ["scores"] = new JsonObject { ["a"] = turn * 10 } };
        if (turn == 1)
            state["bonus"] = true;
        return state.ToJsonString();
    }

    private static IReadOnlyList<SessionAttachment> Photos(int snapshotIndex) =>
    [
        SessionAttachment.Create(
            SessionId, Guid.NewGuid(), AttachmentType.BoardState, "https://blob.test/board.jpg", "image/jpeg", 4096,
            thumbnailUrl: "https://blob.test/board-thumb.jpg", caption: "Plancia", snapshotIndex: snapshotIndex),
    ];

    /// <summary>The checkpoint of turn 0 as the command stores it when photos are linked to it.</summary>
    private static Snapshot CheckpointWithPhotos() =>
        new(Guid.NewGuid(), SessionId, 0, SnapshotTrigger.ManualSave, "start",
            CreateSnapshotCommandHandler.EmbedAttachmentReferences(State(0), Photos(0)), true, 0, null, null);

    /// <summary>The delta from turn 0 to turn 1 as the command stores it when photos are linked to it.</summary>
    private static Snapshot DeltaWithPhotos() =>
        new(Guid.NewGuid(), SessionId, 1, SnapshotTrigger.TurnAdvanced, "t1",
            CreateSnapshotCommandHandler.EmbedAttachmentReferences(JsonDeltaHelper.ComputeDelta(State(0), State(1)), Photos(1)),
            false, 1, null, null);

    private static void ShouldBeState(string actualJson, string expectedJson) =>
        JsonNode.DeepEquals(JsonNode.Parse(actualJson), JsonNode.Parse(expectedJson))
            .Should().BeTrue($"expected {expectedJson} but got {actualJson}");

    // ── JsonDeltaHelper ─────────────────────────────────────────────────────────────────────────

    [Fact]
    public void ApplyDelta_ToAnEnvelope_AppliesTheOperationsOfItsDelta()
    {
        var envelope = DeltaWithPhotos().DeltaDataJson;

        var result = JsonDeltaHelper.ApplyDelta(State(0), envelope);

        ShouldBeState(result, State(1));
    }

    [Fact]
    public void ApplyDelta_ToAnEnvelopeWithoutDelta_ReturnsTheBaseState()
    {
        var result = JsonDeltaHelper.ApplyDelta(State(0), """{"_attachments":[]}""");

        ShouldBeState(result, State(0));
    }

    [Fact]
    public void ApplyDelta_ToAPlainArray_StillWorks()
    {
        var result = JsonDeltaHelper.ApplyDelta(State(0), JsonDeltaHelper.ComputeDelta(State(0), State(1)));

        ShouldBeState(result, State(1));
    }

    [Fact]
    public void ReconstructState_DropsTheAttachmentReferencesOfACheckpoint()
    {
        var result = JsonDeltaHelper.ReconstructState(CheckpointWithPhotos().DeltaDataJson, []);

        ShouldBeState(result, State(0));
    }

    [Fact]
    public void ReconstructState_ThroughAnEnvelope_GivesTheStateTheSessionHad()
    {
        var result = JsonDeltaHelper.ReconstructState(
            CheckpointWithPhotos().DeltaDataJson, [DeltaWithPhotos().DeltaDataJson]);

        ShouldBeState(result, State(1));
    }

    // ── The three places that rebuild a state ───────────────────────────────────────────────────

    [Fact]
    public async Task CreateSnapshot_AfterASnapshotWithPhotos_DoesNotFailAndStoresTheRightDelta()
    {
        var checkpoint = CheckpointWithPhotos();
        var withPhotos = DeltaWithPhotos();
        var snapshots = new Mock<ISessionSnapshotRepository>();
        snapshots.Setup(r => r.GetLatestBySessionIdAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(withPhotos);
        snapshots.Setup(r => r.GetSnapshotsForReconstructionAsync(SessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([checkpoint, withPhotos]);
        Snapshot? added = null;
        snapshots.Setup(r => r.AddAsync(It.IsAny<Snapshot>(), It.IsAny<CancellationToken>()))
            .Callback<Snapshot, CancellationToken>((s, _) => added = s).Returns(Task.CompletedTask);

        var session = LiveGameSession.Create(SessionId, Guid.NewGuid(), "Test Game");
        session.UpdateGameState(JsonDocument.Parse(State(2)));
        var sessions = new Mock<ILiveSessionRepository>();
        sessions.Setup(r => r.GetByIdAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var attachments = new Mock<ISessionAttachmentRepository>();
        attachments.Setup(r => r.GetBySnapshotAsync(SessionId, It.IsAny<int>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<SessionAttachment>());
        var handler = new CreateSnapshotCommandHandler(
            snapshots.Object, sessions.Object, attachments.Object, new Mock<IUnitOfWork>().Object);

        var result = await handler.Handle(
            new CreateSnapshotCommand(SessionId, SnapshotTrigger.TurnAdvanced, "t2", null),
            TestContext.Current.CancellationToken);

        result.SnapshotIndex.Should().Be(2);
        added.Should().NotBeNull();
        // The delta is worked out against the state turn 1 had, without the references of the photos.
        ShouldBeState(JsonDeltaHelper.ApplyDelta(State(1), added!.DeltaDataJson), State(2));
    }

    [Fact]
    public async Task GetSnapshotState_ThroughASnapshotWithPhotos_ReturnsTheStateWithoutTheReferences()
    {
        var snapshots = new Mock<ISessionSnapshotRepository>();
        snapshots.Setup(r => r.GetSnapshotsForReconstructionAsync(SessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CheckpointWithPhotos(), DeltaWithPhotos()]);

        var result = await new GetSnapshotStateQueryHandler(snapshots.Object)
            .Handle(new GetSnapshotStateQuery(SessionId, 1), TestContext.Current.CancellationToken);

        result.Should().NotBeNull();
        ShouldBeState(result!.State.GetRawText(), State(1));
    }

    [Fact]
    public async Task RestoreSnapshot_ThroughASnapshotWithPhotos_WritesTheStateWithoutTheReferences()
    {
        var time = new FakeTimeProvider(new DateTimeOffset(2026, 11, 4, 12, 0, 0, TimeSpan.Zero));
        var session = LiveGameSession.Create(SessionId, Guid.NewGuid(), "Test Game", time);
        session.AddPlayer(Guid.NewGuid(), "Player 1", PlayerColor.Red, time);
        session.Start(time);
        session.UpdateGameState(JsonDocument.Parse(State(5)), time);
        var sessions = new Mock<ILiveSessionRepository>();
        sessions.Setup(r => r.GetByIdAsync(SessionId, It.IsAny<CancellationToken>())).ReturnsAsync(session);
        var target = DeltaWithPhotos();
        var snapshots = new Mock<ISessionSnapshotRepository>();
        snapshots.Setup(r => r.GetBySessionAndIndexAsync(SessionId, 1, It.IsAny<CancellationToken>())).ReturnsAsync(target);
        snapshots.Setup(r => r.GetSnapshotsForReconstructionAsync(SessionId, 1, It.IsAny<CancellationToken>()))
            .ReturnsAsync([CheckpointWithPhotos(), target]);
        var mediator = new Mock<IMediator>();
        mediator.Setup(m => m.Send(It.IsAny<CreateSnapshotCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SessionSnapshotDto(Guid.NewGuid(), SessionId, 6, SnapshotTrigger.PreRestore, "pre", false, 5, 0, DateTime.UtcNow, null));
        var handler = new RestoreSessionSnapshotCommandHandler(
            snapshots.Object, sessions.Object, mediator.Object, time, new Mock<IUnitOfWork>().Object);

        await handler.Handle(new RestoreSessionSnapshotCommand(SessionId, 1), TestContext.Current.CancellationToken);

        ShouldBeState(session.GameState!.RootElement.GetRawText(), State(1));
    }
}
