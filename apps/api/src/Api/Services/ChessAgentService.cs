using Api.Models;
using System.Text.RegularExpressions;

namespace Api.Services;

/// <summary>
/// CHESS-04: Specialized chess conversational agent
/// Answers questions about rules, explains openings, suggests tactics,
/// and analyzes positions using RAG on chess knowledge base
/// </summary>
public class ChessAgentService : IChessAgentService
{
    private readonly IChessKnowledgeService _chessKnowledge;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly ILogger<ChessAgentService> _logger;

    private const string ChessGameId = "chess"; // Standard game ID for chess knowledge

    public ChessAgentService(
        IChessKnowledgeService chessKnowledge,
        ILlmService llmService,
        IAiResponseCacheService cache,
        ILogger<ChessAgentService> logger)
    {
        _chessKnowledge = chessKnowledge;
        _llmService = llmService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// Process chess question with optional FEN position analysis
    /// </summary>
    public async Task<ChessAgentResponse> AskAsync(ChessAgentRequest request, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(request.question))
        {
            return CreateEmptyResponse("Please provide a question.");
        }

        try
        {
            // Check cache first
            var cacheKey = _cache.GenerateQaCacheKey(ChessGameId, $"{request.question}|{request.fenPosition ?? ""}");
            var cachedResponse = await _cache.GetAsync<ChessAgentResponse>(cacheKey, ct);
            if (cachedResponse != null)
            {
                LogInformation("Returning cached chess agent response");
                return cachedResponse;
            }
            // Step 1: Validate FEN position if provided
            bool hasFenPosition = !string.IsNullOrWhiteSpace(request.fenPosition);
            string? fenValidationError = null;
            if (hasFenPosition)
            {
                fenValidationError = ValidateFenPosition(request.fenPosition!);
                if (fenValidationError != null)
                {
                    _logger.LogWarning("Invalid FEN position provided: {FEN}, error: {Error}",
                        request.fenPosition, fenValidationError);
                    // Continue anyway, but note the error
                }
            }

            // Step 2: Search chess knowledge base
            var searchQuery = BuildSearchQuery(request.question, request.fenPosition);
            var searchResult = await _chessKnowledge.SearchChessKnowledgeAsync(searchQuery, limit: 5, ct);

            if (!searchResult.Success || searchResult.Results.Count == 0)
            {
                LogInformation("No chess knowledge found for query: {Query}", request.question);
                return CreateEmptyResponse("I don't have enough information to answer that question about chess.");
            }

            // Step 3: Build context from retrieved chunks
            var sources = searchResult.Results.Select(r => new Snippet(
                r.Text,
                $"ChessKnowledge:{r.ChunkIndex}",
                r.Page,
                0
            )).ToList();

            var context = string.Join("\n\n---\n\n", searchResult.Results.Select((r, i) =>
                $"[Source {i + 1}]\n{r.Text}"));

            // Step 4: Generate response using LLM with chess-specialized prompt
            var systemPrompt = BuildChessSystemPrompt(hasFenPosition, fenValidationError);
            var userPrompt = BuildChessUserPrompt(request.question, request.fenPosition, context, fenValidationError);

            var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, ct);

            if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
            {
                _logger.LogError("Failed to generate LLM response: {Error}", llmResult.ErrorMessage);
                return CreateEmptyResponse("Unable to generate answer.", sources);
            }

            // Step 5: Parse LLM response to extract structured information
            var parsedResponse = ParseLlmResponse(llmResult.Response, request.fenPosition);

            var confidence = searchResult.Results.Count > 0
                ? (double?)searchResult.Results.Max(r => r.Score)
                : null;

            LogInformation(
                "Chess agent query answered with {SourceCount} sources, {MoveCount} suggested moves",
                sources.Count, parsedResponse.SuggestedMoves.Count);

            var metadata = llmResult.Metadata.Count > 0
                ? new Dictionary<string, string>(llmResult.Metadata)
                : null;

            var response = new ChessAgentResponse(
                parsedResponse.Answer,
                parsedResponse.Analysis,
                parsedResponse.SuggestedMoves,
                sources,
                llmResult.Usage.PromptTokens,
                llmResult.Usage.CompletionTokens,
                llmResult.Usage.TotalTokens,
                confidence,
                metadata);

            // Cache the response
            await _cache.SetAsync(cacheKey, response, 86400, ct);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during chess agent query");
            return CreateEmptyResponse("An error occurred while processing your question.");
        }
    }

    private string BuildChessSystemPrompt(bool hasFenPosition, string? fenValidationError)
    {
        var prompt = @"You are a specialized chess AI assistant with deep knowledge of chess rules, openings, tactics, and strategies.

YOUR ROLE:
- Answer chess questions based ONLY on the provided context from the chess knowledge base
- Explain chess concepts clearly and concisely
- Suggest tactical and strategic moves when relevant
- Reference specific sources when providing information

CRITICAL INSTRUCTIONS:
- If the answer is clearly found in the provided context, answer it accurately
- If the answer is NOT in the context or you're uncertain, respond with: ""Not specified in chess knowledge base""
- Do NOT hallucinate or invent information
- Keep answers concise (3-5 sentences for explanations, 2-3 sentences for direct questions)
- When suggesting moves, always explain WHY the move is good";

        if (hasFenPosition)
        {
            prompt += @"

POSITION ANALYSIS:
- A FEN position has been provided - analyze it in your response
- Identify key features: material balance, pawn structure, king safety, piece activity
- Suggest 2-4 candidate moves with brief explanations";

            if (fenValidationError != null)
            {
                prompt += $@"

WARNING: The provided FEN position appears invalid: {fenValidationError}
- Acknowledge this to the user but still answer the question if possible";
            }
        }

        prompt += @"

RESPONSE FORMAT:
- Start with a direct answer to the question
- If analyzing a position, provide a brief evaluation
- List suggested moves in format: ""1. Move notation: Explanation""
- End with ""Sources: [1] [2]..."" to cite your sources";

        return prompt;
    }

    private string BuildChessUserPrompt(string question, string? fenPosition, string context, string? fenValidationError)
    {
        var prompt = $@"CHESS KNOWLEDGE BASE:
{context}

QUESTION:
{question}";

        if (!string.IsNullOrWhiteSpace(fenPosition))
        {
            prompt += $@"

POSITION (FEN):
{fenPosition}";

            if (fenValidationError != null)
            {
                prompt += $@"
Note: This FEN may be invalid - {fenValidationError}";
            }
        }

        prompt += @"

ANSWER:";

        return prompt;
    }

    private string BuildSearchQuery(string question, string? fenPosition)
    {
        // Enhance query with FEN-related keywords if position provided
        if (!string.IsNullOrWhiteSpace(fenPosition))
        {
            return $"{question} position analysis tactics strategy";
        }
        return question;
    }

    /// <summary>
    /// Basic FEN validation (8 ranks, valid characters, spaces)
    /// </summary>
    private string? ValidateFenPosition(string fen)
    {
        if (string.IsNullOrWhiteSpace(fen))
        {
            return "FEN position is empty";
        }

        // FEN format: position active-color castling en-passant halfmove fullmove
        var parts = fen.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length < 1)
        {
            return "FEN position is invalid";
        }

        var position = parts[0];
        var ranks = position.Split('/');

        if (ranks.Length != 8)
        {
            return $"FEN must have 8 ranks, found {ranks.Length}";
        }

        // Validate each rank
        var validPieces = new HashSet<char> { 'p', 'n', 'b', 'r', 'q', 'k', 'P', 'N', 'B', 'R', 'Q', 'K' };
        foreach (var rank in ranks)
        {
            int squares = 0;
            foreach (char c in rank)
            {
                if (char.IsDigit(c))
                {
                    squares += (c - '0');
                }
                else if (validPieces.Contains(c))
                {
                    squares++;
                }
                else
                {
                    return $"Invalid character in FEN: '{c}'";
                }
            }

            if (squares != 8)
            {
                return $"Rank must have 8 squares, found {squares}";
            }
        }

        return null; // Valid
    }

    private ParsedChessResponse ParseLlmResponse(string response, string? fenPosition)
    {
        var answer = response.Trim();
        var suggestedMoves = new List<string>();
        ChessAnalysis? analysis = null;

        // Extract suggested moves (look for patterns like "1. e4" or "Move: e4")
        var movePatterns = new[]
        {
            @"\d+\.\s*([a-h][1-8]|[NBRQK][a-h]?[1-8]?x?[a-h][1-8]|O-O(?:-O)?)[+#]?:?\s*([^\n]+)",
            @"(?:Move|Suggest|Consider):\s*([a-h][1-8]|[NBRQK][a-h]?[1-8]?x?[a-h][1-8]|O-O(?:-O)?)[+#]?"
        };

        foreach (var pattern in movePatterns)
        {
            var matches = Regex.Matches(answer, pattern, RegexOptions.IgnoreCase);
            foreach (Match match in matches)
            {
                if (match.Groups.Count > 1)
                {
                    var move = match.Groups[1].Value.Trim();
                    var explanation = match.Groups.Count > 2 ? match.Groups[2].Value.Trim() : "";

                    if (!string.IsNullOrEmpty(explanation))
                    {
                        suggestedMoves.Add($"{move}: {explanation}");
                    }
                    else
                    {
                        suggestedMoves.Add(move);
                    }
                }
            }
        }

        // If FEN position was provided, extract position analysis
        if (!string.IsNullOrWhiteSpace(fenPosition))
        {
            // Look for evaluation keywords
            var evaluationKeywords = new[] { "advantage", "equal", "better", "worse", "winning", "losing", "unclear", "balanced" };
            var evaluationSummary = evaluationKeywords.FirstOrDefault(kw =>
                answer.Contains(kw, StringComparison.OrdinalIgnoreCase));

            // Extract key considerations (sentences with tactical/strategic keywords)
            var considerations = new List<string>();
            var tacticalKeywords = new[] { "threat", "attack", "defend", "weakness", "strength", "control", "development", "king safety" };

            var sentences = answer.Split('.', StringSplitOptions.RemoveEmptyEntries);
            foreach (var sentence in sentences)
            {
                if (tacticalKeywords.Any(kw => sentence.Contains(kw, StringComparison.OrdinalIgnoreCase)))
                {
                    considerations.Add(sentence.Trim() + ".");
                }
            }

            if (!string.IsNullOrEmpty(evaluationSummary) || considerations.Count > 0)
            {
                analysis = new ChessAnalysis(
                    fenPosition,
                    evaluationSummary,
                    considerations.Take(5).ToList() // Limit to top 5 considerations
                );
            }
        }

        return new ParsedChessResponse(answer, analysis, suggestedMoves.Distinct().Take(6).ToList());
    }

    private ChessAgentResponse CreateEmptyResponse(string message, IReadOnlyList<Snippet>? sources = null)
    {
        return new ChessAgentResponse(
            message,
            null,
            Array.Empty<string>(),
            sources ?? Array.Empty<Snippet>(),
            0,
            0,
            0,
            null,
            null
        );
    }

    private void LogInformation(string message, params object?[] args)
    {
        if (_logger.IsEnabled(LogLevel.Information))
        {
            _logger.LogInformation(message, args);
        }
    }

    private record ParsedChessResponse(
        string Answer,
        ChessAnalysis? Analysis,
        IReadOnlyList<string> SuggestedMoves
    );
}
