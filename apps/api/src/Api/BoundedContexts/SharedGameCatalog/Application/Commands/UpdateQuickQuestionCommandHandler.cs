using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for updating an existing quick question.
/// </summary>
internal sealed class UpdateQuickQuestionCommandHandler
    : ICommandHandler<UpdateQuickQuestionCommand, QuickQuestionDto>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<UpdateQuickQuestionCommandHandler> _logger;

    public UpdateQuickQuestionCommandHandler(
        ISharedGameRepository gameRepository,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<UpdateQuickQuestionCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<QuickQuestionDto> Handle(
        UpdateQuickQuestionCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation("Updating quick question: {QuestionId}", command.QuestionId);

        // Find question to get SharedGameId (respects aggregate boundary)
        var questionEntity = await _context.QuickQuestions
            .AsNoTracking()
            .FirstOrDefaultAsync(q => q.Id == command.QuestionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Quick question with ID {command.QuestionId} not found");

        // Fetch aggregate (SharedGame owns QuickQuestion)
        var game = await _gameRepository.GetByIdAsync(questionEntity.SharedGameId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException($"Shared game with ID {questionEntity.SharedGameId} not found");

        // Find domain entity within aggregate
        var question = game.QuickQuestions.FirstOrDefault(q => q.Id == command.QuestionId)
            ?? throw new InvalidOperationException($"Quick question {command.QuestionId} not found in aggregate");

        // Update via domain methods (preserves encapsulation)
        question.UpdateText(command.Text);
        question.UpdateEmoji(command.Emoji);
        question.UpdateCategory(command.Category);
        question.UpdateDisplayOrder(command.DisplayOrder);

        // Persist aggregate
        _gameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Quick question updated: {QuestionId}", command.QuestionId);

        // Return updated question data
        return new QuickQuestionDto(
            question.Id,
            question.SharedGameId,
            question.Text,
            question.Emoji,
            question.Category,
            question.DisplayOrder,
            question.IsGenerated,
            question.CreatedAt,
            question.IsActive);
    }
}
