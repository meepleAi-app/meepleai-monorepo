using MediatR;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Handler for LoadDatasetCommand.
/// Loads and validates an evaluation dataset from file.
/// </summary>
internal sealed class LoadDatasetCommandHandler : IRequestHandler<LoadDatasetCommand, EvaluationDataset>
{
    private readonly ILogger<LoadDatasetCommandHandler> _logger;

    public LoadDatasetCommandHandler(ILogger<LoadDatasetCommandHandler> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<EvaluationDataset> Handle(LoadDatasetCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!File.Exists(request.FilePath))
        {
            throw new FileNotFoundException($"Dataset file not found: {request.FilePath}");
        }

        _logger.LogInformation("Loading dataset from '{FilePath}'", request.FilePath);

        var json = await File.ReadAllTextAsync(request.FilePath, cancellationToken).ConfigureAwait(false);
        var dataset = EvaluationDataset.FromJson(json);

        var (isValid, errors) = dataset.Validate();

        _logger.LogInformation(
            "Loaded dataset '{Name}' with {Count} samples. Valid: {IsValid}",
            dataset.Name,
            dataset.Count,
            isValid);

        if (!isValid)
        {
            _logger.LogWarning("Dataset validation errors: {Errors}", string.Join("; ", errors));
        }

        return dataset;
    }
}
