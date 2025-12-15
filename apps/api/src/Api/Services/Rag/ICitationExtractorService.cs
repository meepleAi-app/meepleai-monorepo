using Api.Models;

namespace Api.Services.Rag;

/// <summary>
/// Service for extracting and validating citations from RAG responses
/// </summary>
internal interface ICitationExtractorService
{
    /// <summary>
    /// Validates that citation references are accurate and complete
    /// </summary>
    /// <param name="snippets">Retrieved document snippets</param>
    /// <param name="answer">Generated answer</param>
    /// <returns>True if citations are valid</returns>
    bool ValidateCitations(List<Snippet> snippets, string answer);
}
