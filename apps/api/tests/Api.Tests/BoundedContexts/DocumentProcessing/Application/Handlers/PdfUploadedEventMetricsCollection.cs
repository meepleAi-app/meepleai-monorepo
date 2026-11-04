using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>Il MeterListener è di processo e non filtra per test: questa classe non gira in parallelo con altre.</summary>
[CollectionDefinition("PdfUploadedEventMetrics", DisableParallelization = true)]
public sealed class PdfUploadedEventMetricsCollection;
