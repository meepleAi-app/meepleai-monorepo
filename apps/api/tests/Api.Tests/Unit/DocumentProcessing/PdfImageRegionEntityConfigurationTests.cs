using Api.Infrastructure.Entities;
using Api.Tests.Constants;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace Api.Tests.Unit.DocumentProcessing;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Issue", "3447")]
public sealed class PdfImageRegionEntityConfigurationTests
{
    [Fact]
    public async Task PdfImageRegions_RoundTrips_WithBboxAndElementType()
    {
        using var db = TestDbContextFactory.CreateInMemoryDbContext($"imgregion_{Guid.NewGuid():N}");
        var region = new PdfImageRegionEntity
        {
            PdfDocumentId = Guid.NewGuid(),
            PageNumber = 4,
            X = 0.10,
            Y = 0.55,
            Width = 0.80,
            Height = 0.30,
            ElementType = "FigureCaption"
        };
        db.PdfImageRegions.Add(region);
        await db.SaveChangesAsync();

        var loaded = await db.PdfImageRegions.AsNoTracking().SingleAsync();
        loaded.PageNumber.Should().Be(4);
        loaded.ElementType.Should().Be("FigureCaption");
        loaded.X.Should().Be(0.10);
        loaded.Height.Should().Be(0.30);
    }
}
