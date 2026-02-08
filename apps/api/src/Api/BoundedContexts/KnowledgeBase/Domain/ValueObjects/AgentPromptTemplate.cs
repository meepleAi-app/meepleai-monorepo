namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing a prompt template for agent definitions.
/// Issue #3808: Stores role and content for multi-prompt agent configurations.
/// </summary>
public sealed record AgentPromptTemplate
{
    /// <summary>
    /// Gets the prompt role (e.g., "system", "user", "assistant").
    /// </summary>
    public string Role { get; init; }

    /// <summary>
    /// Gets the prompt content/template text.
    /// </summary>
    public string Content { get; init; }

    /// <summary>
    /// Parameterless constructor for EF Core deserialization.
    /// </summary>
#pragma warning disable CS8618
    private AgentPromptTemplate()
#pragma warning restore CS8618
    {
    }

    private AgentPromptTemplate(string role, string content)
    {
        Role = role;
        Content = content;
    }

    /// <summary>
    /// Creates a new prompt template with validation.
    /// </summary>
    public static AgentPromptTemplate Create(string role, string content)
    {
        if (string.IsNullOrWhiteSpace(role))
            throw new ArgumentException("Role cannot be empty", nameof(role));

        if (role.Length > 50)
            throw new ArgumentException("Role cannot exceed 50 characters", nameof(role));

        if (string.IsNullOrWhiteSpace(content))
            throw new ArgumentException("Content cannot be empty", nameof(content));

        if (content.Length > 10000)
            throw new ArgumentException("Content cannot exceed 10000 characters", nameof(content));

        // Validate role is one of the allowed values
        var allowedRoles = new[] { "system", "user", "assistant", "function" };
        if (!allowedRoles.Contains(role, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Role must be one of: {string.Join(", ", allowedRoles)}", nameof(role));

        return new AgentPromptTemplate(role.Trim().ToLowerInvariant(), content.Trim());
    }
}
