using System.Runtime.CompilerServices;
using System.Text.Json;
using Api.BoundedContexts.Administration.Application.Commands.RagExecution;
using Api.BoundedContexts.Administration.Application.Commands.RagPipeline;
using Api.BoundedContexts.Administration.Infrastructure.Persistence;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Handlers.RagExecution;

/// <summary>
/// Handler for ReplayRagExecutionCommand.
/// Fetches a stored execution, re-executes the pipeline with optional overrides,
/// and streams results via SSE.
/// Issue #4459: RAG Query Replay.
/// </summary>
internal sealed class ReplayRagExecutionCommandHandler(
    IRagExecutionRepository executionRepository,
    IMediator mediator,
    ILogger<ReplayRagExecutionCommandHandler> logger,
    TimeProvider timeProvider
) : IRequestHandler<ReplayRagExecutionCommand, IAsyncEnumerable<RagPipelineTestEvent>>
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<IAsyncEnumerable<RagPipelineTestEvent>> Handle(
        ReplayRagExecutionCommand request,
        CancellationToken cancellationToken)
    {
        var execution = await executionRepository.GetByIdAsync(request.ExecutionId, cancellationToken)
            .ConfigureAwait(false);

        if (execution == null)
        {
            return SingleErrorEvent($"Execution {request.ExecutionId} not found", timeProvider);
        }

        logger.LogInformation(
            "Admin {UserId} replaying execution {ExecutionId} (original query: '{Query}')",
            request.UserId,
            request.ExecutionId,
            execution.TestQuery);

        // Apply config overrides to pipeline definition if provided
        var pipelineDefinition = ApplyOverrides(
            execution.PipelineDefinitionJson,
            request.Strategy,
            request.TopK,
            request.Model,
            request.Temperature);

        // Build config overrides JSON for storage
        var configOverrides = BuildConfigOverridesJson(request);

        // Re-execute the pipeline using the existing TestRagPipelineCommand
        var testCommand = new TestRagPipelineCommand(
            pipelineDefinition,
            execution.TestQuery,
            request.UserId);

        var eventStream = await mediator.Send(testCommand, cancellationToken).ConfigureAwait(false);

        // Wrap the event stream to capture and persist the replay result
        return WrapAndPersistStream(
            eventStream,
            execution,
            request.UserId,
            configOverrides,
            cancellationToken);
    }

    private async IAsyncEnumerable<RagPipelineTestEvent> WrapAndPersistStream(
        IAsyncEnumerable<RagPipelineTestEvent> sourceStream,
        Domain.Aggregates.RagExecution.RagExecution originalExecution,
        Guid userId,
        string? configOverridesJson,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var events = new List<RagPipelineTestEvent>();

        await foreach (var evt in sourceStream.ConfigureAwait(false))
        {
            if (cancellationToken.IsCancellationRequested) yield break;

            events.Add(evt);
            yield return evt;
        }

        // After streaming completes, persist the replay as a new execution
        await PersistReplayResult(
            originalExecution,
            userId,
            events,
            configOverridesJson,
            cancellationToken).ConfigureAwait(false);
    }

    private async Task PersistReplayResult(
        Domain.Aggregates.RagExecution.RagExecution originalExecution,
        Guid userId,
        List<RagPipelineTestEvent> events,
        string? configOverridesJson,
        CancellationToken ct)
    {
        try
        {
            var completedEvent = events.OfType<PipelineTestCompletedEvent>().FirstOrDefault();

            var replayExecution = Domain.Aggregates.RagExecution.RagExecution.Create(
                strategyId: originalExecution.StrategyId,
                pipelineDefinitionJson: originalExecution.PipelineDefinitionJson,
                testQuery: originalExecution.TestQuery,
                executedByUserId: userId,
                success: completedEvent?.Success ?? false,
                totalDurationMs: completedEvent?.TotalDurationMs ?? 0,
                totalTokensUsed: completedEvent?.TotalTokensUsed ?? 0,
                totalCost: completedEvent?.TotalCost ?? 0m,
                blocksExecuted: completedEvent?.BlocksExecuted ?? 0,
                blocksFailed: completedEvent?.BlocksFailed ?? 0,
                finalResponse: completedEvent?.FinalResponse,
                executionError: completedEvent?.Error,
                eventsJson: JsonSerializer.Serialize(events, JsonOptions),
                configOverridesJson: configOverridesJson,
                parentExecutionId: originalExecution.Id,
                timeProvider: timeProvider);

            await executionRepository.AddAsync(replayExecution, ct).ConfigureAwait(false);

            logger.LogInformation(
                "Replay execution {ReplayId} persisted (parent: {ParentId}, success: {Success})",
                replayExecution.Id,
                originalExecution.Id,
                replayExecution.Success);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to persist replay execution for parent {ParentId}", originalExecution.Id);
        }
    }

    private static string ApplyOverrides(
        string pipelineDefinitionJson,
        string? strategy,
        int? topK,
        string? model,
        double? temperature)
    {
        if (strategy == null && topK == null && model == null && temperature == null)
        {
            return pipelineDefinitionJson;
        }

        try
        {
            using var doc = JsonDocument.Parse(pipelineDefinitionJson);
            var root = doc.RootElement;

            using var ms = new MemoryStream();
            using var writer = new Utf8JsonWriter(ms);
            writer.WriteStartObject();

            foreach (var prop in root.EnumerateObject())
            {
                if (string.Equals(prop.Name, "nodes", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteNodesWithOverrides(writer, prop.Value, strategy, topK, model, temperature);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
            writer.Flush();

            return System.Text.Encoding.UTF8.GetString(ms.ToArray());
        }
        catch (JsonException)
        {
            return pipelineDefinitionJson;
        }
    }

    private static void WriteNodesWithOverrides(
        Utf8JsonWriter writer,
        JsonElement nodesArray,
        string? strategy,
        int? topK,
        string? model,
        double? temperature)
    {
        writer.WriteStartArray();

        foreach (var node in nodesArray.EnumerateArray())
        {
            writer.WriteStartObject();

            foreach (var prop in node.EnumerateObject())
            {
                if (string.Equals(prop.Name, "data", StringComparison.OrdinalIgnoreCase))
                {
                    writer.WritePropertyName(prop.Name);
                    WriteDataWithOverrides(writer, prop.Value, strategy, topK, model, temperature);
                }
                else
                {
                    prop.WriteTo(writer);
                }
            }

            writer.WriteEndObject();
        }

        writer.WriteEndArray();
    }

    private static void WriteDataWithOverrides(
        Utf8JsonWriter writer,
        JsonElement data,
        string? strategy,
        int? topK,
        string? model,
        double? temperature)
    {
        writer.WriteStartObject();

        foreach (var prop in data.EnumerateObject())
        {
            if (string.Equals(prop.Name, "block", StringComparison.OrdinalIgnoreCase))
            {
                writer.WritePropertyName(prop.Name);
                WriteBlockWithOverrides(writer, prop.Value, strategy, topK, model, temperature);
            }
            else
            {
                prop.WriteTo(writer);
            }
        }

        writer.WriteEndObject();
    }

    private static void WriteBlockWithOverrides(
        Utf8JsonWriter writer,
        JsonElement block,
        string? strategy,
        int? topK,
        string? model,
        double? temperature)
    {
        writer.WriteStartObject();

        var wroteTopK = false;
        var wroteModel = false;
        var wroteTemperature = false;
        var wroteStrategy = false;

        foreach (var prop in block.EnumerateObject())
        {
            var name = prop.Name;

            if (topK.HasValue && string.Equals(name, "topK", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteNumber(name, topK.Value);
                wroteTopK = true;
            }
            else if (model != null && string.Equals(name, "model", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteString(name, model);
                wroteModel = true;
            }
            else if (temperature.HasValue && string.Equals(name, "temperature", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteNumber(name, temperature.Value);
                wroteTemperature = true;
            }
            else if (strategy != null && string.Equals(name, "strategy", StringComparison.OrdinalIgnoreCase))
            {
                writer.WriteString(name, strategy);
                wroteStrategy = true;
            }
            else
            {
                prop.WriteTo(writer);
            }
        }

        // Write overrides that weren't already in the block
        if (topK.HasValue && !wroteTopK) writer.WriteNumber("topK", topK.Value);
        if (model != null && !wroteModel) writer.WriteString("model", model);
        if (temperature.HasValue && !wroteTemperature) writer.WriteNumber("temperature", temperature.Value);
        if (strategy != null && !wroteStrategy) writer.WriteString("strategy", strategy);

        writer.WriteEndObject();
    }

    private static string? BuildConfigOverridesJson(ReplayRagExecutionCommand request)
    {
        if (request.Strategy == null && request.TopK == null && request.Model == null && request.Temperature == null)
        {
            return null;
        }

        var overrides = new Dictionary<string, object?>(StringComparer.Ordinal);
        if (request.Strategy != null) overrides["strategy"] = request.Strategy;
        if (request.TopK.HasValue) overrides["topK"] = request.TopK.Value;
        if (request.Model != null) overrides["model"] = request.Model;
        if (request.Temperature.HasValue) overrides["temperature"] = request.Temperature.Value;

        return JsonSerializer.Serialize(overrides, JsonOptions);
    }

    private static async IAsyncEnumerable<RagPipelineTestEvent> SingleErrorEvent(
        string error,
        TimeProvider? timeProvider = null,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        await Task.CompletedTask.ConfigureAwait(false);
        yield return new PipelineTestCompletedEvent
        {
            EventType = nameof(PipelineTestCompletedEvent),
            Timestamp = (timeProvider ?? TimeProvider.System).GetUtcNow().UtcDateTime,
            Success = false,
            TotalDurationMs = 0,
            TotalTokensUsed = 0,
            TotalCost = 0,
            BlocksExecuted = 0,
            BlocksFailed = 0,
            Error = error
        };
    }
}
