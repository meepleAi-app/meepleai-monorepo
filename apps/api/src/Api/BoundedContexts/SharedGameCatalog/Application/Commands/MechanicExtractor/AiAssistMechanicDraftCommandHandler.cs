using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Handler for AI-assisted mechanic draft generation.
/// CRITICAL COMPLIANCE: This handler has ZERO dependencies on IBlobStorageService,
/// PDF repositories, or any file storage. The AI receives ONLY the human's notes.
/// This architectural constraint enforces the Variant C copyright firewall.
/// </summary>
internal sealed class AiAssistMechanicDraftCommandHandler
    : ICommandHandler<AiAssistMechanicDraftCommand, AiAssistResultDto>
{
    private readonly ILlmService _llmService;
    private readonly ILogger<AiAssistMechanicDraftCommandHandler> _logger;

    public AiAssistMechanicDraftCommandHandler(
        ILlmService llmService,
        ILogger<AiAssistMechanicDraftCommandHandler> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiAssistResultDto> Handle(
        AiAssistMechanicDraftCommand request,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "AI assist requested for draft {DraftId}, section {Section}, game '{GameTitle}'",
            request.DraftId,
            request.Section,
            request.GameTitle);

        var systemPrompt = BuildSystemPrompt(request.Section, request.GameTitle);
        var userPrompt = BuildUserPrompt(request.Section, request.HumanNotes);

        var result = await _llmService.GenerateCompletionAsync(
            systemPrompt,
            userPrompt,
            RequestSource.AdminOperation,
            cancellationToken).ConfigureAwait(false);

        if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
        {
            throw new ConflictException(
                $"AI failed to generate a draft: {result.ErrorMessage ?? "Empty response"}. Please try again.");
        }

        _logger.LogInformation(
            "AI assist completed for draft {DraftId}, section {Section}. Tokens: {Tokens}",
            request.DraftId,
            request.Section,
            result.Usage?.TotalTokens ?? 0);

        return new AiAssistResultDto(request.Section, result.Response.Trim());
    }

    private static string BuildSystemPrompt(string section, string gameTitle)
    {
        var sectionGuidance = section.ToLowerInvariant() switch
        {
            "summary" => "Write a clear, concise game summary (2-4 paragraphs) describing what the game is about, how it plays, and what makes it unique. Use original language.",
            "mechanics" => "List the key game mechanics as a JSON array of strings. Each mechanic should be a short, clear phrase (e.g., \"Worker Placement\", \"Deck Building\"). Extract 5-15 mechanics.",
            "victory" => "Describe victory conditions as a JSON object with fields: primary (string), alternatives (string array), isPointBased (boolean), targetPoints (number or null).",
            "resources" => "List game resources as a JSON array of objects with fields: name (string), type (string: Currency/Material/Action/Card/Token/Other), usage (string or null), isLimited (boolean).",
            "phases" => "List game phases/turns as a JSON array of objects with fields: name (string), description (string), order (number starting at 1), isOptional (boolean).",
            "questions" => "Generate 5-10 common questions players might ask about this game as a JSON array of strings. Focus on rules clarifications and strategic questions.",
            _ => "Produce a well-structured, original text based on the provided notes."
        };

        return $"""
            You are a board game analyst helping to create original, copyright-compliant game mechanic descriptions.
            You are writing about the board game "{gameTitle}".

            CRITICAL RULES:
            1. You are receiving HUMAN NOTES about the game, NOT the original rulebook text.
            2. You must produce ORIGINAL text in your own words. Never reproduce copyrighted text.
            3. Game mechanics (ideas, rules, systems) are NOT copyrightable — only their specific expression is.
            4. Focus on describing HOW the game works, not quoting HOW the rulebook says it works.

            YOUR TASK: {sectionGuidance}

            Respond with ONLY the requested content, no explanations or preamble.
            """;
    }

    private static string BuildUserPrompt(string section, string humanNotes)
    {
        return $"""
            Here are my notes about the "{section}" section of this game:

            {humanNotes}

            Please generate the {section} content based on these notes.
            """;
    }
}
