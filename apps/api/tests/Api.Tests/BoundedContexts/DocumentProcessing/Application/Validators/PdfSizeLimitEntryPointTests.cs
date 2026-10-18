using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.Validators;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.AddRagToSharedGame;
using Api.BoundedContexts.SharedGameCatalog.Application.Validators;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.Results;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Validators;

/// <summary>
/// #4449: one PDF size limit, 100 MB, at every entry point that receives a PDF.
/// </summary>
/// <remarks>
/// Before #4449 the same file got a different answer depending on the door: the wizard's extraction
/// upload and «Aggiungi RAG» refused above 50 MB, chunked upload accepted up to 200 MB, the catalog PDF
/// up to 500 MB, the user library's custom PDF counted 100 MB as 100 000 000 bytes. Each entry point
/// here builds a command that is valid in every other respect, so the only error left is the size.
/// The standard <c>/ingest/pdf</c> upload checks the size in its handler, not in a validator: it is
/// covered live (100 MiB accepted, one byte more refused with 400) in the PR.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfSizeLimitEntryPointTests
{
    private const long Limit = 104_857_600; // 100 MiB

    private static readonly IOptions<PdfProcessingOptions> PdfOptions = Options.Create(new PdfProcessingOptions());

    public static TheoryData<string> EntryPoints => new()
    {
        "chunked-init",
        "private-upload",
        "wizard-extraction",
        "add-rag",
        "custom-game-pdf",
        "rulebook",
        "catalog-pdf",
    };

    [Fact]
    public void TheSingleLimit_Is100MiB()
    {
        PdfOptions.Value.MaxFileSizeBytes.Should().Be(Limit);
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void AFileAtTheLimit_IsAccepted(string entryPoint)
    {
        Validate(entryPoint, Limit).Errors.Should().BeEmpty();
    }

    [Theory]
    [MemberData(nameof(EntryPoints))]
    public void OneByteOverTheLimit_IsRefused_WithTheLimitInTheMessage(string entryPoint)
    {
        var result = Validate(entryPoint, Limit + 1);

        result.Errors.Should().ContainSingle()
            .Which.ErrorMessage.Should().Contain("100 MB");
    }

    private static ValidationResult Validate(string entryPoint, long size) => entryPoint switch
    {
        "chunked-init" => new InitChunkedUploadCommandValidator(PdfOptions).Validate(
            new InitChunkedUploadCommand(Guid.NewGuid(), Guid.NewGuid(), "rules.pdf", size)),
        "private-upload" => new UploadPrivatePdfCommandValidator(PdfOptions).Validate(
            new UploadPrivatePdfCommand(Guid.NewGuid(), Guid.NewGuid(), PdfFile(size))),
        "wizard-extraction" => new UploadPdfForGameExtractionCommandValidator(PdfOptions).Validate(
            new UploadPdfForGameExtractionCommand(PdfFile(size), Guid.NewGuid())),
        "add-rag" => new AddRagToSharedGameCommandValidator(PdfOptions).Validate(
            new AddRagToSharedGameCommand(
                Guid.NewGuid(), PdfFile(size), SharedGameDocumentType.Rulebook, "1.0", null, Guid.NewGuid(), IsAdmin: true)),
        "custom-game-pdf" => new UploadCustomGamePdfCommandValidator(PdfOptions).Validate(
            new UploadCustomGamePdfCommand(Guid.NewGuid(), Guid.NewGuid(), "https://files.example.com/rules.pdf", size, "rules.pdf")),
        "rulebook" => new AddRulebookCommandValidator(PdfOptions).Validate(
            new AddRulebookCommand(Guid.NewGuid(), Guid.NewGuid(), PdfFile(size))),
        "catalog-pdf" => new UploadSharedGamePdfCommandValidator(PdfOptions).Validate(
            new UploadSharedGamePdfCommand(
                Guid.NewGuid(), PdfFile(size), SharedGameDocumentType.Rulebook, "1.0", SetAsActive: true, null, Guid.NewGuid())),
        _ => throw new ArgumentOutOfRangeException(nameof(entryPoint), entryPoint, "entry point not covered"),
    };

    private static IFormFile PdfFile(long size)
    {
        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(size);
        file.Setup(f => f.FileName).Returns("rules.pdf");
        file.Setup(f => f.ContentType).Returns("application/pdf");
        return file.Object;
    }
}
