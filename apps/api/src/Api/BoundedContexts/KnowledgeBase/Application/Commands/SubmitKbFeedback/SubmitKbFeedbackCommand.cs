using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.SubmitKbFeedback;

internal sealed record SubmitKbFeedbackCommand(
    Guid UserId,
    Guid GameId,
    Guid ChatSessionId,
    Guid MessageId,
    string Outcome,
    string? Comment) : IRequest;
