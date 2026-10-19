using Api.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Queries.ProbeRetrieval;

/// <summary>#4482: la sonda accetta le domande che accetta la chat, e solo per un gioco.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4482")]
public sealed class ProbeRetrievalQueryValidatorTests
{
    private readonly ProbeRetrievalQueryValidator _validator = new();

    [Fact]
    public void AQuestionForAGame_IsValid()
    {
        _validator.Validate(new ProbeRetrievalQuery(Guid.NewGuid(), "Posso regalare una risorsa?")).IsValid.Should().BeTrue();
    }

    [Fact]
    public void WithoutAGame_IsInvalid()
    {
        _validator.Validate(new ProbeRetrievalQuery(Guid.Empty, "Posso regalare una risorsa?")).IsValid.Should().BeFalse();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("ok")]
    public void AQuestionTheChatWouldRefuse_IsInvalid(string question)
    {
        _validator.Validate(new ProbeRetrievalQuery(Guid.NewGuid(), question)).IsValid.Should().BeFalse();
    }

    [Fact]
    public void AQuestionLongerThanTheChatAllows_IsInvalid()
    {
        _validator.Validate(new ProbeRetrievalQuery(Guid.NewGuid(), new string('a', 1001))).IsValid.Should().BeFalse();
    }
}
