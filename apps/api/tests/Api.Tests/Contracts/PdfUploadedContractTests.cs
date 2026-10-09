using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Infrastructure.Messaging;
using Api.Tests.Architecture;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Contracts;

/// <summary>Le chiavi sul filo di PdfUploaded v1 sono congelate nella fixture condivisa con apps/functions.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfUploadedContractTests
{
    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !SourceScanner.IsRepoRoot(dir.FullName))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the repo (or a worktree)");
        return Path.Combine(dir!.FullName, "tests", "fixtures", "contracts", "pdf-uploaded.v1.json");
    }

    [Fact]
    public void SerializedEvent_HasExactlyTheFixtureKeys()
    {
        var evt = new PdfUploadedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), null, "pdfs/g/f_a.pdf");
        using var actual = JsonDocument.Parse(JsonSerializer.Serialize(evt, ServiceBusIntegrationEventPublisher.JsonOptions));
        using var expected = JsonDocument.Parse(File.ReadAllText(FixturePath()));

        var actualKeys = actual.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        var expectedKeys = expected.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);

        actualKeys.Should().Equal(expectedKeys);
        expected.RootElement.GetProperty("contractVersion").GetString().Should().Be(PdfUploadedIntegrationEvent.CurrentContractVersion);
    }
}
