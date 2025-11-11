using Api.Models;

namespace Api.Services;

/// <summary>
/// ADMIN-01 Enhanced: Service for managing RAG prompt templates with database-driven configuration
/// Combines AI-07.1 few-shot learning with ADMIN-01 admin-configurable prompt management
/// </summary>
public interface IPromptTemplateService
{
    // AI-07.1: Configuration-based prompt management (backward compatibility)

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

    // ADMIN-01: Database-driven prompt management with Redis caching

    /// <summary>
    /// Gets active prompt from cache-first architecture (Redis → PostgreSQL → Config fallback)
    /// </summary>
    /// <param name="templateName">Unique template name (e.g., "qa-system-prompt")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Active prompt content or null if not found</returns>
    Task<string?> GetActivePromptAsync(string templateName, CancellationToken ct = default);

    /// <summary>
    /// Activates a specific version with transaction safety and cache invalidation
    /// Ensures only ONE active version per template
    /// </summary>
    /// <param name="templateId">Template ID</param>
    /// <param name="versionId">Version ID to activate</param>
    /// <param name="activatedByUserId">User ID performing the activation</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>True if activated successfully, false if version not found</returns>
    Task<bool> ActivateVersionAsync(Guid templateId, Guid versionId, Guid activatedByUserId, CancellationToken ct = default);

    /// <summary>
    /// Invalidates cache for a specific template (manual refresh)
    /// </summary>
    /// <param name="templateName">Template name to invalidate</param>
    /// <param name="ct">Cancellation token</param>
    Task InvalidateCacheAsync(string templateName, CancellationToken ct = default);
}
