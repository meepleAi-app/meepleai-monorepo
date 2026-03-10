using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Unit.KnowledgeBase.Services;

/// <summary>
/// Unit tests for PiiDetector — GDPR PII detection and redaction.
/// Issue #5510: Tests email, phone, fiscal code, and credit card detection
/// with Italian-specific test cases.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class PiiDetectorTests
{
    private readonly PiiDetector _detector;
    private readonly PiiDetectorOptions _options;

    public PiiDetectorTests()
    {
        _options = new PiiDetectorOptions();
        _detector = CreateDetector(_options);
    }

    private static PiiDetector CreateDetector(PiiDetectorOptions options)
    {
        return new PiiDetector(
            Options.Create(options),
            Mock.Of<ILogger<PiiDetector>>());
    }

    #region Email Detection

    [Theory]
    [InlineData("Contact me at mario.rossi@gmail.com for info")]
    [InlineData("Email: giuseppe.verdi@pec.it")]
    [InlineData("Send to user+tag@example.co.uk")]
    public void Scan_DetectsEmailAddresses(string text)
    {
        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeTrue();
        result.Matches.Should().ContainSingle(m => m.Type == PiiType.Email);
    }

    [Fact]
    public void Scan_DetectsMultipleEmails()
    {
        var text = "From mario@test.com to luigi@test.it about the game";

        var result = _detector.Scan(text);

        result.Matches.Where(m => m.Type == PiiType.Email).Should().HaveCount(2);
    }

    [Fact]
    public void Redact_ReplacesEmailWithPlaceholder()
    {
        var text = "Contact mario.rossi@gmail.com for rules";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().Be("Contact [REDACTED_EMAIL] for rules");
        redacted.Should().NotContain("mario.rossi@gmail.com");
    }

    #endregion

    #region Phone Detection

    [Theory]
    [InlineData("Chiamami al +39 02 1234 5678", "+39 02 1234 5678")]
    [InlineData("Cell: +39 333 1234567", "+39 333 1234567")]
    [InlineData("Tel: 06 6789 1234", "06 6789 1234")]
    [InlineData("Call +1 (555) 123-4567", "+1 (555) 123-4567")]
    public void Scan_DetectsPhoneNumbers(string text, string expectedMatch)
    {
        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeTrue();
        result.Matches.Should().Contain(m =>
            m.Type == PiiType.Phone &&
            m.OriginalValue == expectedMatch);
    }

    [Fact]
    public void Redact_ReplacesPhoneWithPlaceholder()
    {
        var text = "My number is +39 333 1234567";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().Contain("[REDACTED_PHONE]");
        redacted.Should().NotContain("333 1234567");
    }

    #endregion

    #region Italian Fiscal Code Detection

    [Theory]
    [InlineData("Il mio codice fiscale è RSSMRA85T10H501Z", "RSSMRA85T10H501Z")]
    [InlineData("CF: VRDGPP90A01F205X", "VRDGPP90A01F205X")]
    [InlineData("codice fiscale rssmra85t10h501z (lowercase)", "rssmra85t10h501z")]
    public void Scan_DetectsItalianFiscalCode(string text, string expectedMatch)
    {
        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeTrue();
        result.Matches.Should().Contain(m =>
            m.Type == PiiType.ItalianFiscalCode &&
            string.Equals(m.OriginalValue, expectedMatch, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Redact_ReplacesFiscalCodeWithPlaceholder()
    {
        var text = "My fiscal code is RSSMRA85T10H501Z and I need help";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().Contain("[REDACTED_FISCAL_CODE]");
        redacted.Should().NotContain("RSSMRA85T10H501Z");
    }

    #endregion

    #region Credit Card Detection

    [Theory]
    [InlineData("Card: 4111 1111 1111 1111")]
    [InlineData("Pay with 5500-0000-0000-0004")]
    public void Scan_DetectsCreditCardNumbers(string text)
    {
        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeTrue();
        result.Matches.Should().Contain(m => m.Type == PiiType.CreditCard);
    }

    [Fact]
    public void Redact_ReplacesCreditCardWithPlaceholder()
    {
        var text = "My card is 4111 1111 1111 1111";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().Contain("[REDACTED_CREDIT_CARD]");
        redacted.Should().NotContain("4111");
    }

    #endregion

    #region Mixed PII

    [Fact]
    public void Scan_DetectsMultiplePiiTypes()
    {
        var text = "Sono Mario, email mario@test.com, CF RSSMRA85T10H501Z, tel +39 333 1234567";

        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeTrue();
        result.Matches.Should().Contain(m => m.Type == PiiType.Email);
        result.Matches.Should().Contain(m => m.Type == PiiType.ItalianFiscalCode);
        result.Matches.Should().Contain(m => m.Type == PiiType.Phone);
    }

    [Fact]
    public void ScanAndRedact_RedactsAllPiiTypes()
    {
        var text = "Email: mario@test.com, CF: RSSMRA85T10H501Z";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().Contain("[REDACTED_EMAIL]");
        redacted.Should().Contain("[REDACTED_FISCAL_CODE]");
        redacted.Should().NotContain("mario@test.com");
        redacted.Should().NotContain("RSSMRA85T10H501Z");
    }

    #endregion

    #region No PII

    [Theory]
    [InlineData("What are the rules of Catan?")]
    [InlineData("How many victory points do I need?")]
    [InlineData("The game has 4 players")]
    [InlineData("")]
    public void Scan_NoPiiInCleanText(string text)
    {
        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeFalse();
        result.Matches.Should().BeEmpty();
    }

    [Fact]
    public void Scan_NullOrEmptyText_ReturnsEmpty()
    {
        _detector.Scan(null!).ContainsPii.Should().BeFalse();
        _detector.Scan("").ContainsPii.Should().BeFalse();
    }

    [Fact]
    public void Redact_NoPii_ReturnsOriginalText()
    {
        var text = "What are the rules of Catan?";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().Be(text);
    }

    #endregion

    #region Configuration

    [Fact]
    public void Scan_DisabledGlobally_ReturnsEmpty()
    {
        var options = new PiiDetectorOptions { Enabled = false };
        var detector = CreateDetector(options);

        var result = detector.Scan("Contact mario@test.com");

        result.ContainsPii.Should().BeFalse();
    }

    [Fact]
    public void Scan_EmailDetectionDisabled_SkipsEmails()
    {
        var options = new PiiDetectorOptions { DetectEmails = false };
        var detector = CreateDetector(options);

        var result = detector.Scan("Contact mario@test.com");

        result.Matches.Should().NotContain(m => m.Type == PiiType.Email);
    }

    [Fact]
    public void Scan_PhoneDetectionDisabled_SkipsPhones()
    {
        var options = new PiiDetectorOptions { DetectPhones = false };
        var detector = CreateDetector(options);

        var result = detector.Scan("Call +39 333 1234567");

        result.Matches.Should().NotContain(m => m.Type == PiiType.Phone);
    }

    [Fact]
    public void Scan_FiscalCodeDetectionDisabled_SkipsFiscalCodes()
    {
        var options = new PiiDetectorOptions { DetectFiscalCodes = false };
        var detector = CreateDetector(options);

        var result = detector.Scan("CF: RSSMRA85T10H501Z");

        result.Matches.Should().NotContain(m => m.Type == PiiType.ItalianFiscalCode);
    }

    [Fact]
    public void Scan_CreditCardDetectionDisabled_SkipsCreditCards()
    {
        var options = new PiiDetectorOptions { DetectCreditCards = false };
        var detector = CreateDetector(options);

        var result = detector.Scan("Card: 4111 1111 1111 1111");

        result.Matches.Should().NotContain(m => m.Type == PiiType.CreditCard);
    }

    #endregion

    #region Performance

    [Fact]
    public void Scan_PerformanceUnder5ms()
    {
        // Generate a realistic prompt with some PII
        var text = string.Concat(Enumerable.Repeat(
            "What are the rules for Catan? My email is mario@test.com. ", 20));

        var result = _detector.Scan(text);

        result.ScanDuration.TotalMilliseconds.Should().BeLessThan(5,
            "PII scan should complete in under 5ms as per Issue #5510 requirement");
    }

    [Fact]
    public void Scan_LargeTextPerformance()
    {
        // ~10KB of text with scattered PII
        var text = string.Concat(Enumerable.Repeat(
            "The player rolls dice and moves. Contact support@game.com for help. " +
            "Italian players can use CF RSSMRA85T10H501Z for verification. " +
            "Call +39 06 1234 5678 for assistance. ", 50));

        var result = _detector.Scan(text);

        result.ScanDuration.TotalMilliseconds.Should().BeLessThan(5,
            "PII scan should handle large text in under 5ms");
        result.ContainsPii.Should().BeTrue();
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void Redact_PreservesNonPiiText()
    {
        var text = "Rules question: How many cards in Catan? Email: test@example.com";

        var redacted = _detector.ScanAndRedact(text);

        redacted.Should().StartWith("Rules question: How many cards in Catan? Email: ");
        redacted.Should().EndWith("[REDACTED_EMAIL]");
    }

    [Fact]
    public void Scan_DoesNotFalsePositiveOnGameTerms()
    {
        // Board game text should not trigger PII detection
        var text = "Player 1 has 10 victory points. The robber blocks hex A3. " +
                   "Draw 2 development cards. Resource: 3 wheat, 2 ore.";

        var result = _detector.Scan(text);

        result.ContainsPii.Should().BeFalse();
    }

    [Fact]
    public void ScanResult_ReportsCorrectPositions()
    {
        var text = "Email: mario@test.com here";

        var result = _detector.Scan(text);

        var match = result.Matches.Single(m => m.Type == PiiType.Email);
        match.StartIndex.Should().Be(7); // "Email: " is 7 chars
        match.OriginalValue.Should().Be("mario@test.com");
        match.Length.Should().Be("mario@test.com".Length);
    }

    #endregion
}
