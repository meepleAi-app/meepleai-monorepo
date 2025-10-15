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
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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
public class PdfIngestEndpointsTests : IntegrationTestBase
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public PdfIngestEndpointsTests(WebApplicationFactoryFixture factory) : base(factory)
    {
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
        var request = new HttpRequestMessage(HttpMethod.Post, $"/api/v1/ingest/pdf/{pdfId}/rulespec");
        AddCookies(request, cookies);

        var response = await client.SendAsync(request);

        // Then: RuleSpec is generated from atomic rules
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var spec = await response.Content.ReadFromJsonAsync<RuleSpec>(JsonOptions);
        Assert.NotNull(spec);
        Assert.Equal(game.Id, spec!.gameId);
        Assert.Single(spec.rules);
        Assert.Equal("Setup: Distribute four cards to each player", spec.rules[0].text);
        Assert.Equal("4", spec.rules[0].page);
        // Cleanup happens automatically via DisposeAsync
    }
}
