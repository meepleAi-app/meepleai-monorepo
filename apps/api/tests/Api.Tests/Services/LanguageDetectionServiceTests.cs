using Api.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services;

/// <summary>
/// Unit tests for LanguageDetectionService
/// Tests language detection across 5 supported languages (EN, IT, DE, FR, ES)
/// AI-09: GREEN PHASE - Making tests pass
/// </summary>
public class LanguageDetectionServiceTests
{
    private readonly ITestOutputHelper _output;

    private readonly ILanguageDetectionService _service;

    public LanguageDetectionServiceTests(ITestOutputHelper output)
    {
        _output = output;
        // AI-09: Use NullLogger for unit tests
        _service = new LanguageDetectionService(NullLogger<LanguageDetectionService>.Instance);
    }

    #region English Detection Tests

    [Fact]
    public async Task DetectLanguageAsync_EnglishText_ReturnsEn()
    {
        // Arrange
        var text = "Players take turns placing their pieces on the board. The first player to align three pieces in a row wins the game.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("en");
    }

    [Fact]
    public async Task DetectLanguageAsync_LongEnglishText_ReturnsEn()
    {
        // Arrange
        var text = @"The game begins with each player receiving five cards from the deck.
            Players must follow suit if possible, otherwise they may play any card.
            The highest card of the leading suit wins the trick.
            The game continues until all cards have been played and the player with the most tricks wins.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("en");
    }

    #endregion

    #region Italian Detection Tests

    [Fact]
    public async Task DetectLanguageAsync_ItalianText_ReturnsIt()
    {
        // Arrange
        var text = "I giocatori a turno posizionano i loro pezzi sulla scacchiera. Il primo giocatore ad allineare tre pezzi in fila vince la partita.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("it");
    }

    [Fact]
    public async Task DetectLanguageAsync_LongItalianText_ReturnsIt()
    {
        // Arrange
        var text = @"Il gioco inizia con ogni giocatore che riceve cinque carte dal mazzo.
            I giocatori devono seguire il seme se possibile, altrimenti possono giocare qualsiasi carta.
            La carta più alta del seme principale vince la presa.
            Il gioco continua fino a quando tutte le carte sono state giocate e il giocatore con più prese vince.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("it");
    }

    #endregion

    #region German Detection Tests

    [Fact]
    public async Task DetectLanguageAsync_GermanText_ReturnsDe()
    {
        // Arrange
        var text = "Die Spieler setzen abwechselnd ihre Spielfiguren auf das Brett. Der erste Spieler, der drei Figuren in einer Reihe ausrichtet, gewinnt das Spiel.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("de");
    }

    [Fact]
    public async Task DetectLanguageAsync_LongGermanText_ReturnsDe()
    {
        // Arrange
        var text = @"Das Spiel beginnt damit, dass jeder Spieler fünf Karten aus dem Stapel erhält.
            Die Spieler müssen der Farbe folgen, wenn möglich, andernfalls können sie jede Karte spielen.
            Die höchste Karte der führenden Farbe gewinnt den Stich.
            Das Spiel wird fortgesetzt, bis alle Karten gespielt wurden und der Spieler mit den meisten Stichen gewinnt.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("de");
    }

    #endregion

    #region French Detection Tests

    [Fact]
    public async Task DetectLanguageAsync_FrenchText_ReturnsFr()
    {
        // Arrange
        var text = "Les joueurs placent à tour de rôle leurs pièces sur le plateau. Le premier joueur à aligner trois pièces dans une rangée remporte la partie.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("fr");
    }

    [Fact]
    public async Task DetectLanguageAsync_LongFrenchText_ReturnsFr()
    {
        // Arrange
        var text = @"Le jeu commence avec chaque joueur recevant cinq cartes du paquet.
            Les joueurs doivent suivre la couleur si possible, sinon ils peuvent jouer n'importe quelle carte.
            La carte la plus élevée de la couleur menante remporte le pli.
            Le jeu continue jusqu'à ce que toutes les cartes aient été jouées et le joueur avec le plus de plis gagne.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("fr");
    }

    #endregion

    #region Spanish Detection Tests

    [Fact]
    public async Task DetectLanguageAsync_SpanishText_ReturnsEs()
    {
        // Arrange
        var text = "Los jugadores colocan sus piezas en el tablero por turnos. El primer jugador que alinee tres piezas en una fila gana el juego.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("es");
    }

    [Fact]
    public async Task DetectLanguageAsync_LongSpanishText_ReturnsEs()
    {
        // Arrange
        var text = @"El juego comienza con cada jugador recibiendo cinco cartas del mazo.
            Los jugadores deben seguir el palo si es posible, de lo contrario pueden jugar cualquier carta.
            La carta más alta del palo principal gana la baza.
            El juego continúa hasta que todas las cartas hayan sido jugadas y el jugador con más bazas gana.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("es");
    }

    #endregion

    #region Edge Cases Tests

    [Fact]
    public async Task DetectLanguageAsync_UnsupportedLanguage_ReturnsEn()
    {
        // Arrange (Japanese text - unsupported)
        var text = "プレイヤーは順番にボード上に駒を配置します。最初に3つの駒を一列に並べたプレイヤーがゲームに勝ちます。";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("en"); // Fallback to English
    }

    [Fact]
    public async Task DetectLanguageAsync_VeryShortText_ReturnsEn()
    {
        // Arrange (too short for reliable detection)
        var text = "Hi";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("en"); // Default to English
    }

    [Fact]
    public async Task DetectLanguageAsync_EmptyText_ReturnsEn()
    {
        // Arrange
        var text = "";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("en"); // Default to English
    }

    [Fact]
    public async Task DetectLanguageAsync_WhitespaceOnly_ReturnsEn()
    {
        // Arrange
        var text = "   \n\t  ";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("en"); // Default to English
    }

    [Fact]
    public async Task DetectLanguageAsync_MixedLanguageText_ReturnsDetectedDominantLanguage()
    {
        // Arrange (Mostly Italian with some English)
        var text = @"Il gioco è molto divertente.
            I giocatori devono seguire le regole.
            The game is fun.
            La strategia è importante per vincere.";

        // Act
        var result = await _service.DetectLanguageAsync(text);

        // Assert
        result.Should().Be("it"); // Italian is dominant
    }

    #endregion

    #region IsSupportedLanguage Tests

    [Theory]
    [InlineData("en", true)]
    [InlineData("it", true)]
    [InlineData("de", true)]
    [InlineData("fr", true)]
    [InlineData("es", true)]
    [InlineData("EN", true)] // Case insensitive
    [InlineData("IT", true)]
    [InlineData("ja", false)] // Japanese not supported
    [InlineData("zh", false)] // Chinese not supported
    [InlineData("ru", false)] // Russian not supported
    [InlineData("pt", false)] // Portuguese not supported
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsSupportedLanguage_VariousLanguageCodes_ReturnsExpectedResult(string languageCode, bool expected)
    {
        // Act
        var result = _service.IsSupportedLanguage(languageCode);

        // Assert
        result.Should().Be(expected);
    }

    #endregion
}
