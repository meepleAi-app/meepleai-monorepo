using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for deleting a quick question.
/// </summary>
internal sealed class DeleteQuickQuestionCommandHandler : ICommandHandler<DeleteQuickQuestionCommand, Unit>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<DeleteQuickQuestionCommandHandler> _logger;

    public DeleteQuickQuestionCommandHandler(
        ISharedGameRepository gameRepository,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<DeleteQuickQuestionCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Unit> Handle(DeleteQuickQuestionCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Deleting quick question: {QuestionId}", command.QuestionId);

        // Find question to get SharedGameId
        var questionEntity = await _context.QuickQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == command.QuestionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Quick question with ID {command.QuestionId} not found");

        // Fetch aggregate to maintain domain invariants
        var game = await _gameRepository.GetByIdAsync(questionEntity.SharedGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {questionEntity.SharedGameId} not found");

        // Remove via aggregate method
        game.RemoveQuickQuestion(command.QuestionId);

        // Persist
        _gameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Quick question deleted: {QuestionId}", command.QuestionId);

        return Unit.Value;
    }
}