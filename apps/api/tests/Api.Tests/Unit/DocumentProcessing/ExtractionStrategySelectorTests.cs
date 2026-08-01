using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

/// <summary>
/// DC-2 (#3419): scoped per-request holder that carries the chosen <see cref="ExtractionStrategy"/>
/// from <c>PdfProcessingPipelineService</c> (setter) to <c>UnstructuredPdfTextExtractor</c> (reader),
/// which share a DI scope via the orchestrator's constructor graph. Defaults to <see cref="ExtractionStrategy.Fast"/>.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3419")]
public sealed class ExtractionStrategySelectorTests
{
    [Fact]
    public void Current_Defaults_ToFast()
    {
        var sut = new ExtractionStrategySelector();

        sut.Current.Should().Be(ExtractionStrategy.Fast);
    }

    [Fact]
    public void Current_WhenSetToHiRes_ReturnsHiRes()
    {
        var sut = new ExtractionStrategySelector { Current = ExtractionStrategy.HiRes };

        sut.Current.Should().Be(ExtractionStrategy.HiRes);
    }

    [Fact]
    public void Current_IsReassignable_AcrossReuse()
    {
        var sut = new ExtractionStrategySelector();

        sut.Current = ExtractionStrategy.HiRes;
        sut.Current.Should().Be(ExtractionStrategy.HiRes);

        sut.Current = ExtractionStrategy.Fast;
        sut.Current.Should().Be(ExtractionStrategy.Fast);
    }
}
