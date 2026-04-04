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
}
