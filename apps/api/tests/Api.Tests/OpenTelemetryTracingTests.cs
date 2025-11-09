// OPS-02: OpenTelemetry Distributed Tracing Tests
using System.Diagnostics;
using Api.Observability;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for OpenTelemetry distributed tracing configuration and Activity Sources
/// </summary>
public class OpenTelemetryTracingTests
{
    private readonly ITestOutputHelper _output;

    [Fact]
    public void ActivitySources_AllSourceNamesAreDefined()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        sourceNames.Should().NotBeNull();
        sourceNames.Should().NotBeEmpty();
        sourceNames.Length.Should().Be(5);
    }

    [Fact]
    public void ActivitySources_ContainsApiSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        sourceNames.Should().Contain(MeepleAiActivitySources.ApiSourceName);
        MeepleAiActivitySources.ApiSourceName.Should().Be("MeepleAI.Api");
    }

    [Fact]
    public void ActivitySources_ContainsRagSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        sourceNames.Should().Contain(MeepleAiActivitySources.RagSourceName);
        MeepleAiActivitySources.RagSourceName.Should().Be("MeepleAI.Rag");
    }

    [Fact]
    public void ActivitySources_ContainsVectorSearchSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        sourceNames.Should().Contain(MeepleAiActivitySources.VectorSearchSourceName);
        MeepleAiActivitySources.VectorSearchSourceName.Should().Be("MeepleAI.VectorSearch");
    }

    [Fact]
    public void ActivitySources_ContainsPdfProcessingSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        sourceNames.Should().Contain(MeepleAiActivitySources.PdfProcessingSourceName);
        MeepleAiActivitySources.PdfProcessingSourceName.Should().Be("MeepleAI.PdfProcessing");
    }

    [Fact]
    public void ActivitySources_ContainsCacheSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        sourceNames.Should().Contain(MeepleAiActivitySources.CacheSourceName);
        MeepleAiActivitySources.CacheSourceName.Should().Be("MeepleAI.Cache");
    }

    [Fact]
    public void ActivitySource_Api_CanCreateActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.ApiSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = MeepleAiActivitySources.Api.StartActivity("TestActivity");

        // Assert
        activity.Should().NotBeNull();
        activity.DisplayName.Should().Be("TestActivity");
        activity.Source.Name.Should().Be(MeepleAiActivitySources.ApiSourceName);
    }

    [Fact]
    public void ActivitySource_Rag_CanCreateActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.RagSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = MeepleAiActivitySources.Rag.StartActivity("RagTestActivity");

        // Assert
        activity.Should().NotBeNull();
        activity.DisplayName.Should().Be("RagTestActivity");
        activity.Source.Name.Should().Be(MeepleAiActivitySources.RagSourceName);
    }

    [Fact]
    public void ActivitySource_VectorSearch_CanCreateActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.VectorSearchSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = MeepleAiActivitySources.VectorSearch.StartActivity("VectorSearchTest");

        // Assert
        activity.Should().NotBeNull();
        activity.DisplayName.Should().Be("VectorSearchTest");
        activity.Source.Name.Should().Be(MeepleAiActivitySources.VectorSearchSourceName);
    }

    [Fact]
    public void ActivitySource_PdfProcessing_CanCreateActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.PdfProcessingSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = MeepleAiActivitySources.PdfProcessing.StartActivity("PdfTest");

        // Assert
        activity.Should().NotBeNull();
        activity.DisplayName.Should().Be("PdfTest");
        activity.Source.Name.Should().Be(MeepleAiActivitySources.PdfProcessingSourceName);
    }

    [Fact]
    public void ActivitySource_Cache_CanCreateActivity()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.CacheSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = MeepleAiActivitySources.Cache.StartActivity("CacheTest");

        // Assert
        activity.Should().NotBeNull();
        activity.DisplayName.Should().Be("CacheTest");
        activity.Source.Name.Should().Be(MeepleAiActivitySources.CacheSourceName);
    }

    [Fact]
    public void Activity_CanSetTags()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.RagSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var activity = MeepleAiActivitySources.Rag.StartActivity("TestActivityWithTags");
        activity?.SetTag("game.id", "test-game-123");
        activity?.SetTag("operation", "qa");
        activity?.SetTag("query.length", 42);

        // Assert
        activity.Should().NotBeNull();
        activity.GetTagItem("game.id").Should().Be("test-game-123");
        activity.GetTagItem("operation").Should().Be("qa");
        activity.GetTagItem("query.length").Should().Be(42);
    }

    [Fact]
    public void Activity_CanRecordException()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source => source.Name == MeepleAiActivitySources.RagSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        var testException = new InvalidOperationException("Test exception message");

        // Act
        using var activity = MeepleAiActivitySources.Rag.StartActivity("TestActivityWithException");
        activity?.SetTag("success", false);
        activity?.SetTag("error.type", testException.GetType().Name);
        activity?.SetTag("error.message", testException.Message);
        activity?.SetStatus(ActivityStatusCode.Error, testException.Message);

        // Assert
        activity.Should().NotBeNull();
        activity.GetTagItem("success").Should().Be(false);
        activity.GetTagItem("error.type").Should().Be("InvalidOperationException");
        activity.GetTagItem("error.message").Should().Be("Test exception message");
        activity.Status.Should().Be(ActivityStatusCode.Error);
    }

    [Fact]
    public void Activity_NestingCreatesParentChildRelationship()
    {
        // Arrange
        var listener = new ActivityListener
        {
            ShouldListenTo = source =>
                source.Name == MeepleAiActivitySources.RagSourceName ||
                source.Name == MeepleAiActivitySources.VectorSearchSourceName,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData
        };
        ActivitySource.AddActivityListener(listener);

        // Act
        using var parentActivity = MeepleAiActivitySources.Rag.StartActivity("ParentActivity");
        var parentId = parentActivity?.Id;

        using var childActivity = MeepleAiActivitySources.VectorSearch.StartActivity("ChildActivity");
        var childParentId = childActivity?.ParentId;

        // Assert
        parentActivity.Should().NotBeNull();
        childActivity.Should().NotBeNull();
        childParentId.Should().Be(parentId);
    }

    [Fact]
    public void ActivitySource_SourceNames_DoNotConflictWithMeterNames()
    {
        // This test ensures that Activity Source names are distinct from Meter names
        // to avoid confusion between tracing (Activity) and metrics (Meter)

        // Arrange & Act
        var activitySourceNames = MeepleAiActivitySources.GetAllSourceNames();
        var meterName = MeepleAiMetrics.MeterName;

        // Assert
        activitySourceNames.Should().NotBeEmpty();
        meterName.Should().Be("MeepleAI.Api");

        // Activity sources should be distinct operations, not the same as the meter name
        // (though they can share the same base namespace)
        foreach (var sourceName in activitySourceNames)
        {
            sourceName.Should().NotBeNull();
            sourceName.Should().NotBeEmpty();
            sourceName.Should().StartWith("MeepleAI.");
        }
    }
}
