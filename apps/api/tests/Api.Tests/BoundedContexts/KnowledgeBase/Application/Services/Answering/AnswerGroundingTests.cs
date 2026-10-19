using Api.BoundedContexts.KnowledgeBase.Application.Services.Answering;
using Api.SharedKernel.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services.Answering;

/// <summary>
/// #4481: una rinuncia non è una risposta ancorata alle fonti, anche se il recupero ne ha trovate.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public sealed class AnswerGroundingTests
{
    [Theory]
    [InlineData("This information is not available in the provided rulebook.")]
    [InlineData("Questa informazione non è disponibile nel regolamento fornito.")]
    public void TheRefusal_WithSources_IsUngrounded(string answer)
    {
        AnswerGrounding.Of(answer, citationCount: 5).Should().Be(GroundingStatus.Ungrounded);
        AnswerGrounding.WireOf(answer, citationCount: 5).Should().Be("Ungrounded");
    }

    [Fact]
    public void AnAnswer_WithSources_IsGrounded()
    {
        AnswerGrounding.WireOf("Sì, puoi scambiare [Page 1].", citationCount: 2).Should().Be("Grounded");
    }

    [Fact]
    public void AnAnswer_WithARefusalInTail_IsStillGrounded()
    {
        AnswerGrounding.WireOf(
            "- La tessera vale 2 PV [Page 8].\n\nQuesta informazione non è disponibile nel regolamento fornito.",
            citationCount: 3).Should().Be("Grounded");
    }

    [Fact]
    public void AnAnswer_WithoutSources_IsUngrounded()
    {
        AnswerGrounding.WireOf("Sì, puoi scambiare.", citationCount: 0).Should().Be("Ungrounded");
    }
}
