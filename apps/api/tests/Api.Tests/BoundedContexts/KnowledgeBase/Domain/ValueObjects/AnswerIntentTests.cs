using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// #4404 (ADR-098 §5). The chat selector stops choosing an «agent» and chooses the SHAPE of the answer the single
/// agent gives: <c>auto</c> (today's answer), <c>tutor</c> (the rule step by step, with an example) or
/// <c>secca</c> (one line and the citation). The list lives in one place.
/// </summary>
/// <remarks>
/// The id of the third one is <c>secca</c> and not <c>decisore</c>: «Decisore» already means something in the
/// repo, and it is the opposite — the strategic move suggestions that ADR-098 §5 takes out of the chat. A
/// <c>decisore</c> already stored on a thread must not start behaving as a one-line answer.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4404")]
public sealed class AnswerIntentTests
{
    [Fact]
    public void TheIntentsOfTheChat_AreAutoTutorAndSecca_InThatOrder()
    {
        AnswerIntent.All.Should().Equal("auto", "tutor", "secca");
    }

    [Theory]
    [InlineData("auto", "auto")]
    [InlineData("tutor", "tutor")]
    [InlineData("secca", "secca")]
    [InlineData("TUTOR", "tutor")]
    [InlineData("  Secca ", "secca")]
    public void AStoredIntent_IsReadTrimmedAndInLowerCase(string stored, string expected)
    {
        AnswerIntent.FromStored(stored).Should().Be(expected);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("arbitro")]
    [InlineData("decisore")]
    [InlineData("stratega")]
    [InlineData("narratore")]
    [InlineData("session-assistant")]
    [InlineData("whatever")]
    public void AnythingStoredOutsideTheList_IsReadAsAuto(string? stored)
    {
        // Expand → contract: values written under the old list (and by other features that use the same
        // column) keep loading, and answer as today.
        AnswerIntent.FromStored(stored).Should().Be("auto");
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("tutor")]
    [InlineData("secca")]
    [InlineData("arbitro")]
    [InlineData("decisore")]
    [InlineData(" Tutor ")]
    public void ForNow_ThreadsMayStillBeGivenTheRetiredIds_ThatOlderClientsSend(string value)
    {
        AnswerIntent.IsAcceptedWhenSwitching(value).Should().BeTrue();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("stratega")]
    [InlineData("narratore")]
    [InlineData("whatever")]
    public void NothingElse_IsAccepted(string? value)
    {
        AnswerIntent.IsAcceptedWhenSwitching(value).Should().BeFalse();
    }

    [Fact]
    public void EveryIntentButAuto_HasADefaultInstruction_ThatSaysWhatIsDifferent()
    {
        AnswerIntent.DefaultInstructionFor("auto").Should().BeNull("auto is today's answer: it adds nothing");
        AnswerIntent.DefaultInstructionFor("tutor").Should().Contain("step by step").And.Contain("example");
        AnswerIntent.DefaultInstructionFor("secca").Should().Contain("single sentence").And.Contain("cite");
    }

    [Theory]
    [InlineData("tutor")]
    [InlineData("secca")]
    public void TheDefaultInstructions_LeaveTheRefusalAndTheCitationStyleToTheRestOfThePrompt(string intent)
    {
        // The refusal only counts as one when it is the WHOLE answer, and a profile can ask for no pages in the
        // text: an instruction that added its own «give the citation» would break both. This pins the wording, not
        // what a model does with it, which only a run against a model can show.
        var text = AnswerIntent.DefaultInstructionFor(intent)!;

        text.Should().Contain("does not contain the answer").And.Contain("and nothing else");
        text.Should().Contain("as instructed above");
        text.Should().NotContain("give the citation");
    }

    [Fact]
    public void AnIdOutsideTheList_HasNoDefaultInstruction()
    {
        AnswerIntent.DefaultInstructionFor("decisore").Should().BeNull();
        AnswerIntent.DefaultInstructionFor("arbitro").Should().BeNull();
    }
}
