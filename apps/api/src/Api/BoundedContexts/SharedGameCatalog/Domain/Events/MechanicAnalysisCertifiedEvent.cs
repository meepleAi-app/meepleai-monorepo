using MediatR;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Events;

public sealed record MechanicAnalysisCertifiedEvent(
    Guid AnalysisId,
    Guid SharedGameId,
    bool WasOverride,
    string? OverrideReason,
    Guid CertifiedByUserId,
    DateTimeOffset CertifiedAt) : INotification;
