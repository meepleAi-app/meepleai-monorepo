using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// #4404 (ADR-098 §5): each answer intent is a SECTION of the agent profile, versioned with it. A version that
/// carries none answers every intent from the built-in instruction; a version that carries one for an intent
/// replaces the built-in for that intent. Like the session assistant section, a version stored before it existed
/// has none.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "4404")]
public sealed class AgentProfileContentIntentInstructionsTests
{
    private static Dictionary<string, string> Instructions(params (string Intent, string Text)[] items) =>
        items.ToDictionary(i => i.Intent, i => i.Text, StringComparer.Ordinal);

    [Fact]
    public void AContentWithoutTheSection_HasNone_AndAnswersEveryIntentFromTheBuiltInInstruction()
    {
        var content = AgentProfileTests.Content();

        content.IntentInstructions.Should().BeEmpty();
        content.InstructionForIntent("tutor").Should().Be(AnswerIntent.DefaultInstructionFor("tutor"));
        content.InstructionForIntent("secca").Should().Be(AnswerIntent.DefaultInstructionFor("secca"));
        content.InstructionForIntent("auto").Should().BeNull();
    }

    [Fact]
    public void ASectionOfTheProfile_ReplacesTheBuiltInInstruction_OfThatIntentOnly()
    {
        var content = AgentProfileTests.Content(intents: Instructions(("tutor", "Teach it like a patient friend.")));

        content.InstructionForIntent("tutor").Should().Be("Teach it like a patient friend.");
        content.InstructionForIntent("secca").Should().Be(AnswerIntent.DefaultInstructionFor("secca"));
    }

    [Fact]
    public void TheSection_IsKeptTrimmed_UnderTheNormalizedIntentId()
    {
        var content = AgentProfileTests.Content(intents: Instructions(("  Tutor ", "  Step by step.  ")));

        content.IntentInstructions.Should().ContainSingle()
            .Which.Should().Be(new KeyValuePair<string, string>("tutor", "Step by step."));
    }

    [Theory]
    [InlineData("auto")]
    [InlineData("decisore")]
    [InlineData("arbitro")]
    [InlineData("whatever")]
    [InlineData("")]
    public void ASectionForAnIdThatIsNotAnIntentWithOne_IsRefused(string intent)
    {
        // «auto» is today's answer and has nothing to add; the retired ids are not intents any more.
        var act = () => AgentProfileTests.Content(intents: Instructions((intent, "Something.")));

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void TheSameIntentDeclaredTwice_IsRefused()
    {
        var act = () => AgentProfileTests.Content(intents: Instructions(("tutor", "One."), ("TUTOR", "Two.")));

        act.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void AnEmptyInstruction_IsRefused_NotRead_AsNoInstruction(string text)
    {
        var act = () => AgentProfileTests.Content(intents: Instructions(("tutor", text)));

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void AnInstructionTooLongForAPrompt_IsRefused()
    {
        var act = () => AgentProfileTests.Content(
            intents: Instructions(("tutor", new string('x', AgentProfileContent.MaxIntentInstructionLength + 1))));

        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void AnInstructionOfExactlyTheLongestLength_IsAccepted()
    {
        var content = AgentProfileTests.Content(
            intents: Instructions(("tutor", new string('x', AgentProfileContent.MaxIntentInstructionLength))));

        content.InstructionForIntent("tutor")!.Length.Should().Be(AgentProfileContent.MaxIntentInstructionLength);
    }

    [Fact]
    public void TwoContents_DifferingOnlyInTheSection_AreNotEqual()
    {
        var plain = AgentProfileTests.Content();
        var withSection = AgentProfileTests.Content(intents: Instructions(("secca", "One line.")));
        var sameSection = AgentProfileTests.Content(intents: Instructions(("secca", "One line.")));
        var otherText = AgentProfileTests.Content(intents: Instructions(("secca", "Two lines.")));
        var otherIntent = AgentProfileTests.Content(intents: Instructions(("tutor", "One line.")));

        withSection.Should().NotBe(plain, "a published version and its copy with an extra section are different versions");
        withSection.Should().Be(sameSection);
        withSection.Should().NotBe(otherText);
        withSection.Should().NotBe(otherIntent);
    }
}
