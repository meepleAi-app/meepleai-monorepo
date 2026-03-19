using Api.BoundedContexts.KnowledgeBase.Application.Commands.AbTest;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Logging;
using System.Diagnostics;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers.AbTest;

/// <summary>
/// Handles creation of A/B test sessions with parallel model response generation.
/// Issue #5494: A/B Test CQRS commands and queries.
/// </summary>
internal sealed class CreateAbTestCommandHandler : ICommandHandler<CreateAbTestCommand, AbTestSessionDto>
{
    private readonly IAbTestSessionRepository _repository;
    private readonly ILlmService _llmService;
    private readonly IAbTestBudgetService _budgetService;
    private readonly ILogger<CreateAbTestCommandHandler> _logger;

    private const string SystemPrompt = "You are a helpful board game assistant. Answer the user's question clearly and accurately.";
    private static readonly TimeSpan s_modelTimeout = TimeSpan.FromSeconds(30);

    // Label sequence for variants
    private static readonly string[] s_labels = ["A", "B", "C", "D"];

    public CreateAbTestCommandHandler(
        IAbTestSessionRepository repository,
        ILlmService llmService,
        IAbTestBudgetService budgetService,
        ILogger<CreateAbTestCommandHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _budgetService = budgetService ?? throw new ArgumentNullException(nameof(budgetService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AbTestSessionDto> Handle(CreateAbTestCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // Check budget and rate limit
        if (!await _budgetService.HasBudgetRemainingAsync(cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Daily A/B test budget exhausted");

        if (!await _budgetService.HasRateLimitRemainingAsync(command.CreatedBy, isAdmin: true, cancellationToken).ConfigureAwait(false))
            throw new InvalidOperationException("Daily A/B test rate limit reached");

        // Create session and add variants
        var session = AbTestSession.Create(command.CreatedBy, command.Query, command.KnowledgeBaseId);

        for (var i = 0; i < command.ModelIds.Count; i++)
        {
            var modelId = command.ModelIds[i];
            var provider = modelId.Contains('/', StringComparison.Ordinal) ? "OpenRouter" : "Ollama";
            session.AddVariant(s_labels[i], provider, modelId);
        }

        session.StartTest();

        // Generate responses in parallel with per-model timeout
        var tasks = session.Variants.Select(variant =>
            GenerateResponseAsync(variant, command.Query, cancellationToken));

        await Task.WhenAll(tasks).ConfigureAwait(false);

        // Record total cost
        if (session.TotalCost > 0)
        {
            await _budgetService.RecordTestCostAsync(session.TotalCost, cancellationToken).ConfigureAwait(false);
        }

        await _budgetService.RecordTestExecutionAsync(command.CreatedBy, cancellationToken).ConfigureAwait(false);

        // Persist
        await _repository.AddAsync(session, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "A/B test session {SessionId} created with {VariantCount} variants, total cost ${TotalCost:F6}",
            session.Id, session.Variants.Count, session.TotalCost);

        return AbTestMapper.ToBlindDto(session);
    }

    private async Task GenerateResponseAsync(AbTestVariant variant, string query, CancellationToken ct)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            cts.CancelAfter(s_modelTimeout);

            // Check cache first
            var cached = await _budgetService.GetCachedResponseAsync(query, variant.ModelId, cts.Token).ConfigureAwait(false);
            if (cached is not null)
            {
                sw.Stop();
                variant.RecordResponse(cached, 0, (int)sw.ElapsedMilliseconds, 0m);
                return;
            }

            var result = await _llmService.GenerateCompletionWithModelAsync(
                variant.ModelId,
                SystemPrompt,
                query,
                RequestSource.ABTesting,
                cts.Token).ConfigureAwait(false);

            sw.Stop();

            if (result.Success)
            {
                variant.RecordResponse(
                    result.Response,
                    result.Usage.TotalTokens,
                    (int)sw.ElapsedMilliseconds,
                    result.Cost.TotalCost);

                // Cache the response
                await _budgetService.CacheResponseAsync(query, variant.ModelId, result.Response, ct).ConfigureAwait(false);
            }
            else
            {
                variant.MarkFailed(result.ErrorMessage ?? "Unknown error");
            }
        }
        catch (OperationCanceledException)
        {
            sw.Stop();
            variant.MarkFailed($"Model timeout after {s_modelTimeout.TotalSeconds}s");
        }
#pragma warning disable CA1031 // Catch all exceptions to prevent single model failure from killing the session
        catch (Exception ex)
#pragma warning restore CA1031
        {
            sw.Stop();
            variant.MarkFailed($"Error: {ex.Message}");
            _logger.LogWarning(ex, "A/B test variant {Label} ({ModelId}) failed", variant.Label, variant.ModelId);
        }
    }
}
