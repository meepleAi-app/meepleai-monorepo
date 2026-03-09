using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Api.Tests.Constants;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Unit tests for AbTestSession aggregate root and AbTestVariant entity.
/// Issue #5491: AbTestSession domain entity.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "5491")]
public sealed class AbTestSessionTests
{
    private static readonly Guid UserId = Guid.NewGuid();
    private static readonly Guid EvaluatorId = Guid.NewGuid();

    // --- Create tests ---

    [Fact]
    public void Create_WithValidData_CreatesSession()
    {
        var session = AbTestSession.Create(UserId, "What are the rules for Catan?");

        Assert.NotEqual(Guid.Empty, session.Id);
        Assert.Equal(UserId, session.CreatedBy);
        Assert.Equal("What are the rules for Catan?", session.Query);
        Assert.Null(session.KnowledgeBaseId);
        Assert.Equal(AbTestStatus.Draft, session.Status);
        Assert.Empty(session.Variants);
    }

    [Fact]
    public void Create_WithKnowledgeBaseId_StoresIt()
    {
        var kbId = Guid.NewGuid();
        var session = AbTestSession.Create(UserId, "Test query", kbId);

        Assert.Equal(kbId, session.KnowledgeBaseId);
    }

    [Fact]
    public void Create_TrimsQuery()
    {
        var session = AbTestSession.Create(UserId, "  spaced query  ");

        Assert.Equal("spaced query", session.Query);
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestSession.Create(Guid.Empty, "Test query"));
    }

    [Fact]
    public void Create_WithNullQuery_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestSession.Create(UserId, null!));
    }

    [Fact]
    public void Create_WithEmptyQuery_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestSession.Create(UserId, "   "));
    }

    [Fact]
    public void Create_WithQueryOver2000Chars_ThrowsValidation()
    {
        var longQuery = new string('x', 2001);

        Assert.Throws<ValidationException>(() =>
            AbTestSession.Create(UserId, longQuery));
    }

    [Fact]
    public void Create_WithQueryExactly2000Chars_Succeeds()
    {
        var query = new string('x', 2000);
        var session = AbTestSession.Create(UserId, query);

        Assert.Equal(2000, session.Query.Length);
    }

    // --- AddVariant tests ---

    [Fact]
    public void AddVariant_InDraft_AddsVariant()
    {
        var session = AbTestSession.Create(UserId, "Test");

        var variant = session.AddVariant("A", "OpenRouter", "gpt-4o-mini");

        Assert.Single(session.Variants);
        Assert.Equal("A", variant.Label);
        Assert.Equal("OpenRouter", variant.Provider);
        Assert.Equal("gpt-4o-mini", variant.ModelId);
        Assert.Equal(session.Id, variant.AbTestSessionId);
    }

    [Fact]
    public void AddVariant_MultipleTimes_SupportsUpTo4()
    {
        var session = AbTestSession.Create(UserId, "Test");

        session.AddVariant("A", "OpenRouter", "gpt-4o-mini");
        session.AddVariant("B", "OpenRouter", "claude-3-haiku");
        session.AddVariant("C", "Ollama", "llama3");
        session.AddVariant("D", "OpenRouter", "gemini-pro");

        Assert.Equal(4, session.Variants.Count);
    }

    [Fact]
    public void AddVariant_FifthVariant_ThrowsValidation()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P", "m1");
        session.AddVariant("B", "P", "m2");
        session.AddVariant("C", "P", "m3");
        session.AddVariant("D", "P", "m4");

        Assert.Throws<ValidationException>(() =>
            session.AddVariant("E", "P", "m5"));
    }

    [Fact]
    public void AddVariant_DuplicateLabel_ThrowsValidation()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P1", "m1");

        Assert.Throws<ValidationException>(() =>
            session.AddVariant("A", "P2", "m2"));
    }

    [Fact]
    public void AddVariant_DuplicateLabelCaseInsensitive_ThrowsValidation()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P1", "m1");

        Assert.Throws<ValidationException>(() =>
            session.AddVariant("a", "P2", "m2"));
    }

    [Fact]
    public void AddVariant_WhenNotDraft_ThrowsInvalidOperation()
    {
        var session = CreateInProgressSession();

        Assert.Throws<InvalidOperationException>(() =>
            session.AddVariant("C", "P", "m3"));
    }

    [Fact]
    public void AddVariant_WithEmptyLabel_ThrowsArgument()
    {
        var session = AbTestSession.Create(UserId, "Test");

        Assert.Throws<ArgumentException>(() =>
            session.AddVariant("", "P", "m1"));
    }

    [Fact]
    public void AddVariant_WithEmptyProvider_ThrowsArgument()
    {
        var session = AbTestSession.Create(UserId, "Test");

        Assert.Throws<ArgumentException>(() =>
            session.AddVariant("A", "", "m1"));
    }

    [Fact]
    public void AddVariant_WithEmptyModelId_ThrowsArgument()
    {
        var session = AbTestSession.Create(UserId, "Test");

        Assert.Throws<ArgumentException>(() =>
            session.AddVariant("A", "P", ""));
    }

    // --- StartTest tests ---

    [Fact]
    public void StartTest_WithTwoVariants_TransitionsToInProgress()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P1", "m1");
        session.AddVariant("B", "P2", "m2");

        session.StartTest();

        Assert.Equal(AbTestStatus.InProgress, session.Status);
    }

    [Fact]
    public void StartTest_WithOneVariant_ThrowsValidation()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P", "m1");

        Assert.Throws<ValidationException>(() => session.StartTest());
    }

    [Fact]
    public void StartTest_WithNoVariants_ThrowsValidation()
    {
        var session = AbTestSession.Create(UserId, "Test");

        Assert.Throws<ValidationException>(() => session.StartTest());
    }

    [Fact]
    public void StartTest_WhenNotDraft_ThrowsInvalidOperation()
    {
        var session = CreateInProgressSession();

        Assert.Throws<InvalidOperationException>(() => session.StartTest());
    }

    // --- Variant RecordResponse tests ---

    [Fact]
    public void RecordResponse_SetsResponseData()
    {
        var session = AbTestSession.Create(UserId, "Test");
        var variant = session.AddVariant("A", "P", "m1");

        variant.RecordResponse("The answer is 42", 150, 1200, 0.003m);

        Assert.Equal("The answer is 42", variant.Response);
        Assert.Equal(150, variant.TokensUsed);
        Assert.Equal(1200, variant.LatencyMs);
        Assert.Equal(0.003m, variant.CostUsd);
    }

    [Fact]
    public void RecordResponse_WithEmptyResponse_ThrowsArgument()
    {
        var session = AbTestSession.Create(UserId, "Test");
        var variant = session.AddVariant("A", "P", "m1");

        Assert.Throws<ArgumentException>(() =>
            variant.RecordResponse("", 100, 500, 0.001m));
    }

    [Fact]
    public void RecordResponse_WithNegativeTokens_ThrowsArgument()
    {
        var session = AbTestSession.Create(UserId, "Test");
        var variant = session.AddVariant("A", "P", "m1");

        Assert.Throws<ArgumentException>(() =>
            variant.RecordResponse("response", -1, 500, 0.001m));
    }

    // --- Variant MarkFailed tests ---

    [Fact]
    public void MarkFailed_SetsFailedState()
    {
        var session = AbTestSession.Create(UserId, "Test");
        var variant = session.AddVariant("A", "P", "m1");

        variant.MarkFailed("Model timeout after 30s");

        Assert.True(variant.Failed);
        Assert.Equal("Model timeout after 30s", variant.ErrorMessage);
    }

    // --- EvaluateVariant tests ---

    [Fact]
    public void EvaluateVariant_InProgress_SetsEvaluation()
    {
        var session = CreateInProgressSession();
        session.Variants[0].RecordResponse("Response A", 100, 500, 0.001m);
        session.Variants[1].RecordResponse("Response B", 120, 600, 0.002m);

        var eval = AbTestEvaluation.Create(EvaluatorId, 5, 4, 3, 4);
        session.EvaluateVariant("A", eval);

        Assert.NotNull(session.Variants[0].Evaluation);
        Assert.Equal(5, session.Variants[0].Evaluation!.Accuracy);
        Assert.Equal(AbTestStatus.InProgress, session.Status); // Not all evaluated yet
    }

    [Fact]
    public void EvaluateVariant_AllEvaluated_TransitionsToEvaluated()
    {
        var session = CreateInProgressSession();
        session.Variants[0].RecordResponse("Response A", 100, 500, 0.001m);
        session.Variants[1].RecordResponse("Response B", 120, 600, 0.002m);

        session.EvaluateVariant("A", AbTestEvaluation.Create(EvaluatorId, 5, 4, 3, 4));
        session.EvaluateVariant("B", AbTestEvaluation.Create(EvaluatorId, 3, 3, 4, 5));

        Assert.Equal(AbTestStatus.Evaluated, session.Status);
        Assert.NotNull(session.CompletedAt);
    }

    [Fact]
    public void EvaluateVariant_FailedVariantsSkipped_CompletesWhenOthersEvaluated()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P1", "m1");
        session.AddVariant("B", "P2", "m2");
        session.StartTest();

        session.Variants[0].RecordResponse("Response A", 100, 500, 0.001m);
        session.Variants[1].MarkFailed("timeout");

        session.EvaluateVariant("A", AbTestEvaluation.Create(EvaluatorId, 5, 5, 5, 5));

        Assert.Equal(AbTestStatus.Evaluated, session.Status);
    }

    [Fact]
    public void EvaluateVariant_WhenDraft_ThrowsInvalidOperation()
    {
        var session = AbTestSession.Create(UserId, "Test");
        session.AddVariant("A", "P", "m1");
        session.AddVariant("B", "P", "m2");

        Assert.Throws<InvalidOperationException>(() =>
            session.EvaluateVariant("A", AbTestEvaluation.Create(EvaluatorId, 5, 5, 5, 5)));
    }

    [Fact]
    public void EvaluateVariant_UnknownLabel_ThrowsValidation()
    {
        var session = CreateInProgressSession();

        Assert.Throws<ValidationException>(() =>
            session.EvaluateVariant("Z", AbTestEvaluation.Create(EvaluatorId, 5, 5, 5, 5)));
    }

    [Fact]
    public void EvaluateVariant_AlreadyEvaluated_ThrowsInvalidOperation()
    {
        var session = CreateInProgressSession();
        session.Variants[0].RecordResponse("Response", 100, 500, 0.001m);

        session.EvaluateVariant("A", AbTestEvaluation.Create(EvaluatorId, 5, 5, 5, 5));

        Assert.Throws<InvalidOperationException>(() =>
            session.EvaluateVariant("A", AbTestEvaluation.Create(EvaluatorId, 3, 3, 3, 3)));
    }

    // --- GetWinner tests ---

    [Fact]
    public void GetWinner_WhenEvaluated_ReturnsHighestScorer()
    {
        var session = CreateInProgressSession();
        session.Variants[0].RecordResponse("A", 100, 500, 0.001m);
        session.Variants[1].RecordResponse("B", 120, 600, 0.002m);

        session.EvaluateVariant("A", AbTestEvaluation.Create(EvaluatorId, 3, 3, 3, 3)); // avg 3.0
        session.EvaluateVariant("B", AbTestEvaluation.Create(EvaluatorId, 5, 5, 5, 5)); // avg 5.0

        var winner = session.GetWinner();

        Assert.NotNull(winner);
        Assert.Equal("B", winner.Label);
    }

    [Fact]
    public void GetWinner_WhenNotEvaluated_ReturnsNull()
    {
        var session = CreateInProgressSession();

        Assert.Null(session.GetWinner());
    }

    // --- TotalCost tests ---

    [Fact]
    public void TotalCost_SumsAllVariantCosts()
    {
        var session = AbTestSession.Create(UserId, "Test");
        var a = session.AddVariant("A", "P1", "m1");
        var b = session.AddVariant("B", "P2", "m2");

        a.RecordResponse("A", 100, 500, 0.003m);
        b.RecordResponse("B", 120, 600, 0.005m);

        Assert.Equal(0.008m, session.TotalCost);
    }

    // --- AbTestEvaluation tests ---

    [Fact]
    public void AbTestEvaluation_Create_Valid()
    {
        var eval = AbTestEvaluation.Create(EvaluatorId, 4, 5, 3, 4, "Good response");

        Assert.Equal(EvaluatorId, eval.EvaluatorId);
        Assert.Equal(4, eval.Accuracy);
        Assert.Equal(5, eval.Completeness);
        Assert.Equal(3, eval.Clarity);
        Assert.Equal(4, eval.Tone);
        Assert.Equal("Good response", eval.Notes);
        Assert.Equal(4.0m, eval.AverageScore);
    }

    [Fact]
    public void AbTestEvaluation_AverageScore_CalculatesCorrectly()
    {
        var eval = AbTestEvaluation.Create(EvaluatorId, 1, 2, 3, 4);

        Assert.Equal(2.5m, eval.AverageScore);
    }

    [Fact]
    public void AbTestEvaluation_ScoreBelow1_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestEvaluation.Create(EvaluatorId, 0, 3, 3, 3));
    }

    [Fact]
    public void AbTestEvaluation_ScoreAbove5_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestEvaluation.Create(EvaluatorId, 6, 3, 3, 3));
    }

    [Fact]
    public void AbTestEvaluation_EmptyEvaluatorId_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestEvaluation.Create(Guid.Empty, 3, 3, 3, 3));
    }

    [Fact]
    public void AbTestEvaluation_NotesOver2000_ThrowsValidation()
    {
        Assert.Throws<ValidationException>(() =>
            AbTestEvaluation.Create(EvaluatorId, 3, 3, 3, 3, new string('x', 2001)));
    }

    // --- Helpers ---

    private static AbTestSession CreateInProgressSession()
    {
        var session = AbTestSession.Create(UserId, "Test query");
        session.AddVariant("A", "OpenRouter", "gpt-4o-mini");
        session.AddVariant("B", "OpenRouter", "claude-3-haiku");
        session.StartTest();
        return session;
    }
}
