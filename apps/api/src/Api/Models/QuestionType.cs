namespace Api.Models;

/// <summary>
/// AI-07.1: Question type classification for prompt template selection
/// Used to select appropriate few-shot examples and prompt structure
/// </summary>
internal enum QuestionType
{
    /// <summary>
    /// Questions about game setup (e.g., "How do I set up Chess?")
    /// Keywords: setup, start, begin, prepare, place pieces
    /// </summary>
    Setup,

    /// <summary>
    /// Questions about gameplay and rules (e.g., "How does a knight move?")
    /// Keywords: move, turn, action, can I, allowed
    /// </summary>
    Gameplay,

    /// <summary>
    /// Questions about winning/losing conditions (e.g., "How do I win?")
    /// Keywords: win, victory, lose, checkmate, three in a row
    /// </summary>
    WinningConditions,

    /// <summary>
    /// Questions about edge cases and special rules (e.g., "What is en passant?")
    /// Keywords: en passant, castling, stalemate, special, exception
    /// </summary>
    EdgeCases,

    /// <summary>
    /// General questions that don't fit other categories
    /// Fallback for all other questions
    /// </summary>
    General
}
