namespace Api.Models;

/// <summary>
/// AI-07.1: Prompt template with few-shot examples for RAG responses
/// Supports LangChain-style prompt engineering with structured examples
/// </summary>
public record PromptTemplate
{
    /// <summary>
    /// System prompt with instructions and guidelines
    /// </summary>
    public required string SystemPrompt { get; init; }

    /// <summary>
    /// User prompt template with placeholders for {context} and {query}
    /// </summary>
    public required string UserPromptTemplate { get; init; }

    /// <summary>
    /// Few-shot examples to improve LLM response quality
    /// Recommended: 3-5 examples per question category
    /// </summary>
    public List<FewShotExample> FewShotExamples { get; init; } = new();

    /// <summary>
    /// Optional game-specific identifier (null for default template)
    /// </summary>
    public Guid? GameId { get; init; }

    /// <summary>
    /// Question type this template is optimized for
    /// </summary>
    public QuestionType QuestionType { get; init; } = QuestionType.General;
}

/// <summary>
/// AI-07.1: Few-shot example for prompt engineering
/// Demonstrates desired response format and quality to the LLM
/// </summary>
public record FewShotExample
{
    /// <summary>
    /// Example question
    /// </summary>
    public required string Question { get; init; }

    /// <summary>
    /// Example answer demonstrating desired response format
    /// </summary>
    public required string Answer { get; init; }

    /// <summary>
    /// Category/topic for this example (for documentation and filtering)
    /// </summary>
    public required string Category { get; init; }
}

/// <summary>
/// AI-07.1: Configuration model for RAG prompts section in appsettings.json
/// </summary>
public class RagPromptsConfiguration
{
    /// <summary>
    /// Default template for generic board game questions
    /// </summary>
    public PromptTemplateConfig? Default { get; set; }

    /// <summary>
    /// Question type-specific templates
    /// </summary>
    public Dictionary<string, PromptTemplateConfig> Templates { get; set; } = new();

    /// <summary>
    /// Game-specific template overrides (keyed by game ID)
    /// </summary>
    public Dictionary<string, Dictionary<string, PromptTemplateConfig>> GameTemplates { get; set; } = new();
}

/// <summary>
/// AI-07.1: Prompt template configuration model
/// </summary>
public class PromptTemplateConfig
{
    public required string SystemPrompt { get; set; }
    public required string UserPromptTemplate { get; set; }
    public List<FewShotExampleConfig> FewShotExamples { get; set; } = new();
}

/// <summary>
/// AI-07.1: Few-shot example configuration model
/// </summary>
public class FewShotExampleConfig
{
    public required string Question { get; set; }
    public required string Answer { get; set; }
    public required string Category { get; set; }
}
