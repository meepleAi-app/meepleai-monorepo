using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Domain.Entities;
using Api.SharedKernel.Domain.Exceptions;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Una proposta dell'assistente in partita (spec 2026-10-11 §5.2): un punteggio o un passaggio di turno
/// che l'host deve confermare. L'assistente non scrive mai la partita: crea questo.
/// <para>
/// La prenotazione (<see cref="BeginApplying"/>) è l'idempotenza della conferma: si salva con
/// <see cref="Xmin"/>, e un secondo tap concorrente perde sulla concorrenza invece di applicare due volte.
/// </para>
/// </summary>
public sealed class AssistantProposal : AggregateRoot<Guid>
{
    public const string NotPendingCode = "proposal_not_pending";
    public const int MaxFailureCodeLength = 64;

    /// <summary>
    /// Il massimo, in valore assoluto, dei punti di una proposta e della sua correzione. I punti li scrive
    /// un modello, e si sommano alla cella: senza un limite un numero assurdo farebbe il giro dell'intero.
    /// </summary>
    public const int MaxPointsPerProposal = 100_000;

    /// <summary>
    /// Il massimo, in valore assoluto, della cella su cui nasce una proposta. Con questo e con
    /// <see cref="MaxPointsPerProposal"/> la somma di <see cref="ScoreValueToWrite"/> sta in un intero.
    /// </summary>
    public const int MaxCellValue = 1_000_000_000;

    public Guid LiveSessionId { get; private set; }
    public Guid ChatThreadId { get; private set; }

    /// <summary>Il messaggio dell'assistente che l'ha prodotta: il suo id nasce prima del ciclo.</summary>
    public Guid MessageId { get; private set; }

