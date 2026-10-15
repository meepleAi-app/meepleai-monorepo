using Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant;
using Api.Models;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.SessionAssistant;

/// <summary>#4411 (A5): le fonti di una risposta nel messaggio salvato, nella forma già filtrata per tier.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4411")]
public sealed class AssistantCitationsTests
{
    [Fact]
    public void WriteThenRead_KeepsTheCitation()
    {
        var citation = new CitationDto("pdf-1", 3, 0.9f, null, "protected", "Quattro tessere.", IsPublic: true);

        AssistantCitations.Read(AssistantCitations.Write([citation])).Should().Equal(citation);
    }

    // Review Focus 2 del piano 3: un JSON che non si legge non deve impedire al pannello di caricarsi.
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("non è json")]
    [InlineData("{\"documentId\":\"x\"}")]
    public void Read_AnUnreadableJson_GivesNoCitations(string? json) =>
        AssistantCitations.Read(json).Should().BeNull();
}
