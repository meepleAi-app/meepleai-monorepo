namespace Api.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Represents the category of a quick question for game assistance.
/// </summary>
public enum QuestionCategory
{
    /// <summary>
    /// Questions about core gameplay mechanics and turn structure.
    /// Example: "Come funziona il turno?" (How does a turn work?)
    /// Emoji: 🎮
    /// </summary>
    Gameplay = 0,

    /// <summary>
    /// Questions about game rules and mechanics.
    /// Example: "Quali sono le regole di combattimento?" (What are the combat rules?)
    /// Emoji: 📖
    /// </summary>
    Rules = 1,

    /// <summary>
    /// Questions about winning conditions and scoring.
    /// Example: "Come si vince una partita?" (How do you win a game?)
    /// Emoji: 🏆
    /// </summary>
    Winning = 2,

    /// <summary>
    /// Questions about game setup and preparation.
    /// Example: "Come si prepara il gioco?" (How do you set up the game?)
    /// Emoji: ⚙️
    /// </summary>
    Setup = 3,

    /// <summary>
    /// Questions about strategies and tactics.
    /// Example: "Quali sono le strategie migliori?" (What are the best strategies?)
    /// Emoji: 💡
    /// </summary>
    Strategy = 4,

    /// <summary>
    /// Clarification questions about edge cases and specific situations.
    /// Example: "Cosa succede se..." (What happens if...)
    /// Emoji: ❓
    /// </summary>
    Clarifications = 5
}
