using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Handler for generating quick questions using AI analysis of the rulebook.
/// Issue #2401: QuickQuestion AI Generation
/// </summary>
internal sealed class GenerateQuickQuestionsCommandHandler
    : ICommandHandler<GenerateQuickQuestionsCommand, GenerateQuickQuestionsResultDto>
{
    private readonly ISharedGameRepository _gameRepository;
    private readonly ISharedGameDocumentRepository _documentRepository;
    private readonly IQuickQuestionGenerator _questionGenerator;
    private readonly MeepleAiDbContext _context;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<GenerateQuickQuestionsCommandHandler> _logger;

    public GenerateQuickQuestionsCommandHandler(
        ISharedGameRepository gameRepository,
        ISharedGameDocumentRepository documentRepository,
        IQuickQuestionGenerator questionGenerator,
        MeepleAiDbContext context,
        IUnitOfWork unitOfWork,
        ILogger<GenerateQuickQuestionsCommandHandler> logger)
    {
        _gameRepository = gameRepository ?? throw new ArgumentNullException(nameof(gameRepository));
        _documentRepository = documentRepository ?? throw new ArgumentNullException(nameof(documentRepository));
        _questionGenerator = questionGenerator ?? throw new ArgumentNullException(nameof(questionGenerator));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GenerateQuickQuestionsResultDto> Handle(
        GenerateQuickQuestionsCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        _logger.LogInformation(
            "Generating quick questions for shared game: {SharedGameId}",
            command.SharedGameId);

        // 1. Verify shared game exists
        var game = await _gameRepository.GetByIdAsync(command.SharedGameId, cancellationToken)
            .ConfigureAwait(false);

        if (game is null)
        {
            throw new InvalidOperationException($"Shared game with ID {command.SharedGameId} not found");
        }

        // 2. Get the active rulebook document
        var rulebookDocument = await _documentRepository.GetActiveDocumentAsync(
            command.SharedGameId,
            SharedGameDocumentType.Rulebook,
            cancellationToken).ConfigureAwait(false);

        // 3. Get rulebook content from PDF
        var rulebookContent = await GetRulebookContentAsync(
            rulebookDocument?.PdfDocumentId,
            cancellationToken).ConfigureAwait(false);

        if (string.IsNullOrWhiteSpace(rulebookContent))
        {
            _logger.LogWarning(
                "No rulebook content found for game {GameId}, using fallback questions",
                command.SharedGameId);
        }

        // 4. Generate questions using AI
        var generationResult = await _questionGenerator.GenerateQuestionsAsync(
            game.Title,
            rulebookContent ?? string.Empty,
            cancellationToken).ConfigureAwait(false);

        // 5. Create domain entities from generated questions
        var quickQuestions = generationResult.Questions
            .Select((q, index) => QuickQuestion.CreateFromAI(
                command.SharedGameId,
                q.Text,
                q.Category,
                displayOrder: index))
            .ToList();

        // 6. Replace existing questions with new ones (clear and add)
        game.ReplaceQuickQuestions(quickQuestions);

        // 7. Persist changes
        _gameRepository.Update(game);
        await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Generated {Count} quick questions for game {GameId} with confidence {Confidence}",
            quickQuestions.Count,
            command.SharedGameId,
            generationResult.ConfidenceScore);

        // 8. Map to DTOs
        var questionDtos = quickQuestions
            .Select(q => new QuickQuestionDto(
                q.Id,
                q.SharedGameId,
                q.Text,
                q.Emoji,
                q.Category,
                q.DisplayOrder,
                q.IsGenerated,
                q.CreatedAt,
                q.IsActive))
            .ToList();

        return new GenerateQuickQuestionsResultDto(
            questionDtos,
            generationResult.ConfidenceScore,
            DateTime.UtcNow);
    }

    /// <summary>
    /// Gets rulebook content from the PDF document.
    /// </summary>
    private async Task<string> GetRulebookContentAsync(
        Guid? pdfDocumentId,
        CancellationToken cancellationToken)
    {
        if (!pdfDocumentId.HasValue)
        {
            return string.Empty;
        }

        // Get PDF document with text content
        var pdfDocument = await _context.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == pdfDocumentId.Value)
            .Select(p => new { p.ExtractedText })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return pdfDocument?.ExtractedText ?? string.Empty;
    }
}