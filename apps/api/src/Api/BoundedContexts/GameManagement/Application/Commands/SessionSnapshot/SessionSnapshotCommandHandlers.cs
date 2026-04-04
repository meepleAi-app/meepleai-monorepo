using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Application.DTOs.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionAttachment;
using Api.BoundedContexts.GameManagement.Domain.Entities.SessionSnapshot;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.GameManagement.Application.Commands.SessionSnapshot;

/// <summary>
/// Creates a session snapshot with delta-based storage.
/// Snapshot 0 is always a full checkpoint; every 10th snapshot is a checkpoint.
/// Non-checkpoint snapshots store JSON Patch deltas only.
/// Issue #4755: SessionSnapshot - Delta-based History + State Reconstruction.
/// </summary>
internal class CreateSnapshotCommandHandler
    : ICommandHandler<CreateSnapshotCommand, SessionSnapshotDto>
{
    private readonly ISessionSnapshotRepository _snapshotRepository;
    private readonly ILiveSessionRepository _sessionRepository;
    private readonly ISessionAttachmentRepository _attachmentRepository;
    private readonly IUnitOfWork _unitOfWork;

    public CreateSnapshotCommandHandler(
        ISessionSnapshotRepository snapshotRepository,
        ILiveSessionRepository sessionRepository,
        ISessionAttachmentRepository attachmentRepository,
        IUnitOfWork unitOfWork)
    {
        _snapshotRepository = snapshotRepository ?? throw new ArgumentNullException(nameof(snapshotRepository));
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _attachmentRepository = attachmentRepository ?? throw new ArgumentNullException(nameof(attachmentRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
    }

    public async Task<SessionSnapshotDto> Handle(
        CreateSnapshotCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Get the live session to capture current state
        var session = await _sessionRepository.GetByIdAsync(command.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", command.SessionId.ToString());

        // Serialize current game state
        var currentStateJson = SerializeGameState(session.GameState);

        // Get latest snapshot to determine next index
        var latestSnapshot = await _snapshotRepository.GetLatestBySessionIdAsync(
            command.SessionId, cancellationToken).ConfigureAwait(false);

        var nextIndex = latestSnapshot == null ? 0 : latestSnapshot.SnapshotIndex + 1;
        var isCheckpoint = Domain.Entities.SessionSnapshot.SessionSnapshot.ShouldBeCheckpoint(nextIndex);

        string deltaDataJson;
        if (isCheckpoint || latestSnapshot == null)
        {
            // Store full state for checkpoints
            deltaDataJson = currentStateJson;
        }
        else
        {
            // Compute delta from previous state
            var previousState = await ReconstructStateAsync(
                command.SessionId, latestSnapshot.SnapshotIndex, cancellationToken)
                .ConfigureAwait(false);

            deltaDataJson = JsonDeltaHelper.ComputeDelta(previousState, currentStateJson);
        }

        // Query attachments linked to this snapshot index (Issue #5367)
        var attachments = await _attachmentRepository.GetBySnapshotAsync(
            command.SessionId, nextIndex, cancellationToken).ConfigureAwait(false);

        // Embed attachment references in delta JSON if any exist
        if (attachments.Count > 0)
        {
            deltaDataJson = EmbedAttachmentReferences(deltaDataJson, attachments);
        }

        var snapshot = new Domain.Entities.SessionSnapshot.SessionSnapshot(
            Guid.NewGuid(),
            command.SessionId,
            nextIndex,
            command.TriggerType,
            command.TriggerDescription,
            deltaDataJson,
            isCheckpoint,
            session.CurrentTurnIndex,
            session.CurrentPhaseIndex,
            command.CreatedByPlayerId);

        await _snapshotRepository.AddAsync(snapshot, cancellationToken).ConfigureAwait(false);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return SessionSnapshotMapper.ToDto(snapshot, attachments.Count);
    }

    private async Task<string> ReconstructStateAsync(
        Guid sessionId, int targetIndex, CancellationToken cancellationToken)
    {
        var snapshots = await _snapshotRepository.GetSnapshotsForReconstructionAsync(
            sessionId, targetIndex, cancellationToken).ConfigureAwait(false);

        if (snapshots.Count == 0)
            return "{}";

        // First snapshot should be the checkpoint (full state)
        var checkpoint = snapshots[0];
        if (!checkpoint.IsCheckpoint)
            throw new InvalidOperationException("State reconstruction failed: no checkpoint found");

        var state = checkpoint.DeltaDataJson;

        // Apply subsequent deltas
        for (int i = 1; i < snapshots.Count; i++)
        {
            state = JsonDeltaHelper.ApplyDelta(state, snapshots[i].DeltaDataJson);
        }

        return state;
    }

    private static string SerializeGameState(JsonDocument? gameState)
    {
        if (gameState == null) return "{}";

        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);
        gameState.WriteTo(writer);
        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    /// <summary>
    /// Embeds attachment references into the delta JSON under "_attachments" key.
    /// Issue #5367 - SessionSnapshot attachment metadata.
    /// </summary>
    internal static string EmbedAttachmentReferences(
        string deltaDataJson,
        IReadOnlyList<Domain.Entities.SessionAttachment.SessionAttachment> attachments)
    {
        using var doc = JsonDocument.Parse(deltaDataJson);
        using var stream = new MemoryStream();
        using var writer = new Utf8JsonWriter(stream);

        if (doc.RootElement.ValueKind == JsonValueKind.Object)
        {
            writer.WriteStartObject();

            // Copy existing properties
            foreach (var prop in doc.RootElement.EnumerateObject())
            {
                prop.WriteTo(writer);
            }

            // Add _attachments array
            writer.WritePropertyName("_attachments");
            WriteAttachmentArray(writer, attachments);

            writer.WriteEndObject();
        }
        else
        {
            // For JSON Patch arrays (non-checkpoint), wrap in envelope
            writer.WriteStartObject();
            writer.WritePropertyName("_delta");
            doc.RootElement.WriteTo(writer);
            writer.WritePropertyName("_attachments");
            WriteAttachmentArray(writer, attachments);
            writer.WriteEndObject();
        }

        writer.Flush();
        return System.Text.Encoding.UTF8.GetString(stream.ToArray());
    }

    private static void WriteAttachmentArray(
        Utf8JsonWriter writer,
        IReadOnlyList<Domain.Entities.SessionAttachment.SessionAttachment> attachments)
    {
        writer.WriteStartArray();
        foreach (var a in attachments)
        {
            writer.WriteStartObject();
            writer.WriteString("id", a.Id);
            writer.WriteString("playerId", a.PlayerId);
            writer.WriteString("type", a.AttachmentType.ToString());
            if (a.ThumbnailUrl != null)
                writer.WriteString("thumbnailUrl", a.ThumbnailUrl);
            if (a.Caption != null)
                writer.WriteString("caption", a.Caption);
            writer.WriteEndObject();
        }
        writer.WriteEndArray();
    }
}
