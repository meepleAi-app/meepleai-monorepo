using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Unit tests for LanguageDetector (Issue #5445).
/// Validates Unicode script detection and language identification.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class LanguageDetectorTests
{
    private readonly LanguageDetector _detector = new();

    #region Empty/Null Input

    [Fact]
    public void Detect_NullText_ReturnsUnknown()
    {
        var result = _detector.Detect(null!);

        Assert.Equal("unknown", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
        Assert.Equal(0, result.Confidence);
        Assert.NotNull(result.RejectionReason);
    }

    [Fact]
    public void Detect_EmptyText_ReturnsUnknown()
    {
        var result = _detector.Detect("");

        Assert.Equal("unknown", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
    }

    [Fact]
    public void Detect_WhitespaceOnly_ReturnsUnknown()
    {
        var result = _detector.Detect("   \n\t  ");

        Assert.Equal("unknown", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
    }

    [Fact]
    public void Detect_NumbersOnly_ReturnsUnknown()
    {
        var result = _detector.Detect("12345 67890 11111");

        Assert.Equal("unknown", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
    }

    #endregion

    #region English Detection

    [Fact]
    public void Detect_EnglishText_ReturnsEnglish()
    {
        var text = "The player draws a card from the deck and places it on the table. Each turn, you may play one card.";

        var result = _detector.Detect(text);

        Assert.Equal("en", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
        Assert.True(result.Confidence > 0.8);
        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void Detect_EnglishBoardGameRules_ReturnsEnglish()
    {
        var text = "Setup: Each player takes 5 cards. The game ends when a player reaches 10 points. " +
                   "On your turn, you may play one card and draw one card from the deck.";

        var result = _detector.Detect(text);

        Assert.Equal("en", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    #endregion

    #region Italian Detection

    [Fact]
    public void Detect_ItalianText_ReturnsItalian()
    {
        var text = "Il giocatore pesca una carta dal mazzo e la posiziona sul tavolo. " +
                   "Ogni turno, il giocatore può giocare una carta.";

        var result = _detector.Detect(text);

        Assert.Equal("it", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    #endregion

    #region German Detection

    [Fact]
    public void Detect_GermanText_ReturnsGerman()
    {
        var text = "Der Spieler zieht eine Karte vom Stapel und legt sie auf den Tisch. " +
                   "Das Spiel endet, wenn ein Spieler 10 Punkte erreicht.";

        var result = _detector.Detect(text);

        Assert.Equal("de", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    #endregion

    #region French Detection

    [Fact]
    public void Detect_FrenchText_ReturnsFrench()
    {
        var text = "Le joueur pioche une carte dans le paquet et la place sur la table. " +
                   "Pour chaque tour, le joueur peut jouer une carte.";

        var result = _detector.Detect(text);

        Assert.Equal("fr", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    #endregion

    #region Spanish Detection

    [Fact]
    public void Detect_SpanishText_ReturnsSpanish()
    {
        var text = "El jugador roba una carta del mazo y la coloca en la mesa. " +
                   "Cada turno, el jugador puede jugar una carta.";

        var result = _detector.Detect(text);

        Assert.Equal("es", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    #endregion

    #region CJK Rejection

    [Fact]
    public void Detect_ChineseText_RejectsAsUnsupported()
    {
        var text = "玩家从牌堆中抽取一张牌并将其放在桌上。每个回合，玩家可以打出一张牌。";

        var result = _detector.Detect(text);

        Assert.Equal("zh", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
        Assert.Contains("CJK", result.RejectionReason);
    }

    [Fact]
    public void Detect_JapaneseText_RejectsAsUnsupported()
    {
        var text = "プレイヤーは山札からカードを引き、テーブルに置きます。各ターン、カードをプレイできます。";

        var result = _detector.Detect(text);

        Assert.Equal("zh", result.DetectedLanguage); // CJK maps to zh by default
        Assert.False(result.IsAnalyzable);
        Assert.Contains("CJK", result.RejectionReason);
    }

    [Fact]
    public void Detect_KoreanText_RejectsAsUnsupported()
    {
        var text = "플레이어는 덱에서 카드를 뽑아 테이블에 놓습니다. 각 턴마다 카드를 한 장 낼 수 있습니다.";

        var result = _detector.Detect(text);

        Assert.Equal("zh", result.DetectedLanguage); // Hangul is in CJK block
        Assert.False(result.IsAnalyzable);
    }

    #endregion

    #region Arabic Rejection

    [Fact]
    public void Detect_ArabicText_RejectsAsUnsupported()
    {
        var text = "يسحب اللاعب بطاقة من الرزمة ويضعها على الطاولة. في كل دور، يمكن للاعب لعب بطاقة واحدة.";

        var result = _detector.Detect(text);

        Assert.Equal("ar", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
        Assert.Contains("Arabic", result.RejectionReason);
    }

    #endregion

    #region Greek Detection (Supported)

    [Fact]
    public void Detect_GreekText_ReturnsGreekAndIsAnalyzable()
    {
        var text = "Ο παίκτης τραβάει μια κάρτα από τη τράπουλα και την τοποθετεί στο τραπέζι.";

        var result = _detector.Detect(text);

        Assert.Equal("el", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    #endregion

    #region Cyrillic Detection

    [Fact]
    public void Detect_RussianText_RejectsAsUnsupported()
    {
        var text = "Игрок берёт карту из колоды и кладёт её на стол. Каждый ход можно сыграть одну карту.";

        var result = _detector.Detect(text);

        Assert.Equal("ru", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable); // Russian not in analyzable list
    }

    #endregion

    #region Confidence and Edge Cases

    [Fact]
    public void Detect_MixedLatinAndCJK_ReturnsCorrectDominant()
    {
        // Mostly CJK with minimal Latin
        var text = "这个游戏非常好玩，每个玩家都应该尝试。这是一个很好的游戏，规则很简单。" +
                   "玩家从牌堆中抽取一张牌并将其放在桌上。每个回合，玩家可以打出一张牌。";

        var result = _detector.Detect(text);

        // CJK should dominate
        Assert.Equal("zh", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
    }

    [Fact]
    public void Detect_ShortText_StillWorks()
    {
        var text = "the game is fun";

        var result = _detector.Detect(text);

        Assert.Equal("en", result.DetectedLanguage);
        Assert.True(result.IsAnalyzable);
    }

    [Fact]
    public void Detect_LatinScriptNoMarkers_DefaultsToEnglish()
    {
        // Latin text with no language-specific markers
        var text = "Lorem ipsum dolor sit amet consectetur adipiscing";

        var result = _detector.Detect(text);

        Assert.Equal("en", result.DetectedLanguage); // Defaults to English for unrecognized Latin
        Assert.True(result.IsAnalyzable);
    }

    [Theory]
    [InlineData("en")]
    [InlineData("it")]
    [InlineData("de")]
    [InlineData("fr")]
    [InlineData("es")]
    [InlineData("pt")]
    [InlineData("nl")]
    [InlineData("pl")]
    [InlineData("sv")]
    [InlineData("da")]
    [InlineData("no")]
    [InlineData("fi")]
    [InlineData("cs")]
    [InlineData("hu")]
    [InlineData("ro")]
    [InlineData("hr")]
    [InlineData("el")]
    public void AnalyzableLanguages_AreAllSupported(string lang)
    {
        // Verify the result record works correctly for all analyzable languages
        var result = new LanguageDetectionResult(
            DetectedLanguage: lang,
            IsAnalyzable: true,
            Confidence: 0.95);

        Assert.True(result.IsAnalyzable);
        Assert.Equal(lang, result.DetectedLanguage);
        Assert.Null(result.RejectionReason);
    }

    #endregion

    #region Thai and Devanagari Rejection

    [Fact]
    public void Detect_ThaiText_RejectsAsUnsupported()
    {
        var text = "ผู้เล่นหยิบไพ่จากกองและวางลงบนโต๊ะ ในแต่ละเทิร์นผู้เล่นสามารถเล่นไพ่ได้หนึ่งใบ";

        var result = _detector.Detect(text);

        Assert.Equal("th", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
        Assert.Contains("Thai", result.RejectionReason);
    }

    [Fact]
    public void Detect_HindiText_RejectsAsUnsupported()
    {
        var text = "खिलाड़ी डेक से एक कार्ड निकालता है और उसे मेज पर रखता है। प्रत्येक मोड़ पर एक कार्ड खेला जा सकता है।";

        var result = _detector.Detect(text);

        Assert.Equal("hi", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
        Assert.Contains("Devanagari", result.RejectionReason);
    }

    #endregion

    #region Hebrew Rejection

    [Fact]
    public void Detect_HebrewText_RejectsAsUnsupported()
    {
        var text = "השחקן שולף קלף מהחפיסה ומניח אותו על השולחן. בכל תור אפשר לשחק קלף אחד.";

        var result = _detector.Detect(text);

        Assert.Equal("he", result.DetectedLanguage);
        Assert.False(result.IsAnalyzable);
        Assert.Contains("Hebrew", result.RejectionReason);
    }

    #endregion

    #region LanguageDetectionResult Record

    [Fact]
    public void LanguageDetectionResult_DefaultRejectionReason_IsNull()
    {
        var result = new LanguageDetectionResult("en", true, 0.95);

        Assert.Null(result.RejectionReason);
    }

    [Fact]
    public void LanguageDetectionResult_WithRejectionReason_IsSet()
    {
        var result = new LanguageDetectionResult("zh", false, 0.9, "CJK not supported");

        Assert.Equal("CJK not supported", result.RejectionReason);
        Assert.False(result.IsAnalyzable);
    }

    #endregion
}
