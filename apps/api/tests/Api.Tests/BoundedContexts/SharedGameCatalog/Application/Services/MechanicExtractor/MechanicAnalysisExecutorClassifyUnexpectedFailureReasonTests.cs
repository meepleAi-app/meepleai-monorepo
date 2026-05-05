using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

/// <summary>
/// Unit tests for <see cref="MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason"/> — the
/// rejection-reason classifier introduced by issue #597. Pins the contract that any exception
/// escaping the pipeline's own error handling maps to
/// <see cref="MechanicAnalysis.AutoRejectionReasons.PipelineCrashed"/> rather than the previous
/// hardcoded <see cref="MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed"/> — which
/// produced misleading audit trails and false-positive LLM alerts on infra failures.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicAnalysisExecutorClassifyUnexpectedFailureReasonTests
{
    [Fact]
    public void Classify_DbUpdateException_ReturnsPipelineCrashed_NotLlmGenerationFailed()
    {
        // Simulates a DB transient failure (FK violation, deadlock retry exhausted, …) crashing
        // mid-pipeline. Pre-fix this was misclassified as `LlmGenerationFailed`.
        var ex = new Microsoft.EntityFrameworkCore.DbUpdateException("FK violation");

        var reason = MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason(ex);

        reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.PipelineCrashed);
        reason.Should().NotBe(MechanicAnalysis.AutoRejectionReasons.LlmGenerationFailed);
    }

    [Fact]
    public void Classify_OutOfMemoryException_ReturnsPipelineCrashed()
    {
        var ex = new OutOfMemoryException("section run buffer exceeded");

        var reason = MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason(ex);

        reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.PipelineCrashed);
    }

    [Fact]
    public void Classify_NullReferenceException_ReturnsPipelineCrashed()
    {
        // Code defect crashing the pipeline — must not pollute LLM telemetry.
        var ex = new NullReferenceException("repository returned null unexpectedly");

        var reason = MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason(ex);

        reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.PipelineCrashed);
    }

    [Fact]
    public void Classify_HttpRequestException_ReturnsPipelineCrashed()
    {
        // Network-layer failure (e.g. DNS, TLS handshake) that escaped retry policy in a
        // dependency. Distinct from LLM API errors (which the pipeline reports via
        // AbortedLlmFailed without throwing).
        var ex = new HttpRequestException("Name or service not known");

        var reason = MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason(ex);

        reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.PipelineCrashed);
    }

    [Fact]
    public void Classify_GenericException_ReturnsPipelineCrashed()
    {
        var ex = new InvalidOperationException("unexpected pipeline state");

        var reason = MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason(ex);

        reason.Should().Be(MechanicAnalysis.AutoRejectionReasons.PipelineCrashed);
    }

    [Fact]
    public void Classify_NullException_Throws()
    {
        Action act = () => MechanicAnalysisExecutor.ClassifyUnexpectedFailureReason(null!);

        act.Should().Throw<ArgumentNullException>();
    }
}
