using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// #4449: the standard <c>/ingest/pdf</c> upload applies the single PDF limit, 100 MB, in its handler.
/// </summary>
/// <remarks>
/// The other entry points check the size in a validator (<c>PdfSizeLimitEntryPointTests</c>); this one
/// checks it in <see cref="UploadPdfCommandHandler"/>, after the plan's cap. The plan here is
/// unlimited (500 MB, as for admins), so the only size check left is the single limit. A file at the
/// limit passes it and stops at the next check — the content type — which proves the size is not
/// what refused it, without needing a real 100 MB PDF.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class UploadPdfCommandHandlerSizeLimitTests
{
    private const long Limit = 104_857_600; // 100 MiB

    [Fact]
    public async Task OneByteOverTheLimit_IsRefused_WithTheLimitInTheMessage()
    {
        var result = await UploadAsync(Limit + 1, "application/pdf");

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("Maximum size is 100MB");
    }

    [Fact]
    public async Task AFileAtTheLimit_PassesTheSizeCheck()
    {
        var result = await UploadAsync(Limit, "text/plain");

        result.Success.Should().BeFalse();
        result.Message.Should().StartWith("Invalid file type");
        result.Message.Should().NotContain("too large");
    }

    private static async Task<PdfUploadResult> UploadAsync(
        long size, string contentType)
    {
        using var fixture = new PdfTestFixtureBuilder()
            .WithDefaultMocks()
            .WithPdfOptions(new PdfProcessingOptions())
            .Build();

        var file = new Mock<IFormFile>();
        file.Setup(f => f.Length).Returns(size);
        file.Setup(f => f.FileName).Returns("rules.pdf");
        file.Setup(f => f.ContentType).Returns(contentType);

        return await fixture.CreateHandler().Handle(
            new UploadPdfCommand(Guid.NewGuid().ToString(), null, null, Guid.NewGuid(), file.Object),
            TestContext.Current.CancellationToken);
    }
}
