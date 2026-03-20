using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Tests for the RulebookAnalysis entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 12
/// </summary>
[Trait("Category", "Unit")]
public sealed class RulebookAnalysisTests
{
    #region CreateFromAI Tests

    [Fact]
    public void CreateFromAI_WithValidData_ReturnsRulebookAnalysis()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            sharedGameId,
            pdfDocumentId,
            "Catan",
            "A game about trading resources",
            new List<string> { "Trading", "Area Control" },
            null,
            new List<Resource>(),
            new List<GamePhase>(),
            new List<string> { "How do I build a road?" },
            0.85m,
            createdBy);

        // Assert
        analysis.Id.Should().NotBe(Guid.Empty);
        analysis.SharedGameId.Should().Be(sharedGameId);
        analysis.PdfDocumentId.Should().Be(pdfDocumentId);
        analysis.GameTitle.Should().Be("Catan");
        analysis.Summary.Should().Be("A game about trading resources");
        analysis.KeyMechanics.Should().HaveCount(2);
        analysis.ConfidenceScore.Should().Be(0.85m);
        analysis.Source.Should().Be(GenerationSource.AI);
        analysis.IsActive.Should().BeFalse();
        analysis.CreatedBy.Should().Be(createdBy);
        analysis.AnalyzedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void CreateFromAI_WithVersion_SetsVersion()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            sharedGameId, pdfDocumentId, "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, createdBy, "2.0");

        // Assert
        analysis.Version.Should().Be("2.0");
    }

    [Fact]
    public void CreateFromAI_WithoutVersion_UsesDefaultVersion()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            sharedGameId, pdfDocumentId, "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, createdBy);

        // Assert
        analysis.Version.Should().Be("1.0");
    }

    [Fact]
    public void CreateFromAI_TrimsGameTitle()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            sharedGameId, pdfDocumentId, "  Catan  ", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, createdBy);

        // Assert
        analysis.GameTitle.Should().Be("Catan");
    }

    [Fact]
    public void CreateFromAI_TrimsSummary()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            sharedGameId, pdfDocumentId, "Game", "  Summary  ",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, createdBy);

        // Assert
        analysis.Summary.Should().Be("Summary");
    }

    #endregion

    #region CreateManual Tests

    [Fact]
    public void CreateManual_WithValidData_ReturnsRulebookAnalysis()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateManual(
            sharedGameId,
            pdfDocumentId,
            "Catan",
            "Manual summary",
            new List<string> { "Trading" },
            null,
            new List<Resource>(),
            new List<GamePhase>(),
            new List<string>(),
            createdBy);

        // Assert
        analysis.Source.Should().Be(GenerationSource.Manual);
        analysis.ConfidenceScore.Should().Be(0.0m);
        analysis.IsActive.Should().BeFalse();
    }

    [Fact]
    public void CreateManual_WithVersion_SetsVersion()
    {
        // Arrange
        var sharedGameId = Guid.NewGuid();
        var pdfDocumentId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();

        // Act
        var analysis = RulebookAnalysis.CreateManual(
            sharedGameId, pdfDocumentId, "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), createdBy, "1.5");

        // Assert
        analysis.Version.Should().Be("1.5");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void CreateFromAI_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var action = () => RulebookAnalysis.CreateFromAI(
            Guid.Empty, Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*SharedGameId cannot be empty*");
    }

    [Fact]
    public void CreateFromAI_WithEmptyPdfDocumentId_ThrowsArgumentException()
    {
        // Act
        var action = () => RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.Empty, "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*PdfDocumentId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void CreateFromAI_WithEmptyGameTitle_ThrowsArgumentException(string? gameTitle)
    {
        // Act
        var action = () => RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), gameTitle!, "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Game title cannot be empty*");
    }

    [Fact]
    public void CreateFromAI_WithGameTitleExceeding300Characters_ThrowsArgumentException()
    {
        // Arrange
        var longTitle = new string('A', 301);

        // Act
        var action = () => RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), longTitle, "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Game title cannot exceed 300 characters*");
    }

    [Fact]
    public void CreateFromAI_WithGameTitleAt300Characters_Succeeds()
    {
        // Arrange
        var title = new string('A', 300);

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), title, "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        analysis.GameTitle.Should().HaveLength(300);
    }

    [Fact]
    public void CreateFromAI_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act
        var action = () => RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.Empty);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*CreatedBy cannot be empty*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(1.1)]
    [InlineData(-1)]
    [InlineData(2)]
    public void CreateFromAI_WithInvalidConfidenceScore_ThrowsArgumentOutOfRangeException(decimal score)
    {
        // Act
        var action = () => RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), score, Guid.NewGuid());

        // Assert
        action.Should().Throw<ArgumentOutOfRangeException>()
            .WithMessage("*Confidence score must be between 0 and 1*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.5)]
    [InlineData(1)]
    public void CreateFromAI_WithValidConfidenceScore_Succeeds(decimal score)
    {
        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), score, Guid.NewGuid());

        // Assert
        analysis.ConfidenceScore.Should().Be(score);
    }

    #endregion

    #region SetAsActive Tests

    [Fact]
    public void SetAsActive_SetsIsActiveToTrue()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());
        analysis.IsActive.Should().BeFalse();

        // Act
        analysis.SetAsActive();

        // Assert
        analysis.IsActive.Should().BeTrue();
    }

    #endregion

    #region Deactivate Tests

    [Fact]
    public void Deactivate_SetsIsActiveToFalse()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());
        analysis.SetAsActive();
        analysis.IsActive.Should().BeTrue();

        // Act
        analysis.Deactivate();

        // Assert
        analysis.IsActive.Should().BeFalse();
    }

    #endregion

    #region UpdateContent Tests

    [Fact]
    public void UpdateContent_WithValidData_UpdatesAllFields()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Original Summary",
            new List<string> { "Original" }, null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Act
        analysis.UpdateContent(
            "Updated Summary",
            new List<string> { "Updated" },
            VictoryConditions.Create("First to 10 points"),
            new List<Resource> { Resource.Create("Gold", "Currency") },
            new List<GamePhase> { GamePhase.Create("Setup", "Initial game setup", 1) },
            new List<string> { "New question?" });

        // Assert
        analysis.Summary.Should().Be("Updated Summary");
        analysis.KeyMechanics.Should().ContainSingle().Which.Should().Be("Updated");
        analysis.VictoryConditions.Should().NotBeNull();
        analysis.Resources.Should().HaveCount(1);
        analysis.GamePhases.Should().HaveCount(1);
        analysis.CommonQuestions.Should().HaveCount(1);
        analysis.Source.Should().Be(GenerationSource.Manual);
        analysis.ConfidenceScore.Should().Be(0.0m);
    }

    [Fact]
    public void UpdateContent_TrimsSummary()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Original",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Act
        analysis.UpdateContent(
            "  Updated  ",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>());

        // Assert
        analysis.Summary.Should().Be("Updated");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateContent_WithEmptySummary_ThrowsArgumentException(string? summary)
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Original",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Act
        var action = () => analysis.UpdateContent(
            summary!,
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>());

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Summary cannot be empty*");
    }

    [Fact]
    public void UpdateContent_WithNullLists_UsesEmptyLists()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Original",
            new List<string> { "Existing" }, null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Act
        analysis.UpdateContent(
            "Updated",
            null!, null, null!, null!, null!);

        // Assert
        analysis.KeyMechanics.Should().BeEmpty();
        analysis.Resources.Should().BeEmpty();
        analysis.GamePhases.Should().BeEmpty();
        analysis.CommonQuestions.Should().BeEmpty();
    }

    #endregion

    #region Collection Tests

    [Fact]
    public void KeyMechanics_ReturnsReadOnlyCollection()
    {
        // Arrange
        var mechanics = new List<string> { "Trading", "Area Control" };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            mechanics, null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        analysis.KeyMechanics.Should().HaveCount(2);
        analysis.KeyMechanics.Should().Contain("Trading");
        analysis.KeyMechanics.Should().Contain("Area Control");
    }

    [Fact]
    public void Resources_ReturnsReadOnlyCollection()
    {
        // Arrange
        var resources = new List<Resource> { Resource.Create("Gold", "Currency"), Resource.Create("Wood", "Material") };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, resources, new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        analysis.Resources.Should().HaveCount(2);
    }

    [Fact]
    public void GamePhases_ReturnsReadOnlyCollection()
    {
        // Arrange
        var phases = new List<GamePhase> { GamePhase.Create("Setup", "Initial setup phase", 1), GamePhase.Create("Action", "Main action phase", 2) };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), phases,
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        analysis.GamePhases.Should().HaveCount(2);
    }

    [Fact]
    public void CommonQuestions_ReturnsReadOnlyCollection()
    {
        // Arrange
        var questions = new List<string> { "Q1?", "Q2?" };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            questions, 0.9m, Guid.NewGuid());

        // Assert
        analysis.CommonQuestions.Should().HaveCount(2);
        analysis.CommonQuestions.Should().Contain("Q1?");
    }

    #endregion

    #region KeyConcepts Tests (Issue #5448)

    [Fact]
    public void CreateFromAI_WithKeyConcepts_StoresKeyConcepts()
    {
        // Arrange
        var keyConcepts = new List<KeyConcept>
        {
            new("Worker Placement", "A mechanic where players place workers on action spaces", "Mechanic"),
            new("Victory Points", "Points earned to win the game", "Rule"),
            new("Meeple", "A wooden playing piece representing a person", "Component")
        };

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid(),
            keyConcepts: keyConcepts);

        // Assert
        analysis.KeyConcepts.Should().HaveCount(3);
        analysis.KeyConcepts[0].Term.Should().Be("Worker Placement");
        analysis.KeyConcepts[0].Definition.Should().Contain("action spaces");
        analysis.KeyConcepts[0].Category.Should().Be("Mechanic");
        analysis.KeyConcepts[2].Term.Should().Be("Meeple");
        analysis.KeyConcepts[2].Category.Should().Be("Component");
    }

    [Fact]
    public void CreateFromAI_WithoutKeyConcepts_DefaultsToEmptyList()
    {
        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        analysis.KeyConcepts.Should().BeEmpty();
    }

    [Fact]
    public void CreateManual_WithKeyConcepts_StoresKeyConcepts()
    {
        // Arrange
        var keyConcepts = new List<KeyConcept>
        {
            new("Resource", "Items collected during gameplay", "Component")
        };

        // Act
        var analysis = RulebookAnalysis.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), Guid.NewGuid(),
            keyConcepts: keyConcepts);

        // Assert
        analysis.KeyConcepts.Should().HaveCount(1);
        analysis.KeyConcepts[0].Term.Should().Be("Resource");
    }

    [Fact]
    public void UpdateContent_WithKeyConcepts_UpdatesKeyConcepts()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Original",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());
        analysis.KeyConcepts.Should().BeEmpty();

        var keyConcepts = new List<KeyConcept>
        {
            new("Tile", "A hex-shaped piece placed on the board", "Component"),
            new("Phase", "A distinct section of a game turn", "Rule")
        };

        // Act
        analysis.UpdateContent(
            "Updated Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(),
            keyConcepts);

        // Assert
        analysis.KeyConcepts.Should().HaveCount(2);
        analysis.KeyConcepts[0].Term.Should().Be("Tile");
        analysis.KeyConcepts[1].Category.Should().Be("Rule");
    }

    [Fact]
    public void UpdateContent_WithNullKeyConcepts_ClearsKeyConcepts()
    {
        // Arrange
        var initial = new List<KeyConcept> { new("Term", "Def", "Cat") };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid(),
            keyConcepts: initial);
        analysis.KeyConcepts.Should().HaveCount(1);

        // Act
        analysis.UpdateContent(
            "Updated",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>());

        // Assert
        analysis.KeyConcepts.Should().BeEmpty();
    }

    [Fact]
    public void KeyConcepts_ReturnsReadOnlyCollection()
    {
        // Arrange
        var keyConcepts = new List<KeyConcept>
        {
            new("Dice Roll", "Rolling dice to determine outcomes", "Action"),
            new("Draw", "Taking a card from a deck", "Action")
        };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid(),
            keyConcepts: keyConcepts);

        // Assert
        analysis.KeyConcepts.Should().HaveCount(2);
        analysis.KeyConcepts.Should().BeAssignableTo<IReadOnlyList<KeyConcept>>();
    }

    [Fact]
    public void InternalConstructor_WithKeyConcepts_ReconstitutesCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var sharedGameId = Guid.NewGuid();
        var pdfId = Guid.NewGuid();
        var createdBy = Guid.NewGuid();
        var analyzedAt = DateTime.UtcNow;
        var keyConcepts = new List<KeyConcept>
        {
            new("Token", "A small piece representing a game element", "Component")
        };

        // Act
        var analysis = new RulebookAnalysis(
            id, sharedGameId, pdfId, "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, "1.0", true, GenerationSource.AI, analyzedAt, createdBy,
            keyConcepts);

        // Assert
        analysis.Id.Should().Be(id);
        analysis.KeyConcepts.Should().HaveCount(1);
        analysis.KeyConcepts[0].Term.Should().Be("Token");
    }

    #endregion

    #region GeneratedFaqs Tests (Issue #5449)

    [Fact]
    public void CreateFromAI_WithGeneratedFaqs_StoresFaqs()
    {
        // Arrange
        var faqs = new List<GeneratedFaq>
        {
            new("How do you win?", "Score the most victory points by end of round 10.", "Scoring", 0.92m, new List<string> { "scoring", "setup" }),
            new("What happens on a tie?", "The player with more resources wins.", "Tiebreaker", 0.85m, new List<string> { "scoring", "resources" })
        };

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid(),
            generatedFaqs: faqs);

        // Assert
        analysis.GeneratedFaqs.Should().HaveCount(2);
        analysis.GeneratedFaqs[0].Question.Should().Be("How do you win?");
        analysis.GeneratedFaqs[0].Answer.Should().Contain("victory points");
        analysis.GeneratedFaqs[0].SourceSection.Should().Be("Scoring");
        analysis.GeneratedFaqs[0].Confidence.Should().Be(0.92m);
        analysis.GeneratedFaqs[0].Tags.Should().Contain("scoring");
    }

    [Fact]
    public void CreateFromAI_WithoutGeneratedFaqs_DefaultsToEmptyList()
    {
        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());

        // Assert
        analysis.GeneratedFaqs.Should().BeEmpty();
    }

    [Fact]
    public void CreateManual_WithGeneratedFaqs_StoresFaqs()
    {
        // Arrange
        var faqs = new List<GeneratedFaq>
        {
            new("How to setup?", "Place the board and deal 5 cards.", "Setup", 0.95m, new List<string> { "setup" })
        };

        // Act
        var analysis = RulebookAnalysis.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), Guid.NewGuid(),
            generatedFaqs: faqs);

        // Assert
        analysis.GeneratedFaqs.Should().HaveCount(1);
        analysis.GeneratedFaqs[0].Question.Should().Be("How to setup?");
    }

    [Fact]
    public void UpdateContent_WithGeneratedFaqs_UpdatesFaqs()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Original",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid());
        analysis.GeneratedFaqs.Should().BeEmpty();

        var faqs = new List<GeneratedFaq>
        {
            new("How many players?", "2-4 players supported.", "Overview", 0.99m, new List<string> { "setup", "players" })
        };

        // Act
        analysis.UpdateContent(
            "Updated Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(),
            generatedFaqs: faqs);

        // Assert
        analysis.GeneratedFaqs.Should().HaveCount(1);
        analysis.GeneratedFaqs[0].Tags.Should().HaveCount(2);
    }

    [Fact]
    public void UpdateContent_WithNullGeneratedFaqs_ClearsFaqs()
    {
        // Arrange
        var initial = new List<GeneratedFaq>
        {
            new("Q?", "A.", "Section", 0.8m, new List<string> { "tag" })
        };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid(),
            generatedFaqs: initial);
        analysis.GeneratedFaqs.Should().HaveCount(1);

        // Act
        analysis.UpdateContent(
            "Updated",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>());

        // Assert
        analysis.GeneratedFaqs.Should().BeEmpty();
    }

    [Fact]
    public void GeneratedFaqs_ReturnsReadOnlyCollection()
    {
        // Arrange
        var faqs = new List<GeneratedFaq>
        {
            new("Q1?", "A1.", "S1", 0.9m, new List<string> { "t1" }),
            new("Q2?", "A2.", "S2", 0.8m, new List<string> { "t2" })
        };
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.9m, Guid.NewGuid(),
            generatedFaqs: faqs);

        // Assert
        analysis.GeneratedFaqs.Should().HaveCount(2);
        analysis.GeneratedFaqs.Should().BeAssignableTo<IReadOnlyList<GeneratedFaq>>();
    }

    [Fact]
    public void InternalConstructor_WithGeneratedFaqs_ReconstitutesCorrectly()
    {
        // Arrange
        var faqs = new List<GeneratedFaq>
        {
            new("How to score?", "Collect sets of cards.", "Scoring", 0.88m, new List<string> { "scoring", "cards" })
        };

        // Act
        var analysis = new RulebookAnalysis(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, "1.0", true, GenerationSource.AI, DateTime.UtcNow, Guid.NewGuid(),
            generatedFaqs: faqs);

        // Assert
        analysis.GeneratedFaqs.Should().HaveCount(1);
        analysis.GeneratedFaqs[0].Question.Should().Be("How to score?");
        analysis.GeneratedFaqs[0].Tags.Should().Contain("cards");
    }

    #endregion

    #region GameStateSchemaJson Tests

    [Fact]
    public void CreateFromAI_WithGameStateSchema_StoresSchema()
    {
        // Arrange
        var schema = """{"type":"object","properties":{"playerResources":{"type":"object"}}}""";

        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid(),
            gameStateSchemaJson: schema);

        // Assert
        analysis.GameStateSchemaJson.Should().Be(schema);
    }

    [Fact]
    public void CreateFromAI_WithoutGameStateSchema_DefaultsToNull()
    {
        // Act
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid());

        // Assert
        analysis.GameStateSchemaJson.Should().BeNull();
    }

    [Fact]
    public void CreateManual_WithGameStateSchema_StoresSchema()
    {
        // Arrange
        var schema = """{"type":"object","properties":{"score":{"type":"integer"}}}""";

        // Act
        var analysis = RulebookAnalysis.CreateManual(
            Guid.NewGuid(), Guid.NewGuid(), "Chess", "Classic strategy",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), Guid.NewGuid(),
            gameStateSchemaJson: schema);

        // Assert
        analysis.GameStateSchemaJson.Should().Be(schema);
    }

    [Fact]
    public void UpdateContent_WithGameStateSchema_UpdatesSchema()
    {
        // Arrange
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid());

        var schema = """{"type":"object","properties":{"turn":{"type":"integer"}}}""";

        // Act
        analysis.UpdateContent(
            "Updated summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(),
            gameStateSchemaJson: schema);

        // Assert
        analysis.GameStateSchemaJson.Should().Be(schema);
    }

    [Fact]
    public void UpdateContent_WithNullGameStateSchema_ClearsSchema()
    {
        // Arrange
        var schema = """{"type":"object"}""";
        var analysis = RulebookAnalysis.CreateFromAI(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", "Trading game",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, Guid.NewGuid(),
            gameStateSchemaJson: schema);

        // Act
        analysis.UpdateContent(
            "Updated summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>());

        // Assert
        analysis.GameStateSchemaJson.Should().BeNull();
    }

    [Fact]
    public void Constructor_WithGameStateSchema_ReconstitutesCorrectly()
    {
        // Arrange
        var schema = """{"type":"object","properties":{"resources":{"type":"array"}}}""";

        // Act
        var analysis = new RulebookAnalysis(
            Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Game", "Summary",
            new List<string>(), null, new List<Resource>(), new List<GamePhase>(),
            new List<string>(), 0.85m, "1.0", true, GenerationSource.AI, DateTime.UtcNow, Guid.NewGuid(),
            gameStateSchemaJson: schema);

        // Assert
        analysis.GameStateSchemaJson.Should().Be(schema);
    }

    #endregion
}