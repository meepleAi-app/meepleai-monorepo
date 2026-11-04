using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.Routing;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing;

/// <summary>
/// #4819: the legacy question-and-answer response says «no page» as the explanation does, with a null, not with a 0
/// the client would have to guess at. Ask writes 0 into the plain-number <c>sources</c> of a document that cannot
/// vouch for its pages (#4782).
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4819")]
public sealed class AiEndpointsLegacyQaMappingTests
{
    private static SearchResultDto Source(int page) =>
        new(
            VectorDocumentId: Guid.NewGuid().ToString(),
            TextContent: "Si pescano quattro tessere.",
            PageNumber: page,
            RelevanceScore: 0.9,
            Rank: 1,
            SearchMethod: "hybrid");

    [Fact]
    public void ASourceWithoutAPage_IsMappedToNull_AndOneWithAPageKeepsIt()
    {
        var response = new QaResponseDto(
            Answer: "risposta",
            Sources: [Source(page: 0), Source(page: 4)],
            SearchConfidence: 0.8,
            LlmConfidence: 0.8,
            OverallConfidence: 0.8,
            IsLowQuality: false,
            Citations: []);

        var legacy = AiEndpoints.MapToLegacyQaResponse(response);

        legacy.snippets.Select(s => s.page).Should().Equal(new int?[] { null, 4 });
    }
}
