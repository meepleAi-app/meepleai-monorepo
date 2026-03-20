using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Tests.Constants;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Services;

/// <summary>
/// Unit tests for SessionContextPromptBuilder.
/// Issue #5580: Session-aware RAG chat.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
public class SessionContextPromptBuilderTests
{
    [Fact]
    public void BuildSessionPreamble_WithFullContext_ContainsGameName()
    {
        // Arrange
        var context = CreateFullContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("Catan");
        preamble.Should().Contain("Gioco attivo: Catan");
    }

    [Fact]
    public void BuildSessionPreamble_WithExpansions_ContainsExpansionNames()
    {
        // Arrange
        var context = CreateFullContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("Espansioni attive:");
        preamble.Should().Contain("Catan: Seafarers");
        preamble.Should().Contain("Catan: Cities & Knights");
    }

    [Fact]
    public void BuildSessionPreamble_WithCurrentPhase_ContainsPhase()
    {
        // Arrange
        var context = CreateFullContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("Fase corrente: Trading");
    }

    [Fact]
    public void BuildSessionPreamble_WithKeyMechanics_ContainsMechanics()
    {
        // Arrange
        var context = CreateFullContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("Meccaniche principali:");
        preamble.Should().Contain("Dice Rolling");
        preamble.Should().Contain("Trading");
    }

    [Fact]
    public void BuildSessionPreamble_WithMissingAnalysis_ContainsWarning()
    {
        // Arrange
        var context = CreateContextWithMissingAnalysis();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("ATTENZIONE: Non hai dati analizzati per:");
        preamble.Should().Contain("Catan: Traders & Barbarians");
        preamble.Should().Contain("dichiaralo");
    }

    [Fact]
    public void BuildSessionPreamble_WithoutExpansions_OmitsExpansionLine()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().NotContain("Espansioni attive:");
    }

    [Fact]
    public void BuildSessionPreamble_WithoutPhase_OmitsPhaseLine()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().NotContain("Fase corrente:");
    }

    [Fact]
    public void BuildSessionPreamble_ContainsSessionMarkers()
    {
        // Arrange
        var context = CreateMinimalContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("=== CONTESTO SESSIONE DI GIOCO ===");
        preamble.Should().Contain("=== FINE CONTESTO SESSIONE ===");
    }

    [Fact]
    public void BuildSessionPreamble_NullContext_ThrowsArgumentNullException()
    {
        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            SessionContextPromptBuilder.BuildSessionPreamble(null!));
    }

    [Fact]
    public void GetNoAiDegradationMessage_ReturnsNonEmptyMessage()
    {
        // Act
        var message = SessionContextPromptBuilder.GetNoAiDegradationMessage();

        // Assert
        Assert.False(string.IsNullOrWhiteSpace(message));
        message.Should().Contain("Knowledge Base");
    }

    [Fact]
    public void BuildSessionPreamble_WithRulesSummary_ContainsSummary()
    {
        // Arrange
        var context = CreateFullContext();

        // Act
        var preamble = SessionContextPromptBuilder.BuildSessionPreamble(context);

        // Assert
        preamble.Should().Contain("Riassunto regole:");
        preamble.Should().Contain("Build settlements");
    }

    // --- Factory methods ---

    private static GameSessionContextDto CreateFullContext()
    {
        var primaryGameId = Guid.NewGuid();
        var expansion1Id = Guid.NewGuid();
        var expansion2Id = Guid.NewGuid();

        return new GameSessionContextDto(
            SessionId: Guid.NewGuid(),
            PrimaryGameId: primaryGameId,
            ExpansionGameIds: new List<Guid> { expansion1Id, expansion2Id },
            AllGameIds: new List<Guid> { primaryGameId, expansion1Id, expansion2Id },
            KbCardIds: new List<Guid> { primaryGameId, expansion1Id },
            CurrentPhase: "Trading",
            PrimaryRules: new RulebookAnalysisSummaryDto(
                GameId: primaryGameId,
                GameTitle: "Catan",
                Summary: "Build settlements and cities to earn victory points.",
                KeyMechanics: new List<string> { "Dice Rolling", "Trading", "Area Control" },
                CurrentPhaseName: "Trading",
                PhaseNames: new List<string> { "Setup", "Trading", "Building" }),
            ExpansionRules: new List<RulebookAnalysisSummaryDto>
            {
                new(
                    GameId: expansion1Id,
                    GameTitle: "Catan: Seafarers",
                    Summary: "Explore the seas and discover new islands.",
                    KeyMechanics: new List<string> { "Exploration", "Shipping" },
                    CurrentPhaseName: null,
                    PhaseNames: new List<string>()),
                new(
                    GameId: expansion2Id,
                    GameTitle: "Catan: Cities & Knights",
                    Summary: "Defend against barbarians and develop cities.",
                    KeyMechanics: new List<string> { "City Development", "Defense" },
                    CurrentPhaseName: null,
                    PhaseNames: new List<string>())
            },
            MissingAnalysisGameNames: new List<string>(),
            GamesWithoutPdf: new List<Guid>(),
            DegradationLevel: SessionDegradationLevel.Full);
    }

    private static GameSessionContextDto CreateContextWithMissingAnalysis()
    {
        var primaryGameId = Guid.NewGuid();

        return new GameSessionContextDto(
            SessionId: Guid.NewGuid(),
            PrimaryGameId: primaryGameId,
            ExpansionGameIds: new List<Guid> { Guid.NewGuid() },
            AllGameIds: new List<Guid> { primaryGameId, Guid.NewGuid() },
            KbCardIds: new List<Guid> { primaryGameId },
            CurrentPhase: null,
            PrimaryRules: new RulebookAnalysisSummaryDto(
                GameId: primaryGameId,
                GameTitle: "Catan",
                Summary: "Build settlements.",
                KeyMechanics: new List<string> { "Dice Rolling" },
                CurrentPhaseName: null,
                PhaseNames: new List<string>()),
            ExpansionRules: new List<RulebookAnalysisSummaryDto>(),
            MissingAnalysisGameNames: new List<string> { "Catan: Traders & Barbarians" },
            GamesWithoutPdf: new List<Guid> { Guid.NewGuid() },
            DegradationLevel: SessionDegradationLevel.Partial);
    }

    private static GameSessionContextDto CreateMinimalContext()
    {
        var primaryGameId = Guid.NewGuid();

        return new GameSessionContextDto(
            SessionId: Guid.NewGuid(),
            PrimaryGameId: primaryGameId,
            ExpansionGameIds: new List<Guid>(),
            AllGameIds: new List<Guid> { primaryGameId },
            KbCardIds: new List<Guid> { primaryGameId },
            CurrentPhase: null,
            PrimaryRules: new RulebookAnalysisSummaryDto(
                GameId: primaryGameId,
                GameTitle: "Chess",
                Summary: "Classic strategy game.",
                KeyMechanics: new List<string>(),
                CurrentPhaseName: null,
                PhaseNames: new List<string>()),
            ExpansionRules: new List<RulebookAnalysisSummaryDto>(),
            MissingAnalysisGameNames: new List<string>(),
            GamesWithoutPdf: new List<Guid>(),
            DegradationLevel: SessionDegradationLevel.Full);
    }
}
