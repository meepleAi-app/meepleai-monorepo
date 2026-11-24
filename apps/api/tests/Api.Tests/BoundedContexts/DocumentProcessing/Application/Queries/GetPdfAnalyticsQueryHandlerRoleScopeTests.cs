using Api.BoundedContexts.DocumentProcessing.Application.Queries;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Queries;

/// <summary>
/// #3994 — the PDF analytics are scoped to the caller's own uploads unless the caller is an admin. A superadmin counts
/// as an admin everywhere: the exact comparison with <c>"Admin"</c> scoped it to its own uploads.
/// </summary>
/// <remarks>
/// The only caller today, the admin endpoint, passes no user id, so the scope never applies there. The handler takes
/// the role and the user id anyway, and this is the contract it states.
/// </remarks>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3994")]
public sealed class GetPdfAnalyticsQueryHandlerRoleScopeTests : IDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Guid _caller = Guid.NewGuid();

    public void Dispose() => _db.Dispose();

    [Theory]
    [InlineData("Admin", 2)]
    [InlineData("SuperAdmin", 2)]
    [InlineData("superadmin", 2)]
    [InlineData("Editor", 1)]
    [InlineData("User", 1)]
    [InlineData("", 1)]
    public async Task Handle_ScopesToTheCallersUploads_UnlessTheCallerIsAnAdmin(string role, int expectedTotal)
    {
        await SeedAsync();

        var result = await new GetPdfAnalyticsQueryHandler(_db, NullLogger<GetPdfAnalyticsQueryHandler>.Instance)
            .Handle(new GetPdfAnalyticsQuery(TimeRangeDays: 30, UserId: _caller, UserRole: role), TestContext.Current.CancellationToken);

        result.TotalUploaded.Should().Be(expectedTotal);
    }

    private async Task SeedAsync()
    {
        foreach (var uploader in new[] { _caller, Guid.NewGuid() })
        {
            _db.PdfDocuments.Add(new PdfDocumentEntity
            {
                Id = Guid.NewGuid(),
                SharedGameId = Guid.NewGuid(),
                FileName = "rules.pdf",
                FilePath = "/uploads/rules.pdf",
                FileSizeBytes = 1024,
                UploadedByUserId = uploader,
                UploadedAt = DateTime.UtcNow.AddDays(-1),
            });
        }

        await _db.SaveChangesAsync(TestContext.Current.CancellationToken);
    }
}
