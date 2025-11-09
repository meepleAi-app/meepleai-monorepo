using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Tests.Fixtures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using FluentAssertions;
using Xunit;

namespace Api.Tests;

/// <summary>
/// BDD-style integration tests for PDF ingest endpoints.
///
/// Feature: PDF ingestion and RuleSpec generation
/// As an admin user
/// I want to generate RuleSpecs from uploaded PDFs
/// So that I can create structured game rules from rulebook documents
/// </summary>
[Collection("Postgres Integration Tests")]
public class PdfIngestEndpointsTests : IntegrationTestBase
{
    private readonly ITestOutputHelper _output;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PdfIngestEndpointsTests(PostgresCollectionFixture fixture, ITestOutputHelper output) : base(fixture)
    {
        _output = output;
    }

    /// <summary>
    /// Scenario: Admin generates RuleSpec from PDF with atomic rules
    ///   Given admin user is authenticated
    ///   And a game exists
    ///   And a PDF with atomic rules exists
    ///   When admin posts to /ingest/pdf/{pdfId}/rulespec
    ///   Then RuleSpec is generated from atomic rules
    ///   And cleanup is automatic
    /// </summary>
    [Fact]
    public async Task PostRulespecIngest_ReturnsGeneratedRuleSpec()
    {
        // Given: Admin user is authenticated
        var user = await CreateTestUserAsync("pdf-ingest", UserRole.Admin);
        var cookies = await AuthenticateUserAsync(user.Email);
        var client = CreateClientWithoutCookies();

        // And: A game exists
        var game = await CreateTestGameAsync("Ingest Game");

        // And: A PDF with atomic rules exists
        var pdfId = $"pdf-ingest-{TestRunId}";

        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        var pdfDoc = new PdfDocumentEntity
        {
            Id = pdfId,
            GameId = game.Id,
            FileName = "rules.pdf",
            FilePath = "/tmp/rules.pdf",
            FileSizeBytes = 2048,
            UploadedByUserId = user.Id,
            UploadedAt = DateTime.UtcNow,
            AtomicRules = JsonSerializer.Serialize(new[]
            {
                "[Table on page 4] Setup: Distribute four cards to each player"
            })
        };

        db.PdfDocuments.Add(pdfDoc);
        await db.SaveChangesAsync();
        TrackPdfDocumentId(pdfId);

        // When: Admin posts to /ingest/pdf/{pdfId}/rulespec
        using var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ingest/pdf/{pdfId}/rulespec");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: RuleSpec is generated from atomic rules
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var spec = await response.Content.ReadFromJsonAsync<RuleSpec>(JsonOptions);
        spec.Should().NotBeNull();
        spec!.gameId.Should().Be(game.Id);
        spec.rules.Should().ContainSingle();
        spec.rules[0].text.Should().Be("Setup: Distribute four cards to each player");
        spec.rules[0].page.Should().Be("4");
        // Cleanup happens automatically via DisposeAsync
    }
}