    public AssistantProposalKind Kind { get; private set; }
    public AssistantProposalPayload Payload { get; private set; }
    public AssistantProposalExpectation Expected { get; private set; }
    public AssistantProposalStatus Status { get; private set; }
    public AssistantProposalConfirmation? ConfirmedPayload { get; private set; }
    public string? FailureCode { get; private set; }
    public Guid ProfileVersionId { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public DateTime? ResolvedAt { get; private set; }
    public Guid? ResolvedBy { get; private set; }

    /// <summary>Concorrenza ottimistica sulla colonna di sistema <c>xmin</c> (ADR-060).</summary>
    public uint Xmin { get; private set; }

    /// <summary>Né accettata né chiusa in altro modo: ancora confermabile, o in corso di applicazione.</summary>
    public bool IsOpen => Status is AssistantProposalStatus.Pending or AssistantProposalStatus.Applying;

#pragma warning disable CS8618
    private AssistantProposal() : base() { } // EF Core
#pragma warning restore CS8618

    private AssistantProposal(
        Guid liveSessionId,
        Guid chatThreadId,
        Guid messageId,
        Guid profileVersionId,
        AssistantProposalKind kind,
        AssistantProposalPayload payload,
        AssistantProposalExpectation expected,
        DateTime now)
        : base(Guid.NewGuid())
    {
        Require(liveSessionId != Guid.Empty, nameof(liveSessionId), "Live session is required.");
        Require(chatThreadId != Guid.Empty, nameof(chatThreadId), "Chat thread is required.");
        Require(messageId != Guid.Empty, nameof(messageId), "Message is required.");
        Require(profileVersionId != Guid.Empty, nameof(profileVersionId), "Profile version is required.");

        LiveSessionId = liveSessionId;
        ChatThreadId = chatThreadId;
        MessageId = messageId;
        ProfileVersionId = profileVersionId;
        Kind = kind;
        Payload = payload;
        Expected = expected;
        Status = AssistantProposalStatus.Pending;
        CreatedAt = now;

        // Una riga mai persistita non ha ancora un xmin; assegnarlo qui evita S1144 sul setter (#3688).
        Xmin = 0;
    }

    public static AssistantProposal ForScore(
        Guid liveSessionId,
        Guid chatThreadId,
        Guid messageId,
        Guid profileVersionId,
        Guid playerId,
        string playerName,
        string dimension,
        int round,
        int points,
        int cellValueBefore,
        DateTime now)
    {
        Require(playerId != Guid.Empty, nameof(playerId), "Player is required.");
        Require(!string.IsNullOrWhiteSpace(playerName), nameof(playerName), "Player name is required.");
        Require(!string.IsNullOrWhiteSpace(dimension), nameof(dimension), "Dimension is required.");
        Require(round >= 1, nameof(round), "Round starts at 1.");
        Require(IsWithinTheLimit(points), nameof(points), $"Points must be between -{MaxPointsPerProposal} and {MaxPointsPerProposal}.");
        Require(Math.Abs((long)cellValueBefore) <= MaxCellValue, nameof(cellValueBefore),
            $"The current score must be between -{MaxCellValue} and {MaxCellValue}.");

        return new AssistantProposal(
            liveSessionId, chatThreadId, messageId, profileVersionId, AssistantProposalKind.Score,
            new AssistantProposalPayload(playerId, playerName.Trim(), dimension.Trim(), round, points),
            new AssistantProposalExpectation(cellValueBefore, TurnIndex: null),
            now);
    }

    public static AssistantProposal ForAdvanceTurn(
        Guid liveSessionId, Guid chatThreadId, Guid messageId, Guid profileVersionId, int currentTurnIndex, DateTime now) =>
        new(
            liveSessionId, chatThreadId, messageId, profileVersionId, AssistantProposalKind.AdvanceTurn,
            new AssistantProposalPayload(null, null, null, null, null),
            new AssistantProposalExpectation(CellValue: null, currentTurnIndex),
            now);

    /// <summary>
    /// Il valore da scrivere nella cella: quello atteso più l'incremento, proposto o corretto dall'host.
    /// <c>RecordScore</c> sostituisce la cella, quindi l'incremento va sommato qui (spec §5.1).
    /// </summary>
    public int ScoreValueToWrite(int? correctedPoints)
    {
        EnsureKind(AssistantProposalKind.Score, "compute a score for");
        return (Expected.CellValue ?? 0) + (correctedPoints ?? Payload.Points ?? 0);
    }

    /// <summary>La prenotazione: da qui in poi un'altra conferma trova la proposta non più <c>Pending</c>.</summary>
    public void BeginApplying(Guid userId)
    {
        EnsureStatus(AssistantProposalStatus.Pending, "confirm");
        Status = AssistantProposalStatus.Applying;
        ResolvedBy = userId;
    }

    public void MarkAccepted(DateTime now)
    {
        EnsureStatus(AssistantProposalStatus.Applying, "accept");
        Close(AssistantProposalStatus.Accepted, now);
    }

    public void MarkEdited(int confirmedPoints, DateTime now)
    {
        EnsureKind(AssistantProposalKind.Score, "edit");
        EnsureStatus(AssistantProposalStatus.Applying, "edit");
        Require(IsWithinTheLimit(confirmedPoints), nameof(confirmedPoints),
            $"Points must be between -{MaxPointsPerProposal} and {MaxPointsPerProposal}.");
        ConfirmedPayload = new AssistantProposalConfirmation(confirmedPoints);
        Close(AssistantProposalStatus.Edited, now);
    }

    public void MarkFailed(string failureCode, DateTime now)
    {
        Require(!string.IsNullOrWhiteSpace(failureCode) && failureCode.Length <= MaxFailureCodeLength,
            nameof(failureCode), $"Failure code is required, up to {MaxFailureCodeLength} characters.");
        EnsureStatus(AssistantProposalStatus.Applying, "fail");
        FailureCode = failureCode;
        Close(AssistantProposalStatus.Failed, now);
    }

    /// <summary>Lo stato della partita non è più quello su cui la proposta è nata.</summary>
    public void MarkStale(DateTime now)
    {
        if (!IsOpen)
        {
            throw NotOpen("mark as stale");
        }

        Close(AssistantProposalStatus.Stale, now);
    }

    public void Reject(Guid userId, DateTime now)
    {
        EnsureStatus(AssistantProposalStatus.Pending, "reject");
        ResolvedBy = userId;
        Close(AssistantProposalStatus.Rejected, now);
    }

    /// <summary>
    /// Inizia un altro turno dell'assistente (spec §5.2). Qui la transizione è definita per l'aggregato;
    /// in produzione la applica il repository a tutte le proposte in attesa di una sessione, con un solo
    /// UPDATE, perché una conferma nello stesso istante non deve far fallire il turno (Task 8 del piano 2).
    /// </summary>
    public void Expire(DateTime now)
    {
        EnsureStatus(AssistantProposalStatus.Pending, "expire");
        Close(AssistantProposalStatus.Expired, now);
    }

    private void Close(AssistantProposalStatus status, DateTime now)
    {
        Status = status;
        ResolvedAt = now;
    }

    private void EnsureStatus(AssistantProposalStatus expected, string action)
    {
        if (Status != expected)
        {
            throw NotOpen(action);
        }
    }

    private void EnsureKind(AssistantProposalKind expected, string action)
    {
        if (Kind != expected)
        {
            throw new ConflictException($"Cannot {action} assistant proposal {Id}: it is a {Kind} proposal.");
        }
    }

    private ConflictException NotOpen(string action) =>
        new(NotPendingCode, $"Cannot {action} assistant proposal {Id}: it is {Status}.");

    // Su long: Math.Abs(int.MinValue) lancerebbe.
    private static bool IsWithinTheLimit(int points) => Math.Abs((long)points) <= MaxPointsPerProposal;

    private static void Require(bool condition, string propertyName, string message)
    {
        if (!condition)
        {
            throw new ValidationException(propertyName, message);
        }
    }
}
