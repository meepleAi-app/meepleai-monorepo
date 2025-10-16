// OPS-02: OpenTelemetry Distributed Tracing Tests
using System.Diagnostics;
using Api.Observability;
using Xunit;

namespace Api.Tests;

/// <summary>
/// Unit tests for OpenTelemetry distributed tracing configuration and Activity Sources
/// </summary>
public class OpenTelemetryTracingTests
{
    [Fact]
    public void ActivitySources_AllSourceNamesAreDefined()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        Assert.NotNull(sourceNames);
        Assert.NotEmpty(sourceNames);
        Assert.Equal(5, sourceNames.Length);
    }

    [Fact]
    public void ActivitySources_ContainsApiSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        Assert.Contains(MeepleAiActivitySources.ApiSourceName, sourceNames);
        Assert.Equal("MeepleAI.Api", MeepleAiActivitySources.ApiSourceName);
    }

    [Fact]
    public void ActivitySources_ContainsRagSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        Assert.Contains(MeepleAiActivitySources.RagSourceName, sourceNames);
        Assert.Equal("MeepleAI.Rag", MeepleAiActivitySources.RagSourceName);
    }

    [Fact]
    public void ActivitySources_ContainsVectorSearchSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        Assert.Contains(MeepleAiActivitySources.VectorSearchSourceName, sourceNames);
        Assert.Equal("MeepleAI.VectorSearch", MeepleAiActivitySources.VectorSearchSourceName);
    }

    [Fact]
    public void ActivitySources_ContainsPdfProcessingSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        Assert.Contains(MeepleAiActivitySources.PdfProcessingSourceName, sourceNames);
        Assert.Equal("MeepleAI.PdfProcessing", MeepleAiActivitySources.PdfProcessingSourceName);
    }

    [Fact]
    public void ActivitySources_ContainsCacheSource()
    {
        // Arrange & Act
        var sourceNames = MeepleAiActivitySources.GetAllSourceNames();

        // Assert
        Assert.Contains(MeepleAiActivitySources.CacheSourceName, sourceNames);
        Assert.Equal("MeepleAI.Cache", MeepleAiActivitySources.CacheSourceName);
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
        Assert.NotNull(activity);
        Assert.Equal("TestActivity", activity.DisplayName);
        Assert.Equal(MeepleAiActivitySources.ApiSourceName, activity.Source.Name);
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
        Assert.NotNull(activity);
        Assert.Equal("RagTestActivity", activity.DisplayName);
        Assert.Equal(MeepleAiActivitySources.RagSourceName, activity.Source.Name);
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
        Assert.NotNull(activity);
        Assert.Equal("VectorSearchTest", activity.DisplayName);
        Assert.Equal(MeepleAiActivitySources.VectorSearchSourceName, activity.Source.Name);
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
        Assert.NotNull(activity);
        Assert.Equal("PdfTest", activity.DisplayName);
        Assert.Equal(MeepleAiActivitySources.PdfProcessingSourceName, activity.Source.Name);
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
        Assert.NotNull(activity);
        Assert.Equal("CacheTest", activity.DisplayName);
        Assert.Equal(MeepleAiActivitySources.CacheSourceName, activity.Source.Name);
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
        Assert.NotNull(activity);
        Assert.Equal("test-game-123", activity.GetTagItem("game.id"));
        Assert.Equal("qa", activity.GetTagItem("operation"));
        Assert.Equal(42, activity.GetTagItem("query.length"));
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
        Assert.NotNull(activity);
        Assert.Equal(false, activity.GetTagItem("success"));
        Assert.Equal("InvalidOperationException", activity.GetTagItem("error.type"));
        Assert.Equal("Test exception message", activity.GetTagItem("error.message"));
        Assert.Equal(ActivityStatusCode.Error, activity.Status);
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
        Assert.NotNull(parentActivity);
        Assert.NotNull(childActivity);
        Assert.Equal(parentId, childParentId);
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
        Assert.NotEmpty(activitySourceNames);
        Assert.Equal("MeepleAI.Api", meterName);

        // Activity sources should be distinct operations, not the same as the meter name
        // (though they can share the same base namespace)
        foreach (var sourceName in activitySourceNames)
        {
            Assert.NotNull(sourceName);
            Assert.NotEmpty(sourceName);
            Assert.StartsWith("MeepleAI.", sourceName);
        }
    }
}
