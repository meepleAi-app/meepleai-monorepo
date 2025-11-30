using Api.SharedKernel.Application.Interfaces;
using Api.Services;

namespace Api.BoundedContexts.Administration.Application.Queries.PromptEvaluation;

/// <summary>
/// Handler for generating evaluation reports in Markdown or JSON format
/// Delegates to IPromptEvaluationService (infrastructure adapter)
/// </summary>
public sealed class GenerateEvaluationReportQueryHandler : IQueryHandler<GenerateEvaluationReportQuery, (string Report, string ContentType)>
{
    private readonly IPromptEvaluationService _evaluationService;
    private readonly ILogger<GenerateEvaluationReportQueryHandler> _logger;

    public GenerateEvaluationReportQueryHandler(
        IPromptEvaluationService evaluationService,
        ILogger<GenerateEvaluationReportQueryHandler> logger)
    {
        _evaluationService = evaluationService ?? throw new ArgumentNullException(nameof(evaluationService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<(string Report, string ContentType)> Handle(GenerateEvaluationReportQuery query, CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Generating {Format} report for evaluation {EvaluationId}",
            query.Format, query.EvaluationId);

        // Retrieve all historical results and find the specific evaluation
        // Note: This approach is used to maintain compatibility with existing service implementation
        var allResults = await _evaluationService.GetHistoricalResultsAsync("", 1000, cancellationToken).ConfigureAwait(false);
        var result = allResults.FirstOrDefault(r => r.EvaluationId == query.EvaluationId);

        if (result == null)
        {
            throw new InvalidOperationException($"Evaluation {query.EvaluationId} not found");
        }

        // Delegate to infrastructure service for report formatting
        var report = _evaluationService.GenerateReport(result, query.Format);

        var contentType = query.Format == ReportFormat.Json
            ? "application/json"
            : "text/markdown";

        _logger.LogDebug("Generated {Format} report ({Length} chars)", query.Format, report.Length);

        return (report, contentType);
    }
}
