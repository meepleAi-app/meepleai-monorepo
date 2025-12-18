using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using FluentAssertions;
using FluentAssertions.Execution;
using FluentAssertions.Primitives;

namespace Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers.Assertions;

/// <summary>
/// Custom FluentAssertions extensions for PdfUploadResult.
/// Based on VERIFIED structure: record PdfUploadResult(bool Success, string Message, PdfDocumentDto? Document)
/// </summary>
/// <remarks>
/// ISSUE-1818: Custom assertions for improved test readability.
/// Usage:
/// <code>
/// result.Should().BeSuccessful();
/// result.Should().HaveFailed();
/// result.Should().HaveMessage("Upload successful");
/// result.Should().HaveDocument();
/// </code>
/// </remarks>
internal class PdfUploadResultAssertions : ReferenceTypeAssertions<PdfUploadResult, PdfUploadResultAssertions>
{
    private readonly AssertionChain _chain;

    public PdfUploadResultAssertions(PdfUploadResult instance, AssertionChain assertionChain)
        : base(instance, assertionChain)
    {
        _chain = assertionChain;
    }

    protected override string Identifier => "pdf upload result";

    /// <summary>
    /// Asserts that the PDF upload was successful (Success = true).
    /// </summary>
    public AndConstraint<PdfUploadResultAssertions> BeSuccessful(string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:pdf upload result} to be successful, but it was <null>.")
            .Then
            .ForCondition(Subject!.Success)
            .FailWith("Expected {context:pdf upload result} to be successful, but it failed with message: {0}.",
                Subject?.Message ?? "no message");

        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the PDF upload failed (Success = false).
    /// </summary>
    public AndConstraint<PdfUploadResultAssertions> HaveFailed(string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject is not null)
            .FailWith("Expected {context:pdf upload result} to have failed, but it was <null>.")
            .Then
            .ForCondition(!Subject!.Success)
            .FailWith("Expected {context:pdf upload result} to have failed, but it was successful.");

        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the message matches the expected value.
    /// </summary>
    public AndConstraint<PdfUploadResultAssertions> HaveMessage(string expectedMessage, string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject?.Message == expectedMessage)
            .FailWith("Expected message to be {0}, but found {1}.",
                expectedMessage, Subject?.Message ?? "null");

        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the message contains the expected substring.
    /// </summary>
    public AndConstraint<PdfUploadResultAssertions> HaveMessageContaining(string expectedSubstring, string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject?.Message?.Contains(expectedSubstring) == true)
            .FailWith("Expected message to contain {0}, but found {1}.",
                expectedSubstring, Subject?.Message ?? "null");

        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that a document was created (Document not null).
    /// </summary>
    public AndConstraint<PdfUploadResultAssertions> HaveDocument(string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject?.Document is not null)
            .FailWith("Expected {context:pdf upload result} to have a document, but Document was null.");

        return new AndConstraint<PdfUploadResultAssertions>(this);
    }

    /// <summary>
    /// Asserts that the document has the expected ID.
    /// </summary>
    public AndConstraint<PdfUploadResultAssertions> HaveDocumentId(Guid expectedId, string because = "", params object[] becauseArgs)
    {
        _chain
            .BecauseOf(because, becauseArgs)
            .ForCondition(Subject?.Document is not null)
            .FailWith("Expected document ID to be {0}, but Document was null.", expectedId)
            .Then
            .ForCondition(Subject!.Document!.Id == expectedId)
            .FailWith("Expected document ID to be {0}, but found {1}.",
                expectedId, Subject.Document.Id);

        return new AndConstraint<PdfUploadResultAssertions>(this);
    }
}

/// <summary>
/// Extension methods to enable fluent assertion syntax for PdfUploadResult.
/// </summary>
internal static class PdfUploadResultAssertionsExtensions
{
    public static PdfUploadResultAssertions Should(this PdfUploadResult instance)
    {
        return new PdfUploadResultAssertions(instance, AssertionChain.GetOrCreate());
    }
}
