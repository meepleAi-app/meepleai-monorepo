using Api.Models;

namespace Api.Services;

/// <summary>
/// AI-07.1: Service for managing RAG prompt templates with few-shot learning support
/// Provides question type classification and template rendering
/// </summary>
public interface IPromptTemplateService
{
    /// <summary>
    /// Gets the appropriate prompt template for a game and question type
    /// </summary>
    /// <param name="gameId">Optional game ID for game-specific templates</param>
    /// <param name="questionType">Type of question for selecting appropriate examples</param>
    /// <returns>Prompt template with system prompt, user template, and few-shot examples</returns>
    Task<PromptTemplate> GetTemplateAsync(Guid? gameId, QuestionType questionType);

    /// <summary>
    /// Renders system prompt with few-shot examples
    /// </summary>
    /// <param name="template">Template to render</param>
    /// <returns>Complete system prompt with examples formatted for LLM</returns>
    string RenderSystemPrompt(PromptTemplate template);

    /// <summary>
    /// Renders user prompt with context and query substituted
    /// </summary>
    /// <param name="template">Template with placeholders</param>
    /// <param name="context">Context text from RAG retrieval</param>
    /// <param name="query">User's query</param>
    /// <returns>Complete user prompt ready for LLM</returns>
    string RenderUserPrompt(PromptTemplate template, string context, string query);

    /// <summary>
    /// Classifies a question into a question type based on keywords
    /// </summary>
    /// <param name="query">User's question</param>
    /// <returns>Classified question type</returns>
    QuestionType ClassifyQuestion(string query);
}
