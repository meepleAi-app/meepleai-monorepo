using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;

/// <summary>
/// Handles evaluation of A/B test variants and returns revealed results.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed class EvaluateAbTestCommandHandler : ICommandHandler<EvaluateAbTestCommand, AbTestSessionRevealedDto>
{
    private readonly IAbTestSessionRepository _repository;

    public EvaluateAbTestCommandHandler(IAbTestSessionRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<AbTestSessionRevealedDto> Handle(EvaluateAbTestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        var session = await _repository.GetByIdWithVariantsAsync(command.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException($"A/B test session {command.SessionId} not found");

        foreach (var input in command.Evaluations)
        {
            var evaluation = AbTestEvaluation.Create(
                command.EvaluatorId,
                input.Accuracy,
                input.Completeness,
                input.Clarity,
                input.Tone,
                input.Notes);

            session.EvaluateVariant(input.Label, evaluation);
        }

        await _repository.UpdateAsync(session, cancellationToken).ConfigureAwait(false);

        return AbTestMapper.ToRevealedDto(session);
    }
}
