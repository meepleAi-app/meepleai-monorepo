using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
public class RagPromptAssemblyServiceCopyrightTests
{
    [Fact]
    public void FormatChunkWithCopyright_Protected_Includes_Annotation()
    {
        var chunk = new ChunkCitation("doc-1", 14, 0.92f, "Original text", CopyrightTier.Protected);
        var formatted = RagPromptAssemblyService.FormatChunkForPrompt(chunk, "chunk content here");

        Assert.Contains("Copyright: PROTECTED", formatted);
        Assert.Contains("chunk content here", formatted);
        Assert.Contains("Document doc-1", formatted);
        Assert.Contains("Page 14", formatted);
    }

    [Fact]
    public void FormatChunkWithCopyright_Full_Includes_Annotation()
    {
        var chunk = new ChunkCitation("doc-1", 14, 0.92f, "Original text", CopyrightTier.Full);
        var formatted = RagPromptAssemblyService.FormatChunkForPrompt(chunk, "chunk content here");

        Assert.Contains("Copyright: FULL", formatted);
    }

    [Fact]
    public void CopyrightSystemInstruction_Italian()
    {
        var instruction = RagPromptAssemblyService.GetCopyrightInstruction("it");
        Assert.Contains("PROTECTED", instruction);
        Assert.Contains("[ref:", instruction);
        Assert.Contains("riformula", instruction);
    }

    [Fact]
    public void CopyrightSystemInstruction_English()
    {
        var instruction = RagPromptAssemblyService.GetCopyrightInstruction("en");
        Assert.Contains("PROTECTED", instruction);
        Assert.Contains("paraphrase", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void CopyrightSystemInstruction_UnknownLanguage_Defaults_To_English()
    {
        var instruction = RagPromptAssemblyService.GetCopyrightInstruction("de");
        Assert.Contains("paraphrase", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSystemPrompt_AllCitationsFull_OmitsCopyrightNotice()
    {
        var prompt = RagPromptAssemblyService.BuildSystemPromptForTest(
            agentTypology: "rules-expert",
            gameTitle: "Catan",
            gameState: null,
            ragContext: "Some rules text",
            hasExpansions: false,
            hasProtectedCitations: false,
            agentLanguage: "it");

        Assert.DoesNotContain("## Copyright Notice", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_AtLeastOneProtected_IncludesItalianNotice()
    {
        var prompt = RagPromptAssemblyService.BuildSystemPromptForTest(
            agentTypology: "rules-expert",
            gameTitle: "Terraforming Mars",
            gameState: null,
            ragContext: "Some rules text",
            hasExpansions: false,
            hasProtectedCitations: true,
            agentLanguage: "it");

        Assert.Contains("## Copyright Notice", prompt);
        Assert.Contains("riformula", prompt);
        Assert.DoesNotContain("paraphrase", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_AtLeastOneProtected_IncludesEnglishNotice()
    {
        var prompt = RagPromptAssemblyService.BuildSystemPromptForTest(
            agentTypology: "rules-expert",
            gameTitle: "Terraforming Mars",
            gameState: null,
            ragContext: "Some rules text",
            hasExpansions: false,
            hasProtectedCitations: true,
            agentLanguage: "en");

        Assert.Contains("## Copyright Notice", prompt);
        Assert.Contains("paraphrase", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("riformula", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ZeroCitationsAndNoProtected_OmitsAllCopyrightContent()
    {
        var prompt = RagPromptAssemblyService.BuildSystemPromptForTest(
            agentTypology: "rules-expert",
            gameTitle: "Catan",
            gameState: null,
            ragContext: "",
            hasExpansions: false,
            hasProtectedCitations: false,
            agentLanguage: "it");

        Assert.DoesNotContain("## Copyright Notice", prompt);
        Assert.DoesNotContain("## Game Rules and Documentation", prompt);
    }

    [Fact]
    public void BuildSystemPrompt_ProtectedCitations_CopyrightNoticeAppearsBeforeGameRules()
    {
        var prompt = RagPromptAssemblyService.BuildSystemPromptForTest(
            agentTypology: "rules-expert",
            gameTitle: "Terraforming Mars",
            gameState: null,
            ragContext: "[Source: Document abc, ...] rules text here\n---",
            hasExpansions: false,
            hasProtectedCitations: true,
            agentLanguage: "it");

        var copyrightIdx = prompt.IndexOf("## Copyright Notice", StringComparison.Ordinal);
        var rulesIdx = prompt.IndexOf("## Game Rules and Documentation", StringComparison.Ordinal);

        Assert.True(copyrightIdx >= 0, "Copyright Notice section missing");
        Assert.True(rulesIdx >= 0, "Game Rules section missing");
        Assert.True(copyrightIdx < rulesIdx,
            $"Copyright Notice (idx {copyrightIdx}) must appear before Game Rules (idx {rulesIdx}) for LLM attention ordering");
    }
}
