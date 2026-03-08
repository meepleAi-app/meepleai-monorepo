using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Unit tests for MechanicDraft domain entity.
/// Mechanic Extractor — Variant C copyright-compliant workflow.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicDraftTests
{
    private static readonly Guid ValidSharedGameId = Guid.NewGuid();
    private static readonly Guid ValidPdfDocumentId = Guid.NewGuid();
    private static readonly Guid ValidCreatedBy = Guid.NewGuid();
    private const string ValidGameTitle = "Catan";

    #region Create Factory Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsDraftWithNewId()
    {
        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);

        // Assert
        draft.Should().NotBeNull();
        draft.Id.Should().NotBe(Guid.Empty);
        draft.SharedGameId.Should().Be(ValidSharedGameId);
        draft.PdfDocumentId.Should().Be(ValidPdfDocumentId);
        draft.GameTitle.Should().Be(ValidGameTitle);
        draft.CreatedBy.Should().Be(ValidCreatedBy);
    }

    [Fact]
    public void Create_SetsStatusToDraft()
    {
        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);

        // Assert
        draft.Status.Should().Be(MechanicDraftStatus.Draft);
    }

    [Fact]
    public void Create_SetsTimestamps()
    {
        // Arrange
        var before = DateTime.UtcNow;

        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);
        var after = DateTime.UtcNow;

        // Assert
        draft.CreatedAt.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
        draft.LastModified.Should().BeOnOrAfter(before).And.BeOnOrBefore(after);
    }

    [Fact]
    public void Create_InitializesAllNotesAsEmpty()
    {
        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);

        // Assert
        draft.SummaryNotes.Should().BeEmpty();
        draft.MechanicsNotes.Should().BeEmpty();
        draft.VictoryNotes.Should().BeEmpty();
        draft.ResourcesNotes.Should().BeEmpty();
        draft.PhasesNotes.Should().BeEmpty();
        draft.QuestionsNotes.Should().BeEmpty();
    }

    [Fact]
    public void Create_InitializesAllDraftsAsEmpty()
    {
        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);

        // Assert
        draft.SummaryDraft.Should().BeEmpty();
        draft.MechanicsDraft.Should().BeEmpty();
        draft.VictoryDraft.Should().BeEmpty();
        draft.ResourcesDraft.Should().BeEmpty();
        draft.PhasesDraft.Should().BeEmpty();
        draft.QuestionsDraft.Should().BeEmpty();
    }

    [Fact]
    public void Create_TrimsGameTitle()
    {
        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, "  Catan  ", ValidCreatedBy);

        // Assert
        draft.GameTitle.Should().Be("Catan");
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        // Act
        var draft1 = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);
        var draft2 = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);

        // Assert
        draft1.Id.Should().NotBe(draft2.Id);
    }

    [Fact]
    public void Create_WithEmptySharedGameId_ThrowsArgumentException()
    {
        // Act
        var act = () => MechanicDraft.Create(Guid.Empty, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("sharedGameId");
    }

    [Fact]
    public void Create_WithEmptyPdfDocumentId_ThrowsArgumentException()
    {
        // Act
        var act = () => MechanicDraft.Create(ValidSharedGameId, Guid.Empty, ValidGameTitle, ValidCreatedBy);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("pdfDocumentId");
    }

    [Fact]
    public void Create_WithEmptyGameTitle_ThrowsArgumentException()
    {
        // Act
        var act = () => MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, "", ValidCreatedBy);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("gameTitle");
    }

    [Fact]
    public void Create_WithWhitespaceGameTitle_ThrowsArgumentException()
    {
        // Act
        var act = () => MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, "   ", ValidCreatedBy);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("gameTitle");
    }

    [Fact]
    public void Create_WithTooLongGameTitle_ThrowsArgumentException()
    {
        // Arrange
        var longTitle = new string('A', 301);

        // Act
        var act = () => MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, longTitle, ValidCreatedBy);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("gameTitle");
    }

    [Fact]
    public void Create_WithMaxLengthGameTitle_Succeeds()
    {
        // Arrange
        var maxTitle = new string('A', 300);

        // Act
        var draft = MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, maxTitle, ValidCreatedBy);

        // Assert
        draft.GameTitle.Should().Be(maxTitle);
    }

    [Fact]
    public void Create_WithEmptyCreatedBy_ThrowsArgumentException()
    {
        // Act
        var act = () => MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, Guid.Empty);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("createdBy");
    }

    #endregion

    #region UpdateNotes Tests

    [Theory]
    [InlineData("summary")]
    [InlineData("mechanics")]
    [InlineData("victory")]
    [InlineData("resources")]
    [InlineData("phases")]
    [InlineData("questions")]
    public void UpdateNotes_WithValidSection_UpdatesCorrectField(string section)
    {
        // Arrange
        var draft = CreateValidDraft();
        var notes = $"Notes for {section}";

        // Act
        draft.UpdateNotes(section, notes);

        // Assert
        var actualNotes = section switch
        {
            "summary" => draft.SummaryNotes,
            "mechanics" => draft.MechanicsNotes,
            "victory" => draft.VictoryNotes,
            "resources" => draft.ResourcesNotes,
            "phases" => draft.PhasesNotes,
            "questions" => draft.QuestionsNotes,
            _ => throw new ArgumentException($"Unknown section: {section}")
        };
        actualNotes.Should().Be(notes);
    }

    [Fact]
    public void UpdateNotes_IsCaseInsensitive()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        draft.UpdateNotes("SUMMARY", "Test notes");

        // Assert
        draft.SummaryNotes.Should().Be("Test notes");
    }

    [Fact]
    public void UpdateNotes_UpdatesLastModified()
    {
        // Arrange
        var draft = CreateValidDraft();
        var originalModified = draft.LastModified;

        // Act
        draft.UpdateNotes("summary", "New notes");

        // Assert
        draft.LastModified.Should().BeOnOrAfter(originalModified);
    }

    [Fact]
    public void UpdateNotes_WithNullNotes_SetsToEmpty()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        draft.UpdateNotes("summary", null!);

        // Assert
        draft.SummaryNotes.Should().BeEmpty();
    }

    [Fact]
    public void UpdateNotes_WithUnknownSection_ThrowsArgumentException()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        var act = () => draft.UpdateNotes("unknown", "notes");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("section");
    }

    [Fact]
    public void UpdateNotes_WithEmptySection_ThrowsArgumentException()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        var act = () => draft.UpdateNotes("", "notes");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("section");
    }

    [Fact]
    public void UpdateNotes_WhenActivated_ThrowsInvalidOperationException()
    {
        // Arrange
        var draft = CreateValidDraft();
        draft.MarkActivated();

        // Act
        var act = () => draft.UpdateNotes("summary", "notes");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    #endregion

    #region AcceptDraft Tests

    [Theory]
    [InlineData("summary")]
    [InlineData("mechanics")]
    [InlineData("victory")]
    [InlineData("resources")]
    [InlineData("phases")]
    [InlineData("questions")]
    public void AcceptDraft_WithValidSection_UpdatesCorrectDraftField(string section)
    {
        // Arrange
        var draft = CreateValidDraft();
        var draftContent = $"AI-generated draft for {section}";

        // Act
        draft.AcceptDraft(section, draftContent);

        // Assert
        var actualDraft = section switch
        {
            "summary" => draft.SummaryDraft,
            "mechanics" => draft.MechanicsDraft,
            "victory" => draft.VictoryDraft,
            "resources" => draft.ResourcesDraft,
            "phases" => draft.PhasesDraft,
            "questions" => draft.QuestionsDraft,
            _ => throw new ArgumentException($"Unknown section: {section}")
        };
        actualDraft.Should().Be(draftContent);
    }

    [Fact]
    public void AcceptDraft_UpdatesLastModified()
    {
        // Arrange
        var draft = CreateValidDraft();
        var originalModified = draft.LastModified;

        // Act
        draft.AcceptDraft("summary", "AI draft");

        // Assert
        draft.LastModified.Should().BeOnOrAfter(originalModified);
    }

    [Fact]
    public void AcceptDraft_WhenActivated_ThrowsInvalidOperationException()
    {
        // Arrange
        var draft = CreateValidDraft();
        draft.MarkActivated();

        // Act
        var act = () => draft.AcceptDraft("summary", "draft");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void AcceptDraft_WithUnknownSection_ThrowsArgumentException()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        var act = () => draft.AcceptDraft("invalid", "draft");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("section");
    }

    #endregion

    #region UpdateAllNotes Tests

    [Fact]
    public void UpdateAllNotes_UpdatesAllFields()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        draft.UpdateAllNotes("s1", "m1", "v1", "r1", "p1", "q1");

        // Assert
        draft.SummaryNotes.Should().Be("s1");
        draft.MechanicsNotes.Should().Be("m1");
        draft.VictoryNotes.Should().Be("v1");
        draft.ResourcesNotes.Should().Be("r1");
        draft.PhasesNotes.Should().Be("p1");
        draft.QuestionsNotes.Should().Be("q1");
    }

    [Fact]
    public void UpdateAllNotes_WhenActivated_ThrowsInvalidOperationException()
    {
        // Arrange
        var draft = CreateValidDraft();
        draft.MarkActivated();

        // Act
        var act = () => draft.UpdateAllNotes("s", "m", "v", "r", "p", "q");

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void UpdateAllNotes_WithNullValues_SetsToEmpty()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        draft.UpdateAllNotes(null!, null!, null!, null!, null!, null!);

        // Assert
        draft.SummaryNotes.Should().BeEmpty();
        draft.MechanicsNotes.Should().BeEmpty();
        draft.VictoryNotes.Should().BeEmpty();
        draft.ResourcesNotes.Should().BeEmpty();
        draft.PhasesNotes.Should().BeEmpty();
        draft.QuestionsNotes.Should().BeEmpty();
    }

    #endregion

    #region Status Transition Tests

    [Fact]
    public void MarkCompleted_SetsStatusToCompleted()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        draft.MarkCompleted();

        // Assert
        draft.Status.Should().Be(MechanicDraftStatus.Completed);
    }

    [Fact]
    public void MarkCompleted_UpdatesLastModified()
    {
        // Arrange
        var draft = CreateValidDraft();
        var originalModified = draft.LastModified;

        // Act
        draft.MarkCompleted();

        // Assert
        draft.LastModified.Should().BeOnOrAfter(originalModified);
    }

    [Fact]
    public void MarkCompleted_WhenActivated_ThrowsInvalidOperationException()
    {
        // Arrange
        var draft = CreateValidDraft();
        draft.MarkActivated();

        // Act
        var act = () => draft.MarkCompleted();

        // Assert
        act.Should().Throw<InvalidOperationException>();
    }

    [Fact]
    public void MarkActivated_SetsStatusToActivated()
    {
        // Arrange
        var draft = CreateValidDraft();

        // Act
        draft.MarkActivated();

        // Assert
        draft.Status.Should().Be(MechanicDraftStatus.Activated);
    }

    [Fact]
    public void MarkActivated_UpdatesLastModified()
    {
        // Arrange
        var draft = CreateValidDraft();
        var originalModified = draft.LastModified;

        // Act
        draft.MarkActivated();

        // Assert
        draft.LastModified.Should().BeOnOrAfter(originalModified);
    }

    #endregion

    #region Helpers

    private static MechanicDraft CreateValidDraft()
    {
        return MechanicDraft.Create(ValidSharedGameId, ValidPdfDocumentId, ValidGameTitle, ValidCreatedBy);
    }

    #endregion
}
